using Assistant.Net.Dynamics.Abstractions;
using Assistant.Net.Dynamics.Internal;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Assistant.Net.Dynamics
{
    /// <summary>
    ///     Registration extensions for opt-in runtime proxy generation.
    /// </summary>
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        ///     Enables runtime Roslyn-based proxy generation for types not resolvable at compile time.
        ///     Replaces any previously registered <see cref="IProxyFactory"/>, regardless of call order.
        /// </summary>
        public static IServiceCollection AddDynamicProxyGeneration(this IServiceCollection services)
        {
            services.Replace(ServiceDescriptor.Singleton<IProxyFactory, DynamicProxyFactory>());
            return services;
        }
    }
}
