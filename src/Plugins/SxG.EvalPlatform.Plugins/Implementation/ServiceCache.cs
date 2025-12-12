namespace SxG.EvalPlatform.Plugins.Common.Implementation
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;

    using SxG.EvalPlatform.Plugins.Common.Interfaces;
    using SxG.EvalPlatform.Plugins.Common.Framework;
    using SxG.EvalPlatform.Plugins.Common.Models;

    /// <summary>
    /// Used for server side caching
    /// </summary>
    /// <typeparam name="TKey">Cache key for a given organization</typeparam>
    /// <typeparam name="TValue">Cache value for a given organization</typeparam>
    public class ServiceCache<TKey, TValue> : IServiceCache<TKey, TValue>
    {
        private const int MaxCachedItemsLimit = 5000;
        private ConcurrentDictionary<Tuple<Guid, TKey>, CacheItem<TValue>> serviceCache;
        private TimeSpan defaultCacheItemMaximumAge;

        public ServiceCache()
        {
            this.serviceCache = new ConcurrentDictionary<Tuple<Guid, TKey>, CacheItem<TValue>>();
        }

        /// <summary>
        /// The default maximum age of the cache items. Set to 5 minutes by default.
        /// <remarks>The maximum value is limited to 2 hours.</remarks>
        /// </summary>
        public TimeSpan DefaultCacheItemMaximumAge
        {
            get
            {
                if (defaultCacheItemMaximumAge == default(TimeSpan))
                {
                    defaultCacheItemMaximumAge = TimeSpan.FromMinutes(5);
                }

                return defaultCacheItemMaximumAge;
            }

            set
            {
                // Limit the max cache item age to 2 hours
                defaultCacheItemMaximumAge = value < TimeSpan.FromHours(2) ? value : TimeSpan.FromHours(2);
            }
        }

        /// <summary>
        /// OrganizationId that supports for Get only
        /// </summary>
        private Guid OrganizationId
        {
            get
            {
                Guid organizationId = Guid.Empty;
                var localPluginContext = LocalPluginContextManager.GetCurrentContext();

                if (localPluginContext != null)
                {
                    organizationId = localPluginContext.PluginExecutionContext.OrganizationId;
                }

                // If the localPluginContext is null, the code is not being run in a plugin.
                return organizationId;
            }
        }

        /// <summary>
        /// Add cache
        /// </summary>
        /// <param name="key">Cache key for a given organization</param>
        /// <param name="value">Cache value for a given organization</param>
        /// <param name="cacheItemAge">Caching item time span</param>
        /// <returns>True if cache item is added successfully</returns>
        public bool TryAdd(TKey key, TValue value, TimeSpan? cacheItemAge = null)
        {
            // Clean up the invalid cache when cache item amount exceeds MaxCachedItemsLimit
            if (this.serviceCache.Count >= MaxCachedItemsLimit)
            {
                this.SweepAndEvict();

                if (this.serviceCache.Count >= MaxCachedItemsLimit)
                {
                    return false;
                }
            }

            // Set time span of cache item
            if (cacheItemAge.HasValue)
            {
                // Limit the cache item age to DefaultMaximumCacheItemAge hours
                cacheItemAge = cacheItemAge < DefaultCacheItemMaximumAge ? cacheItemAge : DefaultCacheItemMaximumAge;
            }
            else
            {
                cacheItemAge = DefaultCacheItemMaximumAge;
            }

            return this.serviceCache.TryAdd(Tuple.Create(OrganizationId, key), new CacheItem<TValue>(value, cacheItemAge.Value));
        }

        /// <summary>
        /// Get cache
        /// </summary>
        /// <param name="key">Cache key for a given organization</param>
        /// <param name="value">Cache value for a given organization</param>
        /// <returns>True if valid cache exists for a given key</returns>
        public bool TryGetValue(TKey key, out TValue value)
        {
            CacheItem<TValue> cacheItem;
            value = default(TValue);
            var validValueFound = false;

            if (this.serviceCache.TryGetValue(Tuple.Create(OrganizationId, key), out cacheItem))
            {
                // Get the cache value if it is valid, otherwise remove it
                if (cacheItem.IsValid())
                {
                    value = cacheItem.Value;
                    validValueFound = true;
                }
                else
                {
                    this.TryRemove(key);
                }
            }

            return validValueFound;
        }

        /// <summary>
        /// Remove cache
        /// </summary>
        /// <param name="key">Cache key for a given organization</param>
        /// <param name="value">Cache value for a given organization</param>
        /// <returns>True is cache is removed successfully</returns>
        public bool TryRemove(TKey key, out TValue value)
        {
            CacheItem<TValue> cacheItem;
            var removeStatus = this.serviceCache.TryRemove(Tuple.Create(OrganizationId, key), out cacheItem);
            value = cacheItem != null ? cacheItem.Value : default(TValue);

            return removeStatus;
        }

        /// <summary>
        /// Clear all service cache
        /// </summary>
        public void ClearCache()
        {
            this.serviceCache.Clear();
        }

        /// <summary>
        /// Looks through the cache and evicts all expired items.
        /// </summary>
        private void SweepAndEvict()
        {
            var keysToRemove = new List<Tuple<Guid, TKey>>();

            foreach (var kv in this.serviceCache)
            {
                if (!kv.Value.IsValid())
                {
                    keysToRemove.Add(kv.Key);
                }
            }

            foreach (var key in keysToRemove)
            {
                CacheItem<TValue> value;
                this.serviceCache.TryRemove(key, out value);
            }
        }

        /// <summary>
        /// Remove cache for a given key
        /// </summary>
        /// <param name="key">Cache key for a given organization</param>
        /// <returns>True if the cache item is removed successfully</returns>
        private bool TryRemove(TKey key)
        {
            TValue value;
            return this.TryRemove(key, out value);
        }
    }
}
