using Azure.Core;
using Azure.Data.Tables;
using Microsoft.Extensions.Logging;
using Sxg.EvalPlatform.API.Storage.TableEntities;
using SXG.EvalPlatform.Common;

namespace Sxg.EvalPlatform.API.Storage.Services
{
    /// <summary>
    /// Service for DataSet Azure Table Storage operations with caching support
    /// </summary>
    public class DataSetTableService : IDataSetTableService
    {
        private readonly Lazy<TableClient> _tableClient;
        private readonly ILogger<DataSetTableService> _logger;
        private readonly IConfigHelper _configHelper;
        private readonly string _tableName;
        private readonly string _accountName;
        private readonly ICacheManager _cacheManager;

        // Cache key constants
        private const string GET_DATASET_BY_ID_CACHE_KEY = "DATASET_ID:{0}";
        private const string GET_DATASET_CACHE_KEY = "DATASET:{0}:{1}"; // agentId:datasetId
        private const string GET_ALL_DATASETS_BY_AGENT_CACHE_KEY = "DATASETS_AGENT:{0}";
        private const string GET_DATASETS_BY_AGENT_TYPE_CACHE_KEY = "DATASETS_AGENT_TYPE:{0}:{1}"; // agentId:type
        private const string GET_DATASETS_BY_NAME_CACHE_KEY = "DATASETS_NAME:{0}:{1}"; // agentId:name
        private const string GET_DATASET_BY_NAME_TYPE_CACHE_KEY = "DATASET_NAME_TYPE:{0}:{1}:{2}"; // agentId:name:type

        public DataSetTableService(IConfigHelper configHelper, ILogger<DataSetTableService> logger, ICacheManager cacheManager)
        {
            _logger = logger;
            _configHelper = configHelper;
            _cacheManager = cacheManager;

            _accountName = configHelper.GetAzureStorageAccountName();
            _tableName = configHelper.GetDataSetsTable();

            if (string.IsNullOrEmpty(_accountName))
            {
                throw new ArgumentException("Azure Storage account name is not configured");
            }

            // Initialize lazy TableClient
            _tableClient = new Lazy<TableClient>(InitializeTableClient);

            _logger.LogInformation($"DataSetTableService initialized (lazy) for table: {_tableName}, account: {_accountName}");
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
                TokenCredential credential = CommonUtils.GetTokenCredential(environment);

                var serviceClient = new TableServiceClient(new Uri(tableUri), credential);
                var tableClient = serviceClient.GetTableClient(_tableName);

                // Ensure table exists
                tableClient.CreateIfNotExists();

                _logger.LogInformation($"TableClient successfully initialized for table: {_tableName}" );

                return tableClient;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to initialize TableClient for table: {_tableName}, account: {_accountName}");
                throw;
            }
        }

        /// <summary>
        /// Get the TableClient instance (initialized on first access)
        /// </summary>
        private TableClient TableClient => _tableClient.Value;

        /// <summary>
        /// Invalidates all cache entries related to a specific dataset
        /// </summary>
        private async Task InvalidateDataSetCacheAsync(string agentId, string datasetId, DataSetTableEntity? entity = null)
        {
            try
            {
                var invalidationTasks = new List<Task>
                {
                    // Invalidate specific dataset caches
                        _cacheManager.RemoveAsync(string.Format(GET_DATASET_BY_ID_CACHE_KEY, datasetId)),
                            _cacheManager.RemoveAsync(string.Format(GET_DATASET_CACHE_KEY, agentId, datasetId)),
        
                // Invalidate list caches for the agent
                        _cacheManager.RemoveAsync(string.Format(GET_ALL_DATASETS_BY_AGENT_CACHE_KEY, agentId))
                };

                // If we have the entity, invalidate more specific caches
                if (entity != null)
                {
                    invalidationTasks.Add(_cacheManager.RemoveAsync(
                            string.Format(GET_DATASETS_BY_AGENT_TYPE_CACHE_KEY, agentId, entity.DatasetType)));

                    invalidationTasks.Add(_cacheManager.RemoveAsync(
                      string.Format(GET_DATASETS_BY_NAME_CACHE_KEY, agentId, entity.DatasetName)));

                    invalidationTasks.Add(_cacheManager.RemoveAsync(
                    string.Format(GET_DATASET_BY_NAME_TYPE_CACHE_KEY, agentId, entity.DatasetName, entity.DatasetType)));
                }

                await Task.WhenAll(invalidationTasks);

                _logger.LogDebug("Invalidated cache for dataset - Agent: {AgentId}, DatasetId: {DatasetId}",
            agentId, datasetId);
            }
            catch (Exception ex)
            {
                // Log but don't throw - cache invalidation failure shouldn't break the operation
                _logger.LogWarning(ex, "Failed to invalidate cache for dataset - Agent: {AgentId}, DatasetId: {DatasetId}",
                agentId, datasetId);
            }
        }

