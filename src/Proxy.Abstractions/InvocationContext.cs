using System.Reflection;

namespace Assistant.Net.Dynamics.Abstractions
{
    /// <summary>
    ///     Represents a single intercepted call. Materialized only when at least one interceptor is attached.
    /// </summary>
    public readonly struct InvocationContext
    {
        /// <summary/>
        public InvocationContext(MethodInfo method, object?[] arguments)
        {
            this.Method = method;
            this.Arguments = arguments;
        }

        /// <summary>
        ///     Intercepted member.
        /// </summary>
        public MethodInfo Method { get; }

        /// <summary>
        ///     Call arguments. Mutable slots enable ref/out write-back.
        /// </summary>
        public object?[] Arguments { get; }
    }
}
