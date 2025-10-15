using Sxg.EvalPlatform.API.Storage.TableEntities;

namespace Sxg.EvalPlatform.API.Storage.Services
{
    /// <summary>
    /// Interface for DataSet table operations
    /// </summary>
    public interface IDataSetTableService
    {
        /// <summary>
        /// Save or update dataset in Azure Table
        /// </summary>
        /// <param name="entity">Dataset entity</param>
        /// <returns>The saved entity</returns>
        Task<DataSetTableEntity> SaveDataSetAsync(DataSetTableEntity entity);

        /// <summary>
        /// Get dataset by Agent ID and Dataset ID
        /// </summary>
        /// <param name="agentId">Agent ID</param>
        /// <param name="datasetId">Dataset ID</param>
        /// <returns>Dataset entity or null if not found</returns>
        Task<DataSetTableEntity?> GetDataSetAsync(string agentId, string datasetId);

        /// <summary>
        /// Get dataset by Dataset ID (searches across all agents)
        /// </summary>
        /// <param name="datasetId">Dataset ID</param>
        /// <returns>Dataset entity or null if not found</returns>
        Task<DataSetTableEntity?> GetDataSetByIdAsync(string datasetId);

        /// <summary>
        /// Get all datasets for an agent
        /// </summary>
        /// <param name="agentId">Agent ID</param>
        /// <returns>List of dataset entities</returns>
        Task<List<DataSetTableEntity>> GetAllDataSetsByAgentIdAsync(string agentId);

        /// <summary>
        /// Get all datasets for an agent by dataset type
        /// </summary>
        /// <param name="agentId">Agent ID</param>
        /// <param name="datasetType">Dataset type (e.g., Synthetic, Golden)</param>
        /// <returns>List of dataset entities</returns>
        Task<List<DataSetTableEntity>> GetAllDataSetsByAgentIdAndTypeAsync(string agentId, string datasetType);

        /// <summary>
        /// Get datasets by dataset name for an agent
        /// </summary>
        /// <param name="agentId">Agent ID</param>
        /// <param name="datasetName">Dataset name to search for</param>
        /// <returns>List of dataset entities with matching dataset name</returns>
        Task<List<DataSetTableEntity>> GetDataSetsByDatasetNameAsync(string agentId, string datasetName);

        /// <summary>
        /// Get dataset by dataset name and type for an agent
        /// </summary>
        /// <param name="agentId">Agent ID</param>
        /// <param name="datasetName">Dataset name to search for</param>
        /// <param name="datasetType">Dataset type</param>
        /// <returns>Dataset entity or null if not found</returns>
        Task<DataSetTableEntity?> GetDataSetByDatasetNameAndTypeAsync(string agentId, string datasetName, string datasetType);

        /// <summary>
        /// Check if dataset exists
        /// </summary>
        /// <param name="agentId">Agent ID</param>
        /// <param name="datasetId">Dataset ID</param>
        /// <returns>True if exists, false otherwise</returns>
        Task<bool> DataSetExistsAsync(string agentId, string datasetId);

        /// <summary>
        /// Delete a specific dataset
        /// </summary>
        /// <param name="agentId">Agent ID</param>
        /// <param name="datasetId">Dataset ID</param>
        /// <returns>True if deleted, false if not found</returns>
        Task<bool> DeleteDataSetAsync(string agentId, string datasetId);

        /// <summary>
        /// Delete all datasets for an agent
        /// </summary>
        /// <param name="agentId">Agent ID</param>
        /// <returns>Number of datasets deleted</returns>
        Task<int> DeleteAllDataSetsByAgentIdAsync(string agentId);

        /// <summary>
        /// Update dataset metadata without changing blob content
        /// </summary>
        /// <param name="agentId">Agent ID</param>
        /// <param name="datasetId">Dataset ID</param>
        /// <param name="updateAction">Action to perform updates on the entity</param>
        /// <returns>Updated entity or null if not found</returns>
        Task<DataSetTableEntity?> UpdateDataSetMetadataAsync(string agentId, string datasetId, Action<DataSetTableEntity> updateAction);
    }
}
