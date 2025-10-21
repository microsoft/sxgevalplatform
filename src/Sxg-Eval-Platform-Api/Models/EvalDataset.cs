using System.ComponentModel.DataAnnotations;
using Azure;
using Azure.Data.Tables;
using SxgEvalPlatformApi.Models.Dtos;

namespace SxgEvalPlatformApi.Models
{
    /// <summary>
    /// Represents a single dataset record for evaluation
    /// </summary>
    public class EvalDataset
    {
        public string Prompt { get; set; } = string.Empty;
        
        public string GroundTruth { get; set; } = string.Empty;
        
        public string ActualResponse { get; set; } = string.Empty;
        
        public string ExpectedResponse { get; set; } = string.Empty;
    }

    /// <summary>
    /// DTO for saving datasets
    /// </summary>
    public class SaveDatasetDto
    {
        [Required]
        public string AgentId { get; set; } = string.Empty;

        [Required]
        [RegularExpression("^(Synthetic|Golden)$", ErrorMessage = "DatasetType must be either 'Synthetic' or 'Golden'")]
        public string DatasetType { get; set; } = string.Empty;

        [Required]
        [StringLength(100, MinimumLength = 1)]
        public string DatasetName { get; set; } = string.Empty;

        [Required]
        [MinLength(1)]
        public List<EvalDataset> DatasetRecords { get; set; } = new();

        [Required]
        public UserMetadataDto UserMetadata { get; set; } = new();
    }

    /// <summary>
    /// DTO for updating datasets
    /// </summary>
    public class UpdateDatasetDto
    {
        [Required]
        [MinLength(1)]
        public List<EvalDataset> DatasetRecords { get; set; } = new();

        [Required]
        public UserMetadataDto UserMetadata { get; set; } = new();
    }

    /// <summary>
    /// Dataset types constants
    /// </summary>
    public static class DatasetTypes
    {
        public const string Synthetic = "Synthetic";
        public const string Golden = "Golden";
    }

    /// <summary>
    /// Response DTO for dataset save operation
    /// </summary>
    public class DatasetSaveResponseDto
    {
        public string DatasetId { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string CreatedBy { get; set; } = string.Empty;
        public DateTime CreatedOn { get; set; }
        public string LastUpdatedBy { get; set; } = string.Empty;
        public DateTime LastUpdatedOn { get; set; }
    }

    /// <summary>
    /// Dataset metadata entity for Azure Table Storage
    /// </summary>
    public class DatasetMetadataEntity : ITableEntity
    {
        /// <summary>
        /// Dataset ID - GUID generated automatically
        /// </summary>
        public string DatasetId { get; set; } = string.Empty;

        /// <summary>
        /// Last updated timestamp
        /// </summary>
        public DateTime LastUpdatedOn { get; set; }

        /// <summary>
        /// Agent ID
        /// </summary>
        public string AgentId { get; set; } = string.Empty;

        /// <summary>
        /// Container name where blob is stored
        /// </summary>
        public string ContainerName { get; set; } = string.Empty;

        /// <summary>
        /// Blob file path including folder and file name
        /// </summary>
        public string BlobFilePath { get; set; } = string.Empty;

        /// <summary>
        /// Dataset type: Synthetic or Golden
        /// </summary>
        public string DatasetType { get; set; } = string.Empty;

        /// <summary>
        /// Dataset name
        /// </summary>
        public string DatasetName { get; set; } = string.Empty;

        /// <summary>
        /// Number of records in the dataset
        /// </summary>
        public int RecordCount { get; set; }

        // ITableEntity implementation
        public string PartitionKey { get; set; } = string.Empty;
        public string RowKey { get; set; } = string.Empty;
        public DateTimeOffset? Timestamp { get; set; }
        public ETag ETag { get; set; }
    }

    /// <summary>
    /// DTO for dataset metadata list response
    /// </summary>
    public class DatasetMetadataDto
    {
        public string DatasetId { get; set; } = string.Empty;
        public string AgentId { get; set; } = string.Empty;
        public string DatasetType { get; set; } = string.Empty;
        public string DatasetName { get; set; } = string.Empty;
        public int RecordCount { get; set; }
        public string CreatedBy { get; set; } = string.Empty;
        public DateTime CreatedOn { get; set; }
        public string LastUpdatedBy { get; set; } = string.Empty;
        public DateTime LastUpdatedOn { get; set; }
    }

    /// <summary>
    /// Response DTO for dataset list by agent
    /// </summary>
    public class DatasetListResponseDto
    {
        public string AgentId { get; set; } = string.Empty;
        public List<DatasetMetadataDto> Datasets { get; set; } = new();
    }
}

