using Assistant.Net.Dynamics.Builders;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System;
using System.Collections.Immutable;
using System.Linq;
using System.Text;

namespace Assistant.Net.Dynamics
{
    /// <summary>
    ///     Incremental proxy source code generator.
    /// </summary>
    /// <seealso href="https://github.com/dotnet/roslyn/blob/main/docs/features/incremental-generators.md"/>
    [Generator(LanguageNames.CSharp)]
    public sealed class ProxySourceGenerator : IIncrementalGenerator
    {
        private const string ProxyAttributeMetadataName = "Assistant.Net.Dynamics.ProxyAttribute";
        private const string GenerateProxyAttributeMetadataName = "Assistant.Net.Dynamics.GenerateProxyAttribute";
        private const string ProxyFactoryDisplayName = "Assistant.Net.Dynamics.Abstractions.IProxyFactory";

        private static readonly DiagnosticDescriptor TypeNotFound = new(
            id: "PRX001",
            title: "Proxy target type not found",
            messageFormat: "Cannot resolve proxy target type '{0}'",
            category: "ProxyGeneration",
            defaultSeverity: DiagnosticSeverity.Warning,
            isEnabledByDefault: true);

        private static readonly DiagnosticDescriptor NotAnInterface = new(
            id: "PRX002",
            title: "Proxy target is not an interface",
            messageFormat: "Proxy target '{0}' must be an interface",
            category: "ProxyGeneration",
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        private static readonly DiagnosticDescriptor GenerationFailed = new(
            id: "PRX003",
            title: "Proxy generation failed",
            messageFormat: "Proxy generation for '{0}' failed: {1}",
            category: "ProxyGeneration",
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        /// <inheritdoc/>
        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            context.RegisterPostInitializationOutput(static ctx =>
                ctx.AddSource("ProxyAttributes.g.cs", SourceText.From(AttributesSource, Encoding.UTF8)));

            // Trigger 1: [Proxy] on an interface declaration.
            var fromProxyAttribute = context.SyntaxProvider
                .ForAttributeWithMetadataName(
                    ProxyAttributeMetadataName,
                    predicate: static (node, _) => node is InterfaceDeclarationSyntax,
                    transform: static (ctx, _) => GetMetadataName(ctx.TargetSymbol as INamedTypeSymbol))
                .Where(static name => name is not null);

            // Trigger 2: [assembly: GenerateProxy(typeof(IFoo))].
            var fromGenerateProxyAttribute = context.SyntaxProvider
                .ForAttributeWithMetadataName(
                    GenerateProxyAttributeMetadataName,
                    predicate: static (_, _) => true,
                    transform: static (ctx, _) => GetGenerateProxyTargets(ctx))
                .SelectMany(static (names, _) => names);

            // Trigger 3: IProxyFactory.Create&lt;T&gt;() invocations.
            var fromCreateInvocation = context.SyntaxProvider
                .CreateSyntaxProvider(
                    predicate: static (node, _) => IsCreateInvocation(node),
                    transform: static (ctx, _) => GetCreateTarget(ctx))
                .Where(static name => name is not null);

            var targets = fromProxyAttribute.Collect()
                .Combine(fromGenerateProxyAttribute.Collect())
                .Combine(fromCreateInvocation.Collect());

            context.RegisterSourceOutput(
                context.CompilationProvider.Combine(targets),
                static (spc, pair) =>
                {
                    var compilation = pair.Left;
                    var ((byProxyAttribute, byGenerateAttribute), byCreate) = pair.Right;
                    var names = byProxyAttribute
                        .Concat(byGenerateAttribute)
                        .Concat(byCreate)
                        .Where(static name => name is not null)
                        .Select(static name => name!)
                        .Distinct()
                        .ToArray();
                    Execute(spc, compilation, names);
                });
        }

        private static void Execute(SourceProductionContext context, Compilation compilation, string[] targetNames)
        {
            if (targetNames.Length == 0)
                return;

            var builder = new SourceBuilder();
            var generated = 0;
            foreach (var name in targetNames)
            {
                var symbol = compilation.GetTypeByMetadataName(name);
                if (symbol is null)
                {
                    context.ReportDiagnostic(Diagnostic.Create(TypeNotFound, Location.None, name));
                    continue;
                }

                if (symbol.TypeKind != TypeKind.Interface)
                {
                    context.ReportDiagnostic(Diagnostic.Create(NotAnInterface, Location.None, name));
                    continue;
                }

                try
                {
                    compilation.GenerateProxy(builder, symbol);
                    generated++;
                }
                catch (Exception exception)
                {
                    context.ReportDiagnostic(Diagnostic.Create(GenerationFailed, Location.None, name, exception.Message));
                }
            }

            if (generated > 0)
                context.AddSource("proxies.g.cs", SourceText.From(builder.ToString(), Encoding.UTF8));
        }

        private static bool IsCreateInvocation(SyntaxNode node) =>
            node is InvocationExpressionSyntax invocation
            && invocation.Expression switch
            {
                MemberAccessExpressionSyntax { Name: GenericNameSyntax { Identifier.ValueText: "Create" } } => true,
                GenericNameSyntax { Identifier.ValueText: "Create" } => true,
                _ => false
            };

        private static string? GetCreateTarget(GeneratorSyntaxContext context)
        {
            if (context.SemanticModel.GetSymbolInfo(context.Node).Symbol is not IMethodSymbol method)
                return null;

            if (method.Name != "Create"
                || method.ContainingType?.ToDisplayString() != ProxyFactoryDisplayName
                || method.TypeArguments.Length != 1)
                return null;

            return GetMetadataName(method.TypeArguments[0] as INamedTypeSymbol);
        }

        private static ImmutableArray<string?> GetGenerateProxyTargets(GeneratorAttributeSyntaxContext context)
        {
            var builder = ImmutableArray.CreateBuilder<string?>();
            foreach (var attribute in context.Attributes)
                if (attribute.ConstructorArguments.Length == 1
                    && attribute.ConstructorArguments[0].Value is INamedTypeSymbol target)
                    builder.Add(GetMetadataName(target));
            return builder.ToImmutable();
        }

        private static string? GetMetadataName(INamedTypeSymbol? symbol)
        {
            if (symbol is null)
                return null;

            var definition = symbol.OriginalDefinition;
            // Nested types are joined with '+', matching Compilation.GetTypeByMetadataName's expected format.
            var qualifiedName = definition.MetadataName;
            for (var containing = definition.ContainingType; containing != null; containing = containing.ContainingType)
                qualifiedName = containing.MetadataName + "+" + qualifiedName;

            var @namespace = definition.ContainingNamespace;
            var prefix = @namespace is { IsGlobalNamespace: false } ? @namespace.ToDisplayString() + "." : string.Empty;
            return prefix + qualifiedName;
        }

        private const string AttributesSource =
@"// <auto-generated/>
#nullable enable
namespace Assistant.Net.Dynamics
{
    /// <summary>Marks an interface for compile-time proxy generation.</summary>
    [global::System.AttributeUsage(global::System.AttributeTargets.Interface, AllowMultiple = false, Inherited = false)]
    internal sealed class ProxyAttribute : global::System.Attribute
    {
    }

    /// <summary>Requests compile-time proxy generation for the specified interface type.</summary>
    [global::System.AttributeUsage(global::System.AttributeTargets.Assembly, AllowMultiple = true, Inherited = false)]
    internal sealed class GenerateProxyAttribute : global::System.Attribute
    {
        public GenerateProxyAttribute(global::System.Type interfaceType) => InterfaceType = interfaceType;

        public global::System.Type InterfaceType { get; }
    }
}
";
    }
}
