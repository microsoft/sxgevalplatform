using Azure;
using Azure.Data.Tables;
using SXG.EvalPlatform.Common;

namespace Sxg.EvalPlatform.API.Storage.TableEntities
{
    /// <summary>
    /// Constants for evaluation run status values
    /// </summary>
    //public static class EvalRunStatusConstants
    //{
    //    public const string Queued = "Queued";
    //    public const string Running = "Running";
    //    public const string Completed = "Completed";
    //    public const string Failed = "Failed";
    //}

    /// <summary>
    /// Entity class for storing evaluation run data in Azure Table Storage
    /// Uses AgentId as PartitionKey and EvalRunId as RowKey
    /// </summary>
    public class EvalRunTableEntity : ITableEntity
    {
        private string _agentId = string.Empty;
        private string _evalRunId = string.Empty;

        /// <summary>
        /// Constructor that initializes EvalRunId with a new UUID
        /// </summary>
        public EvalRunTableEntity()
        {
            var guid = Guid.NewGuid();
            EvalRunId = guid;
            _evalRunId = guid.ToString();
            RowKey = _evalRunId;
            LastUpdatedOn = DateTime.UtcNow;
            StartedDatetime = DateTime.UtcNow;
            Status = CommonConstants.EvalRunStatus.RequestSubmitted;
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
        /// Evaluation Run ID as GUID for business logic
        /// </summary>
        public Guid EvalRunId { get; set; } = Guid.Empty;

        // ITableEntity properties
        public string PartitionKey { get; set; } = string.Empty;
        public string RowKey { get; set; } = string.Empty;
        public DateTimeOffset? Timestamp { get; set; }
        public ETag ETag { get; set; }

        // Business properties
        public string MetricsConfigurationId { get; set; } = string.Empty;
        public string DataSetId { get; set; } = string.Empty;
        public string Status { get; set; } = CommonConstants.EvalRunStatus.RequestSubmitted;
        public string? LastUpdatedBy { get; set; }
        public DateTime? LastUpdatedOn { get; set; }
        public DateTime? StartedDatetime { get; set; }
        public DateTime? CompletedDatetime { get; set; }
        public string? BlobFilePath { get; set; }
        public string? ContainerName { get; set; }
        public string Type { get; set; } = string.Empty;
        public string EnvironmentId { get; set; } = string.Empty;
        public string AgentSchemaName { get; set; } = string.Empty;
        public string EvalRunName { get; set; } = string.Empty;
    }
}