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
            // Ensure IConfigHelper is registered first (if not already)
            // This is safe to call multiple times
            if (!services.Any(x => x.ServiceType == typeof(IConfigHelper)))
            {
                throw new InvalidOperationException("IConfigHelper must be registered before calling AddCacheServices. Please ensure AddBusinessServices is called first.");
            }

            // Build a temporary service provider to resolve IConfigHelper
            using (var serviceProvider = services.BuildServiceProvider())
            {
                var configHelper = serviceProvider.GetRequiredService<IConfigHelper>();
                var dataCachingEnabled = configHelper.IsDataCachingEnabled();

                if (!dataCachingEnabled)
                {
                    var loggerFactory = serviceProvider.GetService<ILoggerFactory>();
                    var logger = loggerFactory?.CreateLogger("CacheServiceExtensions");
                    logger?.LogWarning("⚠️ Data caching is DISABLED via FeatureFlags:EnableDataCaching configuration");

                    // Register NoCacheManager when caching is disabled
                    services.AddScoped<ICacheManager, NoCacheManager>();
                    return services;
                }

                // Get cache configuration from IConfigHelper
                var cacheOptions = configHelper.GetConfigurationSection<CacheOptions>("Cache");
                services.Configure<CacheOptions>(options =>
                {
                    options.Provider = cacheOptions.Provider;
                    options.DefaultExpirationMinutes = cacheOptions.DefaultExpirationMinutes;
                    options.Memory = cacheOptions.Memory;
                    options.Redis = cacheOptions.Redis;
                });

                // Register cache services based on provider type
                switch (cacheOptions.Provider.ToLowerInvariant())
                {
                    case "redis":
                    case "distributed":
                        services.AddRedisCacheServices(cacheOptions, configHelper);
                        break;
                    case "memory":
                    default:
                        services.AddMemoryCacheServices(cacheOptions);
                        break;
                }
            }

            return services;
        }

        /// <summary>
        /// Add memory cache services
        /// </summary>
        private static IServiceCollection AddMemoryCacheServices(this IServiceCollection services, CacheOptions cacheOptions)
        {
            // Configure memory cache options
            services.AddMemoryCache(options =>
            {
                if (cacheOptions.Memory.SizeLimitMB > 0)
                {
                    options.SizeLimit = cacheOptions.Memory.SizeLimitMB * 1024 * 1024; // Convert MB to bytes
                }
                options.CompactionPercentage = cacheOptions.Memory.CompactionPercentage;
                options.ExpirationScanFrequency = TimeSpan.FromSeconds(cacheOptions.Memory.ExpirationScanFrequencySeconds);
            });

            // Register cache manager
            services.AddScoped<ICacheManager>(provider =>
            {
                var memoryCache = provider.GetRequiredService<Microsoft.Extensions.Caching.Memory.IMemoryCache>();
                var logger = provider.GetRequiredService<ILogger<MemoryCacheManager>>();

                return new MemoryCacheManager(memoryCache, logger);
            });

            return services;
        }

        /// <summary>
        /// Add Redis cache services with Azure AD (Managed Identity) authentication
        /// </summary>
        private static IServiceCollection AddRedisCacheServices(this IServiceCollection services, CacheOptions cacheOptions, IConfigHelper configHelper)
        {
            if (string.IsNullOrEmpty(cacheOptions.Redis.Endpoint))
            {
                var logger = services.BuildServiceProvider().GetService<ILoggerFactory>()?.CreateLogger("CacheServiceExtensions");
                logger?.LogWarning("Redis endpoint is not configured. Falling back to memory cache.");
                return services.AddMemoryCacheServices(cacheOptions);
            }

            var environment = configHelper.GetASPNetCoreEnvironment();

            var loggerFactory = services.BuildServiceProvider().GetService<ILoggerFactory>();
            var startupLogger = loggerFactory?.CreateLogger("CacheServiceExtensions");

            startupLogger?.LogInformation("Configuring Redis cache - Endpoint: {Endpoint}, Environment: {Environment}",
                cacheOptions.Redis.Endpoint, environment);

            try
            {
                // Parse endpoint
                var endpointParts = cacheOptions.Redis.Endpoint.Split(',')[0].Split(':');
                var host = endpointParts[0];
                var port = endpointParts.Length > 1 && int.TryParse(endpointParts[1], out var p) ? p : 6380;
                var cacheName = host.Split('.')[0];

                // Get credential
                var credential = CommonUtils.GetTokenCredential(environment);

                // Only set username for PPE and Production (Managed Identity environments)
                var requiresUsername = environment.Equals("PPE", StringComparison.OrdinalIgnoreCase) ||
               environment.Equals("Production", StringComparison.OrdinalIgnoreCase);

                startupLogger?.LogInformation("Using credential type: {CredentialType}, Environment: {Environment}, Username required: {UsernameRequired}",
                    credential.GetType().Name, environment, requiresUsername);

                // Configure distributed cache
                services.AddStackExchangeRedisCache(options =>
                {
                    options.ConnectionMultiplexerFactory = async () =>
                    {
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
                        var isLocal = environment.Equals("Local", StringComparison.OrdinalIgnoreCase);

                        if (isLocal)
                        {
                            await configOptions.ConfigureForAzureWithTokenCredentialAsync(new AzureCliCredential());
                        }
                        else
                        {
                            await configOptions.ConfigureForAzureWithSystemAssignedManagedIdentityAsync();
                        }
                        var multiplexer = await ConnectionMultiplexer.ConnectAsync(configOptions);
                        return multiplexer;
                    };
                    options.InstanceName = cacheOptions.Redis.InstanceName ?? "SXG-EvalPlatform-";
                });

                // Register IConnectionMultiplexer
                services.AddSingleton<IConnectionMultiplexer>(serviceProvider =>
                {
                    var logger = serviceProvider.GetService<ILoggerFactory>()?.CreateLogger("RedisConnection");

                    try
                    {
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

                        logger?.LogInformation("Connecting to Redis - Environment: {Environment}, Username: {Username}",
                            environment, requiresUsername ? cacheName : "(not set)");

                        var connectTask = ConnectionMultiplexer.ConnectAsync(configOptions);
                        connectTask.GetAwaiter().GetResult();

                        var multiplexer = connectTask.Result;
                        logger?.LogInformation("✅ Redis ConnectionMultiplexer created. IsConnected: {IsConnected}", multiplexer.IsConnected);

                        return multiplexer;
                    }
                    catch (Exception ex)
                    {
                        logger?.LogError(ex, "❌ Failed to create Redis ConnectionMultiplexer: {Message}", ex.Message);
                        throw;
                    }
                });

                // Register cache manager WITH ConnectionMultiplexer
                services.AddScoped<ICacheManager>(provider =>
                {
                    var distributedCache = provider.GetRequiredService<Microsoft.Extensions.Caching.Distributed.IDistributedCache>();
                    var cacheLogger = provider.GetRequiredService<ILogger<RedisCacheManager>>();

                    IConnectionMultiplexer? conn = null;
                    try
                    {
                        conn = provider.GetService<IConnectionMultiplexer>();
                    }
                    catch (Exception ex)
                    {
                        cacheLogger.LogWarning(ex, "Could not get IConnectionMultiplexer");
                    }

                    return new RedisCacheManager(distributedCache, cacheLogger, conn);
                });

                startupLogger?.LogInformation("✅ Redis cache services registered successfully");
            }
            catch (Exception ex)
            {
                startupLogger?.LogError(ex, "Failed to configure Redis cache. Falling back to memory cache. Error: {Error}", ex.Message);
                return services.AddMemoryCacheServices(cacheOptions);
            }

            return services;
        }
    }
}