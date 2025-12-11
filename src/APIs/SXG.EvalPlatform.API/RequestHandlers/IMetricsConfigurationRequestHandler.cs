using Sxg.EvalPlatform.API.Storage.Entities;
using SxgEvalPlatformApi.Models.Dtos;

namespace SxgEvalPlatformApi.RequestHandlers
{
    public interface IMetricsConfigurationRequestHandler
    {
        Task<DefaultMetricsConfiguration> GetDefaultMetricsConfigurationAsync();
        Task<IList<MetricsConfigurationMetadataDto>> GetAllMetricsConfigurationsByAgentIdAndEnvironmentAsync(string agentId, string environmentName);
        Task<IList<SelectedMetricsConfiguration>?> GetMetricsConfigurationByConfigurationIdAsync(string configurationId);
        Task<bool> DeleteConfigurationAsync(string configurationId);
        
/// <summary>
        /// Create a new metrics configuration or update if exists based on AgentId, ConfigurationName, and EnvironmentName
        /// </summary>
     Task<ConfigurationSaveResponseDto> CreateConfigurationAsync(CreateConfigurationRequestDto createConfigDto);
        
        /// <summary>
        /// Update an existing metrics configuration by ConfigurationId
        /// </summary>
        Task<ConfigurationSaveResponseDto> UpdateConfigurationAsync(string configurationId, CreateConfigurationRequestDto updateConfigDto);
        
        /// <summary>
     /// Legacy method for backward compatibility - delegates to Create or Update based on DTO type
        /// </summary>
    [Obsolete("Use CreateConfigurationAsync or UpdateConfigurationAsync instead")]
  Task<ConfigurationSaveResponseDto> CreateOrSaveConfigurationAsync(CreateConfigurationRequestDto createConfigDto);
    }
}
