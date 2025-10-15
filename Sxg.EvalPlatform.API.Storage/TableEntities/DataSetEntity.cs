using Azure;
using Azure.Data.Tables;

namespace Sxg.EvalPlatform.API.Storage.TableEntities
{
    /// <summary>
    /// Entity class for storing dataset metadata in Azure Table Storage
    /// Uses AgentId as PartitionKey and DatasetId as RowKey
    /// </summary>
    public class DataSetTableEntity : ITableEntity
    {
        private string _agentId = string.Empty;
        private string _datasetId = string.Empty;

        /// <summary>
        /// Constructor that initializes DatasetId with a new UUID
        /// </summary>
        public DataSetTableEntity()
        {
            DatasetId = Guid.NewGuid().ToString();
            LastUpdatedOn = DateTime.UtcNow;
        }

        /// <summary>
        /// Agent ID - automatically updates PartitionKey when set
        /// </summary>
        public string AgentId
        {
            get => _agentId;
            set
            {
                _agentId = value;
                PartitionKey = value; // Automatically update PartitionKey
            }
        }

        /// <summary>
        /// Dataset ID (UUID) - automatically updates RowKey when set
        /// </summary>
        public string DatasetId
        {
            get => _datasetId;
            set
            {
                _datasetId = value;
                RowKey = value; // Automatically update RowKey
            }
        }

        /// <summary>
        /// Path to the blob file in Azure Blob Storage
        /// </summary>
        public string BlobFilePath { get; set; } = string.Empty;

        /// <summary>
        /// Container name in Azure Blob Storage
        /// </summary>
        public string ContainerName { get; set; } = string.Empty;

        /// <summary>
        /// Type of dataset (e.g., Synthetic, Golden)
        /// </summary>
        public string DatasetType { get; set; } = string.Empty;

        /// <summary>
        /// Original filename of the dataset
        /// </summary>
        public string FileName { get; set; } = string.Empty;

        /// <summary>
        /// When the dataset was last updated
        /// </summary>
        public DateTime LastUpdatedOn { get; set; }

        /// <summary>
        /// Who last updated the dataset
        /// </summary>
        public string LastUpdatedBy { get; set; } = string.Empty;

        // ITableEntity implementation
        // Using AgentId as PartitionKey for efficient agent-based queries
        public string PartitionKey { get; set; } = string.Empty;

        // Using DatasetId as RowKey for guaranteed uniqueness
        public string RowKey { get; set; } = string.Empty;

        public DateTimeOffset? Timestamp { get; set; }

        public ETag ETag { get; set; }

        /// <summary>
        /// Sets the partition key to AgentId for optimal performance
        /// </summary>
        public void SetPartitionKey()
        {
            PartitionKey = AgentId;
        }

        /// <summary>
        /// Sets the row key to DatasetId for guaranteed uniqueness
        /// </summary>
        public void SetRowKey()
        {
            RowKey = DatasetId;
        }

        /// <summary>
        /// Sets both partition key and row key for Azure Table Storage
        /// </summary>
        public void SetKeys()
        {
            SetPartitionKey();
            SetRowKey();
        }

        /// <summary>
        /// Sets the dataset ID and updates the row key
        /// </summary>
        /// <param name="datasetId">The dataset ID to set</param>
        public void SetDatasetId(string datasetId)
        {
            DatasetId = datasetId;
        }

        /// <summary>
        /// Generates a new dataset ID and sets it as the row key
        /// </summary>
        public void GenerateNewDatasetId()
        {
            SetDatasetId(Guid.NewGuid().ToString());
        }
    }
}