using Microsoft.CodeAnalysis;
using System;
using System.Linq;

namespace Assistant.Net.Dynamics.Builders
{
    /// <summary>
    ///     C# class source code builder.
    /// </summary>
    public class ClassSourceBuilder
    {
        private readonly string className;
        private readonly IndentedStringBuilder builder;

        /// <summary/>
        public ClassSourceBuilder(IndentedStringBuilder builder, string name)
        {
            this.className = name;
            this.builder = builder;
        }

        /// <summary>
        ///     Adds public constructor.
        /// </summary>
        public void AddCtor((string name, INamedTypeSymbol type)[] arguments, Action<IndentedStringBuilder> body)
        {
            builder.AppendLine()
                .Append("public ", className, "(")
                .AppendJoin(", ", arguments, buildSection: (b, argument) => b.Type(argument.type).Append(" ", argument.name))
                .AppendLine(")")
                .AddBlock(body)
                .AppendLine();
        }

        /// <summary>
        ///     Adds a public property from name and type symbol.
        /// </summary>
        public ClassSourceBuilder AddProperty(
            string propertyName,
            INamedTypeSymbol propertyType,
            Action<IndentedStringBuilder>? getter = null,
            Action<IndentedStringBuilder>? setter = null)
        {
            builder
                .Append("public ").Type(propertyType).AppendLine(" ", propertyName)
                .AddBlock(b =>
                {
                    if (getter == null && setter == null)
                        b.AppendLine("get;").AppendLine("set;");

                    if (getter != null) b.AppendLine("get").AddBlock(getter);

                    if (setter != null) b.AppendLine("set").AddBlock(setter);
                })
                .AppendLine();
            return this;
        }

        /// <summary>
        ///     Adds an implicit property from property symbol.
        /// </summary>
        public ClassSourceBuilder AddProperty(
            IPropertySymbol property,
            Action<IndentedStringBuilder>? getter = null,
            Action<IndentedStringBuilder>? setter = null)
        {
            builder
                .Type((INamedTypeSymbol)property.Type).Append(" ").Type(property.ContainingType).AppendLine(".", property.Name)
                .AddBlock(b =>
                {
                    if (property.GetMethod != null)
                        if (getter != null)
                            b.AppendLine("get").AddBlock(getter);
                        else
                            b.AppendLine("get;");

                    if (property.SetMethod != null)
                        if (setter != null)
                            b.AppendLine("set").AddBlock(setter);
                        else
                            b.AppendLine("set;");
                })
                .AppendLine();
            return this;
        }

        /// <summary>
        ///     Adds an implicit method from method symbol.
        /// </summary>
        public ClassSourceBuilder AddMethod(IMethodSymbol method, Action<IndentedStringBuilder> body)
        {
            if (method.ReturnsVoid)
                builder.Append("void");
            else
                builder.Type(method.ReturnType);

            builder.Append(" ").Type(method.ContainingType).Append(".", method.Name);

            if (method.IsGenericMethod)
            {
                builder.Append("<")
                    .AppendJoin(", ", method.TypeArguments.ToArray(), (b, argType) => b.Type(argType))
                    .Append(">");
            }

            builder.Append("(")
                .AppendJoin(
                    ", ",
                    method.Parameters.ToArray(),
                    (b, parameter) => b.Append(parameter.RefKind.ToPrefix()).Type(parameter.Type).Append(" ", parameter.Name!))
                .AppendLine(")")
                .AddBlock(body)
                .AppendLine();
            return this;
        }

        /// <summary>
        ///     Adds an implicit event from event symbol.
        /// </summary>
        public ClassSourceBuilder AddEvent(IEventSymbol @event, Action<IndentedStringBuilder> addBody, Action<IndentedStringBuilder> removeBody)
        {
            var eventType = (INamedTypeSymbol)@event.Type;
            var baseType = @event.ContainingType;
            var eventName = @event.Name;
            builder
                .Append("event ").Type(eventType).Append(" ").Type(baseType).AppendLine(".", eventName)
                .AddBlock(b => b
                    .AppendLine("add").AddBlock(addBody)
                    .AppendLine("remove").AddBlock(removeBody));
            return this;
        }

        /// <summary>
        ///     Adds a private field from type symbol and field name builder.
        /// </summary>
        public ClassSourceBuilder AddField(INamedTypeSymbol type, Action<IndentedStringBuilder> buildName)
        {
            builder.Append("private ").Type(type).Append(" ");
            buildName(builder);
            builder.AppendLine(";");
            return this;
        }

        /// <summary>
        ///     Adds a private field from type name.
        /// </summary>
        public ClassSourceBuilder AddField(string type, Action<IndentedStringBuilder> buildName)
        {
            builder.Append("private ", type, " ");
            buildName(builder);
            builder.AppendLine(";");
            return this;
        }

        /// <summary>
        ///     Adds a private field from type symbol and field name.
        /// </summary>
        public ClassSourceBuilder AddField(INamedTypeSymbol type, string name)
        {
            builder.Append("private ").Type(type).AppendLine(" ", name, ";");
            return this;
        }

        /// <summary>
        ///     Adds a private field from type name and field name.
        /// </summary>
        public ClassSourceBuilder AddField(string type, string name)
        {
            builder.AppendLine("private ", type, " ", name, ";");
            return this;
        }
    }
}