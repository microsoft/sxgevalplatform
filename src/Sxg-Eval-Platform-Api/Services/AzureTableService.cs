using Azure;
using Azure.Core;
using Azure.Data.Tables;
using Azure.Identity;
using SxgEvalPlatformApi.Models;

namespace SxgEvalPlatformApi.Services;

/// <summary>
/// Service for Azure Table Storage operations - now using standardized BaseTableService
/// </summary>
public class AzureTableService : BaseTableService, IAzureTableService
{
    private readonly TableClient _configurationTableClient;
    private readonly TableClient _datasetTableClient;
    private const string ConfigurationTableName = "MetricsConfigurations";
    private const string DatasetTableName = "Datasets";

    public AzureTableService(IConfiguration configuration, ILogger<AzureTableService> logger) 
        : base(logger, configuration)
    {
        _configurationTableClient = CreateTableClient(ConfigurationTableName);
        _datasetTableClient = CreateTableClient(DatasetTableName);
        
        _logger.LogInformation("Azure Table Storage service initialized for configuration and dataset operations");
    }

    public async Task<ConfigurationMetadataEntity> SaveConfigurationMetadataAsync(ConfigurationMetadataEntity metadata)
    {
        // Set partition key and row key
        metadata.PartitionKey = metadata.ConfigurationType;
        metadata.RowKey = $"{metadata.AgentId}_{metadata.ConfigurationName}";
        metadata.LastUpdatedOn = DateTime.UtcNow;

        return await WriteEntityAsync(_configurationTableClient, metadata, "SaveConfigurationMetadata");
    }

    public async Task<ConfigurationMetadataEntity?> GetConfigurationMetadataAsync(string agentId, string configurationName)
    {
        var rowKey = $"{agentId}_{configurationName}";
        
        // Try application configuration first
        var result = await ReadEntityAsync<ConfigurationMetadataEntity>(
            _configurationTableClient, ConfigurationTypes.ApplicationConfiguration, rowKey, "GetConfigurationMetadata");
        
        // If not found, try platform configuration
        if (result == null)
        {
            result = await ReadEntityAsync<ConfigurationMetadataEntity>(
                _configurationTableClient, ConfigurationTypes.PlatformConfiguration, rowKey, "GetConfigurationMetadata");
        }
        
        return result;
    }

    public async Task<List<ConfigurationMetadataEntity>> GetAllConfigurationMetadataByAgentIdAsync(string agentId)
    {
        var filter = $"AgentId eq '{agentId}'";
        return await QueryEntitiesAsync<ConfigurationMetadataEntity>(_configurationTableClient, filter, "GetAllConfigurationMetadataByAgentId");
    }

    public async Task<ConfigurationMetadataEntity?> GetPlatformConfigurationMetadataAsync()
    {
        var filter = $"ConfigurationType eq '{ConfigurationTypes.PlatformConfiguration}'";
        var results = await QueryEntitiesAsync<ConfigurationMetadataEntity>(_configurationTableClient, filter, "GetPlatformConfigurationMetadata");
        return results.FirstOrDefault();
    }

    public async Task<bool> ConfigurationExistsAsync(string agentId, string configurationName)
    {
        var metadata = await GetConfigurationMetadataAsync(agentId, configurationName);
        return metadata != null;
    }

    public async Task<DatasetMetadataEntity> SaveDatasetMetadataAsync(DatasetMetadataEntity metadata)
    {
        // Set partition key and row key for datasets
        metadata.PartitionKey = metadata.AgentId;
        metadata.RowKey = metadata.DatasetId;
        metadata.LastUpdatedOn = DateTime.UtcNow;

        return await WriteEntityAsync(_datasetTableClient, metadata, "SaveDatasetMetadata");
    }

    public async Task<List<DatasetMetadataEntity>> GetAllDatasetMetadataByAgentIdAsync(string agentId)
    {
        var filter = $"PartitionKey eq '{agentId}'";
        return await QueryEntitiesAsync<DatasetMetadataEntity>(_datasetTableClient, filter, "GetAllDatasetMetadataByAgentId");
    }

    public async Task<DatasetMetadataEntity?> GetDatasetMetadataByIdAsync(string datasetId)
    {
        // For dataset lookup by ID, we need to query across all partitions
        var filter = $"DatasetId eq '{datasetId}'";
        var results = await QueryEntitiesAsync<DatasetMetadataEntity>(_datasetTableClient, filter, "GetDatasetMetadataById");
        return results.FirstOrDefault();
    }

    public async Task<DatasetMetadataEntity?> GetExistingDatasetMetadataAsync(string agentId, string datasetType, string fileName)
    {
        var filter = $"PartitionKey eq '{agentId}' and DatasetType eq '{datasetType}' and FileName eq '{fileName}'";
        var results = await QueryEntitiesAsync<DatasetMetadataEntity>(_datasetTableClient, filter, "GetExistingDatasetMetadata");
        return results.FirstOrDefault();
    }
}