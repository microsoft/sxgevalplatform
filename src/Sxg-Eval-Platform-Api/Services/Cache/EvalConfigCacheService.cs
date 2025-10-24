using SxgEvalPlatformApi.Models.Dtos;
using Sxg.EvalPlatform.API.Storage.Entities;
using StackExchange.Redis;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace SxgEvalPlatformApi.Services.Cache
{
    /// <summary>
    /// Redis cache implementation specifically optimized for EvalConfig (MetricsConfiguration) entities
    /// Uses proper namespacing and optimized Redis data structures
    /// </summary>
    public class EvalConfigCacheService : IEvalConfigCache
    {
        private readonly IRedisCache _redisCache;
        private readonly ILogger<EvalConfigCacheService> _logger;
        
        // Cache key patterns for EvalConfigs
        private const string CONFIG_KEY_PREFIX = "evalconfig:config:";
        private const string CONFIG_DETAIL_KEY_PREFIX = "evalconfig:detail:";
        private const string AGENT_CONFIGS_KEY_PREFIX = "evalconfig:agent:";
        private const string CONFIG_STATS_KEY = "evalconfig:stats";
        
        // Default TTL for configurations (longer since they change less frequently)
        private static readonly TimeSpan DefaultConfigTtl = TimeSpan.FromHours(2);
        private static readonly TimeSpan DefaultAgentListTtl = TimeSpan.FromMinutes(30);

        public EvalConfigCacheService(
            IRedisCache redisCache,
            ILogger<EvalConfigCacheService> logger)
        {
            _redisCache = redisCache ?? throw new ArgumentNullException(nameof(redisCache));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<MetricsConfigurationMetadataDto?> GetConfigurationAsync(string configurationId)
        {
            if (string.IsNullOrEmpty(configurationId))
                return null;

            try
            {
                var cacheKey = BuildConfigKey(configurationId);
                var config = await _redisCache.GetAsync<MetricsConfigurationMetadataDto>(cacheKey);
                
                if (config != null)
                {
                    _logger.LogDebug("Retrieved configuration {ConfigId} from cache", configurationId);
                }

                return config;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving configuration {ConfigId} from cache", configurationId);
                return null;
            }
        }

        public async Task<bool> SetConfigurationAsync(MetricsConfigurationMetadataDto configuration, TimeSpan? expiry = null)
        {
            if (configuration == null || string.IsNullOrEmpty(configuration.ConfigurationId))
                return false;

            try
            {
                var cacheKey = BuildConfigKey(configuration.ConfigurationId);
                var ttl = expiry ?? DefaultConfigTtl;
                
                var success = await _redisCache.SetAsync(cacheKey, configuration, ttl);
                
                if (success)
                {
                    _logger.LogDebug("Cached configuration {ConfigId} with TTL {TTL}", 
                        configuration.ConfigurationId, ttl);
                }

                return success;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error caching configuration {ConfigId}", configuration.ConfigurationId);
                return false;
            }
        }

        public async Task<IList<SelectedMetricsConfiguration>?> GetDetailedConfigurationAsync(string configurationId)
        {
            if (string.IsNullOrEmpty(configurationId))
                return null;

            try
            {
                var cacheKey = BuildDetailedConfigKey(configurationId);
                var detailedConfig = await _redisCache.GetAsync<IList<SelectedMetricsConfiguration>>(cacheKey);
                
                if (detailedConfig != null)
                {
                    _logger.LogDebug("Retrieved detailed configuration {ConfigId} from cache", configurationId);
                }

                return detailedConfig;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving detailed configuration {ConfigId} from cache", configurationId);
                return null;
            }
        }

        public async Task<bool> SetDetailedConfigurationAsync(string configurationId, IList<SelectedMetricsConfiguration> detailedConfig, TimeSpan? expiry = null)
        {
            if (string.IsNullOrEmpty(configurationId) || detailedConfig == null)
                return false;

            try
            {
                var cacheKey = BuildDetailedConfigKey(configurationId);
                var ttl = expiry ?? DefaultConfigTtl;
                
                var success = await _redisCache.SetAsync(cacheKey, detailedConfig, ttl);
                
                if (success)
                {
                    _logger.LogDebug("Cached detailed configuration {ConfigId} with TTL {TTL}", 
                        configurationId, ttl);
                }

                return success;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error caching detailed configuration {ConfigId}", configurationId);
                return false;
            }
        }

        public async Task<List<MetricsConfigurationMetadataDto>?> GetConfigurationsByAgentAsync(string agentId, string? environmentName = null)
        {
            if (string.IsNullOrEmpty(agentId))
                return null;

            try
            {
                var cacheKey = BuildAgentConfigsKey(agentId, environmentName);
                var configs = await _redisCache.GetAsync<List<MetricsConfigurationMetadataDto>>(cacheKey);
                
                if (configs != null)
                {
                    _logger.LogDebug("Retrieved {Count} configurations for agent {AgentId} and environment {Environment} from cache", 
                        configs.Count, agentId, environmentName ?? "all");
                }

                return configs;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving configurations for agent {AgentId} from cache", agentId);
                return null;
            }
        }

        public async Task<bool> SetConfigurationsByAgentAsync(string agentId, List<MetricsConfigurationMetadataDto> configurations, string? environmentName = null, TimeSpan? expiry = null)
        {
            if (string.IsNullOrEmpty(agentId) || configurations == null)
                return false;

            try
            {
                var cacheKey = BuildAgentConfigsKey(agentId, environmentName);
                var ttl = expiry ?? DefaultAgentListTtl;
                
                var success = await _redisCache.SetAsync(cacheKey, configurations, ttl);
                
                if (success)
                {
                    _logger.LogDebug("Cached {Count} configurations for agent {AgentId} and environment {Environment} with TTL {TTL}", 
                        configurations.Count, agentId, environmentName ?? "all", ttl);
                    
                    // Also cache individual configurations for faster single lookups
                    var individualCacheTasks = configurations.Select(config => 
                        SetConfigurationAsync(config, DefaultConfigTtl));
                    await Task.WhenAll(individualCacheTasks);
                }

                return success;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error caching configurations for agent {AgentId} and environment {Environment}", agentId, environmentName);
                return false;
            }
        }

        public async Task<bool> RemoveConfigurationAsync(string configurationId)
        {
            if (string.IsNullOrEmpty(configurationId))
                return false;

            try
            {
                // Remove both metadata and detailed configuration from cache
                var metadataKey = BuildConfigKey(configurationId);
                var detailedKey = BuildDetailedConfigKey(configurationId);
                
                var metadataRemoved = await _redisCache.RemoveAsync(metadataKey);
                var detailedRemoved = await _redisCache.RemoveAsync(detailedKey);
                
                var success = metadataRemoved || detailedRemoved;
                
                if (success)
                {
                    _logger.LogDebug("Removed configuration {ConfigId} from cache (metadata: {MetadataRemoved}, detailed: {DetailedRemoved})", 
                        configurationId, metadataRemoved, detailedRemoved);
                }

                return success;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing configuration {ConfigId} from cache", configurationId);
                return false;
            }
        }

        public async Task<long> InvalidateAgentConfigurationsAsync(string agentId, string? environmentName = null)
        {
            if (string.IsNullOrEmpty(agentId))
                return 0;

            try
            {
                // Remove the agent's configuration list cache (for specific environment or all)
                var agentCacheKey = BuildAgentConfigsKey(agentId, environmentName);
                await _redisCache.RemoveAsync(agentCacheKey);
                
                // If no specific environment, also remove any environment-specific caches for this agent
                // This ensures complete invalidation when configurations change
                if (string.IsNullOrEmpty(environmentName))
                {
                    // Pattern to match all environment-specific caches for this agent
                    // Note: This is a simplified approach - in production consider maintaining an index
                    var envPatterns = new[] { "dev", "ppe", "prod", "test" }; // Common environments
                    var removalTasks = envPatterns.Select(env => 
                        _redisCache.RemoveAsync(BuildAgentConfigsKey(agentId, env)));
                    await Task.WhenAll(removalTasks);
                }
                
                // Remove all individual configuration caches for this agent
                // Note: This requires a pattern-based removal which might be expensive
                // In production, consider using Redis keyspace notifications or maintaining a separate index
                var pattern = $"{CONFIG_KEY_PREFIX}*"; // This will remove ALL configs, not just for this agent
                
                _logger.LogDebug("Invalidated configuration caches for agent {AgentId} and environment {Environment}", 
                    agentId, environmentName ?? "all");
                
                // For now, just invalidate the agent list - individual configs will expire naturally
                return 1;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error invalidating configuration caches for agent {AgentId} and environment {Environment}", 
                    agentId, environmentName);
                return 0;
            }
        }

        public async Task<bool> ConfigurationExistsAsync(string configurationId)
        {
            if (string.IsNullOrEmpty(configurationId))
                return false;

            try
            {
                var cacheKey = BuildConfigKey(configurationId);
                return await _redisCache.ExistsAsync(cacheKey);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking if configuration {ConfigId} exists in cache", configurationId);
                return false;
            }
        }

        #region Private Helper Methods

        private static string BuildConfigKey(string configurationId)
        {
            return $"{CONFIG_KEY_PREFIX}{configurationId}";
        }

        private static string BuildDetailedConfigKey(string configurationId)
        {
            return $"{CONFIG_DETAIL_KEY_PREFIX}{configurationId}";
        }

        private static string BuildAgentConfigsKey(string agentId, string? environmentName = null)
        {
            var baseKey = $"{AGENT_CONFIGS_KEY_PREFIX}{agentId}";
            return string.IsNullOrEmpty(environmentName) 
                ? $"{baseKey}:list" 
                : $"{baseKey}:{environmentName}:list";
        }

        #endregion
    }
}