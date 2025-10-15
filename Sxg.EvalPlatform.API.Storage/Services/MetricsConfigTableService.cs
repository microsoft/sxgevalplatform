using Azure.Core;
using Azure.Data.Tables;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Sxg.EvalPlatform.API.Storage.TableEntities;
using SXG.EvalPlatform.Common;

namespace Sxg.EvalPlatform.API.Storage.Services
{
    /// <summary>
    /// Service for MetricsConfiguration Azure Table Storage operations
    /// </summary>
    public class MetricsConfigTableService : IMetricsConfigTableService
    {
        private readonly Lazy<TableClient> _tableClient;
        private readonly ILogger<MetricsConfigTableService> _logger;
        private readonly IConfiguration _configuration;
        private readonly string _tableName;
        private readonly string _accountName;

        public MetricsConfigTableService(IConfiguration configuration, ILogger<MetricsConfigTableService> logger)
        {
            _logger = logger;
            _configuration = configuration;

            _accountName = configuration["AzureStorage:AccountName"];
            _tableName = configuration["AzureStorage:MetricsConfigurationsTable"] ?? "MetricsConfigurations";

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
                var environment = _configuration.GetValue<string>("ASPNETCORE_ENVIRONMENT") ?? "Production";
                TokenCredential credential = CommonUtils.GetTokenCredential(environment);

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

        public async Task<MetricsConfigurationTableEntity> SaveMetricsConfigurationAsync(MetricsConfigurationTableEntity entity)
        {
            try
            {
                _logger.LogInformation("Saving metrics configuration for Agent: {AgentId}, Config: {ConfigName}, Environment: {Environment}",
                    entity.AgentId, entity.ConfigurationName, entity.EnvironmentName);

                // Update timestamp
                entity.LastUpdatedOn = DateTime.UtcNow;

                // Keys are automatically set by the entity properties
                await TableClient.UpsertEntityAsync(entity);

                _logger.LogInformation("Successfully saved metrics configuration for Agent: {AgentId}, Config: {ConfigName}, Environment: {Environment}",
                    entity.AgentId, entity.ConfigurationName, entity.EnvironmentName);

                return entity;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save metrics configuration for Agent: {AgentId}, Config: {ConfigName}, Environment: {Environment}",
                    entity.AgentId, entity.ConfigurationName, entity.EnvironmentName);
                throw;
            }
        }

        //public async Task<MetricsConfigurationTableEntity?> GetMetricsConfigurationAsync(string agentId, string configurationName)
        //{
        //    try
        //    {
        //        _logger.LogInformation("Retrieving metrics configuration for Agent: {AgentId}, Config: {ConfigName}",
        //            agentId, configurationName);

        //        // Query all configurations for the agent and filter by configuration name
        //        var filter = $"PartitionKey eq '{agentId}' and ConfigurationName eq '{configurationName}'";

        //        await foreach (var entity in TableClient.QueryAsync<MetricsConfigurationTableEntity>(filter))
        //        {
        //            _logger.LogInformation("Found metrics configuration for Agent: {AgentId}, Config: {ConfigName}, Environment: {Environment}",
        //                agentId, configurationName, entity.EnvironmentName);
        //            return entity;
        //        }

        //        _logger.LogInformation("Metrics configuration not found for Agent: {AgentId}, Config: {ConfigName}",
        //            agentId, configurationName);
        //        return null;
        //    }
        //    catch (Exception ex)
        //    {
        //        _logger.LogError(ex, "Failed to retrieve metrics configuration for Agent: {AgentId}, Config: {ConfigName}",
        //            agentId, configurationName);
        //        throw;
        //    }
        //}

        //public async Task<MetricsConfigurationTableEntity?> GetMetricsConfigurationAsync(string agentId, string configurationName, string environmentName)
        //{
        //    try
        //    {
        //        _logger.LogInformation("Retrieving metrics configuration for Agent: {AgentId}, Config: {ConfigName}, Environment: {Environment}",
        //            agentId, configurationName, environmentName);

