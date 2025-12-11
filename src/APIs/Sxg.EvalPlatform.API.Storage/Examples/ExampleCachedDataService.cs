using Sxg.EvalPlatform.API.Storage.Services;
using Microsoft.Extensions.Logging;

namespace Sxg.EvalPlatform.API.Storage.Examples
{
    /// <summary>
    /// Example service demonstrating how to use ICacheManager for caching data
    /// </summary>
    public class ExampleCachedDataService
    {
        private readonly ICacheManager _cacheManager;
        private readonly ILogger<ExampleCachedDataService> _logger;
        private const string CACHE_KEY_PREFIX = "example_service:";

        public ExampleCachedDataService(ICacheManager cacheManager, ILogger<ExampleCachedDataService> logger)
        {
            _cacheManager = cacheManager;
            _logger = logger;
        }

        /// <summary>
        /// Example: Get user data with caching
        /// </summary>
        public async Task<UserData?> GetUserDataAsync(string userId, CancellationToken cancellationToken = default)
        {
            var cacheKey = $"{CACHE_KEY_PREFIX}user:{userId}";

            try
            {
                // Try to get from cache first
                var cachedUser = await _cacheManager.GetAsync<UserData>(cacheKey, cancellationToken);
                if (cachedUser != null)
                {
                    _logger.LogDebug("User data retrieved from cache for user: {UserId}", userId);
                    return cachedUser;
                }

                // If not in cache, fetch from data source
                var userData = await FetchUserDataFromDatabase(userId, cancellationToken);
                if (userData != null)
                {
                    // Cache the result for 1 hour
                    await _cacheManager.SetAsync(cacheKey, userData, TimeSpan.FromHours(1), cancellationToken);
                    _logger.LogDebug("User data cached for user: {UserId}", userId);
                }

                return userData;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving user data for user: {UserId}", userId);
                // Fallback to database on cache failure
                return await FetchUserDataFromDatabase(userId, cancellationToken);
            }
        }

        /// <summary>
        /// Example: Get evaluation results with cache-aside pattern
        /// </summary>
        public async Task<EvaluationResults?> GetEvaluationResultsAsync(string evalRunId, CancellationToken cancellationToken = default)
        {
            var cacheKey = $"{CACHE_KEY_PREFIX}eval_results:{evalRunId}";

            return await _cacheManager.GetOrCreateAsync(
   cacheKey,
          () => FetchEvaluationResultsFromStorage(evalRunId, cancellationToken),
  TimeSpan.FromMinutes(30), // Cache for 30 minutes
         cancellationToken);
        }

        /// <summary>
        /// Example: Update user data and invalidate cache
        /// </summary>
        public async Task<bool> UpdateUserDataAsync(string userId, UserData userData, CancellationToken cancellationToken = default)
        {
            try
            {
                // Update in database
                var success = await UpdateUserDataInDatabase(userId, userData, cancellationToken);

                if (success)
                {
                    // Invalidate cache
                    var cacheKey = $"{CACHE_KEY_PREFIX}user:{userId}";
                    await _cacheManager.RemoveAsync(cacheKey, cancellationToken);
                    _logger.LogDebug("Cache invalidated for user: {UserId}", userId);
                }

                return success;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating user data for user: {UserId}", userId);
                return false;
            }
        }

        /// <summary>
        /// Example: Bulk cache operations
        /// </summary>
        public async Task<Dictionary<string, MetricData>> GetMetricsDataAsync(IEnumerable<string> metricIds, CancellationToken cancellationToken = default)
        {
            var results = new Dictionary<string, MetricData>();
            var uncachedMetricIds = new List<string>();

            // Check cache for each metric
            foreach (var metricId in metricIds)
            {
                var cacheKey = $"{CACHE_KEY_PREFIX}metric:{metricId}";
                var cachedMetric = await _cacheManager.GetAsync<MetricData>(cacheKey, cancellationToken);

                if (cachedMetric != null)
                {
                    results[metricId] = cachedMetric;
                }
                else
                {
                    uncachedMetricIds.Add(metricId);
                }
            }

            // Fetch uncached Metrics from database
            if (uncachedMetricIds.Any())
            {
                var freshMetrics = await FetchMetricsFromDatabase(uncachedMetricIds, cancellationToken);

                // Cache fresh Metrics and add to results
                foreach (var (metricId, metricData) in freshMetrics)
                {
                    var cacheKey = $"{CACHE_KEY_PREFIX}metric:{metricId}";
                    await _cacheManager.SetAsync(cacheKey, metricData, TimeSpan.FromMinutes(15), cancellationToken);
                    results[metricId] = metricData;
                }
            }

            return results;
        }

