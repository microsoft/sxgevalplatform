using Sxg.EvalPlatform.API.Storage.TableEntities;

namespace Sxg.EvalPlatform.API.Storage.Services
{
    /// <summary>
    /// Interface for MetricsConfiguration table operations
    /// </summary>
    public interface IMetricsConfigTableService
    {
        /// <summary>
        /// Save or update Metrics configuration in Azure Table
        /// </summary>
        /// <param name="entity">Metrics configuration entity</param>
        /// <param name="auditUser">User performing the operation (for audit logging)</param>
        /// <returns>The saved entity</returns>
        Task<MetricsConfigurationTableEntity> SaveMetricsConfigurationAsync(MetricsConfigurationTableEntity entity, string? auditUser = null);

        //Task<MetricsConfigurationTableEntity> GetDefaultMetricsConfigurationAsync();

        /// <summary>
        /// Get Metrics configuration by Agent ID and Configuration Name
        /// </summary>
        /// <param name="agentId">Agent ID</param>
        /// <param name="configurationName">Configuration name</param>
        /// <returns>Metrics configuration entity or null if not found</returns>
        //Task<MetricsConfigurationTableEntity?> GetMetricsConfigurationAsync(string agentId, string configurationName);

        /// <summary>
        /// Get Metrics configuration by Agent ID, Configuration Name, and Environment
        /// </summary>
        /// <param name="agentId">Agent ID</param>
        /// <param name="configurationName">Configuration name</param>
        /// <param name="environmentName">Environment name</param>
        /// <returns>Metrics configuration entity or null if not found</returns>
        //Task<MetricsConfigurationTableEntity?> GetMetricsConfigurationAsync(string agentId, string configurationName, string environmentName);

        /// <summary>
        /// Get all Metrics configurations for an agent
        /// </summary>
        /// <param name="agentId">Agent ID</param>
        /// <returns>List of Metrics configuration entities</returns>
        //Task<List<MetricsConfigurationTableEntity>> GetAllMetricsConfigurationsByAgentIdAsync(string agentId);

        /// <summary>
        /// Get all Metrics configurations for an agent in a specific environment
        /// </summary>
        /// <param name="agentId">Agent ID</param>
        /// <param name="environmentName">Environment name</param>
        /// <returns>List of Metrics configuration entities</returns>
        Task<IList<MetricsConfigurationTableEntity>> GetAllMetricsConfigurations(string agentId, string environmentName = "");
        Task<IList<MetricsConfigurationTableEntity>> GetAllMetricsConfigurations(string agentId, string configurationName, string environmentName);

        /// <summary>
        /// Check if Metrics configuration exists
        /// </summary>
        /// <param name="agentId">Agent ID</param>
        /// <param name="configurationName">Configuration name</param>
        /// <returns>True if exists, false otherwise</returns>
        //Task<bool> MetricsConfigurationExistsAsync(string agentId, string configurationName);

        /// <summary>
        /// Check if Metrics configuration exists for specific environment
        /// </summary>
        /// <param name="agentId">Agent ID</param>
        /// <param name="configurationName">Configuration name</param>
        /// <param name="environmentName">Environment name</param>
        /// <returns>True if exists, false otherwise</returns>
        //Task<bool> MetricsConfigurationExistsAsync(string agentId, string configurationName, string environmentName);

        /// <summary>
        /// Get Metrics configuration by Agent ID and Configuration ID (UUID)
        /// </summary>
        /// <param name="agentId">Agent ID</param>
        /// <param name="configurationId">Configuration ID (UUID)</param>
        /// <returns>Metrics configuration entity or null if not found</returns>
        Task<MetricsConfigurationTableEntity?> GetMetricsConfigurationByConfigurationIdAsync(string configurationId);

        /// <summary>
        /// Delete Metrics configuration by Agent ID and Configuration ID
        /// </summary>
        /// <param name="agentId">Agent ID</param>
        /// <param name="configurationId">Configuration ID (UUID)</param>
        /// <param name="auditUser">User performing the operation (for audit logging)</param>
        /// <returns>True if deleted, false if not found</returns>
        Task<bool> DeleteMetricsConfigurationByIdAsync(string agentId, string configurationId, string? auditUser = null);

        /// <summary>
        /// Delete Metrics configuration
        /// </summary>
        /// <param name="agentId">Agent ID</param>
        /// <param name="configurationName">Configuration name</param>
        /// <param name="environmentName">Environment name</param>
        /// <returns>True if deleted, false if not found</returns>
        Task<bool> DeleteMetricsConfigurationAsync(string agentId, string configurationName, string environmentName);
        
    }
}
