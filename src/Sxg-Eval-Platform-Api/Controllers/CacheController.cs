using Microsoft.AspNetCore.Mvc;
using SxgEvalPlatformApi.Services.Cache;

namespace SxgEvalPlatformApi.Controllers
{
    [Route("api/v1/cache")]
    [ApiController]
    public class CacheController : ControllerBase
    {
        private readonly IRedisCache _cache;
        private readonly ILogger<CacheController> _logger;

        public CacheController(IRedisCache cache, ILogger<CacheController> logger)
        {
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Get cache health information
        /// </summary>
        /// <returns>Cache health status and performance metrics</returns>
        [HttpGet("health")]
        [ProducesResponseType(typeof(CacheHealthInfo), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
        public async Task<ActionResult<CacheHealthInfo>> GetCacheHealth()
        {
            try
            {
                var health = await _cache.GetHealthAsync();
                
                if (!health.IsConnected)
                {
                    return StatusCode(503, health);
                }

                return Ok(health);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking cache health");
                return StatusCode(503, new CacheHealthInfo
                {
                    IsConnected = false,
                    LastChecked = DateTime.UtcNow,
                    Warnings = { $"Health check failed: {ex.Message}" }
                });
            }
        }

        /// <summary>
        /// Get cache size and memory usage information
        /// </summary>
        /// <returns>Cache size statistics</returns>
        [HttpGet("size")]
        [ProducesResponseType(typeof(CacheSizeInfo), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<CacheSizeInfo>> GetCacheSize()
        {
            try
            {
                var sizeInfo = await _cache.GetCacheSizeAsync();
                return Ok(sizeInfo);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting cache size information");
                return StatusCode(500, "Failed to retrieve cache size information");
            }
        }

        /// <summary>
        /// Clear all cache entries (use with extreme caution)
        /// </summary>
        /// <returns>Success status</returns>
        [HttpDelete("clear")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult> ClearCache()
        {
            try
            {
                _logger.LogWarning("Cache clear requested - this will remove all cached data");
                var result = await _cache.ClearAllAsync();
                
                if (result)
                {
                    return Ok(new { message = "Cache cleared successfully", timestamp = DateTime.UtcNow });
                }
                else
                {
                    return StatusCode(500, "Failed to clear cache");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error clearing cache");
                return StatusCode(500, "Failed to clear cache");
            }
        }

        /// <summary>
        /// Remove specific cache entries by pattern
        /// </summary>
        /// <param name="pattern">Cache key pattern (e.g., "metrics:*")</param>
        /// <returns>Number of keys removed</returns>
        [HttpDelete("pattern/{pattern}")]
        [ProducesResponseType(typeof(long), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<long>> ClearCacheByPattern(string pattern)
        {
            if (string.IsNullOrWhiteSpace(pattern))
            {
                return BadRequest("Pattern cannot be empty");
            }

            try
            {
                _logger.LogInformation("Cache pattern clear requested for pattern: {Pattern}", pattern);
                var removedCount = await _cache.RemoveByPatternAsync(pattern);
                
                return Ok(new { removedCount, pattern, timestamp = DateTime.UtcNow });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error clearing cache by pattern: {Pattern}", pattern);
                return StatusCode(500, "Failed to clear cache by pattern");
            }
        }

        /// <summary>
        /// Test cache operations (set and get a test value)
        /// </summary>
        /// <returns>Cache operation test results</returns>
        [HttpPost("test")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult> TestCache()
        {
            try
            {
                var testKey = $"cache-test-{DateTime.UtcNow:yyyyMMdd-HHmmss}";
                var testValue = new { message = "Cache test", timestamp = DateTime.UtcNow };

                // Test SET operation
                var setResult = await _cache.SetAsync(testKey, testValue, TimeSpan.FromMinutes(1));
                if (!setResult)
                {
                    return StatusCode(500, "Failed to set test value in cache");
                }

                // Test GET operation
                var getValue = await _cache.GetAsync<object>(testKey);
                if (getValue == null)
                {
                    return StatusCode(500, "Failed to retrieve test value from cache");
                }

                // Test EXISTS operation
                var exists = await _cache.ExistsAsync(testKey);
                if (!exists)
                {
                    return StatusCode(500, "Test key does not exist in cache");
                }

                // Clean up test key
                await _cache.RemoveAsync(testKey);

                return Ok(new 
                { 
                    message = "Cache test completed successfully",
                    operations = new { set = setResult, get = getValue != null, exists },
                    timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during cache test");
                return StatusCode(500, "Cache test failed");
            }
        }
    }
}