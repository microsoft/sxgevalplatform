using SxgEvalPlatformApi.Models;

namespace SxgEvalPlatformApi.Services;

/// <summary>
/// Interface for dataset service operations
/// </summary>
public interface IDatasetService
{
    /// <summary>
    /// Save a dataset with validation and metadata storage
    /// </summary>
    /// <param name="saveDatasetDto">Dataset save request</param>
    /// <returns>Dataset save response</returns>
    Task<DatasetSaveResponseDto> SaveDatasetAsync(SaveDatasetDto saveDatasetDto);

    /// <summary>
    /// Get dataset list by agent ID
    /// </summary>
    /// <param name="agentId">Agent ID</param>
    /// <returns>Dataset list response</returns>
    Task<DatasetListResponseDto> GetDatasetListByAgentIdAsync(string agentId);

    /// <summary>
    /// Get dataset content by dataset ID
    /// </summary>
    /// <param name="datasetId">Dataset ID</param>
    /// <returns>Dataset content as JSON string</returns>
    Task<string?> GetDatasetByIdAsync(string datasetId);

    /// <summary>
    /// Get dataset metadata by dataset ID
    /// </summary>
    /// <param name="datasetId">Dataset ID</param>
    /// <returns>Dataset metadata</returns>
    Task<DatasetMetadataDto?> GetDatasetMetadataByIdAsync(string datasetId);
}