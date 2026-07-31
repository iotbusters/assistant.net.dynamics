using Assistant.Net.Dynamics.Abstractions;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Assistant.Net.Dynamics
{
    /// <summary>
    ///     Global proxy registry.
    /// </summary>
    public static class KnownProxy
    {
        private static readonly ConcurrentDictionary<Type, Type> Types = new();
        private static readonly ConcurrentDictionary<Type, Func<object?, object>> Factories = new();

        /// <summary>
        ///     Known proxy type implementations.
        /// </summary>
        public static IReadOnlyDictionary<Type, Type> ProxyTypes => Types;

        /// <summary>
        ///     Registers all proxy type implementations from the <paramref name="proxyAssembly"/>.
        /// </summary>
        /// <exception cref="InvalidOperationException" />
        public static void RegisterFrom(Assembly proxyAssembly)
        {
            foreach (var proxyType in GetLoadableTypes(proxyAssembly).Where(x => x.IsProxy()))
                Register(proxyType);
        }

        private static IEnumerable<Type> GetLoadableTypes(Assembly assembly)
        {
            try
            {
                return assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException exception)
            {
                return exception.Types.Where(type => type != null)!;
            }
        }

        /// <summary>
        ///     Registers the <paramref name="proxyType"/> implementation.
        /// </summary>
        /// <exception cref="InvalidOperationException" />
        public static bool Register(Type proxyType) =>
            Types.TryAdd(proxyType.GetInstanceType(), proxyType);

        /// <summary>
        ///     Registers a strongly-typed proxy <paramref name="factory"/> for the <paramref name="instanceType"/> interface.
        /// </summary>
        public static bool RegisterFactory(Type instanceType, Type proxyType, Func<object?, object> factory)
        {
            Types.TryAdd(instanceType, proxyType);
            return Factories.TryAdd(instanceType, factory);
        }

        internal static bool TryGetFactory(Type instanceType, out Func<object?, object>? factory) =>
            Factories.TryGetValue(instanceType, out factory);

        /// <summary>
        ///     Resolves proxy type from proxy type implementation.
        /// </summary>
        /// <exception cref="InvalidOperationException" />
        public static Type GetInstanceType(this Type proxyType)
        {
            if (!proxyType.IsProxy())
                throw NotProxyTypeError(proxyType);

            return proxyType.BaseType!.GetGenericArguments().Single();
        }

        /// <summary>
        ///     Checks if the <paramref name="type"/> is a proxy.
        /// </summary>
        public static bool IsProxy(this Type type) =>
            typeof(IProxy).IsAssignableFrom(type)
            && type.BaseType is { IsGenericType: true }
            && type.BaseType.GetGenericTypeDefinition() == typeof(Proxy<>);

        private static Exception NotProxyTypeError(Type proxyType) =>
            new InvalidOperationException($"Type '{proxyType}' isn't a proxy.");
    }
}