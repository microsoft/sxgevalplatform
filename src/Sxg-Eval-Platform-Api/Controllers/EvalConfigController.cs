using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Sxg.EvalPlatform.API.Storage.Entities;
using SxgEvalPlatformApi.Models.Dtos;
using SxgEvalPlatformApi.RequestHandlers;
using System.ComponentModel.DataAnnotations;

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
        [ProducesResponseType(typeof(MetricsConfiguration), StatusCodes.Status200OK)]
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
        [ProducesResponseType(typeof(IList<SelectedMetricsConfiguration>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<IList<SelectedMetricsConfiguration>>> GetConfigurationsByMetricsConfigurationId(Guid configurationId)
        {
            try
            {
                _logger.LogInformation("Request to retrieve all configurations for agent: {metricsConfigurationId}", configurationId);

                var configurations = await _metricsConfigurationRequestHandler.GetMetricsConfigurationByConfigurationIdAsync(configurationId.ToString());

                if (configurations == null || !configurations.Any())
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
        [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<List<MetricsConfigurationMetadataDto>>> GetConfigurationsByAgentId([FromQuery, Required] string agentId, [FromQuery] string environmentName = "")
        {
            try
            {
                _logger.LogInformation("Request to retrieve all configurations for agent: {AgentId}", agentId);

                ValidateAndAddToModelState(agentId, "agentId", "agentid");
                if (!ModelState.IsValid)
                {
                    _logger.LogWarning("Invalid or missing agent ID");
                    return CreateValidationErrorResponse<List<MetricsConfigurationMetadataDto>>();
                }

                var configurations = await _metricsConfigurationRequestHandler.GetAllMetricsConfigurationsByAgentIdAndEnvironmentAsync(agentId ,environmentName);

                if (configurations == null || !configurations.Any())
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
        
        /// <summary>
        /// Create a new configuration
        /// </summary>
        /// <param name="createConfigDto">The configuration data to create</param>
        /// <returns>Created configuration details</returns>
        /// <response code="201">Configuration created successfully</response>
        /// <response code="400">Invalid input data or validation errors</response>
        /// <response code="409">Configuration with the same name already exists for this agent and environment</response>
        /// <response code="500">Internal server error</response>
        [HttpPost("configurations")]
        [ProducesResponseType(typeof(ConfigurationSaveResponseDto), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ConfigurationConflictResponseDto), StatusCodes.Status409Conflict)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ConfigurationSaveResponseDto>> CreateConfiguration(
            [FromBody] CreateConfigurationRequestDto createConfigDto)
        {
            try
            {
                _logger.LogInformation("Request to create new configuration: {ConfigName} for agent: {AgentId}",
                    createConfigDto.ConfigurationName, createConfigDto.AgentId);

                if (!ModelState.IsValid)
                {
                    _logger.LogWarning("Invalid model state for configuration creation");
                    return CreateValidationErrorResponse<ConfigurationSaveResponseDto>();
                }

                var result = await _metricsConfigurationRequestHandler.CreateConfigurationAsync(createConfigDto);

                if (result.Status == "conflict")
                {
                    _logger.LogWarning("Configuration creation failed due to conflict: {Message}", result.Message);
                    
                    var conflictResponse = new ConfigurationConflictResponseDto
                    {
                        Status = "conflict",
                        Message = $"Configuration save failed due to conflict: {result.Message}. If you want to update the configuration, use the PUT endpoint with configuration ID: {result.ConfigurationId}",
                        ExistingConfigurationId = result.ConfigurationId
                    };
                    
                    return Conflict(conflictResponse);
                }

                if (result.Status == "error")
                {
                    _logger.LogError("Configuration creation failed: {Message}", result.Message);
                    return StatusCode(500, result);
                }

                return CreatedAtAction(
                    nameof(GetConfigurationsByMetricsConfigurationId),
                    new { configurationId = result.ConfigurationId },
                    result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while creating configuration for agent: {AgentId}",
                    createConfigDto.AgentId);
                return CreateErrorResponse<ConfigurationSaveResponseDto>(
                    "Failed to create evaluation configuration", 500);
            }
        }

        #endregion

        #region PUT Methods

        /// <summary>
        /// Update an existing configuration by ID
        /// </summary>
        /// <param name="configurationId">The ID of the configuration to update</param>
        /// <param name="updateConfigDto">The configuration data to update</param>
        /// <returns>Updated configuration details</returns>
        /// <response code="200">Configuration updated successfully</response>
        /// <response code="400">Invalid input data</response>
        /// <response code="404">Configuration with the specified ID not found</response>
        /// <response code="500">Internal server error</response>
        [HttpPut("configurations/{configurationId}")]
        [ProducesResponseType(typeof(ConfigurationSaveResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ConfigurationSaveResponseDto>> UpdateConfiguration(
            [FromRoute] Guid configurationId,
            [FromBody] UpdateConfigurationRequestDto updateConfigDto)
        {
            try
            {
                _logger.LogInformation("Request to update configuration: {ConfigId}",
                    configurationId);

                if (!ModelState.IsValid)
                {
                    _logger.LogWarning("Invalid model state for configuration update");
                    return CreateValidationErrorResponse<ConfigurationSaveResponseDto>();
                }

                var result = await _metricsConfigurationRequestHandler.UpdateConfigurationAsync(
                    configurationId.ToString(), updateConfigDto);

                if (result.Status == "error")
                {
                    _logger.LogError("Configuration update failed: {Message}", result.Message);
                    
                    if (result.Message.Contains("not found"))
                    {
                        return NotFound(result);
                    }
                    
                    return StatusCode(500, result);
                }

                _logger.LogInformation("Configuration updated successfully: {ConfigId}", result.ConfigurationId);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while updating configuration: {ConfigId}", configurationId);
                return CreateErrorResponse<ConfigurationSaveResponseDto>(
                    "Failed to update evaluation configuration", 500);
            }
        }

        #endregion

        #region DELETE Methods

        /// <summary>
        /// Delete a configuration by ID
        /// </summary>
        /// <param name="configurationId">The ID of the configuration to delete</param>
        /// <returns>Deletion result</returns>
        /// <response code="200">Configuration deleted successfully</response>
        /// <response code="404">Configuration with the specified ID not found</response>
        /// <response code="500">Internal server error</response>
        [HttpDelete("configurations/{configurationId}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> DeleteConfiguration([FromRoute] Guid configurationId)
        {
            try
            {
                _logger.LogInformation("Request to delete configuration: {ConfigId}", configurationId);

                bool deleted = await _metricsConfigurationRequestHandler.DeleteConfigurationAsync(configurationId.ToString());

                if (!deleted)
                {
                    _logger.LogWarning("Configuration not found for deletion: {ConfigId}", configurationId);
                    return NotFound($"Configuration with ID '{configurationId}' not found");
                }

                _logger.LogInformation("Configuration deleted successfully: {ConfigId}", configurationId);
                return Ok(new { message = $"Configuration '{configurationId}' deleted successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while deleting configuration: {ConfigId}", configurationId);
                return StatusCode(500, new { message = "Failed to delete evaluation configuration", error = ex.Message });
            }
        }

        #endregion

    }
}