        //        // Since we're using UUID as RowKey, we need to search by filter instead of direct lookup
        //        var filter = $"PartitionKey eq '{agentId}' and ConfigurationName eq '{configurationName}' and EnvironmentName eq '{environmentName}'";

        //        await foreach (var entity in TableClient.QueryAsync<MetricsConfigurationTableEntity>(filter))
        //        {
        //            _logger.LogInformation("Found metrics configuration for Agent: {AgentId}, Config: {ConfigName}, Environment: {Environment}",
        //                agentId, configurationName, environmentName);
        //            return entity;
        //        }

        //        _logger.LogInformation("Metrics configuration not found for Agent: {AgentId}, Config: {ConfigName}, Environment: {Environment}",
        //            agentId, configurationName, environmentName);
        //        return null;
        //    }
        //    catch (Exception ex)
        //    {
        //        _logger.LogError(ex, "Failed to retrieve metrics configuration for Agent: {AgentId}, Config: {ConfigName}, Environment: {Environment}",
        //            agentId, configurationName, environmentName);
        //        throw;
        //    }
        //}

        //public async Task<List<MetricsConfigurationTableEntity>> GetAllMetricsConfigurationsByAgentIdAsync(string agentId)
        //{
        //    try
        //    {
        //        _logger.LogInformation("Retrieving all metrics configurations for Agent: {AgentId}", agentId);

        //        var entities = new List<MetricsConfigurationTableEntity>();
        //        var filter = $"PartitionKey eq '{agentId}'";

        //        await foreach (var entity in TableClient.QueryAsync<MetricsConfigurationTableEntity>(filter))
        //        {
        //            entities.Add(entity);
        //        }

        //        _logger.LogInformation("Retrieved {Count} metrics configurations for Agent: {AgentId}",
        //            entities.Count, agentId);

        //        return entities;
        //    }
        //    catch (Exception ex)
        //    {
        //        _logger.LogError(ex, "Failed to retrieve metrics configurations for Agent: {AgentId}", agentId);
        //        throw;
        //    }
        //}

        public async Task<IList<MetricsConfigurationTableEntity>> GetAllMetricsConfigurations(string agentId, string environmentName)
        {
            try
            {
                _logger.LogInformation("Retrieving all metrics configurations for Agent: {AgentId}, Environment: {Environment}",
                    agentId, environmentName);

                var entities = new List<MetricsConfigurationTableEntity>();
                string filter = string.Empty; 

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

                _logger.LogInformation("Retrieved {Count} metrics configurations for Agent: {AgentId}, Environment: {Environment}",
                    entities.Count, agentId, environmentName);

                return entities;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retrieve metrics configurations for Agent: {AgentId}, Environment: {Environment}",
                    agentId, environmentName);
                throw;
            }
        }

        public async Task<IList<MetricsConfigurationTableEntity>> GetAllMetricsConfigurations(string agentId, string configurationName, string environmentName)
        {
            try
            {
                _logger.LogInformation("Retrieving all metrics configurations for Agent: {AgentId}, Environment: {Environment}",
                    agentId, environmentName);

                var entities = new List<MetricsConfigurationTableEntity>();

                string filter = $"PartitionKey eq '{agentId}' and EnvironmentName eq '{environmentName}' and ConfigurationName eq '{configurationName}'";


                await foreach (var entity in TableClient.QueryAsync<MetricsConfigurationTableEntity>(filter))
                {
                    entities.Add(entity);
                }

                _logger.LogInformation($"Retrieved {entities.Count} metrics configurations for Agent: {agentId}, Environment: {environmentName}, ConfigurationName: {configurationName}");

                return entities;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to retrieve metrics configurations for Agent: {agentId}, Environment: {environmentName}"); 
                throw;
            }
        }

        public async Task<MetricsConfigurationTableEntity?> GetMetricsConfigurationByConfigurationIdAsync(string configurationId)
        {
            try
            {
                _logger.LogInformation($"Retrieving all metrics configurations by ConfiguarationId: {configurationId}");

                var entities = new List<MetricsConfigurationTableEntity>();
                string filter = $"RowKey eq '{configurationId}'"; 

                await foreach (var entity in TableClient.QueryAsync<MetricsConfigurationTableEntity>(filter))
                {
                    entities.Add(entity);
                }

                _logger.LogInformation($"Retrieved metrics configurations by ConfigurationId: {configurationId}");

                return entities.FirstOrDefault();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to retrieve metrics configurations by ConfigurationId: {configurationId}");
                throw;
            }
        }
               


