using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using StackExchange.Redis;
using System.Diagnostics;

namespace Sxg.EvalPlatform.API.Storage.Services
{
    /// <summary>
    /// Redis distributed cache manager implementation using IDistributedCache with telemetry
    /// </summary>
    public class RedisCacheManager : ICacheManager
    {
        private readonly IDistributedCache _distributedCache;
        private readonly ILogger<RedisCacheManager> _logger;
        private readonly IConnectionMultiplexer? _connectionMultiplexer;
        private readonly JsonSerializerOptions _jsonOptions;
        private long _hitCount = 0;
        private long _missCount = 0;
        private long _errorCount = 0;
        private long _timeoutCount = 0;

        public RedisCacheManager(
                  IDistributedCache distributedCache,
              ILogger<RedisCacheManager> logger,
               IConnectionMultiplexer? connectionMultiplexer = null)
        {
            _distributedCache = distributedCache ?? throw new ArgumentNullException(nameof(distributedCache));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _connectionMultiplexer = connectionMultiplexer;

            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = null, // Use original property names (PascalCase)
                WriteIndented = false,
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
                PropertyNameCaseInsensitive = true // Allow case-insensitive deserialization
            };
        }

        /// <inheritdoc />
        public async Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default) where T : class
        {
            if (string.IsNullOrEmpty(key))
                throw new ArgumentException("Cache key cannot be null or empty", nameof(key));

            using var activity = Activity.Current?.Source.StartActivity("Cache.Get");
            var stopwatch = Stopwatch.StartNew();

            try
            {
                activity?.SetTag("cache.operation", "get");
                activity?.SetTag("cache.key", key);
                activity?.SetTag("cache.provider", "redis");

                var cachedBytes = await _distributedCache.GetAsync(key);
                if (cachedBytes != null && cachedBytes.Length > 0)
                {
                    Interlocked.Increment(ref _hitCount);
                    var cachedJson = System.Text.Encoding.UTF8.GetString(cachedBytes);

                    var cachedValue = JsonSerializer.Deserialize<T>(cachedJson, _jsonOptions);

                    activity?.SetTag("cache.hit", true);
                    activity?.SetTag("cache.data_size", cachedBytes.Length);

                    if (cachedValue != null)
                    {
                        _logger.LogDebug("Redis cache HIT for key: {Key} in {Duration}ms", key, stopwatch.ElapsedMilliseconds);
                    }
                    else
                    {
                        _logger.LogWarning("Deserialization returned null for key: {Key}", key);
                    }

                    return cachedValue;
                }

                Interlocked.Increment(ref _missCount);
                activity?.SetTag("cache.hit", false);
                _logger.LogDebug("Redis cache MISS for key: {Key} in {Duration}ms", key, stopwatch.ElapsedMilliseconds);

                return null;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                Interlocked.Increment(ref _missCount);
                activity?.SetTag("cache.error", "cancelled");
                activity?.SetTag("success", false);

                _logger.LogWarning("Redis cache operation was cancelled for key: {Key} after {Duration}ms", key, stopwatch.ElapsedMilliseconds);

                return null;
            }
            catch (TimeoutException ex)
            {
                Interlocked.Increment(ref _missCount);
                Interlocked.Increment(ref _timeoutCount);
                activity?.SetTag("cache.error", "timeout");
                activity?.SetTag("success", false);
                activity?.SetTag("error.message", ex.Message);
                activity?.SetTag("error.type", "TimeoutException");

                _logger.LogWarning(ex, "Redis cache TIMEOUT for key: {Key} after {Duration}ms - operation exceeded time limit", key, stopwatch.ElapsedMilliseconds);

                return null;
            }
            catch (OperationCanceledException ex)
            {
                Interlocked.Increment(ref _missCount);
                Interlocked.Increment(ref _timeoutCount);
                activity?.SetTag("cache.error", "operation_timeout");
                activity?.SetTag("success", false);
                activity?.SetTag("error.message", ex.Message);
                activity?.SetTag("error.type", "OperationCanceledException");

                _logger.LogWarning(ex, "Redis cache operation timed out for key: {Key} after {Duration}ms - cache should be faster!", key, stopwatch.ElapsedMilliseconds);

                return null;
            }
            catch (Exception ex)
            {
                Interlocked.Increment(ref _missCount);
                Interlocked.Increment(ref _errorCount);
                activity?.SetTag("cache.error", ex.GetType().Name);
                activity?.SetTag("success", false);
                activity?.SetTag("error.message", ex.Message);
                activity?.SetTag("error.type", ex.GetType().Name);

                _logger.LogWarning(ex, "Redis cache ERROR for key: {Key} after {Duration}ms - falling back gracefully", key, stopwatch.ElapsedMilliseconds);

                return null;
            }
            finally
            {
                stopwatch.Stop();
                activity?.SetTag("duration_ms", stopwatch.ElapsedMilliseconds);
                activity?.SetTag("cache.timeout_count", _timeoutCount);
                activity?.SetTag("cache.error_count", _errorCount);
            }
        }

        /// <inheritdoc />
        public async Task SetAsync<T>(string key, T value, TimeSpan? expiration = null, CancellationToken cancellationToken = default) where T : class
        {
            if (string.IsNullOrEmpty(key))
                throw new ArgumentException("Cache key cannot be null or empty", nameof(key));

            if (value == null)
                throw new ArgumentNullException(nameof(value));

            using var activity = Activity.Current?.Source.StartActivity("Cache.Set");
            var stopwatch = Stopwatch.StartNew();

            try
            {
                activity?.SetTag("cache.operation", "set");
                activity?.SetTag("cache.key", key);
                activity?.SetTag("cache.provider", "redis");
                activity?.SetTag("cache.expiration", expiration?.TotalMinutes.ToString() ?? "none");

                var jsonValue = JsonSerializer.Serialize(value, _jsonOptions);
                var bytesToCache = System.Text.Encoding.UTF8.GetBytes(jsonValue);

                activity?.SetTag("cache.data_size", bytesToCache.Length);

                var options = new DistributedCacheEntryOptions();
                if (expiration.HasValue)
                {
                    options.AbsoluteExpirationRelativeToNow = expiration.Value;
                }

                await _distributedCache.SetAsync(key, bytesToCache, options);

                activity?.SetTag("success", true);
                _logger.LogDebug("Redis cache SET successful for key: {Key} in {Duration}ms", key, stopwatch.ElapsedMilliseconds);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                activity?.SetTag("cache.error", "cancelled");
                activity?.SetTag("success", false);

                _logger.LogWarning("Redis cache SET operation was cancelled for key: {Key} after {Duration}ms", key, stopwatch.ElapsedMilliseconds);
            }
            catch (TimeoutException ex)
            {
                Interlocked.Increment(ref _timeoutCount);
                activity?.SetTag("cache.error", "timeout");
                activity?.SetTag("success", false);
                activity?.SetTag("error.message", ex.Message);
                activity?.SetTag("error.type", "TimeoutException");

                _logger.LogWarning(ex, "Redis cache SET timeout for key: {Key} after {Duration}ms - operation exceeded time limit", key, stopwatch.ElapsedMilliseconds);
            }
            catch (OperationCanceledException ex)
            {
                Interlocked.Increment(ref _timeoutCount);
                activity?.SetTag("cache.error", "operation_timeout");
                activity?.SetTag("success", false);
                activity?.SetTag("error.message", ex.Message);
                activity?.SetTag("error.type", "OperationCanceledException");

                _logger.LogWarning(ex, "Redis cache SET operation timed out for key: {Key} after {Duration}ms - continuing without cache", key, stopwatch.ElapsedMilliseconds);
            }
            catch (Exception ex)
            {
                Interlocked.Increment(ref _errorCount);
                activity?.SetTag("cache.error", ex.GetType().Name);
                activity?.SetTag("success", false);
                activity?.SetTag("error.message", ex.Message);
                activity?.SetTag("error.type", ex.GetType().Name);

                _logger.LogWarning(ex, "Redis cache SET error for key: {Key} after {Duration}ms - continuing without cache", key, stopwatch.ElapsedMilliseconds);
            }
            finally
            {
                stopwatch.Stop();
                activity?.SetTag("duration_ms", stopwatch.ElapsedMilliseconds);
                activity?.SetTag("cache.timeout_count", _timeoutCount);
                activity?.SetTag("cache.error_count", _errorCount);
            }
        }

        /// <inheritdoc />
        public async Task SetAsync<T>(string key, T value, DateTimeOffset absoluteExpiration, CancellationToken cancellationToken = default) where T : class
        {
            if (string.IsNullOrEmpty(key))
                throw new ArgumentException("Cache key cannot be null or empty", nameof(key));

            if (value == null)
                throw new ArgumentNullException(nameof(value));

            try
            {
                var jsonValue = JsonSerializer.Serialize(value, _jsonOptions);
                var bytesToCache = System.Text.Encoding.UTF8.GetBytes(jsonValue);

                var options = new DistributedCacheEntryOptions
                {
                    AbsoluteExpiration = absoluteExpiration
                };

                await _distributedCache.SetAsync(key, bytesToCache, options, cancellationToken);
                _logger.LogDebug("Redis cache entry set - Key: {Key}, AbsoluteExpiration: {AbsoluteExpiration}", key, absoluteExpiration);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting item in Redis cache with key: {Key}", key);
            }
        }

        /// <inheritdoc />
        public async Task RemoveAsync(string key, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(key))
                throw new ArgumentException("Cache key cannot be null or empty", nameof(key));

            try
            {
                await _distributedCache.RemoveAsync(key, cancellationToken);
                _logger.LogDebug("Redis cache entry removed - Key: {Key}", key);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing item from Redis cache with key: {Key}", key);
            }
        }

        /// <inheritdoc />
        public async Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(key))
                throw new ArgumentException("Cache key cannot be null or empty", nameof(key));

            try
            {
                var cachedBytes = await _distributedCache.GetAsync(key, cancellationToken);
                return cachedBytes != null && cachedBytes.Length > 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking if item exists in Redis cache with key: {Key}", key);
                return false;
            }
        }

        /// <inheritdoc />
        public async Task<T> GetOrCreateAsync<T>(string key, Func<Task<T>> factory, TimeSpan? expiration = null, CancellationToken cancellationToken = default) where T : class
        {
            if (string.IsNullOrEmpty(key))
                throw new ArgumentException("Cache key cannot be null or empty", nameof(key));

            if (factory == null)
                throw new ArgumentNullException(nameof(factory));

            var cachedItem = await GetAsync<T>(key, cancellationToken);
            if (cachedItem != null)
            {
                return cachedItem;
            }

            try
            {
                var newItem = await factory();
                if (newItem != null)
                {
                    await SetAsync(key, newItem, expiration, cancellationToken);
                }
                return newItem;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetOrCreateAsync for key: {Key}", key);
                throw;
            }
        }

        /// <inheritdoc />
        public async Task RefreshAsync(string key, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(key))
                throw new ArgumentException("Cache key cannot be null or empty", nameof(key));

            try
            {
                await _distributedCache.RefreshAsync(key, cancellationToken);
                _logger.LogDebug("Redis cache entry refreshed - Key: {Key}", key);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error refreshing Redis cache entry with key: {Key}", key);
            }
        }

        /// <inheritdoc />
        public async Task ClearAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                if (_connectionMultiplexer != null)
                {
                    var database = _connectionMultiplexer.GetDatabase();
                    var server = _connectionMultiplexer.GetServer(_connectionMultiplexer.GetEndPoints().First());

                    // Warning: This will flush the entire database - use with extreme caution
                    await server.FlushDatabaseAsync(database.Database);
                    _logger.LogWarning("Redis cache cleared - entire database flushed");
                }
                else
                {
                    _logger.LogWarning("Cannot clear Redis cache - IConnectionMultiplexer not available. Individual key removal required.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error clearing Redis cache");
            }
        }

        /// <inheritdoc />
        public async Task<CacheStatistics> GetStatisticsAsync()
        {
            var totalRequests = _hitCount + _missCount;
            var hitRatio = totalRequests > 0 ? (double)_hitCount / totalRequests : 0;

            var statistics = new CacheStatistics
            {
                CacheType = "Redis",
                HitCount = _hitCount,
                MissCount = _missCount,
                HitRatio = hitRatio,
                AdditionalInfo = new Dictionary<string, object>
                {
                    ["TotalRequests"] = totalRequests,
                    ["ErrorCount"] = _errorCount,
                    ["TimeoutCount"] = _timeoutCount,
                    ["ErrorRate"] = totalRequests > 0 ? (double)_errorCount / totalRequests : 0,
                    ["TimeoutRate"] = totalRequests > 0 ? (double)_timeoutCount / totalRequests : 0
                }
            };

            // Try to get Redis-specific statistics if connection multiplexer is available
            if (_connectionMultiplexer != null)
            {
                try
                {
                    var server = _connectionMultiplexer.GetServer(_connectionMultiplexer.GetEndPoints().First());

                    // Simplified approach - just get basic server info
                    var info = await server.InfoAsync();

                    // Look for basic server information
                    foreach (var group in info)
                    {
                        foreach (var kvp in group)
                        {
                            if (kvp.Key == "redis_version")
                            {
                                statistics.AdditionalInfo["RedisVersion"] = kvp.Value;
                            }
                            else if (kvp.Key == "used_memory_human")
                            {
                                statistics.AdditionalInfo["UsedMemory"] = kvp.Value;
                            }
                            else if (kvp.Key.StartsWith("db") && kvp.Key.Contains("keys"))
                            {
                                // Try to parse key count from database info
                                var parts = kvp.Value.Split(',');
                                if (parts.Length > 0)
                                {
                                    var keysPart = parts[0];
                                    var keyValue = keysPart.Split('=');
                                    if (keyValue.Length > 1 && long.TryParse(keyValue[1], out var keyCount))
                                    {
                                        statistics.ItemCount = keyCount;
                                    }
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Could not retrieve Redis server statistics");
                    statistics.AdditionalInfo["StatisticsError"] = ex.Message;
                }
            }

            return statistics;
        }
    }
}