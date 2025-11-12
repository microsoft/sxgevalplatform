using Azure.Core;
using Azure.Data.Tables;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Sxg.EvalPlatform.API.Storage.TableEntities;
using SXG.EvalPlatform.Common;

namespace Sxg.EvalPlatform.API.Storage.Services
{
    /// <summary>
    /// Service for DataSet Azure Table Storage operations
    /// </summary>
    public class DataSetTableService : IDataSetTableService
    {
        private readonly Lazy<TableClient> _tableClient;
        private readonly ILogger<DataSetTableService> _logger;
        private readonly IConfiguration _configuration;
        private readonly string _tableName;
        private readonly string _accountName;

        public DataSetTableService(IConfiguration configuration, ILogger<DataSetTableService> logger)
        {
            _logger = logger;
            _configuration = configuration;

            _accountName = configuration["AzureStorage:AccountName"];
            _tableName = configuration["AzureStorage:DataSetsTable"] ?? "DataSets";

            if (string.IsNullOrEmpty(_accountName))
            {
                throw new ArgumentException("Azure Storage account name is not configured");
            }

            // Initialize lazy TableClient
            _tableClient = new Lazy<TableClient>(InitializeTableClient);

            _logger.LogInformation("DataSetTableService initialized (lazy) for table: {_storageTableName}, account: {AccountName}",
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
                _logger.LogInformation("Initializing TableClient for table: {_storageTableName}, account: {AccountName}",
                    _tableName, _accountName);

                var tableUri = $"https://{_accountName}.table.core.windows.net";
                var environment = _configuration.GetValue<string>("ASPNETCORE_ENVIRONMENT") ?? "Production";
                TokenCredential credential = CommonUtils.GetTokenCredential(environment);

                var serviceClient = new TableServiceClient(new Uri(tableUri), credential);
                var tableClient = serviceClient.GetTableClient(_tableName);

                // Ensure table exists
                tableClient.CreateIfNotExists();

                _logger.LogInformation("TableClient successfully initialized for table: {_storageTableName}", _tableName);

                return tableClient;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize TableClient for table: {_storageTableName}, account: {AccountName}",
                    _tableName, _accountName);
                throw;
            }
        }

        /// <summary>
        /// Get the TableClient instance (initialized on first access)
        /// </summary>
        private TableClient TableClient => _tableClient.Value;

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

                _logger.LogInformation("Successfully saved dataset for Agent: {AgentId}, DatasetId: {DatasetId}",
                    entity.AgentId, entity.DatasetId);

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
                _logger.LogInformation("Retrieving dataset for Agent: {AgentId}, DatasetId: {DatasetId}",
                    agentId, datasetId);

                var response = await TableClient.GetEntityAsync<DataSetTableEntity>(agentId, datasetId);
                _logger.LogInformation("Found dataset for Agent: {AgentId}, DatasetId: {DatasetId}",
                    agentId, datasetId);
                return response.Value;
            }
            catch (Azure.RequestFailedException ex) when (ex.Status == 404)
            {
                _logger.LogInformation("Dataset not found for Agent: {AgentId}, DatasetId: {DatasetId}",
                    agentId, datasetId);
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retrieve dataset for Agent: {AgentId}, DatasetId: {DatasetId}",
                    agentId, datasetId);
                throw;
            }
        }

        public async Task<DataSetTableEntity?> GetDataSetByIdAsync(string datasetId)
        {
            try
            {
                _logger.LogInformation("Searching for dataset by ID: {DatasetId}", datasetId);

                // Since we don't know the partition key (AgentId), we need to search across all partitions
                var filter = $"DatasetId eq '{datasetId}'";

                await foreach (var entity in TableClient.QueryAsync<DataSetTableEntity>(filter))
                {
                    _logger.LogInformation("Found dataset by ID: {DatasetId} for Agent: {AgentId}",
                        datasetId, entity.AgentId);
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
                _logger.LogInformation("Retrieving all datasets for Agent: {AgentId}", agentId);

                var entities = new List<DataSetTableEntity>();
                var filter = $"PartitionKey eq '{agentId}'";

                await foreach (var entity in TableClient.QueryAsync<DataSetTableEntity>(filter))
                {
                    entities.Add(entity);
                }

                _logger.LogInformation("Retrieved {Count} datasets for Agent: {AgentId}",
                    entities.Count, agentId);

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
                _logger.LogInformation("Retrieving all datasets for Agent: {AgentId}, Type: {DatasetType}",
                    agentId, datasetType);

                var entities = new List<DataSetTableEntity>();
                var filter = $"PartitionKey eq '{agentId}' and DatasetType eq '{datasetType}'";

                await foreach (var entity in TableClient.QueryAsync<DataSetTableEntity>(filter))
                {
                    entities.Add(entity);
                }

                _logger.LogInformation("Retrieved {Count} datasets for Agent: {AgentId}, Type: {DatasetType}",
                    entities.Count, agentId, datasetType);

                return entities;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retrieve datasets for Agent: {AgentId}, Type: {DatasetType}",
                    agentId, datasetType);
                throw;
            }
        }

        public async Task<List<DataSetTableEntity>> GetDataSetsByDatasetNameAsync(string agentId, string datasetName)
        {
            try
            {
                _logger.LogInformation("Retrieving datasets by dataset name for Agent: {AgentId}, DatasetName: {DatasetName}",
                    agentId, datasetName);

                var entities = new List<DataSetTableEntity>();
                var filter = $"PartitionKey eq '{agentId}' and DatasetName eq '{datasetName}'";

                await foreach (var entity in TableClient.QueryAsync<DataSetTableEntity>(filter))
                {
                    entities.Add(entity);
                }

                _logger.LogInformation("Retrieved {Count} datasets for Agent: {AgentId}, DatasetName: {DatasetName}",
                    entities.Count, agentId, datasetName);

                return entities;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retrieve datasets by dataset name for Agent: {AgentId}, DatasetName: {DatasetName}",
                    agentId, datasetName);
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
                _logger.LogInformation("Retrieving dataset for Agent: {AgentId}, DatasetName: {DatasetName}, Type: {DatasetType}",
                    agentId, datasetName, datasetType);

                var filter = $"PartitionKey eq '{agentId}' and DatasetName eq '{datasetName}' and DatasetType eq '{datasetType}'";

                await foreach (var entity in TableClient.QueryAsync<DataSetTableEntity>(filter))
                {
                    _logger.LogInformation("Found dataset for Agent: {AgentId}, DatasetName: {DatasetName}, Type: {DatasetType}",
                        agentId, datasetName, datasetType);
                    return entity;
                }

                _logger.LogInformation("Dataset not found for Agent: {AgentId}, DatasetName: {DatasetName}, Type: {DatasetType}",
                    agentId, datasetName, datasetType);
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retrieve dataset for Agent: {AgentId}, DatasetName: {DatasetName}, Type: {DatasetType}",
                    agentId, datasetName, datasetType);
                throw;
            }
        }

        public async Task<bool> DeleteDataSetAsync(string agentId, string datasetId)
        {
            try
            {
                _logger.LogInformation("Deleting dataset for Agent: {AgentId}, DatasetId: {DatasetId}",
                    agentId, datasetId);

                await TableClient.DeleteEntityAsync(agentId, datasetId);
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
