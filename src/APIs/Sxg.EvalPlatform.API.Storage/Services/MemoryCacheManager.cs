using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Collections.Concurrent;
using SXG.EvalPlatform.Common;

namespace Sxg.EvalPlatform.API.Storage.Services
{
    /// <summary>
    /// In-memory cache manager implementation using IMemoryCache
    /// </summary>
    public class MemoryCacheManager : ICacheManager
    {
        private readonly IMemoryCache _memoryCache;
        private readonly ILogger<MemoryCacheManager> _logger;
        private readonly ConcurrentDictionary<string, DateTime> _keyAccessTimes;
        private long _hitCount = 0;
        private long _missCount = 0;

        public MemoryCacheManager(IMemoryCache memoryCache, ILogger<MemoryCacheManager> logger)
        {
            _memoryCache = memoryCache ?? throw new ArgumentNullException(nameof(memoryCache));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _keyAccessTimes = new ConcurrentDictionary<string, DateTime>();
        }

        /// <inheritdoc />
        public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default) where T : class
        {
            if (string.IsNullOrEmpty(key))
                throw new ArgumentException("Cache key cannot be null or empty", nameof(key));

            try
            {
                if (_memoryCache.TryGetValue(key, out var cachedValue))
                {
                    Interlocked.Increment(ref _hitCount);
                    _keyAccessTimes[key] = DateTime.UtcNow;

                    _logger.LogDebug("Cache hit for key: {Key}", CommonUtils.SanitizeForLog(key));
                    return Task.FromResult(cachedValue as T);
                }

                Interlocked.Increment(ref _missCount);
                _logger.LogDebug("Cache miss for key: {Key}", CommonUtils.SanitizeForLog(key));
                return Task.FromResult<T?>(null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting item from memory cache with key: {Key}", CommonUtils.SanitizeForLog(key));
                return Task.FromResult<T?>(null);
            }
        }

        /// <inheritdoc />
        public Task SetAsync<T>(string key, T value, TimeSpan? expiration = null, CancellationToken cancellationToken = default) where T : class
        {
            if (string.IsNullOrEmpty(key))
                throw new ArgumentException("Cache key cannot be null or empty", nameof(key));

            if (value == null)
                throw new ArgumentNullException(nameof(value));

            try
            {
                var options = new MemoryCacheEntryOptions();

                if (expiration.HasValue)
                {
                    options.AbsoluteExpirationRelativeToNow = expiration.Value;
                }

                // Set size for cache entry (required when SizeLimit is configured)
                // Using a simple heuristic: 1 unit per entry (can be refined based on actual object size)
                options.Size = 1;

                // Add callback to remove from tracking when expired
                options.RegisterPostEvictionCallback((evictedKey, evictedValue, reason, state) =>
                 {
                     _keyAccessTimes.TryRemove(evictedKey.ToString()!, out _);
                     _logger.LogDebug("Cache entry evicted - Key: {Key}, Reason: {Reason}", CommonUtils.SanitizeForLog(evictedKey?.ToString() ?? ""), reason);
                 });

                _memoryCache.Set(key, value, options);
                _keyAccessTimes[key] = DateTime.UtcNow;

                _logger.LogDebug("Cache entry set - Key: {Key}, Expiration: {Expiration}, Size: 1", CommonUtils.SanitizeForLog(key), expiration);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting item in memory cache with key: {Key}", CommonUtils.SanitizeForLog(key));
            }

            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public Task SetAsync<T>(string key, T value, DateTimeOffset absoluteExpiration, CancellationToken cancellationToken = default) where T : class
        {
            if (string.IsNullOrEmpty(key))
                throw new ArgumentException("Cache key cannot be null or empty", nameof(key));

            if (value == null)
                throw new ArgumentNullException(nameof(value));

            try
            {
                var options = new MemoryCacheEntryOptions
                {
                    AbsoluteExpiration = absoluteExpiration,
                    // Set size for cache entry (required when SizeLimit is configured)
                    Size = 1
                };

                // Add callback to remove from tracking when expired
                options.RegisterPostEvictionCallback((evictedKey, evictedValue, reason, state) =>
                 {
                     _keyAccessTimes.TryRemove(evictedKey.ToString()!, out _);
                     _logger.LogDebug("Cache entry evicted - Key: {Key}, Reason: {Reason}", CommonUtils.SanitizeForLog(evictedKey?.ToString() ?? ""), reason);
                 });

                _memoryCache.Set(key, value, options);
                _keyAccessTimes[key] = DateTime.UtcNow;

                _logger.LogDebug("Cache entry set - Key: {Key}, AbsoluteExpiration: {AbsoluteExpiration}, Size: 1", CommonUtils.SanitizeForLog(key), absoluteExpiration);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting item in memory cache with key: {Key}", CommonUtils.SanitizeForLog(key));
            }

            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public Task RemoveAsync(string key, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(key))
                throw new ArgumentException("Cache key cannot be null or empty", nameof(key));

            try
            {
                _memoryCache.Remove(key);
                _keyAccessTimes.TryRemove(key, out _);
                _logger.LogDebug("Cache entry removed - Key: {Key}", CommonUtils.SanitizeForLog(key));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing item from memory cache with key: {Key}", CommonUtils.SanitizeForLog(key));
            }

            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(key))
                throw new ArgumentException("Cache key cannot be null or empty", nameof(key));

            try
            {
                var exists = _memoryCache.TryGetValue(key, out _);
                return Task.FromResult(exists);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking if item exists in memory cache with key: {Key}", CommonUtils.SanitizeForLog(key));
                return Task.FromResult(false);
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
                _logger.LogError(ex, "Error in GetOrCreateAsync for key: {Key}", CommonUtils.SanitizeForLog(key));
                throw;
            }
        }

        /// <inheritdoc />
        public Task RefreshAsync(string key, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(key))
                throw new ArgumentException("Cache key cannot be null or empty", nameof(key));

            try
            {
                // In memory cache, we can just touch the entry to update sliding expiration
                if (_memoryCache.TryGetValue(key, out var value))
                {
                    _keyAccessTimes[key] = DateTime.UtcNow;
                    _logger.LogDebug("Cache entry refreshed - Key: {Key}", CommonUtils.SanitizeForLog(key));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error refreshing memory cache entry with key: {Key}", CommonUtils.SanitizeForLog(key));
            }

            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public Task ClearAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                // Memory cache doesn't have a direct clear method, but we can dispose and recreate
                // For now, we'll remove tracked keys individually
                var keys = _keyAccessTimes.Keys.ToList();
                foreach (var key in keys)
                {
                    _memoryCache.Remove(key);
                }

                _keyAccessTimes.Clear();
                _logger.LogWarning("Memory cache cleared - {KeyCount} entries removed", keys.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error clearing memory cache");
            }

            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public Task<CacheStatistics> GetStatisticsAsync()
        {
            var totalRequests = _hitCount + _missCount;
            var hitRatio = totalRequests > 0 ? (double)_hitCount / totalRequests : 0;

            var statistics = new CacheStatistics
            {
                CacheType = "Memory",
                ItemCount = _keyAccessTimes.Count,
                HitCount = _hitCount,
                MissCount = _missCount,
                HitRatio = hitRatio,
                AdditionalInfo = new Dictionary<string, object>
                {
                    ["TotalRequests"] = totalRequests,
                    ["TrackedKeys"] = _keyAccessTimes.Count
                }
            };

            return Task.FromResult(statistics);
        }
    }
}