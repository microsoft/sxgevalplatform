using Azure.Core;
using Azure.Data.Tables;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Sxg.EvalPlatform.API.Storage.TableEntities;
using SXG.EvalPlatform.Common;

namespace Sxg.EvalPlatform.API.Storage.Services
{
    /// <summary>
    /// Service for MetricsConfiguration Azure Table Storage operations with caching support
    /// </summary>
    public class MetricsConfigTableService : IMetricsConfigTableService
    {
        private readonly Lazy<TableClient> _tableClient;
        private readonly ILogger<MetricsConfigTableService> _logger;
        private readonly IConfigHelper _configHelper;
        private readonly string _tableName;
        private readonly string _accountName;
        private readonly ICacheManager _cacheManager;

        // Cache key constants
        private const string GET_METRICS_CONFIG_BY_ID_CACHE_KEY = "METRICS_CONFIG_ID:{0}";
        private const string GET_ALL_METRICS_CONFIGS_BY_AGENT_CACHE_KEY = "METRICS_CONFIGS_AGENT:{0}";
        private const string GET_METRICS_CONFIGS_BY_AGENT_ENV_CACHE_KEY = "METRICS_CONFIGS_AGENT_ENV:{0}:{1}";
        private const string GET_METRICS_CONFIGS_BY_AGENT_NAME_ENV_CACHE_KEY = "METRICS_CONFIGS_AGENT_NAME_ENV:{0}:{1}:{2}";

        public MetricsConfigTableService(IConfigHelper configHelper,
                                         ILogger<MetricsConfigTableService> logger,
                                         ICacheManager cacheManager)
        {
            _logger = logger;
            _configHelper = configHelper;
            _cacheManager = cacheManager;

            _accountName = configHelper.GetAzureStorageAccountName();
            _tableName = configHelper.GetMetricsConfigurationsTable();

            if (string.IsNullOrEmpty(_accountName))
            {
                throw new ArgumentException("Azure Storage account name is not configured");
            }

            // Initialize lazy TableClient
            _tableClient = new Lazy<TableClient>(InitializeTableClient);

            _logger.LogInformation("MetricsConfigTableService initialized (lazy) for table: {TableName}, account: {AccountName}",
     _tableName, _accountName);
        }

        /// <summary>
        /// Initialize the TableClient with lazy loading
        /// </summary>
        /// <returns>Configured TableClient</returns>
        private TableClient InitializeTableClient()
        {
            try
            {
                _logger.LogInformation("Initializing TableClient for table: {TableName}, account: {AccountName}",
                      _tableName, _accountName);

                var tableUri = $"https://{_accountName}.table.core.windows.net";
                var environment = _configHelper.GetASPNetCoreEnvironment() ?? "Production";
                var managedIdentityClientId = _configHelper.GetManagedIdentityClientId();
                TokenCredential credential = CommonUtils.GetTokenCredential(environment, managedIdentityClientId);

                var serviceClient = new TableServiceClient(new Uri(tableUri), credential);
                var tableClient = serviceClient.GetTableClient(_tableName);

                // Ensure table exists
                tableClient.CreateIfNotExists();

                _logger.LogInformation("TableClient successfully initialized for table: {TableName}", _tableName);

                return tableClient;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize TableClient for table: {TableName}, account: {AccountName}",
           _tableName, _accountName);
                throw;
            }
        }

        /// <summary>
        /// Get the TableClient instance (initialized on first access)
        /// </summary>
        private TableClient TableClient => _tableClient.Value;

