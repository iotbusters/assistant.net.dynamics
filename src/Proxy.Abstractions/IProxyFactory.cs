namespace Assistant.Net.Dynamics.Abstractions
{
    /// <summary>
    ///     Dynamic proxy factory abstraction.
    /// </summary>
    public interface IProxyFactory
    {
        /// <summary>
        ///     Creates proxy instance of <typeparamref name="T"/> interface.
        /// </summary>
        /// <param name="instance">A fallback instance for the created proxy.</param>
        Proxy<T> Create<T>(T? instance = null) where T : class;
    }
}
