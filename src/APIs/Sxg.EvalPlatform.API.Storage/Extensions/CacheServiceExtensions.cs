using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.StackExchangeRedis;
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
            // Always register memory cache
            services.AddMemoryCache(options =>
            {
                // Default memory cache configuration
                options.SizeLimit = 100 * 1024 * 1024; // 100 MB default
                options.CompactionPercentage = 0.25;
                options.ExpirationScanFrequency = TimeSpan.FromMinutes(1);
            });

            // Always register distributed memory cache as fallback - this ensures IDistributedCache is always available
            services.AddDistributedMemoryCache();

            // Register Connection Multiplexer conditionally
            services.AddSingleton(serviceProvider =>
            {
                var configHelper = serviceProvider.GetRequiredService<IConfigHelper>();
                var cacheOptions = configHelper.GetConfigurationSection<CacheOptions>("Cache");

                // Only configure Redis if Redis provider is selected
                if (cacheOptions.Provider.ToLowerInvariant() == "redis" ||
                    cacheOptions.Provider.ToLowerInvariant() == "distributed")
                {
                    try
                    {
                        return ConfigureRedisConnection(serviceProvider, configHelper, cacheOptions);
                    }
                    catch (Exception ex)
                    {
                        var logger = serviceProvider.GetService<ILogger<RedisCacheManager>>();
                        logger?.LogError(ex, "Failed to configure Redis connection, will fall back to memory cache");
                        return (IConnectionMultiplexer)null;
                    }
                }

                return (IConnectionMultiplexer)null;
            });

            // Register ICacheManager with runtime configuration resolution
            services.AddSingleton<ICacheManager>(serviceProvider =>
            {
                var configHelper = serviceProvider.GetRequiredService<IConfigHelper>();
                var loggerFactory = serviceProvider.GetService<ILoggerFactory>();
                var logger = loggerFactory?.CreateLogger("CacheServiceExtensions");

                // Get cache configuration
                var cacheOptions = configHelper.GetConfigurationSection<CacheOptions>("Cache");
                logger?.LogInformation("🔍 Cache configuration check - Provider: {Provider}", cacheOptions.Provider);

                // Return appropriate cache manager based on provider
                switch (cacheOptions.Provider.ToLowerInvariant())
                {
                    case "none":
                    case "disabled":
                    case "":
                        logger?.LogWarning("⚠️ Data caching is DISABLED via Cache:Provider configuration");
                        logger?.LogInformation("✅ Returning NoCacheManager - all cache operations will be no-ops");
                        return new NoCacheManager(logger.GetType().Name == "ILogger`1"
                            ? serviceProvider.GetRequiredService<ILogger<NoCacheManager>>()
                            : loggerFactory.CreateLogger<NoCacheManager>());

                    case "redis":
                    case "distributed":
                        logger?.LogInformation("✅ Redis caching is ENABLED - creating Redis cache manager");
                        // Try to get Redis connection, fallback to memory if not available
                        var conn = serviceProvider.GetService<IConnectionMultiplexer>();
                        if (conn != null && conn.IsConnected)
                        {
                            var distributedCache = serviceProvider.GetRequiredService<Microsoft.Extensions.Caching.Distributed.IDistributedCache>();
                            var cacheLogger = serviceProvider.GetRequiredService<ILogger<RedisCacheManager>>();
                            return new RedisCacheManager(distributedCache, cacheLogger, conn);
                        }
                        else
                        {
                            logger?.LogWarning("Redis connection not available, falling back to memory cache manager");
                            var fallbackMemoryCache = serviceProvider.GetRequiredService<Microsoft.Extensions.Caching.Memory.IMemoryCache>();
                            var fallbackMemLogger = serviceProvider.GetRequiredService<ILogger<MemoryCacheManager>>();
                            return new MemoryCacheManager(fallbackMemoryCache, fallbackMemLogger);
                        }

                    case "memory":
                    default:
                        logger?.LogInformation("✅ Memory caching is ENABLED - creating memory cache manager");
                        var memoryCache = serviceProvider.GetRequiredService<Microsoft.Extensions.Caching.Memory.IMemoryCache>();
                        var memLogger = serviceProvider.GetRequiredService<ILogger<MemoryCacheManager>>();
                        return new MemoryCacheManager(memoryCache, memLogger);
                }
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
            var environment = configHelper.GetASPNetCoreEnvironment();
            var managedIdentityClientId = configHelper.GetManagedIdentityClientId();
            var credential = CommonUtils.GetTokenCredential(environment, managedIdentityClientId);

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