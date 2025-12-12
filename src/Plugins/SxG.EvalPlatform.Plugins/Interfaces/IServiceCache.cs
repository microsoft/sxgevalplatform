namespace SxG.EvalPlatform.Plugins.Common.Interfaces
{
    using System;

    public interface IServiceCache<TKey, TValue>
    {
        /// <summary>
        /// The default maximum age of the cache items. Set to 5 minutes by default.
        /// <remarks>The maximum value is limited to 2 hours.</remarks>
        /// </summary>
        TimeSpan DefaultCacheItemMaximumAge { get; set; }

        /// <summary>
        /// Add cache
        /// </summary>
        /// <param name="key">Cache key for a given organization</param>
        /// <param name="value">Cache value for a given organization</param>
        /// <param name="cacheItemAge">Caching item time span</param>
        /// <returns>True if cache item is added successfully</returns>
        bool TryAdd(TKey key, TValue value, TimeSpan? cacheItemAge = null);

        /// <summary>
        /// Get cache
        /// </summary>
        /// <param name="key">Cache key for a given organization</param>
        /// <param name="value">Cache value for a given organization</param>
        /// <returns>True if valid cache exists for a given key</returns>
        bool TryGetValue(TKey key, out TValue value);

        /// <summary>
        /// Remove cache
        /// </summary>
        /// <param name="key">Cache key for a given organization</param>
        /// <param name="value">Cache value for a given organization</param>
        /// <returns>True is cache is removed successfully</returns>
        bool TryRemove(TKey key, out TValue value);

        /// <summary>
        /// Clear all service cache
        /// </summary>
        void ClearCache();
    }
}
