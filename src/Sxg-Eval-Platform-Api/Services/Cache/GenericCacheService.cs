using System.Text.Json;

namespace SxgEvalPlatformApi.Services.Cache
{
    /// <summary>
    /// Generic cache service implementation providing consistent caching patterns
    /// Built on top of the Redis cache service for all entity types
    /// </summary>
    public class GenericCacheService : IGenericCacheService
    {
        private readonly IRedisCache _redisCache;
        private readonly ILogger<GenericCacheService> _logger;
        private readonly TimeSpan _defaultExpiry = TimeSpan.FromMinutes(60);

        public GenericCacheService(IRedisCache redisCache, ILogger<GenericCacheService> logger)
        {
            _redisCache = redisCache ?? throw new ArgumentNullException(nameof(redisCache));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc />
        public async Task<T?> GetOrSetAsync<T>(string cacheKey, Func<Task<T?>> fetchFunction, TimeSpan? expiry = null) where T : class
        {
            try
            {
                // Try to get from cache first
                var cached = await _redisCache.GetAsync<T>(cacheKey);
                if (cached != null)
                {
                    _logger.LogDebug("Cache hit for key: {CacheKey}", cacheKey);
                    return cached;
                }

                _logger.LogDebug("Cache miss for key: {CacheKey}", cacheKey);

                // Fetch from source
                var data = await fetchFunction();
                if (data != null)
                {
                    // Cache the result
                    await _redisCache.SetAsync(cacheKey, data, expiry ?? _defaultExpiry);
                    _logger.LogDebug("Cached data for key: {CacheKey}", cacheKey);
                }

                return data;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetOrSetAsync for key: {CacheKey}", cacheKey);
                // Fallback to fetch function if caching fails
                return await fetchFunction();
            }
        }

        /// <inheritdoc />
        public async Task<IList<T>> GetOrSetListAsync<T>(string cacheKey, Func<Task<IList<T>>> fetchFunction, TimeSpan? expiry = null)
        {
            try
            {
                // Try to get from cache first
                var cached = await _redisCache.GetAsync<IList<T>>(cacheKey);
                if (cached != null)
                {
                    _logger.LogDebug("Cache hit for list key: {CacheKey}", cacheKey);
                    return cached;
                }

                _logger.LogDebug("Cache miss for list key: {CacheKey}", cacheKey);

                // Fetch from source
                var data = await fetchFunction();
                if (data != null && data.Any())
                {
                    // Cache the result
                    await _redisCache.SetAsync(cacheKey, data, expiry ?? _defaultExpiry);
                    _logger.LogDebug("Cached list data for key: {CacheKey}, items: {Count}", cacheKey, data.Count);
                }

                return data ?? new List<T>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetOrSetListAsync for key: {CacheKey}", cacheKey);
                // Fallback to fetch function if caching fails
                return await fetchFunction() ?? new List<T>();
            }
        }

        /// <inheritdoc />
        public async Task InvalidateAsync(string cacheKey)
        {
            try
            {
                await _redisCache.RemoveAsync(cacheKey);
                _logger.LogDebug("Invalidated cache key: {CacheKey}", cacheKey);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error invalidating cache key: {CacheKey}", cacheKey);
            }
        }

        /// <inheritdoc />
        public async Task InvalidatePatternAsync(string pattern)
        {
            try
            {
                await _redisCache.RemoveByPatternAsync(pattern);
                _logger.LogDebug("Invalidated cache pattern: {Pattern}", pattern);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error invalidating cache pattern: {Pattern}", pattern);
            }
        }

        /// <inheritdoc />
        public async Task InvalidateEntityAsync(string entityType, string entityId)
        {
            var pattern = BuildCacheKey(entityType, entityId, "*");
            await InvalidatePatternAsync(pattern);
        }

        /// <inheritdoc />
        public async Task UpdateCacheAsync<T>(string cacheKey, T data, TimeSpan? expiry = null) where T : class
        {
            try
            {
                if (data != null)
                {
                    await _redisCache.SetAsync(cacheKey, data, expiry ?? _defaultExpiry);
                    _logger.LogDebug("Updated cache for key: {CacheKey}", cacheKey);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating cache for key: {CacheKey}", cacheKey);
            }
        }

        /// <inheritdoc />
        public string BuildCacheKey(string entityType, params string[] keyParts)
        {
            var sanitizedParts = keyParts.Where(p => !string.IsNullOrEmpty(p)).ToArray();
            return $"sxg-eval:{entityType.ToLower()}:{string.Join(":", sanitizedParts)}";
        }

        /// <inheritdoc />
        public async Task<bool> ExistsAsync(string cacheKey)
        {
            try
            {
                return await _redisCache.ExistsAsync(cacheKey);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking existence for key: {CacheKey}", cacheKey);
                return false;
            }
        }

        /// <inheritdoc />
        public async Task<CacheStatsDto> GetStatsAsync()
        {
            try
            {
                var healthInfo = await _redisCache.GetHealthAsync();
                var sizeInfo = await _redisCache.GetCacheSizeAsync();

                return new CacheStatsDto
                {
                    IsConnected = healthInfo.IsConnected,
                    ResponseTime = healthInfo.ResponseTime,
                    TotalKeys = sizeInfo.TotalKeys,
                    UsedMemory = sizeInfo.UsedMemory,
                    MaxMemory = sizeInfo.MaxMemory,
                    MemoryUsagePercentage = sizeInfo.MemoryUsagePercentage,
                    KeysByPattern = sizeInfo.KeysByPattern,
                    LastChecked = healthInfo.LastChecked,
                    Warnings = healthInfo.Warnings?.ToList() ?? new List<string>()
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting cache stats");
                return new CacheStatsDto
                {
                    IsConnected = false,
                    LastChecked = DateTime.UtcNow,
                    Warnings = new List<string> { $"Error getting stats: {ex.Message}" }
                };
            }
        }
    }
}