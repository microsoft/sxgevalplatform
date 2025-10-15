using Sxg.EvalPlatform.API.Storage.Entities;
using SxgEvalPlatformApi.Models.Dtos;

namespace SxgEvalPlatformApi.RequestHandlers
{
    public interface IMetricsConfigurationRequestHandler
    {
        Task<MetricsConfiguration> GetDefaultMetricsConfigurationAsync();
        Task<IList<MetricsConfigurationMetadataDto>> GetAllMetricsConfigurationsByAgentIdAndEnvironmentAsync(string agentId, string enviornmentName);
        Task<IList<SelectedMetricsConfiguration>> GetMetricsConfigurationByConfigurationIdAsync(string configurationId);

        Task<ConfigurationSaveResponseDto> CreateOrSaveConfigurationAsync(CreateMetricsConfigurationDto createConfigDto); 
    }
}
