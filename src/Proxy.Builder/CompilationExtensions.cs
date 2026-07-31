using Assistant.Net.Dynamics.Abstractions;
using Assistant.Net.Dynamics.Builders;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System;
using System.IO;
using System.Linq;
using System.Reflection;

namespace Assistant.Net.Dynamics
{
    /// <summary>
    ///     Compilation extensions for proxy generation.
    /// </summary>
    public static class CompilationExtensions
    {
        /// <summary>
        ///     Adds a generated proxy for <typeparamref name="T"/> interface to new compilation.
        /// </summary>
        public static Compilation AddProxy<T>(this Compilation compilation, string? @namespace = null) where T : class =>
            compilation.AddProxy(typeof(T), @namespace);

        /// <summary>
        ///     Adds a generated proxy from type to new compilation.
        /// </summary>
        public static Compilation AddProxy(this Compilation compilation, Type proxyType, string? @namespace = null)
        {
            var objectAssemblyLocation = typeof(object).Assembly.Location;
            if (string.IsNullOrEmpty(objectAssemblyLocation))
                throw new PlatformNotSupportedException(
                    "Runtime proxy generation requires on-disk framework assemblies and isn't supported "
                    + "under single-file publish or Native AOT.");

            var assemblyPath = Path.GetDirectoryName(objectAssemblyLocation)!;
            var systemAssemblies = new[]
            {
                Path.Combine(assemblyPath, "System.Runtime.dll"),
                Path.Combine(assemblyPath, "netstandard.dll"),
                proxyType.Assembly.Location
            }.Distinct().Select(x => MetadataReference.CreateFromFile(x));
            compilation = compilation.AddReferences(systemAssemblies);

            var proxyTypeSymbol = compilation.GetTypeSymbol(proxyType);
            return compilation.AddProxy(proxyTypeSymbol, @namespace);
        }

        /// <summary>
        ///     Adds a generated proxy from type symbol to new compilation.
        /// </summary>
        public static Compilation AddProxy(this Compilation compilation, INamedTypeSymbol proxyType, string? @namespace = null)
        {
            var builder = new SourceBuilder();
            return compilation
                .GenerateProxy(builder, proxyType, @namespace)
                .AddSyntaxTrees(CSharpSyntaxTree.ParseText(builder.ToString()));
        }

