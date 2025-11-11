using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Sxg.EvalPlatform.API.Storage.Configuration;
using Sxg.EvalPlatform.API.Storage.Services;
using StackExchange.Redis;

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
        /// <param name="configuration">The configuration</param>
        /// <returns>The service collection for chaining</returns>
        public static IServiceCollection AddCacheServices(this IServiceCollection services, IConfiguration configuration)
        {
            // Bind cache configuration
            var cacheOptions = new CacheOptions();
            configuration.GetSection("Cache").Bind(cacheOptions);
            services.Configure<CacheOptions>(configuration.GetSection("Cache"));

            // Register cache services based on provider type
            switch (cacheOptions.Provider.ToLowerInvariant())
            {
                case "redis":
                case "distributed":
                    services.AddRedisCacheServices(cacheOptions);
                    break;
                case "memory":
                default:
                    services.AddMemoryCacheServices(cacheOptions);
                    break;
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
        /// Add Redis cache services with fast performance settings
        /// </summary>
        private static IServiceCollection AddRedisCacheServices(this IServiceCollection services, CacheOptions cacheOptions)
        {
            if (string.IsNullOrEmpty(cacheOptions.Redis.Endpoint))
            {
                throw new InvalidOperationException("Redis endpoint is required when using Redis cache provider. Configure 'Cache:Redis:Endpoint' in application settings.");
            }

            // Configure Redis connection using ConfigurationOptions for better control
            services.AddSingleton<IConnectionMultiplexer>(provider =>
            {
                var logger = provider.GetService<ILogger<IConnectionMultiplexer>>();

                try
                {
                    // Use ConfigurationOptions for better control over timeouts and connection settings
                    var configurationOptions = new ConfigurationOptions();

                    // Parse endpoint to get host and port
                    var endpointParts = cacheOptions.Redis.Endpoint.Split(':');
                    var host = endpointParts[0];
                    var port = endpointParts.Length > 1 && int.TryParse(endpointParts[1], out var p) ? p : 6380;

                    configurationOptions.EndPoints.Add(host, port);
                    configurationOptions.Ssl = cacheOptions.Redis.UseSsl;
                    configurationOptions.ConnectTimeout = cacheOptions.Redis.ConnectTimeoutSeconds * 1000;
                    configurationOptions.SyncTimeout = cacheOptions.Redis.CommandTimeoutSeconds * 1000;
                    configurationOptions.ConnectRetry = cacheOptions.Redis.Retry.MaxRetryAttempts;
                    configurationOptions.AbortOnConnectFail = false; // Important for cloud scenarios
                    configurationOptions.ReconnectRetryPolicy = new LinearRetry(cacheOptions.Redis.Retry.BaseDelayMs);

                    // Add keepalive settings
                    configurationOptions.KeepAlive = 60; // Send keepalive every 60 seconds
                    configurationOptions.DefaultDatabase = 0;

                    logger?.LogInformation("Configuring Redis connection to {Host}:{Port} with SSL:{UseSsl}, ConnectTimeout:{ConnectTimeout}ms, SyncTimeout:{SyncTimeout}ms",
        host, port, cacheOptions.Redis.UseSsl, configurationOptions.ConnectTimeout, configurationOptions.SyncTimeout);

                    return ConnectionMultiplexer.Connect(configurationOptions);
                }
                catch (Exception ex)
                {
                    logger?.LogError(ex, "Failed to connect to Redis cache at {Endpoint}", cacheOptions.Redis.Endpoint);
                    throw new InvalidOperationException($"Failed to connect to Redis cache: {ex.Message}", ex);
                }
            });

            // Configure distributed cache with connection string approach
            var connectionString = BuildRedisConnectionString(cacheOptions.Redis);
            services.AddStackExchangeRedisCache(options =>
            {
                options.Configuration = connectionString;
                options.InstanceName = cacheOptions.Redis.InstanceName ?? "SXG-EvalPlatform";
            });

            // Register cache manager with telemetry service
            services.AddScoped<ICacheManager>(provider =>
                      {
                          var distributedCache = provider.GetRequiredService<Microsoft.Extensions.Caching.Distributed.IDistributedCache>();
                          var logger = provider.GetRequiredService<ILogger<RedisCacheManager>>();
                          var connectionMultiplexer = provider.GetService<IConnectionMultiplexer>();

                          return new RedisCacheManager(distributedCache, logger, connectionMultiplexer);
                      });

            return services;
        }

        /// <summary>
        /// Build Redis connection string with supported parameters only
        /// </summary>
        private static string BuildRedisConnectionString(RedisOptions redisOptions)
        {
            var connectionStringBuilder = new List<string>
        {
             redisOptions.Endpoint!
     };

            if (redisOptions.UseSsl)
            {
                connectionStringBuilder.Add("ssl=true");
            }

            // Use fast timeouts for performance
            if (redisOptions.ConnectTimeoutSeconds > 0)
            {
                connectionStringBuilder.Add($"connectTimeout={redisOptions.ConnectTimeoutSeconds * 1000}");
            }

            if (redisOptions.CommandTimeoutSeconds > 0)
            {
                connectionStringBuilder.Add($"syncTimeout={redisOptions.CommandTimeoutSeconds * 1000}");
            }

            // Add retry configuration
            if (redisOptions.Retry.Enabled)
            {
                connectionStringBuilder.Add($"connectRetry={redisOptions.Retry.MaxRetryAttempts}");
            }

            // Add only supported resilience settings
            connectionStringBuilder.Add("abortConnect=false");

            return string.Join(",", connectionStringBuilder);
        }
    }
}