        /// <summary>
        /// Invalidates all cache entries for a specific agent
        /// </summary>
        private async Task InvalidateAgentCacheAsync(string agentId)
        {
            try
            {
                // Note: We can't easily invalidate all type/name variations without knowing them
                // So we invalidate the most common cache keys
                await _cacheManager.RemoveAsync(string.Format(GET_ALL_DATASETS_BY_AGENT_CACHE_KEY, agentId));

                _logger.LogDebug("Invalidated agent-level cache for Agent: {AgentId}", agentId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to invalidate agent cache for Agent: {AgentId}", agentId);
            }
        }

        public async Task<DataSetTableEntity> SaveDataSetAsync(DataSetTableEntity entity)
        {
            try
            {
                _logger.LogInformation("Saving dataset for Agent: {AgentId}, DatasetId: {DatasetId}, Type: {DatasetType}",
                         entity.AgentId, entity.DatasetId, entity.DatasetType);

                // Update timestamp
                entity.LastUpdatedOn = DateTime.UtcNow;

                // Keys are automatically set by the entity properties
                await TableClient.UpsertEntityAsync(entity);

                // Invalidate related caches
                await InvalidateDataSetCacheAsync(entity.AgentId, entity.DatasetId, entity);

                _logger.LogInformation($"Successfully saved dataset for Agent: {entity.AgentId}, DatasetId: {entity.DatasetId}");

                return entity;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save dataset for Agent: {AgentId}, DatasetId: {DatasetId}",
                 entity.AgentId, entity.DatasetId);
                throw;
            }
        }

        public async Task<DataSetTableEntity?> GetDataSetAsync(string agentId, string datasetId)
        {
            try
            {
                var cacheKey = string.Format(GET_DATASET_CACHE_KEY, agentId, datasetId);
                var cachedEntity = await _cacheManager.GetAsync<DataSetTableEntity>(cacheKey);

                if (cachedEntity != null)
                {
                    _logger.LogDebug("Cache hit for dataset - Agent: {AgentId}, DatasetId: {DatasetId}",
                                agentId, datasetId);
                    return cachedEntity;
                }

                _logger.LogInformation($"Retrieving dataset for Agent: {agentId}, DatasetId: {datasetId}");

                var response = await TableClient.GetEntityAsync<DataSetTableEntity>(agentId, datasetId);
                var entity = response.Value;

                // Cache the result
                await _cacheManager.SetAsync(cacheKey, entity, _configHelper.GetDefaultCacheExpiration());

                _logger.LogInformation("Found dataset for Agent: {AgentId}, DatasetId: {DatasetId}",
               agentId, datasetId);

                return entity;
            }
            catch (Azure.RequestFailedException ex) when (ex.Status == 404)
            {
                _logger.LogInformation($"Dataset not found for Agent: {agentId}, DatasetId: {datasetId}");
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to retrieve dataset for Agent: {agentId}, DatasetId: {datasetId}");
                throw;
            }
        }

        public async Task<DataSetTableEntity?> GetDataSetByIdAsync(string datasetId)
        {
            try
            {
                var cacheKey = string.Format(GET_DATASET_BY_ID_CACHE_KEY, datasetId);
                var cachedEntity = await _cacheManager.GetAsync<DataSetTableEntity>(cacheKey);

                if (cachedEntity != null)
                {
                    _logger.LogDebug("Cache hit for dataset by ID: {DatasetId}", datasetId);
                    return cachedEntity;
                }

                _logger.LogInformation("Searching for dataset by ID: {DatasetId}", datasetId);

                // Since we don't know the partition key (AgentId), we need to search across all partitions
                var filter = $"DatasetId eq '{datasetId}'";

                await foreach (var entity in TableClient.QueryAsync<DataSetTableEntity>(filter))
                {
                    _logger.LogInformation("Found dataset by ID: {DatasetId} for Agent: {AgentId}",
                               datasetId, entity.AgentId);

                    // Cache the result
                    await _cacheManager.SetAsync(cacheKey, entity, _configHelper.GetDefaultCacheExpiration());

                    return entity;
                }

                _logger.LogInformation("Dataset not found by ID: {DatasetId}", datasetId);
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retrieve dataset by ID: {DatasetId}", datasetId);
                throw;
            }
        }

