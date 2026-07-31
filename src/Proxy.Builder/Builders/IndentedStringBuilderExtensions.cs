using System;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace Assistant.Net.Dynamics.Builders
{
    /// <summary>
    ///     Fluent extensions for <see cref="IndentedStringBuilder"/>.
    /// </summary>
    public static class IndentedStringBuilderExtensions
    {
        /// <summary>
        ///     Adds a C# source code block.
        /// </summary>
        public static IndentedStringBuilder AddBlock(this IndentedStringBuilder builder, Action<IndentedStringBuilder> body)
        {
            builder.AppendLine("{");
            body(builder.Indent());
            builder.AppendLine("}");
            return builder;
        }

        /// <summary>
        ///     Appends a type from type symbol.
        /// </summary>
        public static IndentedStringBuilder Type(this IndentedStringBuilder builder, ITypeSymbol type)
        {
            builder.Append(type.Name);

            if (type is not INamedTypeSymbol namedType)
                return builder;

            builder.Imports.Add(namedType.ContainingNamespace.ToString());

            if (!namedType.IsGenericType)
                return builder;

            return builder
                .Append("<")
                .AppendJoin(", ", namedType.TypeArguments.ToArray(), (b, argType) => b.Type(argType))
                .Append(">");
        }

        /// <summary>
        ///     Appends a type name from type symbol.
        /// </summary>
        public static IndentedStringBuilder TypeName(this IndentedStringBuilder builder, ITypeSymbol type)
        {
            if (type is not INamedTypeSymbol namedType || !namedType.IsGenericType)
                return builder.Append(type.Name);

            return builder
                .Append(type.Name, "Of")
                .AppendJoin("And", namedType.TypeArguments.ToArray(), (b, argType) => b.TypeName(argType));
        }

        /// <summary>
        ///     Appends a method name from method symbol.
        /// </summary>
        public static IndentedStringBuilder MethodName(this IndentedStringBuilder builder, IMethodSymbol method)
        {
            var parameterTypes = method.Parameters.Select(x => x.Type).ToArray();
            return builder
                .Append(method.Name, "Of")
                .AppendJoin("And", parameterTypes, (b, type) => b.TypeName(type));
        }

        /// <summary>
        ///     Returns the `ref`/`out`/`in` declaration keyword for the <paramref name="kind"/>, or empty for by-value.
        /// </summary>
        public static string ToPrefix(this RefKind kind) => kind switch
        {
            RefKind.Ref => "ref ",
            RefKind.Out => "out ",
            RefKind.In => "in ",
            _ => string.Empty
        };
    }
}