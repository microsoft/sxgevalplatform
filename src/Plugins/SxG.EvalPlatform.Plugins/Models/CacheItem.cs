namespace SxG.EvalPlatform.Plugins.Common.Models
{
    using System;

    public class CacheItem<T>
    {
        /// <summary>
        /// Cache item expiration time
        /// </summary>
        private DateTime expirationTime;

        public CacheItem(T value, TimeSpan maxCacheAge)
        {
            this.Value = value;
            this.expirationTime = DateTime.UtcNow.Add(maxCacheAge);
        }

        /// <summary>
        /// The actual value of the cache item
        /// </summary>
        public T Value { get; private set; }

        /// <summary>
        /// Determines if the cached item has expired.
        /// </summary>
        /// <returns>True if the cache item has not expired; otherwise, false.</returns>
        public bool IsValid()
        {
            // The item is valid if expiration time is in the future
            return DateTime.UtcNow < this.expirationTime;
        }
    }
}