        //public async Task<bool> MetricsConfigurationExistsAsync(string agentId, string configurationName)
        //{
        //    var entity = await GetMetricsConfigurationAsync(agentId, configurationName);
        //    return entity != null;
        //}



        //public async Task<MetricsConfigurationTableEntity?> GetMetricsConfigurationByIdAsync(string agentId, string configurationId)
        //{
        //    try
        //    {
        //        _logger.LogInformation("Retrieving metrics configuration by ID for Agent: {AgentId}, ConfigurationId: {ConfigurationId}",
        //            agentId, configurationId);

        //        var response = await TableClient.GetEntityAsync<MetricsConfigurationTableEntity>(agentId, configurationId);
        //        _logger.LogInformation("Found metrics configuration for Agent: {AgentId}, ConfigurationId: {ConfigurationId}",
        //            agentId, configurationId);
        //        return response.Value;
        //    }
        //    catch (Azure.RequestFailedException ex) when (ex.Status == 404)
        //    {
        //        _logger.LogInformation("Metrics configuration not found for Agent: {AgentId}, ConfigurationId: {ConfigurationId}",
        //            agentId, configurationId);
        //        return null;
        //    }
        //    catch (Exception ex)
        //    {
        //        _logger.LogError(ex, "Failed to retrieve metrics configuration for Agent: {AgentId}, ConfigurationId: {ConfigurationId}",
        //            agentId, configurationId);
        //        throw;
        //    }
        //}

        public async Task<bool> DeleteMetricsConfigurationByIdAsync(string agentId, string configurationId)
        {
            try
            {
                _logger.LogInformation("Deleting metrics configuration by ID for Agent: {AgentId}, ConfigurationId: {ConfigurationId}",
                    agentId, configurationId);

                await TableClient.DeleteEntityAsync(agentId, configurationId);
                _logger.LogInformation("Successfully deleted metrics configuration for Agent: {AgentId}, ConfigurationId: {ConfigurationId}",
                    agentId, configurationId);
                return true;
            }
            catch (Azure.RequestFailedException ex) when (ex.Status == 404)
            {
                _logger.LogInformation("Metrics configuration not found for deletion - Agent: {AgentId}, ConfigurationId: {ConfigurationId}",
                    agentId, configurationId);
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete metrics configuration for Agent: {AgentId}, ConfigurationId: {ConfigurationId}",
                    agentId, configurationId);
                throw;
            }
        }

       

       

        public Task<bool> DeleteMetricsConfigurationAsync(string agentId, string configurationName, string environmentName)
        {
            throw new NotImplementedException();
        }

        //public async Task<bool> DeleteMetricsConfigurationAsync(string agentId, string configurationName, string environmentName)
        //{
        //    try
        //    {
        //        _logger.LogInformation("Deleting metrics configuration for Agent: {AgentId}, Config: {ConfigName}, Environment: {Environment}",
        //            agentId, configurationName, environmentName);

        //        // First find the entity to get its UUID RowKey
        //        var entityToDelete = await GetMetricsConfigurationAsync(agentId, configurationName, environmentName);

        //        if (entityToDelete == null)
        //        {
        //            _logger.LogInformation("Metrics configuration not found for deletion - Agent: {AgentId}, Config: {ConfigName}, Environment: {Environment}",
        //                agentId, configurationName, environmentName);
        //            return false;
        //        }

        //        // Use the direct delete by ID method
        //        return await DeleteMetricsConfigurationByIdAsync(agentId, entityToDelete.ConfigurationId);
        //    }
        //    catch (Exception ex)
        //    {
        //        _logger.LogError(ex, "Failed to delete metrics configuration for Agent: {AgentId}, Config: {ConfigName}, Environment: {Environment}",
        //            agentId, configurationName, environmentName);
        //        throw;
        //    }
        //}
    }
}
