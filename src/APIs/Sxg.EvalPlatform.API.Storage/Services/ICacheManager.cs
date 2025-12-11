namespace Sxg.EvalPlatform.API.Storage.Services
{
    /// <summary>
    /// Interface for cache management operations providing both memory and distributed caching capabilities
    /// </summary>
    public interface ICacheManager
    {
        /// <summary>
        /// Gets a cached item by key
        /// </summary>
        /// <typeparam name="T">The type of the cached item</typeparam>
        /// <param name="key">The cache key</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>The cached item or default if not found</returns>
        Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default) where T : class;

        /// <summary>
        /// Sets a cached item with the specified key and expiration
        /// </summary>
        /// <typeparam name="T">The type of the item to cache</typeparam>
        /// <param name="key">The cache key</param>
        /// <param name="value">The value to cache</param>
        /// <param name="expiration">The expiration time (null for no expiration)</param>
        /// <param name="cancellationToken">Cancellation token</param>
        Task SetAsync<T>(string key, T value, TimeSpan? expiration = null, CancellationToken cancellationToken = default) where T : class;

        /// <summary>
        /// Sets a cached item with the specified key and absolute expiration
        /// </summary>
        /// <typeparam name="T">The type of the item to cache</typeparam>
        /// <param name="key">The cache key</param>
        /// <param name="value">The value to cache</param>
        /// <param name="absoluteExpiration">The absolute expiration time</param>
        /// <param name="cancellationToken">Cancellation token</param>
        Task SetAsync<T>(string key, T value, DateTimeOffset absoluteExpiration, CancellationToken cancellationToken = default) where T : class;

        /// <summary>
        /// Removes a cached item by key
        /// </summary>
        /// <param name="key">The cache key</param>
        /// <param name="cancellationToken">Cancellation token</param>
        Task RemoveAsync(string key, CancellationToken cancellationToken = default);

        /// <summary>
        /// Checks if a cached item exists
        /// </summary>
        /// <param name="key">The cache key</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>True if the item exists in cache, false otherwise</returns>
        Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets or creates a cached item using the provided factory function
        /// </summary>
        /// <typeparam name="T">The type of the cached item</typeparam>
        /// <param name="key">The cache key</param>
        /// <param name="factory">Factory function to create the item if not cached</param>
        /// <param name="expiration">The expiration time (null for no expiration)</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>The cached or newly created item</returns>
        Task<T> GetOrCreateAsync<T>(string key, Func<Task<T>> factory, TimeSpan? expiration = null, CancellationToken cancellationToken = default) where T : class;

        /// <summary>
        /// Refreshes the expiration of a cached item (sliding expiration)
        /// </summary>
        /// <param name="key">The cache key</param>
        /// <param name="cancellationToken">Cancellation token</param>
        Task RefreshAsync(string key, CancellationToken cancellationToken = default);

        /// <summary>
        /// Clears all cached items (use with caution in distributed cache scenarios)
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        Task ClearAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets cache statistics (implementation-specific)
        /// </summary>
        /// <returns>Cache statistics information</returns>
        Task<CacheStatistics> GetStatisticsAsync();
    }

    /// <summary>
    /// Cache statistics information
    /// </summary>
    public class CacheStatistics
    {
        /// <summary>
        /// Cache type (Memory, Redis, etc.)
        /// </summary>
        public string CacheType { get; set; } = string.Empty;

        /// <summary>
        /// Number of items in cache (if available)
        /// </summary>
        public long? ItemCount { get; set; }

        /// <summary>
        /// Cache hit count (if available)
        /// </summary>
        public long? HitCount { get; set; }

        /// <summary>
        /// Cache miss count (if available)
        /// </summary>
        public long? MissCount { get; set; }

        /// <summary>
        /// Hit ratio (if available)
        /// </summary>
        public double? HitRatio { get; set; }

        /// <summary>
        /// Additional cache-specific information
        /// </summary>
        public Dictionary<string, object> AdditionalInfo { get; set; } = new();
    }
}