        /// <summary>
        /// Invalidates all cache entries related to a specific metrics configuration
        /// </summary>
        private async Task InvalidateMetricsConfigCacheAsync(string agentId, string configurationId, MetricsConfigurationTableEntity? entity = null)
        {
            try
            {
                var invalidationTasks = new List<Task>
        {
        // Invalidate specific config cache
           _cacheManager.RemoveAsync(string.Format(GET_METRICS_CONFIG_BY_ID_CACHE_KEY, configurationId)),
       
           // Invalidate agent-level cache (all configs for agent)
      _cacheManager.RemoveAsync(string.Format(GET_ALL_METRICS_CONFIGS_BY_AGENT_CACHE_KEY, agentId))
           };

                // If we have the entity, invalidate more specific caches
                if (entity != null)
                {
                    // Invalidate by agent + environment
                    if (!string.IsNullOrWhiteSpace(entity.EnvironmentName))
                    {
                        invalidationTasks.Add(_cacheManager.RemoveAsync(
                              string.Format(GET_METRICS_CONFIGS_BY_AGENT_ENV_CACHE_KEY, agentId, entity.EnvironmentName)));
                    }

                    // Invalidate by agent + name + environment
                    if (!string.IsNullOrWhiteSpace(entity.ConfigurationName) && !string.IsNullOrWhiteSpace(entity.EnvironmentName))
                    {
                        invalidationTasks.Add(_cacheManager.RemoveAsync(
                              string.Format(GET_METRICS_CONFIGS_BY_AGENT_NAME_ENV_CACHE_KEY,
                        agentId, entity.ConfigurationName, entity.EnvironmentName)));
                    }
                }

                await Task.WhenAll(invalidationTasks);

                _logger.LogDebug("Invalidated cache for metrics config - Agent: {AgentId}, ConfigurationId: {ConfigurationId}",
                         CommonUtils.SanitizeForLog(agentId), CommonUtils.SanitizeForLog(configurationId));
            }
            catch (Exception ex)
            {
                // Log but don't throw - cache invalidation failure shouldn't break the operation
                _logger.LogWarning(ex, "Failed to invalidate cache for metrics config - Agent: {AgentId}, ConfigurationId: {ConfigurationId}",
                  CommonUtils.SanitizeForLog(agentId), CommonUtils.SanitizeForLog(configurationId));
            }
        }

        /// <summary>
        /// Invalidates all cache entries for a specific agent
        /// </summary>
        private async Task InvalidateAgentCacheAsync(string agentId)
        {
            try
            {
                await _cacheManager.RemoveAsync(string.Format(GET_ALL_METRICS_CONFIGS_BY_AGENT_CACHE_KEY, agentId));
                _logger.LogDebug("Invalidated agent-level cache for Agent: {AgentId}", CommonUtils.SanitizeForLog(agentId));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to invalidate agent cache for Agent: {AgentId}", CommonUtils.SanitizeForLog(agentId));
            }
        }

