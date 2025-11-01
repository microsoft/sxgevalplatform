using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Caching.Memory;
using StackExchange.Redis;
using SxgEvalPlatformApi.Services.Cache;
using SxgEvalPlatformApi.Configuration;
using Moq;
using System.Linq;

namespace SXG.EvalPlatform.API.IntegrationTests.Infrastructure;

/// <summary>
/// WebApplicationFactory using in-memory implementations for all external dependencies.
/// No connection strings, no Docker, no external services required.
/// </summary>
public class InMemoryWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((context, config) =>
        {
            // Override with test-specific in-memory configuration
            var testConfig = new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = "Server=(localdb)\\mssqllocaldb;Database=TestDb;Trusted_Connection=true;",
                ["Azure:StorageAccount:ConnectionString"] = "UseDevelopmentStorage=true",
                ["Azure:StorageAccount:AccountName"] = "devstoreaccount1",
                ["Azure:StorageAccount:MetricsContainer"] = "test-metrics",
                ["Azure:StorageAccount:DataSetsContainer"] = "test-datasets",
                ["Azure:StorageAccount:ResultsContainer"] = "test-results",
                ["Redis:ConnectionString"] = "localhost:6379", // Not used - in-memory cache replaces Redis
                ["Redis:InstanceName"] = "SXGEvalPlatformTest",
                ["Environment"] = "Testing"
            };

            config.AddInMemoryCollection(testConfig);
        });

        builder.ConfigureServices(services =>
        {
            // Remove real Redis connection
            services.RemoveAll<IConnectionMultiplexer>();
            services.RemoveAll<IRedisCache>();
            
            // Remove startup validation services to prevent Azure connection validation
            services.RemoveAll<SxgEvalPlatformApi.Services.IStartupConnectionValidator>();
            
            // Remove the startup validation hosted service manually
            var hostedServiceDescriptor = services.FirstOrDefault(d => 
                d.ImplementationType == typeof(SxgEvalPlatformApi.Services.StartupValidationHostedService));
            if (hostedServiceDescriptor != null)
            {
                services.Remove(hostedServiceDescriptor);
            }
            
            // Add in-memory cache implementation
            services.AddMemoryCache();
            services.AddSingleton<IRedisCache, InMemoryRedisCache>();
            
            // Replace Azure Storage services with mocks (NO connection strings)
            // Remove both interface and concrete class registrations
            services.RemoveAll<Sxg.EvalPlatform.API.Storage.Services.IMetricsConfigTableService>();
            services.RemoveAll<Sxg.EvalPlatform.API.Storage.Services.MetricsConfigTableService>();
            services.RemoveAll<Sxg.EvalPlatform.API.Storage.Services.IAzureBlobStorageService>();
            services.RemoveAll<Sxg.EvalPlatform.API.Storage.Services.AzureBlobStorageService>();
            services.RemoveAll<Sxg.EvalPlatform.API.Storage.Services.IDataSetTableService>();
            services.RemoveAll<Sxg.EvalPlatform.API.Storage.Services.DataSetTableService>();
            services.RemoveAll<Sxg.EvalPlatform.API.Storage.Services.IEvalRunTableService>();
            services.RemoveAll<Sxg.EvalPlatform.API.Storage.Services.EvalRunTableService>();
            
            // Also remove any potential direct concrete registrations made by Program.cs
            services.RemoveAll(typeof(Sxg.EvalPlatform.API.Storage.Services.MetricsConfigTableService));
            services.RemoveAll(typeof(Sxg.EvalPlatform.API.Storage.Services.AzureBlobStorageService));
            services.RemoveAll(typeof(Sxg.EvalPlatform.API.Storage.Services.DataSetTableService));
            services.RemoveAll(typeof(Sxg.EvalPlatform.API.Storage.Services.EvalRunTableService));

            // Add mock implementations that actually store data in memory
            services.AddSingleton<Sxg.EvalPlatform.API.Storage.Services.IMetricsConfigTableService>(serviceProvider =>
            {
                return new InMemoryMetricsConfigTableService();
            });

            services.AddSingleton<Sxg.EvalPlatform.API.Storage.Services.IAzureBlobStorageService>(serviceProvider =>
            {
                return new InMemoryAzureBlobStorageService();
            });

            services.AddSingleton<Sxg.EvalPlatform.API.Storage.Services.IDataSetTableService>(serviceProvider =>
            {
                return new InMemoryDataSetTableService();
            });

            services.AddSingleton<Sxg.EvalPlatform.API.Storage.Services.IEvalRunTableService>(serviceProvider =>
            {
                return new InMemoryEvalRunTableService();
            });
            
            // Override logging to reduce noise in tests
            services.AddLogging(logging =>
            {
                logging.ClearProviders();
                logging.AddConsole();
                logging.SetMinimumLevel(LogLevel.Warning);
            });

            // Mock Redis configuration
            var redisConfig = new RedisConfiguration
            {
                Hostname = "localhost:6379",
                User = null,
                Cache = new CacheConfiguration
                {
                    KeyPrefix = "SXGEvalPlatformTest",
                    DefaultTtlMinutes = 60
                }
            };
            services.Configure<RedisConfiguration>(opt =>
            {
                opt.Hostname = redisConfig.Hostname;
                opt.User = redisConfig.User;
                opt.Cache = redisConfig.Cache;
            });
        });

        builder.UseEnvironment("Testing");
    }
}

