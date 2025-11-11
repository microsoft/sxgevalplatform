using Sxg.EvalPlatform.API.Storage.Entities;
using SxgEvalPlatformApi.Models.Dtos;

namespace SxgEvalPlatformApi.RequestHandlers
{
    public interface IMetricsConfigurationRequestHandler
    {
        Task<DefaultMetricsConfiguration> GetDefaultMetricsConfigurationAsync();
        Task<IList<MetricsConfigurationMetadataDto>> GetAllMetricsConfigurationsByAgentIdAndEnvironmentAsync(string agentId, string enviornmentName);
        Task<IList<SelectedMetricsConfiguration>?> GetMetricsConfigurationByConfigurationIdAsync(string configurationId);
        Task<bool> DeleteConfigurationAsync(string configurationId);
        Task<ConfigurationSaveResponseDto> CreateOrSaveConfigurationAsync(CreateConfigurationRequestDto createConfigDto); 

    }
}
