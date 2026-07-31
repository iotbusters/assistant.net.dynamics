using Assistant.Net.Dynamics.Abstractions;
using Assistant.Net.Dynamics.Options;
using Microsoft.Extensions.Options;
using System;
using System.Linq;

namespace Assistant.Net.Dynamics.Internal
{
    /// <summary>
    ///     Default proxy factory implementation resolving proxies precompiled or registered via
    ///     <see cref="KnownProxy"/>. Reference the `Assistant.Net.Dynamics.Proxy.Runtime.Dynamic` package
    ///     and call `AddDynamicProxyGeneration()` to enable compiling unknown proxies at runtime.
    /// </summary>
    internal sealed class ProxyFactory : IProxyFactory
    {
        private readonly ProxyGenerationStrategy strategy;

        public ProxyFactory(IOptions<ProxyFactoryOptions> options)
        {
            strategy = options.Value.Strategy;

            var unknownTypes = options.Value.ProxyTypes.Where(x => !KnownProxy.ProxyTypes.Keys.Contains(x)).ToArray();
            if (!unknownTypes.Any())
                return;

            if (strategy == ProxyGenerationStrategy.Precompiled)
                throw new InvalidOperationException(
                    "Runtime generation wasn't allowed but the following types were configured: "
                    + string.Join(", ", unknownTypes.Select(x => x.FullName)));

            throw RuntimeGenerationNotAvailable(unknownTypes.Select(x => x.FullName));
        }

        Proxy<T> IProxyFactory.Create<T>(T? instance) where T : class
        {
            var type = typeof(T);
            if (!KnownProxy.TryGetFactory(type, out var factory))
            {
                if (strategy != ProxyGenerationStrategy.ByRequest)
                    throw new InvalidOperationException($"Proxy generation by request wasn't allowed but the type was requested: {type.FullName}");

                throw RuntimeGenerationNotAvailable(new[] { type.FullName });
            }

            return (Proxy<T>)factory!(instance);
        }

        private static InvalidOperationException RuntimeGenerationNotAvailable(System.Collections.Generic.IEnumerable<string?> typeNames) =>
            new(
                "Runtime proxy generation was requested for the following types but isn't available in this package: "
                + string.Join(", ", typeNames) + ". "
                + "Reference the 'Assistant.Net.Dynamics.Proxy.Runtime.Dynamic' package and call `AddDynamicProxyGeneration()`, "
                + "or precompile the proxy at build time instead.");
    }
}
