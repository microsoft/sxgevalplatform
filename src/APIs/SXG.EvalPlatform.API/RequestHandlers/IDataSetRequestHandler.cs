using SxgEvalPlatformApi.Models;

namespace SxgEvalPlatformApi.RequestHandlers
{
    /// <summary>
    /// Interface for DataSet request handler operations
    /// </summary>
    public interface IDataSetRequestHandler
    {
        /// <summary>
        /// Save a dataset (create new or update existing)
        /// </summary>
        /// <param name="saveDatasetDto">Dataset save request</param>
        /// <returns>Dataset save response</returns>
        Task<DatasetSaveResponseDto> SaveDatasetAsync(SaveDatasetDto saveDatasetDto);

        /// <summary>
        /// Get all datasets for an agent
        /// </summary>
        /// <param name="agentId">Agent ID</param>
        /// <returns>List of dataset metadata</returns>
        Task<IList<DatasetMetadataDto>> GetDatasetsByAgentIdAsync(string agentId);

        /// <summary>
        /// Get dataset content by dataset ID (returns deserialized list)
        /// </summary>
        /// <param name="datasetId">Dataset ID</param>
        /// <returns>Dataset content as list of EvalDataset objects, or null if not found</returns>
        Task<List<EvalDataset>?> GetDatasetByIdAsync(string datasetId);

        /// <summary>
        /// Get dataset content as raw JSON string by dataset ID
        /// </summary>
        /// <param name="datasetId">Dataset ID</param>
        /// <returns>Dataset content as JSON string, or null if not found</returns>
        Task<string?> GetDatasetByIdAsJsonAsync(string datasetId);

        /// <summary>
        /// Get dataset metadata by dataset ID
        /// </summary>
        /// <param name="datasetId">Dataset ID</param>
        /// <returns>Dataset metadata</returns>
        Task<DatasetMetadataDto?> GetDatasetMetadataByIdAsync(string datasetId);

        /// <summary>
        /// Update an existing dataset
        /// </summary>
        /// <param name="datasetId">Dataset ID</param>
        /// <param name="updateDatasetDto">Update dataset request</param>
        /// <returns>Dataset save response</returns>
        Task<DatasetSaveResponseDto> UpdateDatasetAsync(string datasetId, UpdateDatasetDto updateDatasetDto);

        /// <summary>
        /// Delete a dataset by dataset ID
        /// </summary>
        /// <param name="datasetId">Dataset ID</param>
        /// <returns>True if deleted, false if not found</returns>
        Task<bool> DeleteDatasetAsync(string datasetId);
    }
}
