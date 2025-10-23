using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace SxgEvalPlatformApi.Services.Cache
{
    /// <summary>
    /// Extension methods for adding automatic caching to services
    /// </summary>
    public static class CachingExtensions
    {
        /// <summary>
        /// Adds a service with automatic caching decoration
        /// </summary>
        /// <typeparam name="TInterface">Service interface</typeparam>
        /// <typeparam name="TImplementation">Service implementation</typeparam>
        /// <param name="services">Service collection</param>
        /// <param name="lifetime">Service lifetime</param>
        /// <returns>Service collection for chaining</returns>
        public static IServiceCollection AddCachedService<TInterface, TImplementation>(
            this IServiceCollection services,
            ServiceLifetime lifetime = ServiceLifetime.Scoped)
            where TInterface : class
            where TImplementation : class, TInterface
        {
            // Register the actual implementation
            services.Add(new ServiceDescriptor(typeof(TImplementation), typeof(TImplementation), lifetime));

            // Register the interface with caching decorator
            services.Add(new ServiceDescriptor(typeof(TInterface), provider =>
            {
                var implementation = provider.GetRequiredService<TImplementation>();
                var cacheService = provider.GetRequiredService<IGenericCacheService>();
                var logger = provider.GetRequiredService<ILogger<TImplementation>>();

                return CachingDecorator<TInterface>.Create(implementation, cacheService, logger);
            }, lifetime));

            return services;
        }

        /// <summary>
        /// Adds caching to an existing service registration
        /// </summary>
        /// <typeparam name="T">Service type</typeparam>
        /// <param name="services">Service collection</param>
        /// <returns>Service collection for chaining</returns>
        public static IServiceCollection AddCachingTo<T>(this IServiceCollection services)
            where T : class
        {
            services.Decorate<T>((implementation, provider) =>
            {
                var cacheService = provider.GetRequiredService<IGenericCacheService>();
                var logger = provider.GetRequiredService<ILogger<T>>();

                return CachingDecorator<T>.Create(implementation, cacheService, logger);
            });

            return services;
        }
    }

    /// <summary>
    /// Simple decorator extension method
    /// </summary>
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection Decorate<T>(this IServiceCollection services, Func<T, IServiceProvider, T> decorator)
        {
            var serviceDescriptor = services.FirstOrDefault(s => s.ServiceType == typeof(T));
            if (serviceDescriptor == null)
            {
                throw new InvalidOperationException($"Service of type {typeof(T).Name} not found");
            }

            services.Remove(serviceDescriptor);

            services.Add(new ServiceDescriptor(typeof(T), provider =>
            {
                var originalService = serviceDescriptor.ImplementationType != null
                    ? (T)ActivatorUtilities.CreateInstance(provider, serviceDescriptor.ImplementationType)
                    : (T)serviceDescriptor.ImplementationFactory!(provider);

                return decorator(originalService, provider);
            }, serviceDescriptor.Lifetime));

            return services;
        }
    }
}