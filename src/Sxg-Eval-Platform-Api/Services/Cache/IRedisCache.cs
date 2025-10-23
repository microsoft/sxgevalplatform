using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SxgEvalPlatformApi.Services.Cache
{
    /// <summary>
    /// Interface for Redis cache operations with distributed system architecture patterns
    /// </summary>
    public interface IRedisCache
    {
        /// <summary>
        /// Get a value from cache by key
        /// </summary>
        /// <typeparam name="T">Type of the cached object</typeparam>
        /// <param name="key">Cache key</param>
        /// <returns>Cached value or null if not found</returns>
        Task<T?> GetAsync<T>(string key) where T : class;

        /// <summary>
        /// Set a value in cache with optional TTL
        /// </summary>
        /// <typeparam name="T">Type of the object to cache</typeparam>
        /// <param name="key">Cache key</param>
        /// <param name="value">Value to cache</param>
        /// <param name="expiry">Optional expiry time (defaults to configuration)</param>
        /// <returns>True if successful</returns>
        Task<bool> SetAsync<T>(string key, T value, TimeSpan? expiry = null) where T : class;

        /// <summary>
        /// Remove a value from cache
        /// </summary>
        /// <param name="key">Cache key</param>
        /// <returns>True if key existed and was removed</returns>
        Task<bool> RemoveAsync(string key);

        /// <summary>
        /// Remove multiple keys matching a pattern
        /// </summary>
        /// <param name="pattern">Key pattern (e.g., "user:*")</param>
        /// <returns>Number of keys removed</returns>
        Task<long> RemoveByPatternAsync(string pattern);

        /// <summary>
        /// Check if a key exists in cache
        /// </summary>
        /// <param name="key">Cache key</param>
        /// <returns>True if key exists</returns>
        Task<bool> ExistsAsync(string key);

        /// <summary>
        /// Get cache size information
        /// </summary>
        /// <returns>Cache size information</returns>
        Task<CacheSizeInfo> GetCacheSizeAsync();

        /// <summary>
        /// Get cache health information
        /// </summary>
        /// <returns>Cache health status</returns>
        Task<CacheHealthInfo> GetHealthAsync();

        /// <summary>
        /// Clear all cache entries (use with caution)
        /// </summary>
        /// <returns>True if successful</returns>
        Task<bool> ClearAllAsync();

        /// <summary>
        /// Refresh TTL for an existing key
        /// </summary>
        /// <param name="key">Cache key</param>
        /// <param name="expiry">New expiry time</param>
        /// <returns>True if key existed and TTL was updated</returns>
        Task<bool> RefreshAsync(string key, TimeSpan expiry);

        /// <summary>
        /// Get or set pattern - if key doesn't exist, execute factory and cache result
        /// </summary>
        /// <typeparam name="T">Type of the cached object</typeparam>
        /// <param name="key">Cache key</param>
        /// <param name="factory">Function to execute if cache miss</param>
        /// <param name="expiry">Optional expiry time</param>
        /// <returns>Cached or newly created value</returns>
        Task<T?> GetOrSetAsync<T>(string key, Func<Task<T?>> factory, TimeSpan? expiry = null) where T : class;
    }

    /// <summary>
    /// Cache size information
    /// </summary>
    public class CacheSizeInfo
    {
        public long TotalKeys { get; set; }
        public long UsedMemory { get; set; }
        public long MaxMemory { get; set; }
        public double MemoryUsagePercentage { get; set; }
        public Dictionary<string, long> KeysByPattern { get; set; } = new();
    }

    /// <summary>
    /// Cache health information
    /// </summary>
    public class CacheHealthInfo
    {
        public bool IsConnected { get; set; }
        public TimeSpan ResponseTime { get; set; }
        public string Version { get; set; } = string.Empty;
        public CacheSizeInfo SizeInfo { get; set; } = new();
        public DateTime LastChecked { get; set; }
        public List<string> Warnings { get; set; } = new();
    }
}