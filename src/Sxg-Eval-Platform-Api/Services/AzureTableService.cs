using Azure.Core;
using Azure.Data.Tables;
using Azure.Identity;
using SxgEvalPlatformApi.Models;

namespace SxgEvalPlatformApi.Services;

/// <summary>
/// Service for Azure Table Storage operations
/// </summary>
public class AzureTableService : IAzureTableService
{
    private readonly TableClient _tableClient;
    private readonly ILogger<AzureTableService> _logger;
    private const string TableName = "MetricsConfigurations";

    public AzureTableService(IConfiguration configuration, ILogger<AzureTableService> logger)
    {
        _logger = logger;
        
        var accountName = configuration["AzureStorage:AccountName"];
        
        if (string.IsNullOrEmpty(accountName))
        {
            throw new ArgumentException("Azure Storage account name is not configured");
        }

        var tableUri = $"https://{accountName}.table.core.windows.net";

        var environment = configuration.GetValue<string>("ASPNETCORE_ENVIRONMENT") ?? "Production";
        TokenCredential credential = TokenCredentailUtils.GetTokenCredential(environment);

                
        var serviceClient = new TableServiceClient(new Uri(tableUri), credential);
        _tableClient = serviceClient.GetTableClient(TableName);
        
        // Ensure table exists
        _tableClient.CreateIfNotExists();
        
        _logger.LogInformation("Azure Table Storage service initialized with managed identity for account: {AccountName}", accountName);
    }