        public async Task<List<DataSetTableEntity>> GetAllDataSetsByAgentIdAsync(string agentId)
        {
            try
            {
                var cacheKey = string.Format(GET_ALL_DATASETS_BY_AGENT_CACHE_KEY, agentId);
                var cachedEntities = await _cacheManager.GetAsync<List<DataSetTableEntity>>(cacheKey);

                if (cachedEntities != null)
                {
                    _logger.LogDebug("Cache hit for all datasets by Agent: {AgentId}, Count: {Count}",
                         agentId, cachedEntities.Count);
                    return cachedEntities;
                }

                _logger.LogInformation("Retrieving all datasets for Agent: {AgentId}", agentId);

                var entities = new List<DataSetTableEntity>();
                var filter = $"PartitionKey eq '{agentId}'";

                await foreach (var entity in TableClient.QueryAsync<DataSetTableEntity>(filter))
                {
                    entities.Add(entity);
                }

                // Cache the result
                await _cacheManager.SetAsync(cacheKey, entities, _configHelper.GetDefaultCacheExpiration());

                _logger.LogInformation($"Retrieved {entities.Count} datasets for Agent: {agentId}");

                return entities;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retrieve datasets for Agent: {AgentId}", agentId);
                throw;
            }
        }

        public async Task<List<DataSetTableEntity>> GetAllDataSetsByAgentIdAndTypeAsync(string agentId, string datasetType)
        {
            try
            {
                var cacheKey = string.Format(GET_DATASETS_BY_AGENT_TYPE_CACHE_KEY, agentId, datasetType);
                var cachedEntities = await _cacheManager.GetAsync<List<DataSetTableEntity>>(cacheKey);

                if (cachedEntities != null)
                {
                    _logger.LogDebug($"Cache hit for datasets by Agent: {agentId}, Type: {datasetType}, Count: {cachedEntities.Count}");
                    return cachedEntities;
                }

                _logger.LogInformation($"Retrieving all datasets for Agent: {agentId}, Type: {datasetType}");

                var entities = new List<DataSetTableEntity>();
                var filter = $"PartitionKey eq '{agentId}' and DatasetType eq '{datasetType}'";

                await foreach (var entity in TableClient.QueryAsync<DataSetTableEntity>(filter))
                {
                    entities.Add(entity);
                }

                // Cache the result
                await _cacheManager.SetAsync(cacheKey, entities, _configHelper.GetDefaultCacheExpiration());

                _logger.LogInformation($"Retrieved {entities.Count} datasets for Agent: {agentId}, Type: {datasetType}");

                return entities;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to retrieve datasets for Agent: {agentId}, Type: {datasetType}");
                throw;
            }
        }

        public async Task<List<DataSetTableEntity>> GetDataSetsByDatasetNameAsync(string agentId, string datasetName)
        {
            try
            {
                var cacheKey = string.Format(GET_DATASETS_BY_NAME_CACHE_KEY, agentId, datasetName);
                var cachedEntities = await _cacheManager.GetAsync<List<DataSetTableEntity>>(cacheKey);

                if (cachedEntities != null)
                {
                    _logger.LogDebug($"Cache hit for datasets by Agent: {agentId}, DatasetName: {datasetName}, Count: {cachedEntities.Count}");
                    return cachedEntities;
                }

                _logger.LogInformation($"Retrieving datasets by dataset name for Agent: {agentId}, DatasetName: {datasetName}");

                var entities = new List<DataSetTableEntity>();
                var filter = $"PartitionKey eq '{agentId}' and DatasetName eq '{datasetName}'";

                await foreach (var entity in TableClient.QueryAsync<DataSetTableEntity>(filter))
                {
                    entities.Add(entity);
                }

                // Cache the result
                await _cacheManager.SetAsync(cacheKey, entities, _configHelper.GetDefaultCacheExpiration());

                _logger.LogInformation($"Retrieved {entities.Count} datasets for Agent: {agentId}, DatasetName: {datasetName}");

                return entities;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to retrieve datasets by dataset name for Agent: {agentId}, DatasetName: {datasetName}");
                throw;
            }
        }

        public async Task<bool> DataSetExistsAsync(string agentId, string datasetId)
        {
            var entity = await GetDataSetAsync(agentId, datasetId);
            return entity != null;
        }

