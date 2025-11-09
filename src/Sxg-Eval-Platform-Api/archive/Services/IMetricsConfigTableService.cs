//using SxgEvalPlatformApi.Models.Entities;

//namespace SxgEvalPlatformApi.Services
//{
//    /// <summary>
//    /// Interface for MetricsConfiguration table operations
//    /// </summary>
//    public interface IMetricsConfigTableService
//    {
//        /// <summary>
//        /// Save or update metrics configuration in Azure Table
//        /// </summary>
//        /// <param name="entity">Metrics configuration entity</param>
//        /// <returns>The saved entity</returns>
//        Task<MetricsConfigurationEntity> SaveMetricsConfigurationAsync(MetricsConfigurationEntity entity);

//        /// <summary>
//        /// Get metrics configuration by Agent ID and Configuration Name
//        /// </summary>
//        /// <param name="agentId">Agent ID</param>
//        /// <param name="configurationName">Configuration name</param>
//        /// <returns>Metrics configuration entity or null if not found</returns>
//        Task<MetricsConfigurationEntity?> GetMetricsConfigurationAsync(string agentId, string configurationName);

//        /// <summary>
//        /// Get metrics configuration by Agent ID, Configuration Name, and Environment
//        /// </summary>
//        /// <param name="agentId">Agent ID</param>
//        /// <param name="configurationName">Configuration name</param>
//        /// <param name="environmentName">Environment name</param>
//        /// <returns>Metrics configuration entity or null if not found</returns>
//        Task<MetricsConfigurationEntity?> GetMetricsConfigurationAsync(string agentId, string configurationName, string environmentName);

//        /// <summary>
//        /// Get all metrics configurations for an agent
//        /// </summary>
//        /// <param name="agentId">Agent ID</param>
//        /// <returns>List of metrics configuration entities</returns>
//        Task<List<MetricsConfigurationEntity>> GetAllMetricsConfigurationsByAgentIdAsync(string agentId);

//        /// <summary>
//        /// Get all metrics configurations for an agent in a specific environment
//        /// </summary>
//        /// <param name="agentId">Agent ID</param>
//        /// <param name="environmentName">Environment name</param>
//        /// <returns>List of metrics configuration entities</returns>
//        Task<List<MetricsConfigurationEntity>> GetAllMetricsConfigurationsByAgentIdAndEnvironmentAsync(string agentId, string environmentName);

//        /// <summary>
//        /// Check if metrics configuration exists
//        /// </summary>
//        /// <param name="agentId">Agent ID</param>
//        /// <param name="configurationName">Configuration name</param>
//        /// <returns>True if exists, false otherwise</returns>
//        Task<bool> MetricsConfigurationExistsAsync(string agentId, string configurationName);

//        /// <summary>
//        /// Check if metrics configuration exists for specific environment
//        /// </summary>
//        /// <param name="agentId">Agent ID</param>
//        /// <param name="configurationName">Configuration name</param>
//        /// <param name="environmentName">Environment name</param>
//        /// <returns>True if exists, false otherwise</returns>
//        Task<bool> MetricsConfigurationExistsAsync(string agentId, string configurationName, string environmentName);

//        /// <summary>
//        /// Delete metrics configuration
//        /// </summary>
//        /// <param name="agentId">Agent ID</param>
//        /// <param name="configurationName">Configuration name</param>
//        /// <param name="environmentName">Environment name</param>
//        /// <returns>True if deleted, false if not found</returns>
//        Task<bool> DeleteMetricsConfigurationAsync(string agentId, string configurationName, string environmentName);
//    }
//}
