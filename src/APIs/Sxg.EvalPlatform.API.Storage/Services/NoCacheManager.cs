using Microsoft.Extensions.Logging;

namespace Sxg.EvalPlatform.API.Storage.Services
{
    /// <summary>
    /// No-operation cache manager implementation used when caching is disabled via feature flag.
    /// All cache operations are no-ops, effectively bypassing the cache layer.
    /// </summary>
    public class NoCacheManager : ICacheManager
    {
        private readonly ILogger<NoCacheManager> _logger;

        public NoCacheManager(ILogger<NoCacheManager> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _logger.LogInformation("NoCacheManager initialized - Data caching is DISABLED via feature flag");
        }

        /// <inheritdoc />
        public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default) where T : class
        {
            _logger.LogDebug("Cache bypass (GetAsync) - Caching disabled, returning null for key: {Key}", key);
            return Task.FromResult<T?>(null);
        }

        /// <inheritdoc />
        public Task SetAsync<T>(string key, T value, TimeSpan? expiration = null, CancellationToken cancellationToken = default) where T : class
        {
            _logger.LogDebug("Cache bypass (SetAsync) - Caching disabled, ignoring set operation for key: {Key}", key);
            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public Task SetAsync<T>(string key, T value, DateTimeOffset absoluteExpiration, CancellationToken cancellationToken = default) where T : class
        {
            _logger.LogDebug("Cache bypass (SetAsync with absolute expiration) - Caching disabled, ignoring set operation for key: {Key}", key);
            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public Task RemoveAsync(string key, CancellationToken cancellationToken = default)
        {
            _logger.LogDebug("Cache bypass (RemoveAsync) - Caching disabled, ignoring remove operation for key: {Key}", key);
            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default)
        {
            _logger.LogDebug("Cache bypass (ExistsAsync) - Caching disabled, returning false for key: {Key}", key);
            return Task.FromResult(false);
        }

        /// <inheritdoc />
        public async Task<T> GetOrCreateAsync<T>(string key, Func<Task<T>> factory, TimeSpan? expiration = null, CancellationToken cancellationToken = default) where T : class
        {
            if (factory == null)
                throw new ArgumentNullException(nameof(factory));

            _logger.LogDebug("Cache bypass (GetOrCreateAsync) - Caching disabled, executing factory for key: {Key}", key);

            // Always execute the factory since cache is disabled
            return await factory();
        }

        /// <inheritdoc />
        public Task RefreshAsync(string key, CancellationToken cancellationToken = default)
        {
            _logger.LogDebug("Cache bypass (RefreshAsync) - Caching disabled, ignoring refresh operation for key: {Key}", key);
            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public Task ClearAsync(CancellationToken cancellationToken = default)
        {
            _logger.LogDebug("Cache bypass (ClearAsync) - Caching disabled, ignoring clear operation");
            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public Task<CacheStatistics> GetStatisticsAsync()
        {
            var statistics = new CacheStatistics
            {
                CacheType = "NoCacheManager (Disabled)",
                ItemCount = 0,
                HitCount = 0,
                MissCount = 0,
                HitRatio = 0,
                AdditionalInfo = new Dictionary<string, object>
                {
                    ["Enabled"] = false,
                    ["Reason"] = "Data caching is disabled via FeatureFlags:EnableDataCaching configuration"
                }
            };

            return Task.FromResult(statistics);
        }
    }
}
