using Azure;
using Azure.Data.Tables;
using SXG.EvalPlatform.Common;

namespace Sxg.EvalPlatform.API.Storage.TableEntities
{
    /// <summary>
    /// Entity class for storing Metrics configuration in Azure Table Storage
    /// Uses AgentId as PartitionKey for efficient querying by agent
    /// Uses UUID as RowKey for guaranteed uniqueness
    /// </summary>
    public class MetricsConfigurationTableEntity : ITableEntity, IAuditableEntity
    {
        private string _agentId = string.Empty;

        /// <summary>
        /// Constructor that initializes ConfigurationId with a new UUID
        /// </summary>
        public MetricsConfigurationTableEntity()
        {
            ConfigurationId = Guid.NewGuid().ToString(); // This automatically sets RowKey
            var now = DateTime.UtcNow;
            CreatedOn = now;
            LastUpdatedOn = now;
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

        public string ConfigurationName { get; set; } = string.Empty;

        public string EnvironmentName { get; set; } = string.Empty;

        public string? Description { get; set; }

        //public IList<MetricsConfiguration> MetricsConfiguration { get; set; } = new List<MetricsConfiguration>();

        public string? CreatedBy { get; set; }

        public DateTime? CreatedOn { get; set; }

        public string? LastUpdatedBy { get; set; }

        public DateTime? LastUpdatedOn { get; set; }

        private string _configurationId = string.Empty;

        public string ContainerName { get; set; } = string.Empty;

        public string BlobFilePath { get; set; } = string.Empty;

        /// <summary>
        /// Unique identifier for this configuration entity - automatically updates RowKey when set
        /// </summary>
        public string ConfigurationId 
        { 
            get => _configurationId;
            set 
            { 
                _configurationId = value;
                RowKey = value; // Automatically update RowKey
            }
        }

        //// Since IList<MetricsConfiguration> cannot be directly stored in Azure Table Storage,
        //// we need to serialize it to JSON string
        //public string MetricsConfigurationJson 
        //{ 
        //    get => JsonSerializer.Serialize(MetricsConfiguration, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        //    set => MetricsConfiguration = string.IsNullOrEmpty(value) 
        //        ? new List<MetricsConfiguration>() 
        //        : JsonSerializer.Deserialize<List<MetricsConfiguration>>(value, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }) ?? new List<MetricsConfiguration>();
        //}

        // ITableEntity implementation
        // Using AgentId as PartitionKey for efficient agent-based queries
        public string PartitionKey { get; set; } = string.Empty;

        // Using UUID as RowKey for guaranteed uniqueness
        public string RowKey { get; set; } = string.Empty;

        public DateTimeOffset? Timestamp { get; set; }

        public ETag ETag { get; set; }

        /// <summary>
        /// Sets the partition key to AgentId for optimal performance
        /// Note: This is now automatic when AgentId is set, but kept for backward compatibility
        /// </summary>
        public void SetPartitionKey()
        {
            PartitionKey = AgentId;
        }

        /// <summary>
        /// Sets the row key to the ConfigurationId UUID
        /// Note: RowKey is automatically set in constructor and when ConfigurationId changes
        /// </summary>
        public void SetRowKey()
        {
            RowKey = ConfigurationId;
        }

        /// <summary>
        /// Sets both partition key and row key for Azure Table Storage
        /// Note: Most key setting is now automatic, but kept for backward compatibility
        /// </summary>
        public void SetKeys()
        {
            SetPartitionKey();
            SetRowKey();
        }

        /// <summary>
        /// Sets the configuration ID and updates the row key
        /// </summary>
        /// <param name="configurationId">The configuration ID to set</param>
        public void SetConfigurationId(string configurationId)
        {
            ConfigurationId = configurationId;
            RowKey = configurationId;
        }

        /// <summary>
        /// Generates a new configuration ID and sets it as the row key
        /// </summary>
        public void GenerateNewConfigurationId()
        {
            SetConfigurationId(Guid.NewGuid().ToString());
        }
    }
}
