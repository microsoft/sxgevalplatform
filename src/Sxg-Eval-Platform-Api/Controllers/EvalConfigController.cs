using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Logging;
using Sxg.EvalPlatform.API.Storage.Entities;
using SxgEvalPlatformApi.Models.Dtos;
using SxgEvalPlatformApi.RequestHandlers;

namespace SxgEvalPlatformApi.Controllers
{
    [Route("api/v1/eval")]
    public class EvalConfigController : BaseController
    {
        //private readonly IEvaluationConfigurationService _configurationService;
        private readonly IMetricsConfigurationRequestHandler _metricsConfigurationRequestHandler; 
        private readonly IConfiguration _configuration;

        public EvalConfigController(
            IMetricsConfigurationRequestHandler metricsConfigurationRequestHandler,
            IConfiguration configuration,
            ILogger<EvalConfigController> logger)
            : base(logger)
        {
            _metricsConfigurationRequestHandler = metricsConfigurationRequestHandler;
            _configuration = configuration;

            // Log controller initialization for debugging
            _logger.LogInformation("EvalConfigController initialized");
        }

        #region GET Methods

        [HttpGet("defaultconfiguration")]
        [ProducesResponseType(typeof(List<MetricsConfigurationMetadataDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<MetricsConfiguration>> GetDefaultMetricsConfiguration()
        {
            try
            {
                _logger.LogInformation("Request to retrieve default metrics configuration");

                return await _metricsConfigurationRequestHandler.GetDefaultMetricsConfigurationAsync();

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while retrieving default metrics configuration");
                return CreateErrorResponse<MetricsConfiguration>(
                    $"Failed to retrieve default metrics configuration", 500);
            }
        }

        /// <summary>
        /// Retrieve configuration by metrics configuration ID
        /// </summary>
        /// <param name="configurationId">Unique ID of the metrics configuration</param>
        /// <returns>Configuration with the specified ID</returns>
        /// <response code="200">Configuration retrieved successfully</response>
        /// <response code="404">Configuration not found</response>
        /// <response code="500">Internal server error</response>
        [HttpGet("configurations/{configurationId}")]
        [ProducesResponseType(typeof(MetricsConfigurationMetadataDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<IList<SelectedMetricsConfiguration>>> GetConfigurationsByMetricsConfigurationId(string configurationId)
        {
            try
            {
                _logger.LogInformation("Request to retrieve all configurations for agent: {metricsConfigurationId}", configurationId);

                var configIdValidation = ValidateConfigurationId(configurationId);
                if (configIdValidation != null)
                {
                    _logger.LogWarning("Invalid or missing configuration ID");
                    return configIdValidation;
                }

                var configurations = await _metricsConfigurationRequestHandler.GetMetricsConfigurationByConfigurationIdAsync(configurationId);


                if (!configurations.Any())
                {
                    _logger.LogInformation($"No configurations found for ConfigurationId: {configurationId}");
                    return NotFound($"No configurations found for ConfigurationId: {configurationId}");
                }

                _logger.LogInformation($"Retrieved configurations for ConfigurationId: {configurationId}");
                    

                return Ok(configurations);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error occurred while retrieving configurations for ConfigurationId: {configurationId}");
                
                return CreateErrorResponse<IList<SelectedMetricsConfiguration>>(
                    $"Failed to retrieve configurations for ConfigurationId: {configurationId}", 500);
            }
        }

        /// <summary>
        /// Retrieve all configurations for an agent (from query string)
        /// </summary>
        /// <param name="agentId">Unique ID of the agent (from query string)</param>
        /// <param name="environmentName"></param>
        /// <returns>All configurations associated with the agent</returns>
        /// <response code="200">Configurations retrieved successfully</response>
        /// <response code="404">No configurations found for this agent</response>
        /// <response code="500">Internal server error</response>
        [HttpGet("configurations")]
        [ProducesResponseType(typeof(List<MetricsConfigurationMetadataDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<List<MetricsConfigurationMetadataDto>>> GetConfigurationsByAgentId([FromQuery] string agentId, [FromQuery] string environmentName = "")
        {
            try
            {
                _logger.LogInformation("Request to retrieve all configurations for agent: {AgentId}", agentId);

                var agentIdValidation = ValidateAgentId(agentId);
                if (agentIdValidation != null)
                {
                    _logger.LogWarning("Invalid or missing agent ID");
                    return agentIdValidation;
                }

                var configurations = await _metricsConfigurationRequestHandler.GetAllMetricsConfigurationsByAgentIdAndEnvironmentAsync(agentId ,environmentName);


                if (!configurations.Any())
                {
                    _logger.LogInformation("No configurations found for agent: {AgentId}", agentId);
                    return NotFound($"No configurations found for agent: {agentId}");
                }

                _logger.LogInformation("Retrieved {Count} configurations for agent: {AgentId}",
                    configurations.Count, agentId);

                return Ok(configurations);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while retrieving configurations for agent: {AgentId}", agentId);
                return CreateErrorResponse<List<MetricsConfigurationMetadataDto>>(
                    $"Failed to retrieve configurations for agent: {agentId}", 500);
            }
        }

        
        #endregion

        #region POST Methods
                
        [HttpPost("configurations")]
        [EnableRateLimiting("StrictApiPolicy")]
        [ProducesResponseType(typeof(ConfigurationSaveResponseDto), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(ConfigurationSaveResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ConfigurationSaveResponseDto>> CreateOrSaveConfiguration(
            [FromBody] CreateMetricsConfigurationDto createConfigDto)
        {
            try
            {
                _logger.LogInformation("Request to create/save configuration: {ConfigName} for agent: {AgentId}",
                    createConfigDto.ConfigurationName, createConfigDto.AgentId);

                if (!ModelState.IsValid)
                {
                    _logger.LogWarning("Invalid model state for configuration creation");
                    return CreateValidationErrorResponse<ConfigurationSaveResponseDto>();
                }

                // Check if configuration already exists
                //var exists = await _metricsConfigurationRequestHandler.ConfigurationExistsAsync(
                //    createConfigDto.AgentId, createConfigDto.ConfigurationName, createConfigDto.EnvironmentName);
                
                var result = await _metricsConfigurationRequestHandler.CreateOrSaveConfigurationAsync(createConfigDto);

                if (result.Status == "error")
                {
                    _logger.LogError("Configuration creation/update failed: {Message}", result.Message);
                    return StatusCode(500, result);
                }

                return CreatedAtAction(
                    nameof(GetConfigurationsByMetricsConfigurationId),
                    new { ConfigurationId = result.ConfigId },
                    result);

                //if (exists)
                //{
                //    _logger.LogInformation("Configuration updated successfully: {ConfigId}", result.ConfigId);
                //    return Ok(result);
                //}
                //else
                //{
                //    _logger.LogInformation("Configuration created successfully: {ConfigId}", result.ConfigId);
                //    return CreatedAtAction(
                //        nameof(GetConfigurationsByAgentId),
                //        new { agentId = createConfigDto.AgentId },
                //        result);
                //}
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while creating/saving configuration for agent: {AgentId}",
                    createConfigDto.AgentId);
                return CreateErrorResponse<ConfigurationSaveResponseDto>(
                    "Failed to create or save evaluation configuration", 500);
            }
        }

        #endregion

    }
}
