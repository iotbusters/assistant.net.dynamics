using System;
using System.Collections.Concurrent;
using System.Reflection;

namespace Assistant.Net.Dynamics.Abstractions
{
    /// <summary>
    ///     Proxy builder abstraction.
    /// </summary>
    /// <typeparam name="T">Proxy interface type.</typeparam>
    public class Proxy<T> : IProxy
    {
        // Per-member interceptor chain. Each value is an immutable array (newest first = outermost).
        // ConcurrentDictionary gives lock-free reads on the hot path; configuration-time mutation is thread-safe.
        private readonly ConcurrentDictionary<MethodInfo, Func<Func<object?[], object?>, object?[], object?>[]> chains = new();

        void IProxy.AddInterceptor(MethodInfo method, Func<Func<object?[], object?>, object?[], object?> interceptor) =>
            this.chains.AddOrUpdate(
                method,
                _ => new[] { interceptor },
                (_, existing) => Prepend(interceptor, existing));

        /// <summary>
        ///     Fast-path check whether the <paramref name="method"/> member has any interceptor attached.
        /// </summary>
        protected bool IsIntercepted(MethodInfo method) => this.chains.ContainsKey(method);

        /// <summary>
        ///     Runs the cached interceptor chain for the <paramref name="method"/> member, falling back to
        ///     <paramref name="defaultBehaviour"/> once the chain is exhausted or when no interceptor is attached.
        /// </summary>
        protected object? Invoke(MethodInfo method, object?[] arguments, Func<object?[], object?> defaultBehaviour)
        {
            if (!this.chains.TryGetValue(method, out var chain) || chain.Length == 0)
                return defaultBehaviour(arguments);

            var context = new InvocationContext(method, arguments);
            return Run(chain, defaultBehaviour, in context);
        }

        /// <summary>
        ///     Proxy object.
        /// </summary>
        public T Object => (T)(object)this;

        private static Func<Func<object?[], object?>, object?[], object?>[] Prepend(
            Func<Func<object?[], object?>, object?[], object?> interceptor,
            Func<Func<object?[], object?>, object?[], object?>[] existing)
        {
            var updated = new Func<Func<object?[], object?>, object?[], object?>[existing.Length + 1];
            updated[0] = interceptor;
            Array.Copy(existing, 0, updated, 1, existing.Length);
            return updated;
        }

        private static object? Run(
            Func<Func<object?[], object?>, object?[], object?>[] chain,
            Func<object?[], object?> tail,
            in InvocationContext context)
        {
            return Step(0, context.Arguments);

            object? Step(int index, object?[] args)
            {
                if (index >= chain.Length)
                    return tail(args);
                var current = chain[index];
                return current(next => Step(index + 1, next), args);
            }
        }
    }
}
