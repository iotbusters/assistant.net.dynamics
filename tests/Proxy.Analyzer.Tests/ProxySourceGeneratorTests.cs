using Assistant.Net.Dynamics.Abstractions;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using NUnit.Framework;
using System.IO;
using System.Linq;

namespace Assistant.Net.Dynamics.Proxy.Analyzer.Tests
{
    public class ProxySourceGeneratorTests
    {
        private static Compilation CreateCompilation(string source)
        {
            var frameworkAssemblyDirectory = Path.GetDirectoryName(typeof(object).Assembly.Location)!;
            var references = new[]
            {
                typeof(object).Assembly.Location,
                typeof(Proxy<>).Assembly.Location,
                Path.Combine(frameworkAssemblyDirectory, "System.Runtime.dll"),
                Path.Combine(frameworkAssemblyDirectory, "netstandard.dll")
            }.Select(x => MetadataReference.CreateFromFile(x));

            return CSharpCompilation.Create(
                "test-assembly",
                new[] {CSharpSyntaxTree.ParseText(source)},
                references,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        }

        private static (Compilation Output, Diagnostic[] Diagnostics) RunGenerator(Compilation compilation)
        {
            var driver = CSharpGeneratorDriver.Create(new ProxySourceGenerator());
            driver.RunGeneratorsAndUpdateCompilation(compilation, out var output, out var diagnostics);
            return (output, diagnostics.ToArray());
        }

        [Test]
        public void Generator_emitsProxy_forProxyAttribute()
        {
            var compilation = CreateCompilation(
                @"namespace Sample { [Assistant.Net.Dynamics.Proxy] public interface IFoo { string Bar(); } }");

            var (output, diagnostics) = RunGenerator(compilation);

            diagnostics.Should().BeEmpty();
            output.SyntaxTrees.Should().Contain(tree => tree.ToString().Contains("IFooProxy"));
        }

        [Test]
        public void Generator_emitsProxy_forGenerateProxyAttribute()
        {
            var compilation = CreateCompilation(
                @"[assembly: Assistant.Net.Dynamics.GenerateProxy(typeof(Sample.IBaz))]
                  namespace Sample { public interface IBaz { int Qux(); } }");

            var (output, diagnostics) = RunGenerator(compilation);

            diagnostics.Should().BeEmpty();
            output.SyntaxTrees.Should().Contain(tree => tree.ToString().Contains("IBazProxy"));
        }

        [Test]
        public void Generator_emitsProxy_forCreateInvocation()
        {
            var compilation = CreateCompilation(
                @"namespace Sample
                  {
                      public interface IQuux { void Do(); }
                      public class Consumer
                      {
                          public void Use(Assistant.Net.Dynamics.Abstractions.IProxyFactory factory) => factory.Create<IQuux>();
                      }
                  }");

            var (output, diagnostics) = RunGenerator(compilation);

            diagnostics.Should().BeEmpty();
            output.SyntaxTrees.Should().Contain(tree => tree.ToString().Contains("IQuuxProxy"));
        }

        [Test]
        public void Generator_reportsDiagnostic_whenTargetIsNotInterface()
        {
            // [Proxy] can only ever target interfaces (AttributeUsage enforced by the C# compiler itself),
            // so PRX002 is only reachable via the [assembly: GenerateProxy(...)] or Create<T>() triggers.
            var compilation = CreateCompilation(
                @"[assembly: Assistant.Net.Dynamics.GenerateProxy(typeof(Sample.NotAnInterface))]
                  namespace Sample { public class NotAnInterface { } }");

            var (_, diagnostics) = RunGenerator(compilation);

            diagnostics.Should().Contain(d => d.Id == "PRX002");
        }
    }
}
