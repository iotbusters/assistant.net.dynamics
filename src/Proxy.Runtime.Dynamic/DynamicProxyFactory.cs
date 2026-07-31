using Assistant.Net.Dynamics.Abstractions;
using Assistant.Net.Dynamics.Options;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.Extensions.Options;
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Assistant.Net.Dynamics.Internal
{
    /// <summary>
    ///     Proxy factory implementation capable of compiling and loading missing proxies at runtime via Roslyn.
    /// </summary>
    internal sealed class DynamicProxyFactory : IProxyFactory
    {
        private static readonly ProxyLoadContext LoadContext = new();

        private readonly ProxyGenerationStrategy strategy;

        public DynamicProxyFactory(IOptions<ProxyFactoryOptions> options)
        {
            strategy = options.Value.Strategy;

            var proxyTypes = options.Value.ProxyTypes;
            var unknownTypes = proxyTypes.Where(x => !KnownProxy.ProxyTypes.Keys.Contains(x)).ToArray();
            if (!unknownTypes.Any())
                return;

            if (strategy == ProxyGenerationStrategy.Precompiled)
                throw new InvalidOperationException(
                    "Runtime generation wasn't allowed but the following types were configured: "
                    + string.Join(", ", unknownTypes.Select(x => x.FullName)));

            GenerateProxies(unknownTypes);
        }

        Proxy<T> IProxyFactory.Create<T>(T? instance) where T : class
        {
            var type = typeof(T);
            if (!KnownProxy.TryGetFactory(type, out var factory))
            {
                if (strategy != ProxyGenerationStrategy.ByRequest)
                    throw new InvalidOperationException($"Proxy generation by request wasn't allowed but the type was requested: {type.FullName}");

                GenerateProxies(type);
                KnownProxy.TryGetFactory(type, out factory);
            }

            if (factory is null)
                throw new InvalidOperationException($"Proxy for '{type.FullName}' wasn't registered.");

            return (Proxy<T>) factory(instance);
        }

        private static void GenerateProxies(params Type[] proxyTypes)
        {
            Compilation compilation = CSharpCompilation.Create(ProxyAssemblyName)
                .WithOptions(new CSharpCompilationOptions(
                    OutputKind.DynamicallyLinkedLibrary,
                    optimizationLevel: OptimizationLevel.Release))
                .AddReferences(MetadataReference.CreateFromFile(typeof(KnownProxy).Assembly.Location));

            foreach (var proxyType in proxyTypes)
                compilation = compilation.AddProxy(proxyType);

            using var memory = new MemoryStream();
            var result = compilation.Emit(memory);
            if (!result.Success)
                throw new InvalidOperationException(
                    "Proxy compilation failed:" + Environment.NewLine
                    + string.Join(Environment.NewLine, result.Diagnostics
                        .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
                        .Select(diagnostic => diagnostic.ToString())));

            memory.Seek(0, SeekOrigin.Begin);
            var assembly = LoadContext.LoadFromStream(memory);

            // The generated assembly self-registers its proxy factories via a module initializer.
            RuntimeHelpers.RunModuleConstructor(assembly.ManifestModule.ModuleHandle);
        }

        private static string ProxyAssemblyName
        {
            get
            {
                var location = Assembly.GetExecutingAssembly().Location;
                var fileName = Path.GetFileNameWithoutExtension(location);
                return fileName + ".proxies.dll";
            }
        }
    }
}
