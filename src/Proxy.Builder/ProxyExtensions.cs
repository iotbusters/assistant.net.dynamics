using System;
using System.Linq.Expressions;
using System.Reflection;
using Assistant.Net.Dynamics.Abstractions;

namespace Assistant.Net.Dynamics
{
    /// <summary>
    ///     Fluent interception extensions for <see cref="Proxy{T}"/>.
    /// </summary>
    public static class ProxyExtensions
    {
        /// <summary>
        ///     Intercepts a method or property call defined in <paramref name="selector"/> to return <paramref name="result"/>.
        /// </summary>
        public static Proxy<T> Intercept<T, TResult>(
            this Proxy<T> proxy,
            Expression<Func<T, TResult>> selector,
            TResult result) =>
            proxy.Intercept(selector, (_, _, _) => result);

        /// <summary>
        ///     Intercepts a method or property call defined in <paramref name="selector"/> with <paramref name="interceptor"/>.
        /// </summary>
        public static Proxy<T> Intercept<T, TResult>(
            this Proxy<T> proxy,
            Expression<Func<T, TResult>> selector,
            Func<object?[], TResult> interceptor) =>
            proxy.Intercept(selector, (_, _, args) => interceptor(args));

        /// <summary>
        ///     Intercepts a method or property call defined in <paramref name="selector"/> with <paramref name="interceptor"/> in pipeline manner.
        /// </summary>
        public static Proxy<T> Intercept<T, TResult>(
            this Proxy<T> proxy,
            Expression<Func<T, TResult>> selector,
            Func<Func<object?[], TResult>, object?[], TResult> interceptor) =>
            proxy.Intercept(selector, (next, _, args) => interceptor(next, args));

        /// <summary>
        ///     Intercepts a method or property call defined in <paramref name="selector"/> with <paramref name="interceptor"/> in pipeline manner.
        /// </summary>
        public static Proxy<T> Intercept<T, TResult>(
            this Proxy<T> proxy,
            Expression<Func<T, TResult>> selector,
            Func<Func<object?[], TResult>, MethodInfo, object?[], TResult> interceptor)
        {
            switch (selector.Body)
            {
                case MemberExpression { Member: PropertyInfo { GetMethod: var getProperty/*, SetMethod: var setProperty*/ } }:
                    if (getProperty != null)
                        proxy.AddOrUpdate(getProperty!, (next, mi, args) => interceptor(x => (TResult) next(x)!, mi, args));
                    // note: setters are ignored as they weren't properly planned
                    // todo: implement setters
                    //if (setProperty != null)
                    //    proxy.AddOrUpdate(setProperty!, (next, method, args) => interceptor(x => (TResult) next(x)!, method, args));
                    return proxy;

                case MethodCallExpression { Method: var method }:
                    proxy.AddOrUpdate(method!, (next, mi, args) => interceptor(x => (TResult)next(x)!, mi, args));
                    return proxy;

                default:
                    throw new ArgumentException("Invalid expression value.", nameof(selector));
            }
        }

        /// <summary>
        ///     Intercepts a method or property call defined in <paramref name="selector"/> with <paramref name="interceptor"/>.
        /// </summary>
        public static Proxy<T> Intercept<T>(
            this Proxy<T> proxy,
            Expression<Action<T>> selector,
            Action<object?[]> interceptor) =>
            proxy.Intercept(selector, (_, _, args) => interceptor(args));

        /// <summary>
        ///     Intercepts a method or property call defined in <paramref name="selector"/> with <paramref name="interceptor"/> in pipeline manner.
        /// </summary>
        public static Proxy<T> Intercept<T>(
            this Proxy<T> proxy,
            Expression<Action<T>> selector,
            Action<Action<object?[]>, object?[]> interceptor) =>
            proxy.Intercept(selector, (next, _, args) => interceptor(next, args));

        /// <summary>
        ///     Intercepts a method or property call defined in <paramref name="selector"/> with <paramref name="interceptor"/> in pipeline manner.
        /// </summary>
        public static Proxy<T> Intercept<T>(
            this Proxy<T> proxy,
            Expression<Action<T>> selector,
            Action<Action<object?[]>, MethodInfo, object?[]> interceptor)
        {
            switch (selector.Body)
            {
                case MemberExpression { Member: PropertyInfo { GetMethod: var getProperty } }:
                    proxy.AddOrUpdate(getProperty!, (next, method, args) =>
                    {
                        interceptor(x => next(x), method, args);
                        return (object?)null;
                    });
                    return proxy;

                case MethodCallExpression { Method: var getMethod }:
                    proxy.AddOrUpdate(getMethod!, (next, method, args) =>
                    {
                        interceptor(x => next(x), method, args);
                        return (object?)null;
                    });
                    return proxy;

                default:
                    throw new ArgumentException("Invalid expression value.", nameof(selector));
            }
        }

        /// <summary>
        ///     Intercepts the setter of the property defined in <paramref name="selector"/> to override the assigned value.
        ///     Note: only properties with a getter can be targeted, since C# expression trees cannot reference set-only members.
        /// </summary>
        public static Proxy<T> InterceptSet<T, TValue>(
            this Proxy<T> proxy,
            Expression<Func<T, TValue>> selector,
            Action<TValue> interceptor) =>
            proxy.InterceptSet(selector, (_, args) => interceptor((TValue) args[0]!));

        /// <summary>
        ///     Intercepts the setter of the property defined in <paramref name="selector"/> in pipeline manner.
        /// </summary>
        public static Proxy<T> InterceptSet<T, TValue>(
            this Proxy<T> proxy,
            Expression<Func<T, TValue>> selector,
            Action<Action<object?[]>, object?[]> interceptor)
        {
            if (selector.Body is not MemberExpression { Member: PropertyInfo { SetMethod: { } setMethod } })
                throw new ArgumentException("Selector must reference a property with a setter.", nameof(selector));

            proxy.AddOrUpdate(setMethod, (next, _, args) =>
            {
                interceptor(x => next(x), args);
                return (object?) null;
            });
            return proxy;
        }

        private static void AddOrUpdate<TResult>(
            this IProxy proxy,
            MethodInfo method,
            Func<Func<object?[], object?>, MethodInfo, object?[], TResult> interceptor) =>
            proxy.AddInterceptor(method, (next, args) => interceptor(next, method, args));
    }

}
