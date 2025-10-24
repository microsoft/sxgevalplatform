using SxgEvalPlatformApi.Models;
using Sxg.EvalPlatform.API.Storage.TableEntities;

namespace SxgEvalPlatformApi.Cache
{
    /// <summary>
    /// Interface for dataset-specific Redis caching operations
    /// Provides specialized caching for dataset metadata and content with proper namespacing
    /// </summary>
    public interface IEvalDatasetCache
    {
        /// <summary>
        /// Cache dataset metadata by dataset ID
        /// </summary>
        /// <param name="datasetMetadata">Dataset metadata to cache</param>
        /// <param name="ttlMinutes">Time to live in minutes (default: 2 hours)</param>
        /// <returns>True if cached successfully</returns>
        Task<bool> SetDatasetMetadataAsync(DatasetMetadataDto datasetMetadata, int ttlMinutes = 120);

        /// <summary>
        /// Get dataset metadata from cache by dataset ID
        /// </summary>
        /// <param name="datasetId">Dataset ID</param>
        /// <returns>Dataset metadata or null if not found</returns>
        Task<DatasetMetadataDto?> GetDatasetMetadataAsync(string datasetId);

        /// <summary>
        /// Cache dataset content by dataset ID
        /// </summary>
        /// <param name="datasetId">Dataset ID</param>
        /// <param name="content">Dataset content as JSON string</param>
        /// <param name="ttlMinutes">Time to live in minutes (default: 2 hours)</param>
        /// <returns>True if cached successfully</returns>
        Task<bool> SetDatasetContentAsync(string datasetId, string content, int ttlMinutes = 120);

        /// <summary>
        /// Get dataset content from cache by dataset ID
        /// </summary>
        /// <param name="datasetId">Dataset ID</param>
        /// <returns>Dataset content as JSON string or null if not found</returns>
        Task<string?> GetDatasetContentAsync(string datasetId);

        /// <summary>
        /// Cache dataset list for an agent
        /// </summary>
        /// <param name="agentId">Agent ID</param>
        /// <param name="datasets">List of dataset metadata</param>
        /// <param name="ttlMinutes">Time to live in minutes (default: 30 minutes)</param>
        /// <returns>True if cached successfully</returns>
        Task<bool> SetDatasetsByAgentAsync(string agentId, IList<DatasetMetadataDto> datasets, int ttlMinutes = 30);

        /// <summary>
        /// Get dataset list from cache for an agent
        /// </summary>
        /// <param name="agentId">Agent ID</param>
        /// <returns>List of dataset metadata or null if not found</returns>
        Task<IList<DatasetMetadataDto>?> GetDatasetsByAgentAsync(string agentId);

        /// <summary>
        /// Remove dataset from cache (both metadata and content)
        /// </summary>
        /// <param name="datasetId">Dataset ID</param>
        /// <returns>True if removed successfully</returns>
        Task<bool> RemoveDatasetAsync(string datasetId);

        /// <summary>
        /// Remove dataset list cache for an agent
        /// </summary>
        /// <param name="agentId">Agent ID</param>
        /// <returns>True if removed successfully</returns>
        Task<bool> RemoveDatasetsByAgentAsync(string agentId);
    }
}