        public async Task<DataSetTableEntity?> GetDataSetByDatasetNameAndTypeAsync(string agentId, string datasetName, string datasetType)
        {
            try
            {
                var cacheKey = string.Format(GET_DATASET_BY_NAME_TYPE_CACHE_KEY, agentId, datasetName, datasetType);
                var cachedEntity = await _cacheManager.GetAsync<DataSetTableEntity>(cacheKey);

                if (cachedEntity != null)
                {
                    _logger.LogDebug($"Cache hit for dataset by Agent: {agentId}, DatasetName: {datasetName}, Type: {datasetType}");
                    return cachedEntity;
                }

                _logger.LogInformation($"Retrieving dataset for Agent: {agentId}, DatasetName: {datasetName}, Type: {datasetType}");

                var filter = $"PartitionKey eq '{agentId}' and DatasetName eq '{datasetName}' and DatasetType eq '{datasetType}'";

                await foreach (var entity in TableClient.QueryAsync<DataSetTableEntity>(filter))
                {
                    // Cache the result
                    await _cacheManager.SetAsync(cacheKey, entity, _configHelper.GetDefaultCacheExpiration());

                    _logger.LogInformation($"Found dataset for Agent: {agentId}, DatasetName: {datasetName}, Type: {datasetType}");

                    return entity;
                }

                _logger.LogInformation($"Dataset not found for Agent: {agentId}, DatasetName: {datasetName}, Type: {datasetType}");
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to retrieve dataset for Agent: {agentId}, DatasetName: {datasetName}, Type: {datasetType}");
                throw;
            }
        }

        public async Task<bool> DeleteDataSetAsync(string agentId, string datasetId)
        {
            try
            {
                _logger.LogInformation("Deleting dataset for Agent: {AgentId}, DatasetId: {DatasetId}",
                     agentId, datasetId);

                // Get the entity first to have full details for cache invalidation
                var entity = await GetDataSetAsync(agentId, datasetId);

                await TableClient.DeleteEntityAsync(agentId, datasetId);

                // Invalidate related caches
                await InvalidateDataSetCacheAsync(agentId, datasetId, entity);

                _logger.LogInformation("Successfully deleted dataset for Agent: {AgentId}, DatasetId: {DatasetId}",
                agentId, datasetId);

                return true;
            }
            catch (Azure.RequestFailedException ex) when (ex.Status == 404)
            {
                _logger.LogInformation("Dataset not found for deletion - Agent: {AgentId}, DatasetId: {DatasetId}",
                          agentId, datasetId);
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete dataset for Agent: {AgentId}, DatasetId: {DatasetId}",
      agentId, datasetId);
                throw;
            }
        }

        public async Task<int> DeleteAllDataSetsByAgentIdAsync(string agentId)
        {
            try
            {
                _logger.LogInformation("Deleting all datasets for Agent: {AgentId}", agentId);

                var datasets = await GetAllDataSetsByAgentIdAsync(agentId);
                int deletedCount = 0;

                foreach (var dataset in datasets)
                {
                    try
                    {
                        await TableClient.DeleteEntityAsync(agentId, dataset.DatasetId);
                        deletedCount++;
                    }
                    catch (Azure.RequestFailedException ex) when (ex.Status == 404)
                    {
                        // Entity already deleted, continue
                        _logger.LogWarning("Dataset {DatasetId} was already deleted", dataset.DatasetId);
                    }
                }

                // Invalidate all agent-related caches
                await InvalidateAgentCacheAsync(agentId);

                // Also invalidate individual dataset caches
                foreach (var dataset in datasets)
                {
                    await InvalidateDataSetCacheAsync(agentId, dataset.DatasetId, dataset);
                }

                _logger.LogInformation("Successfully deleted {DeletedCount} datasets for Agent: {AgentId}",
                 deletedCount, agentId);

                return deletedCount;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete all datasets for Agent: {AgentId}", agentId);
                throw;
            }
        }

        public async Task<DataSetTableEntity?> UpdateDataSetMetadataAsync(string agentId, string datasetId, Action<DataSetTableEntity> updateAction)
        {
            try
            {
                _logger.LogInformation("Updating dataset metadata for Agent: {AgentId}, DatasetId: {DatasetId}",
      agentId, datasetId);

                // Get the existing entity
                var existingEntity = await GetDataSetAsync(agentId, datasetId);
                if (existingEntity == null)
                {
                    _logger.LogInformation("Dataset not found for update - Agent: {AgentId}, DatasetId: {DatasetId}",
                          agentId, datasetId);
                    return null;
                }

                // Apply the updates
                updateAction(existingEntity);
                existingEntity.LastUpdatedOn = DateTime.UtcNow;

                // Save the updated entity
                await TableClient.UpsertEntityAsync(existingEntity);

                // Invalidate related caches
                await InvalidateDataSetCacheAsync(agentId, datasetId, existingEntity);

                _logger.LogInformation("Successfully updated dataset metadata for Agent: {AgentId}, DatasetId: {DatasetId}",
          agentId, datasetId);

                return existingEntity;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update dataset metadata for Agent: {AgentId}, DatasetId: {DatasetId}",
                    agentId, datasetId);
                throw;
            }
        }
    }
}
