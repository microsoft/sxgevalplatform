//using SxgEvalPlatformApi.Models;

//namespace SxgEvalPlatformApi.Services;

///// <summary>
///// Interface for Azure Table Storage operations
///// </summary>
//public interface IAzureTableService
//{
//    /// <summary>
//    /// Save or update configuration metadata in Azure Table
//    /// </summary>
//    /// <param name="metadata">Configuration metadata</param>
//    /// <returns>The saved metadata entity</returns>
//    Task<ConfigurationMetadataEntity> SaveConfigurationMetadataAsync(ConfigurationMetadataEntity metadata);
    
//    /// <summary>
//    /// Get configuration metadata by Agent ID and Configuration Name
//    /// </summary>
//    /// <param name="agentId">Agent ID</param>
//    /// <param name="configurationName">Configuration name</param>
//    /// <returns>Configuration metadata or null if not found</returns>
//    Task<ConfigurationMetadataEntity?> GetConfigurationMetadataAsync(string agentId, string configurationName);
    
//    /// <summary>
//    /// Get all configuration metadata for an agent
//    /// </summary>
//    /// <param name="agentId">Agent ID</param>
//    /// <returns>List of configuration metadata</returns>
//    Task<List<ConfigurationMetadataEntity>> GetAllConfigurationMetadataByAgentIdAsync(string agentId);
    
//    /// <summary>
//    /// Get platform configuration metadata (there should be only one)
//    /// </summary>
//    /// <returns>Platform configuration metadata or null if not found</returns>
//    Task<ConfigurationMetadataEntity?> GetPlatformConfigurationMetadataAsync();
    
//    /// <summary>
//    /// Check if configuration exists by Agent ID and Configuration Name
//    /// </summary>
//    /// <param name="agentId">Agent ID</param>
//    /// <param name="configurationName">Configuration name</param>
//    /// <returns>True if exists, false otherwise</returns>
//    Task<bool> ConfigurationExistsAsync(string agentId, string configurationName);

//    /// <summary>
//    /// Save or update dataset metadata in Azure Table
//    /// </summary>
//    /// <param name="metadata">Dataset metadata</param>
//    /// <returns>The saved metadata entity</returns>
//    Task<DatasetMetadataEntity> SaveDatasetMetadataAsync(DatasetMetadataEntity metadata);

//    /// <summary>
//    /// Get all dataset metadata for an agent
//    /// </summary>
//    /// <param name="agentId">Agent ID</param>
//    /// <returns>List of dataset metadata</returns>
//    Task<List<DatasetMetadataEntity>> GetAllDatasetMetadataByAgentIdAsync(string agentId);

//    /// <summary>
//    /// Get dataset metadata by Dataset ID
//    /// </summary>
//    /// <param name="datasetId">Dataset ID</param>
//    /// <returns>Dataset metadata or null if not found</returns>
//    Task<DatasetMetadataEntity?> GetDatasetMetadataByIdAsync(string datasetId);

//    /// <summary>
//    /// Get existing dataset metadata by Agent ID, Dataset Type, and File Name
//    /// </summary>
//    /// <param name="agentId">Agent ID</param>
//    /// <param name="datasetType">Dataset type (Synthetic or Golden)</param>
//    /// <param name="fileName">File name</param>
//    /// <returns>Dataset metadata or null if not found</returns>
//    Task<DatasetMetadataEntity?> GetExistingDatasetMetadataAsync(string agentId, string datasetType, string fileName);
//}