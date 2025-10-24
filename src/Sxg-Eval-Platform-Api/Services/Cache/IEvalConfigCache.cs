using SxgEvalPlatformApi.Models.Dtos;
using Sxg.EvalPlatform.API.Storage.Entities;

namespace SxgEvalPlatformApi.Services.Cache
{
    /// <summary>
    /// Specialized cache interface for EvalConfig (MetricsConfiguration) entities
    /// Provides optimized Redis operations with proper namespacing
    /// </summary>
    public interface IEvalConfigCache
    {
        /// <summary>
        /// Get configuration by ID
        /// </summary>
        /// <param name="configurationId">Configuration ID</param>
        /// <returns>Configuration or null if not found</returns>
        Task<MetricsConfigurationMetadataDto?> GetConfigurationAsync(string configurationId);

        /// <summary>
        /// Set configuration in cache
        /// </summary>
        /// <param name="configuration">Configuration to cache</param>
        /// <param name="expiry">Optional custom expiry time</param>
        /// <returns>True if successful</returns>
        Task<bool> SetConfigurationAsync(MetricsConfigurationMetadataDto configuration, TimeSpan? expiry = null);

        /// <summary>
        /// Get detailed configuration content (SelectedMetricsConfiguration) by ID
        /// </summary>
        /// <param name="configurationId">Configuration ID</param>
        /// <returns>Detailed configuration content or null if not found</returns>
        Task<IList<SelectedMetricsConfiguration>?> GetDetailedConfigurationAsync(string configurationId);

        /// <summary>
        /// Set detailed configuration content in cache
        /// </summary>
        /// <param name="configurationId">Configuration ID</param>
        /// <param name="detailedConfig">Detailed configuration content</param>
        /// <param name="expiry">Optional custom expiry time</param>
        /// <returns>True if successful</returns>
        Task<bool> SetDetailedConfigurationAsync(string configurationId, IList<SelectedMetricsConfiguration> detailedConfig, TimeSpan? expiry = null);

        /// <summary>
        /// Get all configurations for an agent and environment (cached list)
        /// </summary>
        /// <param name="agentId">Agent ID</param>
        /// <param name="environmentName">Environment name (optional)</param>
        /// <returns>List of configurations for the agent and environment</returns>
        Task<List<MetricsConfigurationMetadataDto>?> GetConfigurationsByAgentAsync(string agentId, string? environmentName = null);

        /// <summary>
        /// Set cached list of configurations for an agent and environment
        /// </summary>
        /// <param name="agentId">Agent ID</param>
        /// <param name="configurations">List of configurations</param>
        /// <param name="environmentName">Environment name (optional)</param>
        /// <param name="expiry">Optional custom expiry time</param>
        /// <returns>True if successful</returns>
        Task<bool> SetConfigurationsByAgentAsync(string agentId, List<MetricsConfigurationMetadataDto> configurations, string? environmentName = null, TimeSpan? expiry = null);

        /// <summary>
        /// Remove configuration from cache
        /// </summary>
        /// <param name="configurationId">Configuration ID to remove</param>
        /// <returns>True if removed</returns>
        Task<bool> RemoveConfigurationAsync(string configurationId);

        /// <summary>
        /// Invalidate all cached configurations for an agent and optionally for a specific environment
        /// This should be called when configurations are added/updated/deleted for an agent
        /// </summary>
        /// <param name="agentId">Agent ID</param>
        /// <param name="environmentName">Environment name (optional, if specified only that environment will be invalidated)</param>
        /// <returns>Number of keys removed</returns>
        Task<long> InvalidateAgentConfigurationsAsync(string agentId, string? environmentName = null);

        /// <summary>
        /// Check if configuration exists in cache
        /// </summary>
        /// <param name="configurationId">Configuration ID</param>
        /// <returns>True if exists in cache</returns>
        Task<bool> ConfigurationExistsAsync(string configurationId);
    }
}