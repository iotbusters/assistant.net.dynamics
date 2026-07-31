using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Assistant.Net.Dynamics.Builders
{
    /// <summary>
    ///     C# source code builder.
    /// </summary>
    public sealed class SourceBuilder
    {
        private readonly IndentedStringBuilder builder;

        /// <summary/>
        public SourceBuilder() =>
            this.builder = new(this, 0);

        /// <summary/>
        public SourceBuilder(IndentedStringBuilder builder) =>
            this.builder = builder;

        /// <summary>
        ///     Backed string builder.
        /// </summary>
        internal StringBuilder StringBuilder { get; } = new();

        /// <summary>
        ///     All used namespaces in source builder.
        /// </summary>
        internal HashSet<string> Imports { get; } = new() {"System", "System.Linq"};

        /// <summary>
        ///     Adds a namespace.
        /// </summary>
        public SourceBuilder AddNamespace(string? @namespace, Action<SourceBuilder> body)
        {
            if (@namespace == null)
                return this;
            builder.AppendLine()
                .AppendLine("#pragma warning disable 1591")
                .AppendLine("namespace ", @namespace)
                .AddBlock(b => body(new SourceBuilder(b)))
                .AppendLine("#pragma warning restore 1591");
            return this;
        }

        /// <summary>
        ///     Adds a public class.
        /// </summary>
        public SourceBuilder AddClass(string className, INamedTypeSymbol[] inherits, Action<ClassSourceBuilder> body)
        {
            builder.Append("public class ", className);
            if (inherits.Any())
                builder.Append(" : ").AppendJoin(", ", inherits, (b, type) => b.Type(type)).AppendLine();

            builder.AddBlock(b => body(new(b, className))).AppendLine();
            return this;
        }

        /// <summary>
        ///     Adds a module initializer that self-registers the proxy factory in the global registry.
        /// </summary>
        public SourceBuilder AddProxyRegistration(string? @namespace, INamedTypeSymbol proxyType, string proxyTypeName)
        {
            if (@namespace == null)
                return this;
            builder.AppendLine()
                .AppendLine("#pragma warning disable 1591")
                .AppendLine("namespace ", @namespace)
                .AddBlock(nb => nb
                    .AppendLine("internal static class ", proxyTypeName, "Registration")
                    .AddBlock(cb => cb
                        .AppendLine("[global::System.Runtime.CompilerServices.ModuleInitializer]")
                        .AppendLine("internal static void Register()")
                        .AddBlock(mb => mb
                            .AppendLine("global::Assistant.Net.Dynamics.KnownProxy.RegisterFactory(")
                            .Append("typeof(").Type(proxyType).AppendLine("),")
                            .AppendLine("typeof(", proxyTypeName, "),")
                            .Append("instance => new ", proxyTypeName, "((").Type(proxyType).AppendLine(") instance));"))))
                .AppendLine("#pragma warning restore 1591");
            return this;
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            var resultBuilder = new StringBuilder();
            foreach (var @using in Imports)
                resultBuilder.Append("using ").Append(@using).AppendLine(";");
            resultBuilder
                .AppendLine()
                .Append(StringBuilder);
            return resultBuilder.ToString();
        }
    }

}