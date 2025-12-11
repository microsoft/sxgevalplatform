using Sxg.EvalPlatform.API.Storage.TableEntities;
using SXG.EvalPlatform.Common;

namespace Sxg.EvalPlatform.API.Storage.Helpers
{
    /// <summary>
    /// Helper class for working with DataSetTableEntity and Azure Table Storage partitioning
    /// </summary>
    public static class DataSetTableEntityHelper
    {
        /// <summary>
        /// Creates a new DataSetTableEntity with proper partitioning keys set
        /// </summary>
        /// <param name="agentId">Agent ID (will be used as PartitionKey)</param>
        /// <param name="blobFilePath">Path to the blob file</param>
        /// <param name="containerName">Container name</param>
        /// <param name="datasetType">Dataset type (e.g., Synthetic, Golden)</param>
        /// <param name="datasetName">Dataset name</param>
        /// <param name="lastUpdatedBy">Who updated the dataset</param>
        /// <returns>DataSetTableEntity with keys properly set</returns>
        public static DataSetTableEntity CreateEntity(
            string agentId, 
            string blobFilePath, 
            string containerName, 
            string datasetType, 
            string datasetName, 
            string lastUpdatedBy = "system")
        {
            var entity = new DataSetTableEntity
            {
                AgentId = agentId, // Automatically sets PartitionKey
                BlobFilePath = blobFilePath,
                ContainerName = containerName,
                DatasetType = datasetType,
                DatasetName = datasetName,
                LastUpdatedBy = lastUpdatedBy,
                LastUpdatedOn = DateTime.UtcNow
            };
            
            // Keys are automatically set by constructor and property setters
            return entity;
        }

        /// <summary>
        /// Creates a new DataSetTableEntity with a specific dataset ID
        /// </summary>
        /// <param name="agentId">Agent ID (will be used as PartitionKey)</param>
        /// <param name="datasetId">Specific dataset ID to use</param>
        /// <param name="blobFilePath">Path to the blob file</param>
        /// <param name="containerName">Container name</param>
        /// <param name="datasetType">Dataset type (e.g., Synthetic, Golden)</param>
        /// <param name="datasetName">Dataset name</param>
        /// <param name="lastUpdatedBy">Who updated the dataset</param>
        /// <returns>DataSetTableEntity with keys properly set</returns>
        public static DataSetTableEntity CreateEntity(
            string agentId, 
            string datasetId, 
            string blobFilePath, 
            string containerName, 
            string datasetType, 
            string datasetName, 
            string lastUpdatedBy = "system")
        {
            var entity = new DataSetTableEntity
            {
                AgentId = agentId, // Automatically sets PartitionKey
                DatasetId = datasetId, // Automatically sets RowKey
                BlobFilePath = blobFilePath,
                ContainerName = containerName,
                DatasetType = datasetType,
                DatasetName = datasetName,
                LastUpdatedBy = lastUpdatedBy,
                LastUpdatedOn = DateTime.UtcNow
            };
            
            // Keys are automatically set by property setters
            return entity;
        }

        /// <summary>
        /// Gets the partition key for a specific agent (same as AgentId)
        /// </summary>
        /// <param name="agentId">Agent ID</param>
        /// <returns>Partition key string</returns>
        public static string GetPartitionKey(string agentId)
        {
            return agentId;
        }

        /// <summary>
        /// Gets the row key for a specific dataset ID (UUID)
        /// </summary>
        /// <param name="datasetId">Dataset ID (UUID)</param>
        /// <returns>Row key string (same as dataset ID)</returns>
        public static string GetRowKey(string datasetId)
        {
            return datasetId;
        }

        /// <summary>
        /// Generates a new UUID for use as a dataset ID and row key
        /// </summary>
        /// <returns>New UUID string</returns>
        public static string GenerateNewDatasetId()
        {
            return Guid.NewGuid().ToString();
        }

        /// <summary>
        /// Validates that the entity has proper keys set for Azure Table Storage
        /// </summary>
        /// <param name="entity">Entity to validate</param>
        /// <returns>True if valid, false otherwise</returns>
        public static bool ValidateKeys(DataSetTableEntity entity)
        {
            return !string.IsNullOrEmpty(entity.PartitionKey) 
                && !string.IsNullOrEmpty(entity.RowKey)
                && !string.IsNullOrEmpty(entity.DatasetId)
                && entity.PartitionKey == entity.AgentId
                && entity.RowKey == entity.DatasetId
                && IsValidGuid(entity.DatasetId);
        }