        /// <summary>
        /// Example: Cache with sliding expiration
        /// </summary>
        public async Task<SessionData?> GetSessionDataAsync(string sessionId, CancellationToken cancellationToken = default)
        {
            var cacheKey = $"{CACHE_KEY_PREFIX}session:{sessionId}";

            var sessionData = await _cacheManager.GetAsync<SessionData>(cacheKey, cancellationToken);
            if (sessionData != null)
            {
                // Refresh the cache entry to extend its lifetime (sliding expiration)
                await _cacheManager.RefreshAsync(cacheKey, cancellationToken);
                return sessionData;
            }

            // Create new session data if not found
            var newSessionData = new SessionData
            {
                SessionId = sessionId,
                CreatedAt = DateTime.UtcNow,
                LastAccessedAt = DateTime.UtcNow
            };

            await _cacheManager.SetAsync(cacheKey, newSessionData, TimeSpan.FromMinutes(20), cancellationToken);
            return newSessionData;
        }

        /// <summary>
        /// Example: Get cache statistics
        /// </summary>
        public async Task<CacheStatistics> GetCacheStatisticsAsync()
        {
            return await _cacheManager.GetStatisticsAsync();
        }

        /// <summary>
        /// Example: Clear cache entries by pattern (be careful with this in production)
        /// </summary>
        public async Task ClearUserCacheAsync(string userId, CancellationToken cancellationToken = default)
        {
            // For individual key removal
            var userCacheKey = $"{CACHE_KEY_PREFIX}user:{userId}";
            await _cacheManager.RemoveAsync(userCacheKey, cancellationToken);

            // Note: Pattern-based removal is not directly supported by the interface
            // as it's not available in all cache providers. Implement as needed.
        }

        // Mock database methods for demonstration
        private async Task<UserData?> FetchUserDataFromDatabase(string userId, CancellationToken cancellationToken)
        {
            // Simulate database call
            await Task.Delay(100, cancellationToken);
            return new UserData { Id = userId, Name = $"User {userId}", LastLogin = DateTime.UtcNow };
        }

        private async Task<EvaluationResults?> FetchEvaluationResultsFromStorage(string evalRunId, CancellationToken cancellationToken)
        {
            // Simulate storage call
            await Task.Delay(200, cancellationToken);
            return new EvaluationResults { EvalRunId = evalRunId, Results = new Dictionary<string, object>() };
        }

        private async Task<bool> UpdateUserDataInDatabase(string userId, UserData userData, CancellationToken cancellationToken)
        {
            // Simulate database update
            await Task.Delay(150, cancellationToken);
            return true;
        }

        private async Task<Dictionary<string, MetricData>> FetchMetricsFromDatabase(IEnumerable<string> metricIds, CancellationToken cancellationToken)
        {
            // Simulate database call
            await Task.Delay(300, cancellationToken);
            return metricIds.ToDictionary(id => id, id => new MetricData { Id = id, Value = Random.Shared.NextDouble() });
        }
    }

    // Example data models
    public class UserData
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public DateTime LastLogin { get; set; }
    }

    public class EvaluationResults
    {
        public string EvalRunId { get; set; } = string.Empty;
        public Dictionary<string, object> Results { get; set; } = new();
    }

    public class MetricData
    {
        public string Id { get; set; } = string.Empty;
        public double Value { get; set; }
    }

    public class SessionData
    {
        public string SessionId { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime LastAccessedAt { get; set; }
    }
}