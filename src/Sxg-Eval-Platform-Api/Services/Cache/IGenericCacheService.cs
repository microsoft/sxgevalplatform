using System.Text.Json;

namespace SxgEvalPlatformApi.Services.Cache
{
    /// <summary>
    /// Generic cache service interface for consistent caching across all services
    /// Provides type-safe methods for cache operations with standard patterns
    /// </summary>
    public interface IGenericCacheService
    {
        /// <summary>
        /// Get or set a cached value using the cache-aside pattern
        /// </summary>
        /// <typeparam name="T">The type of data being cached</typeparam>
        /// <param name="cacheKey">Unique cache key</param>
        /// <param name="fetchFunction">Function to fetch data if not in cache</param>
        /// <param name="expiry">Cache expiry time (optional, uses default if not specified)</param>
        /// <returns>The cached or freshly fetched data</returns>
        Task<T?> GetOrSetAsync<T>(string cacheKey, Func<Task<T?>> fetchFunction, TimeSpan? expiry = null) where T : class;

        /// <summary>
        /// Get or set a cached list using the cache-aside pattern
        /// </summary>
        /// <typeparam name="T">The type of data being cached</typeparam>
        /// <param name="cacheKey">Unique cache key</param>
        /// <param name="fetchFunction">Function to fetch data if not in cache</param>
        /// <param name="expiry">Cache expiry time (optional, uses default if not specified)</param>
        /// <returns>The cached or freshly fetched list</returns>
        Task<IList<T>> GetOrSetListAsync<T>(string cacheKey, Func<Task<IList<T>>> fetchFunction, TimeSpan? expiry = null);

        /// <summary>
        /// Invalidate cache for a specific key
        /// </summary>
        /// <param name="cacheKey">Cache key to invalidate</param>
        Task InvalidateAsync(string cacheKey);

        /// <summary>
        /// Invalidate cache for multiple keys matching a pattern
        /// </summary>
        /// <param name="pattern">Cache key pattern (e.g., "prefix:*")</param>
        Task InvalidatePatternAsync(string pattern);

        /// <summary>
        /// Invalidate cache for an entity type and ID
        /// </summary>
        /// <param name="entityType">Type of entity (e.g., "metrics", "dataset", "evalrun")</param>
        /// <param name="entityId">Entity ID</param>
        Task InvalidateEntityAsync(string entityType, string entityId);

        /// <summary>
        /// Update cache after a write operation
        /// </summary>
        /// <typeparam name="T">The type of data being cached</typeparam>
        /// <param name="cacheKey">Cache key</param>
        /// <param name="data">Updated data to cache</param>
        /// <param name="expiry">Cache expiry time (optional, uses default if not specified)</param>
        Task UpdateCacheAsync<T>(string cacheKey, T data, TimeSpan? expiry = null) where T : class;

        /// <summary>
        /// Generate a standardized cache key for an entity
        /// </summary>
        /// <param name="entityType">Type of entity (e.g., "metrics", "dataset", "evalrun")</param>
        /// <param name="keyParts">Key components (e.g., agentId, entityId)</param>
        /// <returns>Standardized cache key</returns>
        string BuildCacheKey(string entityType, params string[] keyParts);

        /// <summary>
        /// Check if a cache key exists
        /// </summary>
        /// <param name="cacheKey">Cache key to check</param>
        /// <returns>True if key exists, false otherwise</returns>
        Task<bool> ExistsAsync(string cacheKey);

        /// <summary>
        /// Get cache statistics for monitoring
        /// </summary>
        /// <returns>Cache health and size information</returns>
        Task<CacheStatsDto> GetStatsAsync();
    }

    /// <summary>
    /// Cache statistics for monitoring
    /// </summary>
    public class CacheStatsDto
    {
        public bool IsConnected { get; set; }
        public TimeSpan ResponseTime { get; set; }
        public long TotalKeys { get; set; }
        public long UsedMemory { get; set; }
        public long MaxMemory { get; set; }
        public double MemoryUsagePercentage { get; set; }
        public Dictionary<string, long> KeysByPattern { get; set; } = new();
        public DateTime LastChecked { get; set; }
        public List<string> Warnings { get; set; } = new();
    }
}