        /// <summary>
        ///     Generates a proxy in source builder and adds dependencies to new compilation.
        /// </summary>
        public static Compilation GenerateProxy(this Compilation compilation, SourceBuilder builder, INamedTypeSymbol proxyType, string? @namespace = null)
        {
            if (proxyType.TypeKind != TypeKind.Interface)
                throw new ArgumentException(
                    $"Expected an interface type but provided `{proxyType.Name}` instead.",
                    proxyType.Name);

            var systemAssemblies = new[] {typeof(Proxy<>).Assembly.Location}.Select(x => MetadataReference.CreateFromFile(x));
            compilation = compilation.AddReferences(systemAssemblies);

            var baseProxyTypeDefinition = compilation.GetTypeSymbol(typeof(Proxy<>));
            if(baseProxyTypeDefinition == null)
                throw new ArgumentException(
                    "Package `assistant.net.dynamics.proxy` is required. Please ensure it was installed.",
                    proxyType.Name);

            var defaultNamespace = proxyType.ContainingNamespace.ToString();
            var localAssemblies = new[]
                {
                    typeof(Func<>).Assembly.Location,
                    //typeof(Proxy<>).Assembly.Location,
                    typeof(Exception).Assembly.Location,
                    typeof(Enumerable).Assembly.Location,
                    typeof(MethodInfo).Assembly.Location
                }.Distinct().Select(x => MetadataReference.CreateFromFile(x));

            compilation = compilation.AddReferences(localAssemblies);

            var proxyTypeName = proxyType.Name + "Proxy";
            //var baseProxyType = compilation.GetTypeSymbol(typeof(Proxy<>))!.Construct(proxyType);
            var baseProxyType = baseProxyTypeDefinition!.Construct(proxyType);
            // Source generator couldn't resolve type symbols for some reason.
            // assuming, multiple types are being resolved because framework mixture.
            var exceptionType = "System.Exception";//compilation.GetTypeSymbol(typeof(Exception))!;
            var methodInfoType = "System.Reflection.MethodInfo";//compilation.GetTypeSymbol(typeof(MethodInfo))!;

            builder.AddNamespace(@namespace ?? defaultNamespace, nb =>
            {
                nb.AddClass(proxyTypeName, new []{ baseProxyType, proxyType }, cb =>
                {
                    var proxyTypeProperties = proxyType.GetMembers().OfType<IPropertySymbol>().ToArray();
                    var proxyTypeMethods = proxyType.GetMembers().OfType<IMethodSymbol>().Where(x => x.MethodKind == MethodKind.Ordinary).ToArray();
                    var proxyTypeEvents = proxyType.GetMembers().OfType<IEventSymbol>().ToArray();
                    var instanceFieldName = "instance";
                    var errorFieldName = "interceptionFailure";

                    cb.AddField(proxyType, instanceFieldName);
                    cb.AddField(exceptionType, errorFieldName);

                    foreach (var property in proxyTypeProperties)
                    {
                        if (property.GetMethod != null)
                            cb.AddField(methodInfoType, buildName: b => b.Append("get", property.Name));
                        if (property.SetMethod != null)
                            cb.AddField(methodInfoType, buildName: b => b.Append("set", property.Name));
                    }

                    foreach (var method in proxyTypeMethods)
                        cb.AddField(methodInfoType, buildName: b => b.MethodName(method));

                    foreach (var @event in proxyTypeEvents)
                    {
                        if (@event.AddMethod != null)
                            cb.AddField(methodInfoType, buildName: b => b.Append("add", @event.Name));
                        if (@event.RemoveMethod != null)
                            cb.AddField(methodInfoType, buildName: b => b.Append("remove", @event.Name));
                    }

                    cb.AddCtor(new[] {(instanceFieldName, proxyType)}, ccb =>
                    {
                        ccb.AppendLine("this.", instanceFieldName, " = ", instanceFieldName, ";");
                        ccb.AppendLine("this.", errorFieldName, " = ")
                            .Append("new InvalidOperationException(\"Neither ", instanceFieldName, " field was set")
                            .AppendLine(" nor interception configured.\");");

                        foreach (var property in proxyTypeProperties)
                        {
                            if (property.GetMethod != null)
                                ccb.Append("this.get", property.Name, " = typeof(").Type(proxyType).Append(")")
                                    .AppendLine(".GetProperty(\"", property.Name, "\").GetMethod;");
                            if (property.SetMethod != null)
                                ccb.Append("this.set", property.Name, " = typeof(").Type(proxyType).Append(")")
                                    .AppendLine(".GetProperty(\"", property.Name, "\").SetMethod;");
                        }

                        foreach (var method in proxyTypeMethods)
                        {
                            var parameterTypes = method.Parameters.Select(x => x.Type).ToArray();
                            if (method.IsGenericMethod && parameterTypes.Any())
                                ccb.Append("this.").MethodName(method).Append(" = typeof(").Type(proxyType)
                                    .Append(").GetMethods()")
                                    .Append(".Single(x => x.IsGenericMethod && x.Name == \"").Append(method.Name).Append("\" ")
                                    .Append("&& x.GetParameters().Select(y => y.ParameterType.Name).SequenceEqual(new string[] {")
                                    .AppendJoin(", ", parameterTypes, (b, type) => b.Append("\"", type.Name, "\""))
                                    .AppendLine("}));");
                            else
                                ccb.Append("this.").MethodName(method).Append(" = typeof(").Type(proxyType)
                                    .Append(").GetMethod(\"")
                                    .Append(method.Name).Append("\", new Type[] {")
                                    .AppendJoin(
                                        ", ",
                                        method.Parameters.ToArray(),
                                        (b, parameter) =>
                                        {
                                            if (parameter.RefKind == RefKind.None)
                                                b.Append("typeof(").Type(parameter.Type).Append(")");
                                            else
                                                b.Append("typeof(").Type(parameter.Type).Append(").MakeByRefType()");
                                        })
                                    .AppendLine("});");
                        }

                        foreach (var @event in proxyTypeEvents)
                        {
                            if (@event.AddMethod != null)
                                ccb.Append("this.add", @event.Name, " = typeof(").Type(proxyType)
                                    .AppendLine(").GetEvent(\"", @event.Name, "\").AddMethod;");
                            if (@event.RemoveMethod != null)
                                ccb.Append("this.remove", @event.Name, " = typeof(").Type(proxyType)
                                    .AppendLine(").GetEvent(\"", @event.Name, "\").RemoveMethod;");
                        }
                    });

                    foreach (var property in proxyTypeProperties)
                        cb.AddProperty(
                            property,
                            getter: b => b
                                .Append("if (!IsIntercepted(this.get", property.Name, "))")
                                .AddBlock(ib => ib
                                    .Append("if (this.", instanceFieldName, " == null) ")
                                    .AppendLine("throw this.", errorFieldName, ";")
                                    .AppendLine("return this.", instanceFieldName, ".", property.Name, ";"))
                                .Append("return (").Type(property.Type).Append(") Invoke(this.get", property.Name, ", new object[0], x =>")
                                .AddBlock(ib => ib
                                    .Append("if (this.", instanceFieldName, " == null) ")
                                    .AppendLine("throw this.", errorFieldName, ";")
                                    .AppendLine("return this.", instanceFieldName, ".", property.Name, ";"))
                                .AppendLine(");"),
                            setter: b => b
                                .Append("if (!IsIntercepted(this.set", property.Name, "))")
                                .AddBlock(ib => ib
                                    .Append("if (this.", instanceFieldName, " == null) ")
                                    .AppendLine("throw this.", errorFieldName, ";")
                                    .AppendLine("this.", instanceFieldName, ".", property.Name, " = value;")
                                    .AppendLine("return;"))
                                .Append("Invoke(this.set", property.Name, ", new object[] {value}, x =>")
                                .AddBlock(ib => ib
                                    .Append("if (this.", instanceFieldName, " == null) ")
                                    .AppendLine("throw this.", errorFieldName, ";")
                                    .Append("this.", instanceFieldName, ".", property.Name, " = (").Type(property.Type).AppendLine(") x[0];")
                                    .AppendLine("return null;"))
                                .AppendLine(");")
                            );

                    foreach (var method in proxyTypeMethods)
                    {
                        var parameters = method.Parameters.ToArray();
                        var argumentNames = parameters.Select(x => x.Name!).ToArray();
                        var hasByRefParameters = parameters.Any(x => x.RefKind != RefKind.None);

                        cb.AddMethod(method, b =>
                        {
                            b.Append("var key = this.").MethodName(method);
                            if (method.IsGenericMethod)
                                b.Append(".MakeGenericMethod(")
                                    .AppendJoin(", ", method.TypeArguments.ToArray(), (bt, type) => bt.Append("typeof(").Type(type).Append(")"))
                                    .Append(")");
                            b.AppendLine(";");

                            b.Append("if (!IsIntercepted(key))");
                            if (method.ReturnsVoid)
                                b.AddBlock(ib => ib
                                    .Append("if (this.", instanceFieldName, " == null) ")
                                    .AppendLine("throw this.", errorFieldName, ";")
                                    .Append("this.", instanceFieldName, ".", method.Name, "(")
                                    .AppendJoin(", ", parameters, (pb, p) => pb.Append(p.RefKind.ToPrefix(), p.Name!)).AppendLine(");")
                                    .AppendLine("return;"));
                            else
                                b.AddBlock(ib => ib
                                    .Append("if (this.", instanceFieldName, " == null) ")
                                    .AppendLine("throw this.", errorFieldName, ";")
                                    .Append("return this.", instanceFieldName, ".", method.Name, "(")
                                    .AppendJoin(", ", parameters, (pb, p) => pb.Append(p.RefKind.ToPrefix(), p.Name!)).AppendLine(");"));

                            if (!hasByRefParameters)
                            {
                                if (method.ReturnsVoid)
                                    b.Append("Invoke(key, new object[] {")
                                        .AppendJoin(", ", argumentNames).Append("}, x =>")
                                        .AddBlock(ib => ib
                                            .Append("if (this.", instanceFieldName, " == null) ")
                                            .AppendLine("throw this.", errorFieldName, ";")
                                            .Append("this.", instanceFieldName, ".", method.Name, "(")
                                            .AppendJoin(", ", argumentNames).AppendLine(");")
                                            .AppendLine("return null;"))
                                        .AppendLine(");");
                                else
                                    b.Append("return (").Type(method.ReturnType).Append(") Invoke(key, new object[] {")
                                        .AppendJoin(", ", argumentNames).Append("}, x =>")
                                        .AddBlock(ib => ib
                                            .Append("if (this.", instanceFieldName, " == null) ")
                                            .AppendLine("throw this.", errorFieldName, ";")
                                            .Append("return this.", instanceFieldName, ".", method.Name, "(")
                                            .AppendJoin(", ", argumentNames).AppendLine(");"))
                                        .AppendLine(");");
                                return;
                            }

                            // ref/out parameters: materialize args into a local so mutations can be written back after Invoke.
                            b.Append("var args = new object[] {")
                                .AppendJoin(", ", parameters, (pb, p) =>
                                {
                                    if (p.RefKind == RefKind.Out)
                                        pb.Append("default(").Type(p.Type).Append(")");
                                    else
                                        pb.Append(p.Name!);
                                })
                                .AppendLine("};");

                            void Tail(IndentedStringBuilder ib)
                            {
                                ib.Append("if (this.", instanceFieldName, " == null) ")
                                    .AppendLine("throw this.", errorFieldName, ";");
                                for (var i = 0; i < parameters.Length; i++)
                                    if (parameters[i].RefKind != RefKind.None)
                                        ib.Append("var local", i.ToString(), " = (").Type(parameters[i].Type).Append(") x[", i.ToString(), "];").AppendLine();

                                ib.Append(method.ReturnsVoid ? string.Empty : "var result = ", "this.", instanceFieldName, ".", method.Name, "(")
                                    .AppendJoin(", ", Enumerable.Range(0, parameters.Length).ToArray(), (pb, i) =>
                                    {
                                        if (parameters[i].RefKind == RefKind.None)
                                            pb.Append(parameters[i].Name!);
                                        else
                                            pb.Append(parameters[i].RefKind.ToPrefix(), "local", i.ToString());
                                    })
                                    .AppendLine(");");

                                for (var i = 0; i < parameters.Length; i++)
                                    if (parameters[i].RefKind is RefKind.Ref or RefKind.Out)
                                        ib.AppendLine("x[", i.ToString(), "] = local", i.ToString(), ";");

                                ib.AppendLine(method.ReturnsVoid ? "return null;" : "return result;");
                            }

                            if (method.ReturnsVoid)
                                b.Append("Invoke(key, args, x =>").AddBlock(Tail).AppendLine(");");
                            else
                                b.Append("var invokeResult = Invoke(key, args, x =>").AddBlock(Tail).AppendLine(");");

                            for (var i = 0; i < parameters.Length; i++)
                                if (parameters[i].RefKind is RefKind.Ref or RefKind.Out)
                                    b.Append(parameters[i].Name!, " = (").Type(parameters[i].Type).Append(") args[", i.ToString(), "];").AppendLine();

                            if (!method.ReturnsVoid)
                                b.Append("return (").Type(method.ReturnType).Append(") invokeResult;").AppendLine();
                        });
                    }

                    foreach (var @event in proxyTypeEvents)
                    {
                        cb.AddEvent(
                            @event,
                            addBody: b => b
                                .Append("if (!IsIntercepted(this.add", @event.Name, "))")
                                .AddBlock(ib => ib
                                    .Append("if (this.", instanceFieldName, " == null) ")
                                    .AppendLine("throw this.", errorFieldName, ";")
                                    .AppendLine("this.", instanceFieldName, ".", @event.Name, " += value;")
                                    .AppendLine("return;"))
                                .Append("Invoke(this.add", @event.Name, ", new object[] {value}, x =>")
                                .AddBlock(ib => ib
                                    .Append("if (this.", instanceFieldName, " == null) ")
                                    .AppendLine("throw this.", errorFieldName, ";")
                                    .AppendLine("this.", instanceFieldName, ".", @event.Name, " += value;")
                                    .AppendLine("return null;"))
                                .AppendLine(");"),
                            removeBody: b => b
                                .Append("if (!IsIntercepted(this.remove", @event.Name, "))")
                                .AddBlock(ib => ib
                                    .Append("if (this.", instanceFieldName, " == null) ")
                                    .AppendLine("throw this.", errorFieldName, ";")
                                    .AppendLine("this.", instanceFieldName, ".", @event.Name, " -= value;")
                                    .AppendLine("return;"))
                                .Append("Invoke(this.remove", @event.Name, ", new object[] {value}, x =>")
                                .AddBlock(ib => ib
                                    .Append("if (this.", instanceFieldName, " == null) ")
                                    .AppendLine("throw this.", errorFieldName, ";")
                                    .AppendLine("this.", instanceFieldName, ".", @event.Name, " -= value;")
                                    .AppendLine("return null;"))
                                .AppendLine(");"));
                    }
                });
            });

            if (compilation.GetTypeByMetadataName("Assistant.Net.Dynamics.KnownProxy") != null)
                builder.AddProxyRegistration(@namespace ?? defaultNamespace, proxyType, proxyTypeName);

            return compilation;
        }

        private static INamedTypeSymbol GetTypeSymbol(this Compilation compilation, Type proxyType) =>
            compilation.GetTypeByMetadataName(proxyType.FullName!)
            ?? throw new InvalidOperationException($"Cannot find a symbol for '{proxyType}' type.");
    }
}