/// <summary>
/// In-memory implementation of IRedisCache for testing without Redis dependency
/// </summary>
public class InMemoryRedisCache : IRedisCache
{
    private readonly IMemoryCache _memoryCache;
    private readonly ILogger<InMemoryRedisCache> _logger;

    public InMemoryRedisCache(IMemoryCache memoryCache, ILogger<InMemoryRedisCache> logger)
    {
        _memoryCache = memoryCache;
        _logger = logger;
    }

    public async Task<string?> GetStringAsync(string key)
    {
        _logger.LogDebug("Getting key: {Key}", key);
        return await Task.FromResult(_memoryCache.Get<string>(key));
    }

    public async Task<T?> GetAsync<T>(string key) where T : class
    {
        _logger.LogDebug("Getting key: {Key}", key);
        return await Task.FromResult(_memoryCache.Get<T>(key));
    }

    public async Task SetStringAsync(string key, string value, TimeSpan? expiry = null)
    {
        _logger.LogDebug("Setting key: {Key} with expiry: {Expiry}", key, expiry);
        var options = new MemoryCacheEntryOptions();
        if (expiry.HasValue)
        {
            options.AbsoluteExpirationRelativeToNow = expiry;
        }
        _memoryCache.Set(key, value, options);
        await Task.CompletedTask;
    }

    public async Task<bool> SetAsync<T>(string key, T value, TimeSpan? expiry = null) where T : class
    {
        _logger.LogDebug("Setting key: {Key} with expiry: {Expiry}", key, expiry);
        var options = new MemoryCacheEntryOptions();
        if (expiry.HasValue)
        {
            options.AbsoluteExpirationRelativeToNow = expiry;
        }
        _memoryCache.Set(key, value, options);
        return await Task.FromResult(true);
    }

    public async Task<bool> RemoveAsync(string key)
    {
        _logger.LogDebug("Removing key: {Key}", key);
        var exists = _memoryCache.TryGetValue(key, out _);
        _memoryCache.Remove(key);
        return await Task.FromResult(exists);
    }

    public async Task<long> RemoveByPatternAsync(string pattern)
    {
        _logger.LogDebug("Removing by pattern: {Pattern}", pattern);
        // For in-memory implementation, we'll simulate pattern removal
        // In a real implementation, this would be more sophisticated
        return await Task.FromResult(0L);
    }

    public async Task<bool> ExistsAsync(string key)
    {
        _logger.LogDebug("Checking existence of key: {Key}", key);
        return await Task.FromResult(_memoryCache.TryGetValue(key, out _));
    }

    public async Task<TimeSpan?> GetTtlAsync(string key)
    {
        _logger.LogDebug("Getting TTL for key: {Key}", key);
        // Memory cache doesn't easily expose TTL, return null
        return await Task.FromResult<TimeSpan?>(null);
    }

    public async Task<CacheSizeInfo> GetCacheSizeAsync()
    {
        _logger.LogDebug("Getting cache size info");
        return await Task.FromResult(new CacheSizeInfo
        {
            TotalKeys = 0, // Memory cache doesn't expose this easily
            UsedMemory = 0,
            MaxMemory = 0,
            MemoryUsagePercentage = 0
        });
    }

    public async Task<CacheHealthInfo> GetHealthAsync()
    {
        _logger.LogDebug("Getting cache health info");
        return await Task.FromResult(new CacheHealthInfo
        {
            IsConnected = true,
            ResponseTime = TimeSpan.FromMilliseconds(1),
            Version = "InMemory-1.0",
            SizeInfo = new CacheSizeInfo(),
            LastChecked = DateTime.UtcNow
        });
    }

    public async Task<bool> ClearAllAsync()
    {
        _logger.LogDebug("Clearing all cache entries");
        // Memory cache doesn't have a clear all method, this is a simulation
        return await Task.FromResult(true);
    }

    public async Task<bool> RefreshAsync(string key, TimeSpan expiry)
    {
        _logger.LogDebug("Refreshing TTL for key: {Key}", key);
        if (_memoryCache.TryGetValue(key, out var value))
        {
            var options = new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = expiry
            };
            _memoryCache.Set(key, value, options);
            return await Task.FromResult(true);
        }
        return await Task.FromResult(false);
    }

    public async Task<T?> GetOrSetAsync<T>(string key, Func<Task<T?>> factory, TimeSpan? expiry = null) where T : class
    {
        _logger.LogDebug("GetOrSet for key: {Key}", key);
        
        if (_memoryCache.TryGetValue(key, out T? cached))
        {
            return await Task.FromResult(cached);
        }

        var value = await factory();
        if (value != null)
        {
            await SetAsync(key, value, expiry);
        }
        
        return value;
    }
}