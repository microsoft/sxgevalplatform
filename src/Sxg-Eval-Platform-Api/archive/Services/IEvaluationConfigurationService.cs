//using SxgEvalPlatformApi.Models;
//using SxgEvalPlatformApi.Models.Dtos;

//namespace SxgEvalPlatformApi.Services;

///// <summary>
///// Interface for evaluation configuration service operations
///// </summary>
//public interface IEvaluationConfigurationService
//{
//    /// <summary>
//    /// Create or save an evaluation configuration
//    /// </summary>
//    /// <param name="createConfigDto">Configuration creation data</param>
//    /// <returns>Configuration save response</returns>
//    Task<ConfigurationSaveResponseDto> CreateOrSaveConfigurationAsync(CreateMetricsConfigurationDto createConfigDto);
    
//    /// <summary>
//    /// Get specific configuration for an agent by configuration name
//    /// </summary>
//    /// <param name="agentId">Agent ID</param>
//    /// <param name="configurationName">Configuration name</param>
//    /// <returns>Configuration for the agent</returns>
//    Task<CreateMetricsConfigurationDto?> GetConfigurationsByAgentIdAsync(string agentId, string configurationName);
    
//    /// <summary>
//    /// Get all configurations for a specific agent
//    /// </summary>
//    /// <param name="agentId">Agent ID</param>
//    /// <returns>List of configurations for the agent</returns>
//    Task<List<CreateMetricsConfigurationDto>> GetConfigurationsByAgentIdAsync(string agentId);
    
//    /// <summary>
//    /// Get the default platform configuration
//    /// </summary>
//    /// <returns>Default platform configuration</returns>
//    Task<CreateMetricsConfigurationDto?> GetConfigurationsByAgentIdAsync();
    
//    /// <summary>
//    /// Get single metrics configuration for a specific agent (returns the first/latest CreateMetricsConfigurationDto object)
//    /// </summary>
//    /// <param name="agentId">Agent ID</param>
//    /// <returns>Single CreateMetricsConfigurationDto object</returns>
//    Task<CreateMetricsConfigurationDto?> GetMetricsConfigurationByAgentIdAsync(string agentId);
    
//    /// <summary>
//    /// Get configuration by ID
//    /// </summary>
//    /// <param name="configId">Configuration ID</param>
//    /// <returns>Configuration details</returns>
//    Task<EvaluationConfigurationDto?> GetConfigurationByIdAsync(string configId);
    
//    /// <summary>
//    /// Check if configuration exists by name for an agent
//    /// </summary>
//    /// <param name="agentId">Agent ID</param>
//    /// <param name="configName">Configuration name</param>
//    /// <returns>True if exists, false otherwise</returns>
//    Task<bool> ConfigurationExistsAsync(string agentId, string configName);
    
//    /// <summary>
//    /// Get the default metric configuration
//    /// </summary>
//    /// <returns>Default configuration as JSON string</returns>
//    Task<string?> GetDefaultConfigurationAsync();
//}