    public async Task<ConfigurationMetadataEntity> SaveConfigurationMetadataAsync(ConfigurationMetadataEntity metadata)
    {
        try
        {
            _logger.LogInformation("Saving configuration metadata for Agent: {AgentId}, Config: {ConfigName}", 
                metadata.AgentId, metadata.ConfigurationName);

            // Set partition key and row key
            metadata.PartitionKey = metadata.ConfigurationType;
            metadata.RowKey = $"{metadata.AgentId}_{metadata.ConfigurationName}";
            metadata.LastUpdatedOn = DateTime.UtcNow;

            await _tableClient.UpsertEntityAsync(metadata);
            
            _logger.LogInformation("Successfully saved configuration metadata with ID: {ConfigId}", metadata.ConfigurationId);
            return metadata;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save configuration metadata for Agent: {AgentId}, Config: {ConfigName}", 
                metadata.AgentId, metadata.ConfigurationName);
            throw;
        }
    }

    public async Task<ConfigurationMetadataEntity?> GetConfigurationMetadataAsync(string agentId, string configurationName)
    {
        try
        {
            _logger.LogInformation("Retrieving configuration metadata for Agent: {AgentId}, Config: {ConfigName}", 
                agentId, configurationName);

            // Try both configuration types since we don't know which one it is
            var rowKey = $"{agentId}_{configurationName}";
            
            try
            {
                var response = await _tableClient.GetEntityAsync<ConfigurationMetadataEntity>(
                    ConfigurationTypes.ApplicationConfiguration, rowKey);
                return response.Value;
            }
            catch (Azure.RequestFailedException ex) when (ex.Status == 404)
            {
                // Try platform configuration
                try
                {
                    var response = await _tableClient.GetEntityAsync<ConfigurationMetadataEntity>(
                        ConfigurationTypes.PlatformConfiguration, rowKey);
                    return response.Value;
                }
                catch (Azure.RequestFailedException ex2) when (ex2.Status == 404)
                {
                    _logger.LogInformation("Configuration metadata not found for Agent: {AgentId}, Config: {ConfigName}", 
                        agentId, configurationName);
                    return null;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve configuration metadata for Agent: {AgentId}, Config: {ConfigName}", 
                agentId, configurationName);
            throw;
        }
    }

    public async Task<List<ConfigurationMetadataEntity>> GetAllConfigurationMetadataByAgentIdAsync(string agentId)
    {
        try
        {
            _logger.LogInformation("Retrieving all configuration metadata for Agent: {AgentId}", agentId);

            var entities = new List<ConfigurationMetadataEntity>();
            
            // Query both partition keys for the agent
            var filter = $"AgentId eq '{agentId}'";
            
            await foreach (var entity in _tableClient.QueryAsync<ConfigurationMetadataEntity>(filter))
            {
                entities.Add(entity);
            }

            _logger.LogInformation("Retrieved {Count} configuration metadata entries for Agent: {AgentId}", 
                entities.Count, agentId);
            
            return entities;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve configuration metadata for Agent: {AgentId}", agentId);
            throw;
        }
    }

    public async Task<ConfigurationMetadataEntity?> GetPlatformConfigurationMetadataAsync()
    {
        try
        {
            _logger.LogInformation("Retrieving platform configuration metadata");

            var filter = $"PartitionKey eq '{ConfigurationTypes.PlatformConfiguration}'";
            
            await foreach (var entity in _tableClient.QueryAsync<ConfigurationMetadataEntity>(filter))
            {
                _logger.LogInformation("Found platform configuration with ID: {ConfigId}", entity.ConfigurationId);
                return entity;
            }

            _logger.LogInformation("No platform configuration found");
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve platform configuration metadata");
            throw;
        }
    }

    public async Task<bool> ConfigurationExistsAsync(string agentId, string configurationName)
    {
        var metadata = await GetConfigurationMetadataAsync(agentId, configurationName);
        return metadata != null;
    }

    public async Task<DatasetMetadataEntity> SaveDatasetMetadataAsync(DatasetMetadataEntity metadata)
    {
        try
        {
            _logger.LogInformation("Saving dataset metadata for Agent: {AgentId}, Dataset: {DatasetId}", 
                metadata.AgentId, metadata.DatasetId);

            // Create datasets table client
            var datasetTableClient = GetDatasetTableClient();

            // Set partition key and row key for datasets
            metadata.PartitionKey = metadata.AgentId;
            metadata.RowKey = metadata.DatasetId;
            metadata.LastUpdatedOn = DateTime.UtcNow;

            await datasetTableClient.UpsertEntityAsync(metadata);
            
            _logger.LogInformation("Successfully saved dataset metadata with ID: {DatasetId}", metadata.DatasetId);
            return metadata;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save dataset metadata for Agent: {AgentId}, Dataset: {DatasetId}", 
                metadata.AgentId, metadata.DatasetId);
            throw;
        }
    }

    public async Task<List<DatasetMetadataEntity>> GetAllDatasetMetadataByAgentIdAsync(string agentId)
    {
        try
        {
            _logger.LogInformation("Retrieving all dataset metadata for Agent: {AgentId}", agentId);

            var datasetTableClient = GetDatasetTableClient();
            var entities = new List<DatasetMetadataEntity>();
            
            var filter = $"PartitionKey eq '{agentId}'";
            
            await foreach (var entity in datasetTableClient.QueryAsync<DatasetMetadataEntity>(filter))
            {
                entities.Add(entity);
            }

            _logger.LogInformation("Retrieved {Count} dataset metadata entries for Agent: {AgentId}", 
                entities.Count, agentId);
            
            return entities;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve dataset metadata for Agent: {AgentId}", agentId);
            throw;
        }
    }

    public async Task<DatasetMetadataEntity?> GetDatasetMetadataByIdAsync(string datasetId)
    {
        try
        {
            _logger.LogInformation("Retrieving dataset metadata for Dataset: {DatasetId}", datasetId);

            var datasetTableClient = GetDatasetTableClient();
            
            // Since we don't know the partition key (AgentId), we need to query by DatasetId
            var filter = $"DatasetId eq '{datasetId}'";
            
            await foreach (var entity in datasetTableClient.QueryAsync<DatasetMetadataEntity>(filter))
            {
                _logger.LogInformation("Found dataset metadata for Dataset: {DatasetId}", datasetId);
                return entity;
            }

            _logger.LogInformation("Dataset metadata not found for Dataset: {DatasetId}", datasetId);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve dataset metadata for Dataset: {DatasetId}", datasetId);
            throw;
        }
    }

    public async Task<DatasetMetadataEntity?> GetExistingDatasetMetadataAsync(string agentId, string datasetType, string fileName)
    {
        try
        {
            _logger.LogInformation("Checking for existing dataset metadata for Agent: {AgentId}, Type: {DatasetType}, File: {FileName}", 
                agentId, datasetType, fileName);

            var datasetTableClient = GetDatasetTableClient();
            
            // Query by AgentId (partition key) and filter by DatasetType and FileName
            var filter = $"PartitionKey eq '{agentId}' and DatasetType eq '{datasetType}' and FileName eq '{fileName}'";
            
            await foreach (var entity in datasetTableClient.QueryAsync<DatasetMetadataEntity>(filter))
            {
                _logger.LogInformation("Found existing dataset metadata for Agent: {AgentId}, Type: {DatasetType}, File: {FileName}, DatasetId: {DatasetId}", 
                    agentId, datasetType, fileName, entity.DatasetId);
                return entity;
            }

            _logger.LogInformation("No existing dataset found for Agent: {AgentId}, Type: {DatasetType}, File: {FileName}", 
                agentId, datasetType, fileName);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check for existing dataset metadata for Agent: {AgentId}, Type: {DatasetType}, File: {FileName}", 
                agentId, datasetType, fileName);
            throw;
        }
    }

    private TableClient GetDatasetTableClient()
    {
        var accountName = _tableClient.AccountName;
        var tableUri = $"https://{accountName}.table.core.windows.net";
        
        var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production";
        TokenCredential credential = TokenCredentailUtils.GetTokenCredential(environment);
        
        var serviceClient = new TableServiceClient(new Uri(tableUri), credential);
        var datasetTableClient = serviceClient.GetTableClient("DataSets");
        
        // Ensure table exists
        datasetTableClient.CreateIfNotExists();
        
        return datasetTableClient;
    }
}