using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Sxg.EvalPlatform.API.Storage.Configuration;
using Sxg.EvalPlatform.API.Storage.Services;
using StackExchange.Redis;
using SXG.EvalPlatform.Common;
using Azure.Identity;
using Microsoft.Azure.StackExchangeRedis;

namespace Sxg.EvalPlatform.API.Storage.Extensions
{
    /// <summary>
    /// Extension methods for configuring cache services
    /// </summary>
    public static class CacheServiceExtensions
    {
        /// <summary>
        /// Add cache services to the service collection
        /// </summary>
        /// <param name="services">The service collection</param>
        /// <returns>The service collection for chaining</returns>
        public static IServiceCollection AddCacheServices(this IServiceCollection services)
        {
            // Register a delegate that will resolve configuration at runtime instead of build time
            services.AddSingleton<ICacheManager>(serviceProvider =>
            {
                var configHelper = serviceProvider.GetRequiredService<IConfigHelper>();
                var dataCachingEnabled = configHelper.IsDataCachingEnabled();
                var loggerFactory = serviceProvider.GetService<ILoggerFactory>();
                var logger = loggerFactory?.CreateLogger("CacheServiceExtensions");

                logger?.LogInformation("🔍 Runtime cache configuration check - EnableDataCaching: {Enabled}", dataCachingEnabled);

                if (!dataCachingEnabled)
                {
                    logger?.LogWarning("⚠️ Data caching is DISABLED via FeatureFlags:EnableDataCaching configuration");
                    logger?.LogInformation("✅ Returning NoCacheManager - all cache operations will be no-ops");

                    // Return NoCacheManager directly - no Redis connection will be attempted
                    return new NoCacheManager(logger.GetType().Name == "ILogger`1"
                        ? serviceProvider.GetRequiredService<ILogger<NoCacheManager>>()
                        : loggerFactory.CreateLogger<NoCacheManager>());
                }

                logger?.LogInformation("✅ Data caching is ENABLED - creating cache manager");

                // Get cache configuration
                var cacheOptions = configHelper.GetConfigurationSection<CacheOptions>("Cache");
                logger?.LogInformation("Cache provider selected: {Provider}", cacheOptions.Provider);

                // Return appropriate cache manager based on provider
                switch (cacheOptions.Provider.ToLowerInvariant())
                {
                    case "redis":
                    case "distributed":
                        var distributedCache = serviceProvider.GetRequiredService<Microsoft.Extensions.Caching.Distributed.IDistributedCache>();
                        var cacheLogger = serviceProvider.GetRequiredService<ILogger<RedisCacheManager>>();
                        var conn = serviceProvider.GetService<IConnectionMultiplexer>();
                        return new RedisCacheManager(distributedCache, cacheLogger, conn);

                    case "memory":
                    default:
                        var memoryCache = serviceProvider.GetRequiredService<Microsoft.Extensions.Caching.Memory.IMemoryCache>();
                        var memLogger = serviceProvider.GetRequiredService<ILogger<MemoryCacheManager>>();
                        return new MemoryCacheManager(memoryCache, memLogger);
                }
            });

            // ONLY register distributed cache and ConnectionMultiplexer if caching is enabled
            // This prevents Redis connection attempts during startup when caching is disabled
            services.AddSingleton(serviceProvider =>
            {
                var configHelper = serviceProvider.GetRequiredService<IConfigHelper>();
                var dataCachingEnabled = configHelper.IsDataCachingEnabled();

                if (!dataCachingEnabled)
                {
                    // Return a null marker to indicate caching is disabled
                    return (IConnectionMultiplexer)null;
                }

                // Only configure Redis if caching is enabled
                var cacheOptions = configHelper.GetConfigurationSection<CacheOptions>("Cache");
                if (cacheOptions.Provider.ToLowerInvariant() == "redis" ||
                    cacheOptions.Provider.ToLowerInvariant() == "distributed")
                {
                    return ConfigureRedisConnection(serviceProvider, configHelper, cacheOptions);
                }

                return (IConnectionMultiplexer)null;
            });

            return services;
        }

        private static IConnectionMultiplexer ConfigureRedisConnection(
            IServiceProvider serviceProvider,
            IConfigHelper configHelper,
            CacheOptions cacheOptions)
        {
            // Parse endpoint
            var endpointParts = cacheOptions.Redis.Endpoint.Split(',')[0].Split(':');
            var host = endpointParts[0];
            var port = endpointParts.Length > 1 && int.TryParse(endpointParts[1], out var p) ? p : 6380;
            var cacheName = host.Split('.')[0];

            // Get credential
            var credential = CommonUtils.GetTokenCredential(configHelper.GetASPNetCoreEnvironment());

            // Only set username for PPE and Production (Managed Identity environments)
            var requiresUsername = configHelper.GetASPNetCoreEnvironment().Equals("PPE", StringComparison.OrdinalIgnoreCase) ||
           configHelper.GetASPNetCoreEnvironment().Equals("Production", StringComparison.OrdinalIgnoreCase);

            // Configure Redis connection
            var configOptions = new ConfigurationOptions
            {
                EndPoints = { { host, port } },
                Ssl = true,
                AbortOnConnectFail = false,
                ConnectTimeout = cacheOptions.Redis.ConnectTimeoutSeconds * 1000,
                SyncTimeout = cacheOptions.Redis.CommandTimeoutSeconds * 1000,
                AsyncTimeout = cacheOptions.Redis.CommandTimeoutSeconds * 1000,
                ConnectRetry = cacheOptions.Redis.Retry.MaxRetryAttempts,
                ReconnectRetryPolicy = new LinearRetry(cacheOptions.Redis.Retry.BaseDelayMs),
                KeepAlive = 60,
                AllowAdmin = false
            };

            // Only set username for cloud environments
            if (requiresUsername)
            {
                configOptions.User = cacheName;
            }

            // Configure with token credential
            var configTask = configOptions.ConfigureForAzureWithTokenCredentialAsync(credential);
            configTask.GetAwaiter().GetResult();

            return ConnectionMultiplexer.Connect(configOptions);
        }
    }
}