using System;
using System.Reflection;

namespace Assistant.Net.Dynamics.Abstractions
{
    /// <summary>
    ///     Proxy abstraction.
    /// </summary>
    public interface IProxy
    {
        /// <summary>
        ///     Registers an <paramref name="interceptor"/> for the <paramref name="method"/> member.
        ///     The most recently added interceptor becomes the outermost in the pipeline.
        /// </summary>
        void AddInterceptor(MethodInfo method, Func<Func<object?[], object?>, object?[], object?> interceptor);
    }
}
