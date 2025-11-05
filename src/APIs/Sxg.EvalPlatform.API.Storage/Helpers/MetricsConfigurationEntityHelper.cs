using Sxg.EvalPlatform.API.Storage.TableEntities;

namespace Sxg.EvalPlatform.API.Storage.Helpers
{
    /// <summary>
    /// Helper class for working with MetricsConfigurationEntity and Azure Table Storage partitioning
    /// </summary>
    public static class MetricsConfigurationEntityHelper
    {
        /// <summary>
        /// Creates a new MetricsConfigurationEntity with proper partitioning keys set
        /// </summary>
        /// <param name="agentId">Agent ID (will be used as PartitionKey)</param>
        /// <param name="configurationName">Configuration name</param>
        /// <param name="environmentName">Environment name</param>
        /// <returns>MetricsConfigurationEntity with keys properly set</returns>
        public static MetricsConfigurationTableEntity CreateEntity(string agentId, string configurationName, string environmentName)
        {
            var entity = new MetricsConfigurationTableEntity
            {
                AgentId = agentId, // Automatically sets PartitionKey
                ConfigurationName = configurationName,
                EnvironmentName = environmentName,
                LastUpdatedOn = DateTime.UtcNow
            };
            
            // Keys are automatically set by constructor and property setters
            // No need to call SetKeys() anymore, but keeping for backward compatibility
            return entity;
        }

        /// <summary>
        /// Creates a new MetricsConfigurationEntity with a specific configuration ID
        /// </summary>
        /// <param name="agentId">Agent ID (will be used as PartitionKey)</param>
        /// <param name="configurationName">Configuration name</param>
        /// <param name="environmentName">Environment name</param>
        /// <param name="configurationId">Specific configuration ID to use</param>
        /// <returns>MetricsConfigurationEntity with keys properly set</returns>
        public static MetricsConfigurationTableEntity CreateEntity(string agentId, string configurationName, string environmentName, string configurationId)
        {
            var entity = new MetricsConfigurationTableEntity
            {
                AgentId = agentId, // Automatically sets PartitionKey
                ConfigurationName = configurationName,
                EnvironmentName = environmentName,
                ConfigurationId = configurationId, // Automatically sets RowKey
                LastUpdatedOn = DateTime.UtcNow
            };
            
            // Keys are automatically set by property setters
            // No need to call SetKeys() anymore
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
        /// Gets the row key for a specific configuration ID (UUID)
        /// </summary>
        /// <param name="configurationId">Configuration ID (UUID)</param>
        /// <returns>Row key string (same as configuration ID)</returns>
        public static string GetRowKey(string configurationId)
        {
            return configurationId;
        }

        /// <summary>
        /// Generates a new UUID for use as a configuration ID and row key
        /// </summary>
        /// <returns>New UUID string</returns>
        public static string GenerateNewConfigurationId()
        {
            return Guid.NewGuid().ToString();
        }

        /// <summary>
        /// Validates that the entity has proper keys set for Azure Table Storage
        /// </summary>
        /// <param name="entity">Entity to validate</param>
        /// <returns>True if valid, false otherwise</returns>
        public static bool ValidateKeys(MetricsConfigurationTableEntity entity)
        {
            return !string.IsNullOrEmpty(entity.PartitionKey) 
                && !string.IsNullOrEmpty(entity.RowKey)
                && !string.IsNullOrEmpty(entity.ConfigurationId)
                && entity.PartitionKey == entity.AgentId
                && entity.RowKey == entity.ConfigurationId
                && IsValidGuid(entity.ConfigurationId);
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
        /// Finds entities by configuration name and environment within an agent's partition
        /// This generates the filter string for querying
        /// </summary>
        /// <param name="agentId">Agent ID</param>
        /// <param name="configurationName">Configuration name (optional)</param>
        /// <param name="environmentName">Environment name (optional)</param>
        /// <returns>Filter string for Azure Table Storage query</returns>
        public static string BuildFilterString(string agentId, string? configurationName = null, string? environmentName = null)
        {
            var filter = $"PartitionKey eq '{agentId}'";
            
            if (!string.IsNullOrEmpty(configurationName))
            {
                filter += $" and ConfigurationName eq '{configurationName}'";
            }
            
            if (!string.IsNullOrEmpty(environmentName))
            {
                filter += $" and EnvironmentName eq '{environmentName}'";
            }
            
            return filter;
        }
    }
}