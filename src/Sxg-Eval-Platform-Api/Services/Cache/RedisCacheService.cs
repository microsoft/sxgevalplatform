using StackExchange.Redis;
using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace SxgEvalPlatformApi.Services.Cache
{
    /// <summary>
    /// Redis cache implementation with distributed system patterns
    /// </summary>
    public class RedisCacheService : IRedisCache, IDisposable
    {
        private readonly IDatabase _database;
        private readonly IServer _server;
        private readonly ConnectionMultiplexer _redis;
        private readonly ILogger<RedisCacheService> _logger;
        private readonly RedisCacheOptions _options;
        private readonly JsonSerializerOptions _jsonOptions;

        public RedisCacheService(
            ConnectionMultiplexer redis,
            IConfiguration configuration,
            ILogger<RedisCacheService> logger)
        {
            _redis = redis ?? throw new ArgumentNullException(nameof(redis));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            
            _database = _redis.GetDatabase();
            
            // Try to get server, but don't fail if admin mode is not available
            try
            {
                _server = _redis.GetServer(_redis.GetEndPoints().First());
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not get Redis server instance - some monitoring features will be limited");
                _server = null!; // We'll handle null checks in methods that use it
            }
            
            // Load cache configuration
            _options = new RedisCacheOptions();
            configuration.GetSection("Redis:Cache").Bind(_options);
            
            // Configure JSON serialization
            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = false,
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
            };

            _logger.LogInformation("RedisCacheService initialized with TTL: {DefaultTtl}min, MaxMemory: {MaxMemory}MB", 
                _options.DefaultTtlMinutes, _options.MaxMemoryMB);
        }

        public async Task<T?> GetAsync<T>(string key) where T : class
        {
            try
            {
                var cacheKey = BuildCacheKey(key);
                var cached = await _database.StringGetAsync(cacheKey);

                if (!cached.HasValue)
                {
                    _logger.LogDebug("Cache miss for key: {Key}", cacheKey);
                    return null;
                }

                var value = JsonSerializer.Deserialize<T>(cached!, _jsonOptions);
                _logger.LogDebug("Cache hit for key: {Key}", cacheKey);
                return value;
            }
            catch (RedisConnectionException ex)
            {
                _logger.LogWarning(ex, "Redis connection issue for key: {Key}, falling back to null (cache miss)", key);
                return null; // Graceful degradation - treat as cache miss
            }
            catch (RedisTimeoutException ex)
            {
                _logger.LogWarning(ex, "Redis timeout for key: {Key}, falling back to null (cache miss)", key);
                return null; // Graceful degradation - treat as cache miss
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting cache value for key: {Key}", key);
                return null;
            }
        }

        public async Task<bool> SetAsync<T>(string key, T value, TimeSpan? expiry = null) where T : class
        {
            try
            {
                var cacheKey = BuildCacheKey(key);
                var serialized = JsonSerializer.Serialize(value, _jsonOptions);
                var ttl = expiry ?? TimeSpan.FromMinutes(_options.DefaultTtlMinutes);

                // Check cache size limits before setting
                if (_options.MaxMemoryMB > 0)
                {
                    var sizeInfo = await GetCacheSizeAsync();
                    if (sizeInfo.MemoryUsagePercentage > 90) // 90% threshold
                    {
                        _logger.LogWarning("Cache memory usage high: {Usage}%, evicting old entries", 
                            sizeInfo.MemoryUsagePercentage);
                        await EvictOldEntriesAsync();
                    }
                }

                var result = await _database.StringSetAsync(cacheKey, serialized, ttl);
                
                if (result)
                {
                    _logger.LogDebug("Cache set for key: {Key}, TTL: {Ttl}", cacheKey, ttl);
                }
                else
                {
                    _logger.LogWarning("Failed to set cache for key: {Key}", cacheKey);
                }

                return result;
            }
            catch (RedisConnectionException ex)
            {
                _logger.LogWarning(ex, "Redis connection issue setting key: {Key}, continuing without cache", key);
                return false; // Graceful degradation - continue without caching
            }
            catch (RedisTimeoutException ex)
            {
                _logger.LogWarning(ex, "Redis timeout setting key: {Key}, continuing without cache", key);
                return false; // Graceful degradation - continue without caching
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting cache value for key: {Key}", key);
                return false;
            }
        }

        public async Task<bool> RemoveAsync(string key)
        {
            try
            {
                var cacheKey = BuildCacheKey(key);
                var result = await _database.KeyDeleteAsync(cacheKey);
                
                if (result)
                {
                    _logger.LogDebug("Cache key removed: {Key}", cacheKey);
                }
                
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing cache key: {Key}", key);
                return false;
            }
        }

        public async Task<long> RemoveByPatternAsync(string pattern)
        {
            try
            {
                if (_server == null)
                {
                    _logger.LogWarning("Cannot remove by pattern - server instance not available");
                    return 0;
                }

                var cachePattern = BuildCacheKey(pattern);
                var keys = _server.Keys(pattern: cachePattern).ToArray();
                
                if (keys.Length == 0)
                    return 0;

                var result = await _database.KeyDeleteAsync(keys);
                _logger.LogDebug("Removed {Count} cache keys matching pattern: {Pattern}", result, cachePattern);
                
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing cache keys by pattern: {Pattern}", pattern);
                return 0;
            }
        }

        public async Task<bool> ExistsAsync(string key)
        {
            try
            {
                var cacheKey = BuildCacheKey(key);
                return await _database.KeyExistsAsync(cacheKey);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking cache key existence: {Key}", key);
                return false;
            }
        }

        public async Task<CacheSizeInfo> GetCacheSizeAsync()
        {
            try
            {
                // Since we can't use INFO command without admin mode, provide basic information
                var sizeInfo = new CacheSizeInfo
                {
                    TotalKeys = 0, // Can't get this without admin mode
                    UsedMemory = 0, // Can't get this without admin mode
                    MaxMemory = 0, // Can't get this without admin mode
                    MemoryUsagePercentage = 0
                };

                // Get key counts by pattern (this works without admin mode)
                try
                {
                    var patterns = new[] { $"{_options.KeyPrefix}:metrics:*", $"{_options.KeyPrefix}:blob:*", $"{_options.KeyPrefix}:dataset:*" };
                    foreach (var pattern in patterns)
                    {
                        // Use SCAN instead of KEYS for better performance and no admin requirement
                        var keys = _database.ExecuteAsync("SCAN", "0", "MATCH", pattern, "COUNT", "100");
                        // Note: This is a simplified approach. In production, you'd implement proper SCAN iteration
                        sizeInfo.KeysByPattern[pattern] = 0; // Simplified for now
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Could not scan keys by pattern");
                }

                return sizeInfo;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting cache size information");
                return new CacheSizeInfo();
            }
        }

        public async Task<CacheHealthInfo> GetHealthAsync()
        {
            var stopwatch = Stopwatch.StartNew();
            var health = new CacheHealthInfo
            {
                LastChecked = DateTime.UtcNow
            };

            try
            {
                // Test connection with ping (doesn't require admin mode)
                var pingTime = await _database.PingAsync();
                health.ResponseTime = pingTime;
                health.IsConnected = true;

                // Try to get basic Redis version info (may fail without admin mode)
                try
                {
                    if (_server != null)
                    {
                        var info = await _server.InfoAsync("server");
                        health.Version = GetInfoValue(info, "redis_version");
                    }
                    else
                    {
                        health.Version = "Unknown (server not available)";
                        health.Warnings.Add("Server instance not available - limited monitoring capabilities");
                    }
                }
                catch (RedisCommandException ex) when (ex.Message.Contains("admin mode"))
                {
                    health.Version = "Unknown (admin mode required)";
                    health.Warnings.Add("Admin mode not enabled - limited monitoring capabilities");
                }

                // Get size information (simplified without admin mode)
                health.SizeInfo = await GetCacheSizeAsync();

                // Add warnings based on thresholds
                if (health.ResponseTime.TotalMilliseconds > 100)
                {
                    health.Warnings.Add($"High response time: {health.ResponseTime.TotalMilliseconds:F1}ms");
                }

                // Test basic cache operations to ensure it's working
                var testKey = $"health-test-{DateTime.UtcNow:yyyyMMddHHmmss}";
                try
                {
                    await _database.StringSetAsync(testKey, "test", TimeSpan.FromSeconds(10));
                    var testValue = await _database.StringGetAsync(testKey);
                    await _database.KeyDeleteAsync(testKey);
                    
                    if (!testValue.HasValue || testValue != "test")
                    {
                        health.Warnings.Add("Cache read/write test failed");
                    }
                }
                catch (Exception ex)
                {
                    health.Warnings.Add($"Cache operation test failed: {ex.Message}");
                }

            }
            catch (Exception ex)
            {
                health.IsConnected = false;
                health.Warnings.Add($"Connection error: {ex.Message}");
                _logger.LogError(ex, "Error checking cache health");
            }
            finally
            {
                stopwatch.Stop();
            }

            return health;
        }

        public async Task<bool> ClearAllAsync()
        {
            try
            {
                if (_server == null)
                {
                    _logger.LogWarning("Cannot clear all cache entries - server instance not available");
                    return false;
                }

                await _server.FlushDatabaseAsync();
                _logger.LogWarning("All cache entries cleared");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error clearing all cache entries");
                return false;
            }
        }

        public async Task<bool> RefreshAsync(string key, TimeSpan expiry)
        {
            try
            {
                var cacheKey = BuildCacheKey(key);
                var result = await _database.KeyExpireAsync(cacheKey, expiry);
                
                if (result)
                {
                    _logger.LogDebug("Cache TTL refreshed for key: {Key}, new TTL: {Ttl}", cacheKey, expiry);
                }
                
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error refreshing cache TTL for key: {Key}", key);
                return false;
            }
        }

        public async Task<T?> GetOrSetAsync<T>(string key, Func<Task<T?>> factory, TimeSpan? expiry = null) where T : class
        {
            // Try to get from cache first
            var cached = await GetAsync<T>(key);
            if (cached != null)
            {
                return cached;
            }

            // Cache miss - execute factory
            try
            {
                var value = await factory();
                if (value != null)
                {
                    // Cache the result (fire and forget to avoid blocking)
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await SetAsync(key, value, expiry);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error caching result for key: {Key}", key);
                        }
                    });
                }
                return value;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing factory function for key: {Key}", key);
                throw;
            }
        }

        private string BuildCacheKey(string key)
        {
            return $"{_options.KeyPrefix}:{key}";
        }

        private async Task EvictOldEntriesAsync()
        {
            try
            {
                if (_server == null)
                {
                    _logger.LogWarning("Cannot evict old entries - server instance not available");
                    return;
                }

                // Get keys that are close to expiring and remove them
                var keys = _server.Keys(pattern: $"{_options.KeyPrefix}:*", pageSize: 100).Take(50);
                foreach (var key in keys)
                {
                    var ttl = await _database.KeyTimeToLiveAsync(key);
                    if (ttl.HasValue && ttl.Value.TotalMinutes < 5) // Remove keys expiring in < 5 minutes
                    {
                        await _database.KeyDeleteAsync(key);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during cache eviction");
            }
        }

        private static long GetMemoryValue(IGrouping<string, KeyValuePair<string, string>>[] info, string key)
        {
            var memory = info.FirstOrDefault(g => g.Key == "Memory");
            if (memory != null)
            {
                var value = memory.FirstOrDefault(kv => kv.Key == key).Value;
                if (long.TryParse(value, out var result))
                    return result;
            }
            return 0;
        }

        private static string GetInfoValue(IGrouping<string, KeyValuePair<string, string>>[] info, string key)
        {
            foreach (var group in info)
            {
                var value = group.FirstOrDefault(kv => kv.Key == key).Value;
                if (!string.IsNullOrEmpty(value))
                    return value;
            }
            return "Unknown";
        }

        public void Dispose()
        {
            _redis?.Dispose();
            GC.SuppressFinalize(this);
        }
    }

    /// <summary>
    /// Configuration options for Redis cache
    /// </summary>
    public class RedisCacheOptions
    {
        public string KeyPrefix { get; set; } = "sxg-eval";
        public int DefaultTtlMinutes { get; set; } = 60;
        public int MaxMemoryMB { get; set; } = 500;
        public bool EnableCompression { get; set; } = false;
    }
}