        /// <summary>
        /// Validates that a string is a valid GUID format
        /// </summary>
        /// <param name="guidString">String to validate</param>
        /// <returns>True if valid GUID format, false otherwise</returns>
        public static bool IsValidGuid(string guidString)
        {
            return Guid.TryParse(guidString, out _);
        }

        /// <summary>
        /// Builds filter strings for different query scenarios
        /// </summary>
        /// <param name="agentId">Agent ID</param>
        /// <param name="datasetType">Dataset type (optional)</param>
        /// <param name="datasetName">Dataset name (optional)</param>
        /// <returns>Filter string for Azure Table Storage query</returns>
        public static string BuildFilterString(string agentId, string? datasetType = null, string? datasetName = null)
        {
            var filter = $"PartitionKey eq '{agentId}'";
            
            if (!string.IsNullOrEmpty(datasetType))
            {
                filter += $" and DatasetType eq '{datasetType}'";
            }
            
            if (!string.IsNullOrEmpty(datasetName))
            {
                filter += $" and DatasetName eq '{datasetName}'";
            }
            
            return filter;
        }

        /// <summary>
        /// Creates a blob file path for a dataset
        /// </summary>
        /// <param name="agentId">Agent ID</param>
        /// <param name="datasetId">Dataset ID</param>
        /// <param name="datasetName">Dataset name</param>
        /// <returns>Blob file path</returns>
        public static string CreateBlobFilePath(string agentId, string datasetId, string datasetName)
        {
            return $"datasets/{agentId}/{datasetId}_{datasetName}.json";
        }

        /// <summary>
        /// Creates a container name for an agent (valid for Azure Blob Storage)
        /// </summary>
        /// <param name="agentId">Agent ID</param>
        /// <returns>Container name (valid for Azure Blob Storage)</returns>
        public static string CreateContainerName(string agentId)
        {
            // Azure container names must comply with Azure Blob Storage naming restrictions
            return CommonUtils.TrimAndRemoveSpaces(agentId);
        }

        /// <summary>
        /// Validates required entity properties
        /// </summary>
        /// <param name="entity">Entity to validate</param>
        /// <returns>List of validation errors (empty if valid)</returns>
        public static List<string> ValidateEntity(DataSetTableEntity entity)
        {
            var errors = new List<string>();

            if (string.IsNullOrEmpty(entity.AgentId))
                errors.Add("AgentId is required");

            if (string.IsNullOrEmpty(entity.DatasetId))
                errors.Add("DatasetId is required");

            if (string.IsNullOrEmpty(entity.BlobFilePath))
                errors.Add("BlobFilePath is required");

            if (string.IsNullOrEmpty(entity.ContainerName))
                errors.Add("ContainerName is required");

            if (string.IsNullOrEmpty(entity.DatasetType))
                errors.Add("DatasetType is required");

            if (string.IsNullOrEmpty(entity.DatasetName))
                errors.Add("DatasetName is required");

            if (!ValidateKeys(entity))
                errors.Add("Invalid PartitionKey/RowKey configuration");

            return errors;
        }

        /// <summary>
        /// Creates a copy of an entity with updated metadata
        /// </summary>
        /// <param name="original">Original entity</param>
        /// <param name="lastUpdatedBy">Who is updating the entity</param>
        /// <returns>Updated entity copy</returns>
        public static DataSetTableEntity CreateUpdatedCopy(DataSetTableEntity original, string lastUpdatedBy)
        {
            return new DataSetTableEntity
            {
                AgentId = original.AgentId,
                DatasetId = original.DatasetId,
                BlobFilePath = original.BlobFilePath,
                ContainerName = original.ContainerName,
                DatasetType = original.DatasetType,
                DatasetName = original.DatasetName,
                LastUpdatedBy = lastUpdatedBy,
                LastUpdatedOn = DateTime.UtcNow,
                // Preserve Azure Table Storage metadata
                PartitionKey = original.PartitionKey,
                RowKey = original.RowKey,
                Timestamp = original.Timestamp,
                ETag = original.ETag
            };
        }
    }
}