        public async Task<MetricsConfigurationTableEntity> SaveMetricsConfigurationAsync(MetricsConfigurationTableEntity entity
)
        {
            try
            {
                _logger.LogInformation("Saving Metrics configuration for Agent: {AgentId}, Config: {ConfigName}, Environment: {Environment}",
        CommonUtils.SanitizeForLog(entity.AgentId), CommonUtils.SanitizeForLog(entity.ConfigurationName), CommonUtils.SanitizeForLog(entity.EnvironmentName));

                // Keys are automatically set by the entity properties
                await TableClient.UpsertEntityAsync(entity);

                // Invalidate related caches
                await InvalidateMetricsConfigCacheAsync(entity.AgentId, entity.ConfigurationId, entity);

                _logger.LogInformation("Successfully saved Metrics configuration for Agent: {AgentId}, Config: {ConfigName}, Environment: {Environment}",
                         CommonUtils.SanitizeForLog(entity.AgentId), CommonUtils.SanitizeForLog(entity.ConfigurationName), CommonUtils.SanitizeForLog(entity.EnvironmentName));

                return entity;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save Metrics configuration for Agent: {AgentId}, Config: {ConfigName}, Environment: {Environment}",
                   CommonUtils.SanitizeForLog(entity.AgentId), CommonUtils.SanitizeForLog(entity.ConfigurationName), CommonUtils.SanitizeForLog(entity.EnvironmentName));
                throw;
            }
        }

        public async Task<IList<MetricsConfigurationTableEntity>> GetAllMetricsConfigurations(string agentId, string environmentName = "")
        {
            try
            {
                // Determine cache key based on whether environment filter is applied
                string cacheKey;
                if (string.IsNullOrWhiteSpace(environmentName) || environmentName.Equals("All", StringComparison.OrdinalIgnoreCase))
                {
                    cacheKey = string.Format(GET_ALL_METRICS_CONFIGS_BY_AGENT_CACHE_KEY, agentId);
                }
                else
                {
                    cacheKey = string.Format(GET_METRICS_CONFIGS_BY_AGENT_ENV_CACHE_KEY, agentId, environmentName);
                }

                // Check cache
                var cachedEntities = await _cacheManager.GetAsync<List<MetricsConfigurationTableEntity>>(cacheKey);
                if (cachedEntities != null)
                {
                    _logger.LogDebug("Cache hit for metrics configs - Agent: {AgentId}, Environment: {Environment}, Count: {Count}",
                         CommonUtils.SanitizeForLog(agentId), CommonUtils.SanitizeForLog(environmentName), cachedEntities.Count);
                    return cachedEntities;
                }

                _logger.LogInformation("Retrieving all Metrics configurations for Agent: {AgentId}, Environment: {Environment}",
                   CommonUtils.SanitizeForLog(agentId), CommonUtils.SanitizeForLog(environmentName));

                var entities = new List<MetricsConfigurationTableEntity>();
                string filter;

                if (string.IsNullOrWhiteSpace(environmentName) || environmentName.Equals("All", StringComparison.OrdinalIgnoreCase))
                {
                    filter = $"PartitionKey eq '{agentId}'";
                }
                else
                {
                    filter = $"PartitionKey eq '{agentId}' and EnvironmentName eq '{environmentName}'";
                }

                await foreach (var entity in TableClient.QueryAsync<MetricsConfigurationTableEntity>(filter))
                {
                    entities.Add(entity);
                }

                // Cache the result
                await _cacheManager.SetAsync(cacheKey, entities, _configHelper.GetDefaultCacheExpiration());

                _logger.LogInformation("Retrieved {Count} Metrics configurations for Agent: {AgentId}, Environment: {Environment}",
                         entities.Count, CommonUtils.SanitizeForLog(agentId), CommonUtils.SanitizeForLog(environmentName));

                return entities;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retrieve Metrics configurations for Agent: {AgentId}, Environment: {Environment}",
                    CommonUtils.SanitizeForLog(agentId), CommonUtils.SanitizeForLog(environmentName));
                throw;
            }
        }

        public async Task<IList<MetricsConfigurationTableEntity>> GetAllMetricsConfigurations(string agentId, string configurationName, string environmentName)
        {
            try
            {
                var cacheKey = string.Format(GET_METRICS_CONFIGS_BY_AGENT_NAME_ENV_CACHE_KEY, agentId, configurationName, environmentName);

                // Check cache
                var cachedEntities = await _cacheManager.GetAsync<List<MetricsConfigurationTableEntity>>(cacheKey);
                if (cachedEntities != null)
                {
                    _logger.LogDebug("Cache hit for metrics configs - Agent: {AgentId}, Name: {ConfigName}, Environment: {Environment}, Count: {Count}",
                          CommonUtils.SanitizeForLog(agentId), CommonUtils.SanitizeForLog(configurationName), CommonUtils.SanitizeForLog(environmentName), cachedEntities.Count);
                    return cachedEntities;
                }

                _logger.LogInformation("Retrieving all Metrics configurations for Agent: {AgentId}, ConfigName: {ConfigName}, Environment: {Environment}",
                   CommonUtils.SanitizeForLog(agentId), CommonUtils.SanitizeForLog(configurationName), CommonUtils.SanitizeForLog(environmentName));

                var entities = new List<MetricsConfigurationTableEntity>();
                string filter = $"PartitionKey eq '{agentId}' and EnvironmentName eq '{environmentName}' and ConfigurationName eq '{configurationName}'";

                await foreach (var entity in TableClient.QueryAsync<MetricsConfigurationTableEntity>(filter))
                {
                    entities.Add(entity);
                }

                // Cache the result
                await _cacheManager.SetAsync(cacheKey, entities, _configHelper.GetDefaultCacheExpiration());

                _logger.LogInformation("Retrieved {Count} Metrics configurations for Agent: {AgentId}, ConfigName: {ConfigName}, Environment: {Environment}",
                  entities.Count, CommonUtils.SanitizeForLog(agentId), CommonUtils.SanitizeForLog(configurationName), CommonUtils.SanitizeForLog(environmentName));

                return entities;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retrieve Metrics configurations for Agent: {AgentId}, ConfigName: {ConfigName}, Environment: {Environment}",
              CommonUtils.SanitizeForLog(agentId), CommonUtils.SanitizeForLog(configurationName), CommonUtils.SanitizeForLog(environmentName));
                throw;
            }
        }

        public async Task<MetricsConfigurationTableEntity?> GetMetricsConfigurationByConfigurationIdAsync(string configurationId)
        {
            try
            {
                var cacheKey = string.Format(GET_METRICS_CONFIG_BY_ID_CACHE_KEY, configurationId);

                // Check cache
                var cachedEntity = await _cacheManager.GetAsync<MetricsConfigurationTableEntity>(cacheKey);
                if (cachedEntity != null)
                {
                    _logger.LogDebug("Cache hit for metrics config by ID: {ConfigurationId}", CommonUtils.SanitizeForLog(configurationId));
                    return cachedEntity;
                }

                _logger.LogInformation("Retrieving Metrics configuration by ConfigurationId: {ConfigurationId}", CommonUtils.SanitizeForLog(configurationId));

                var entities = new List<MetricsConfigurationTableEntity>();
                string filter = $"RowKey eq '{configurationId}'";

                await foreach (var entity in TableClient.QueryAsync<MetricsConfigurationTableEntity>(filter))
                {
                    entities.Add(entity);
                }

                var result = entities.FirstOrDefault();

                // Cache the result if found
                if (result != null)
                {
                    await _cacheManager.SetAsync(cacheKey, result, _configHelper.GetDefaultCacheExpiration());
                }

                _logger.LogInformation("Retrieved Metrics configuration by ConfigurationId: {ConfigurationId}, Found: {Found}",
           CommonUtils.SanitizeForLog(configurationId), result != null);

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retrieve Metrics configuration by ConfigurationId: {ConfigurationId}", CommonUtils.SanitizeForLog(configurationId));
                throw;
            }
        }

        public async Task<bool> DeleteMetricsConfigurationByIdAsync(string agentId, string configurationId)
        {
            try
            {
                _logger.LogInformation("Deleting Metrics configuration by ID for Agent: {AgentId}, ConfigurationId: {ConfigurationId}",
                       CommonUtils.SanitizeForLog(agentId), CommonUtils.SanitizeForLog(configurationId));

                // Get the entity first to have full details for cache invalidation
                var entity = await GetMetricsConfigurationByConfigurationIdAsync(configurationId);

                await TableClient.DeleteEntityAsync(agentId, configurationId);

                // Invalidate related caches
                await InvalidateMetricsConfigCacheAsync(agentId, configurationId, entity);

                _logger.LogInformation("Successfully deleted Metrics configuration for Agent: {AgentId}, ConfigurationId: {ConfigurationId}",
             CommonUtils.SanitizeForLog(agentId), CommonUtils.SanitizeForLog(configurationId));

                return true;
            }
            catch (Azure.RequestFailedException ex) when (ex.Status == 404)
            {
                _logger.LogInformation("Metrics configuration not found for deletion - Agent: {AgentId}, ConfigurationId: {ConfigurationId}",
           CommonUtils.SanitizeForLog(agentId), CommonUtils.SanitizeForLog(configurationId));
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete Metrics configuration for Agent: {AgentId}, ConfigurationId: {ConfigurationId}",
                    CommonUtils.SanitizeForLog(agentId), CommonUtils.SanitizeForLog(configurationId));
                throw;
            }
        }

        public Task<bool> DeleteMetricsConfigurationAsync(string agentId, string configurationName, string environmentName)
        {
            throw new NotImplementedException();
        }
    }
}
