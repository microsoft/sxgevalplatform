using Microsoft.AspNetCore.Mvc;
using Sxg.EvalPlatform.API.Storage.Entities;
using SxgEvalPlatformApi.Models.Dtos;
using SxgEvalPlatformApi.RequestHandlers;
using SxgEvalPlatformApi.Services;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;

namespace SxgEvalPlatformApi.Controllers
{
    [Route("api/v1/eval")]
    public class EvalConfigsController : BaseController
    {
        private readonly IMetricsConfigurationRequestHandler _metricsConfigurationRequestHandler;
        private readonly IConfiguration _configuration;
        private readonly IOpenTelemetryService _telemetryService;

        public EvalConfigsController(IMetricsConfigurationRequestHandler metricsConfigurationRequestHandler,
                                     IConfiguration configuration,
                                     ILogger<EvalConfigsController> logger,
                                     IOpenTelemetryService telemetryService) : base(logger)

        {
            _metricsConfigurationRequestHandler = metricsConfigurationRequestHandler;
            _configuration = configuration;
            _telemetryService = telemetryService;
            _logger.LogInformation("EvalConfigController initialized");
        }

        #region GET Methods

        [HttpGet("configurations/defaultconfiguration")]
        [ProducesResponseType(typeof(DefaultMetricsConfiguration), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<DefaultMetricsConfiguration>> GetDefaultMetricsConfiguration()
        {
            using var activity = _telemetryService.StartActivity("EvalConfigs.GetDefaultMetricsConfiguration");
            var stopwatch = Stopwatch.StartNew();
            bool success = false;

            try
            {
                _logger.LogInformation("Request to retrieve default Metrics configuration");
                activity?.SetTag("operation", "GetDefaultMetricsConfiguration");

                var result = await _metricsConfigurationRequestHandler.GetDefaultMetricsConfigurationAsync();

                success = true;
                activity?.SetTag("success", true);
                _logger.LogInformation("Request to retrieve default Metrics configuration succeeded");

                return result;
            }
            catch (Exception ex)
            {
                activity?.SetTag("success", false);
                activity?.SetTag("error.message", ex.Message);
                activity?.SetTag("error.type", ex.GetType().Name);

                _logger.LogError(ex, "Error occurred while retrieving default Metrics configuration");
                return CreateErrorResponse<DefaultMetricsConfiguration>(
    $"Failed to retrieve default Metrics configuration", 500);
            }
            finally
            {
                stopwatch.Stop();
                _telemetryService.TrackMetricsConfigOperation("GetDefaultConfiguration", "default", success, stopwatch.Elapsed);
            }
        }

        /// <summary>
        /// Retrieve configuration by Metrics configuration ID
        /// </summary>
        /// <param name="configurationId">Unique ID of the Metrics configuration</param>
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
            using var activity = _telemetryService.StartActivity("EvalConfigs.GetConfigurationsByMetricsConfigurationId");
            var stopwatch = Stopwatch.StartNew();
            bool success = false;

            try
            {
                _logger.LogInformation("Request to retrieve all configurations for agent: {metricsConfigurationId}", configurationId);

                activity?.SetTag("operation", "GetConfigurationsByMetricsConfigurationId");
                activity?.SetTag("configuration_id", configurationId.ToString());

                var configurations = await _metricsConfigurationRequestHandler.GetMetricsConfigurationByConfigurationIdAsync(configurationId.ToString());

                if (configurations == null || !configurations.Any())
                {
                    _logger.LogInformation($"No configurations found for ConfigurationId: {configurationId}");
                    activity?.SetTag("found", false);
                    return NotFound($"No configurations found for ConfigurationId: {configurationId}");
                }

                success = true;
                activity?.SetTag("success", true);
                activity?.SetTag("found", true);
                activity?.SetTag("configuration_count", configurations.Count());

                _logger.LogInformation($"Retrieved configurations for ConfigurationId: {configurationId}");

                return Ok(configurations);
            }
            catch (Exception ex)
            {
                activity?.SetTag("success", false);
                activity?.SetTag("error.message", ex.Message);
                activity?.SetTag("error.type", ex.GetType().Name);

                _logger.LogError(ex, $"Error occurred while retrieving configurations for ConfigurationId: {configurationId}");

                return CreateErrorResponse<IList<SelectedMetricsConfiguration>>(
            $"Failed to retrieve configurations for ConfigurationId: {configurationId}", 500);
            }
            finally
            {
                stopwatch.Stop();
                _telemetryService.TrackMetricsConfigOperation("GetConfigurationById", configurationId.ToString(), success, stopwatch.Elapsed);
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
            using var activity = _telemetryService.StartActivity("EvalConfigs.GetConfigurationsByAgentId");
            var stopwatch = Stopwatch.StartNew();
            bool success = false;

            try
            {
                _logger.LogInformation("Request to retrieve all configurations for agent: {AgentId}", agentId);

                activity?.SetTag("operation", "GetConfigurationsByAgentId");
                activity?.SetTag("agent_id", agentId);
                activity?.SetTag("environment_name", environmentName);

                ValidateAndAddToModelState(agentId, "agentId", "agentid");
                if (!ModelState.IsValid)
                {
                    _logger.LogWarning("Invalid or missing agent ID");
                    activity?.SetTag("success", false);
                    activity?.SetTag("error.message", "Invalid or missing agent ID");
                    return CreateValidationErrorResponse<List<MetricsConfigurationMetadataDto>>();
                }

                var configurations = await _metricsConfigurationRequestHandler.GetAllMetricsConfigurationsByAgentIdAndEnvironmentAsync(agentId, environmentName);

                if (configurations == null || !configurations.Any())
                {
                    _logger.LogInformation("No configurations found for agent: {AgentId}", agentId);
                    activity?.SetTag("found", false);
                    return NotFound($"No configurations found for agent: {agentId}");
                }

                success = true;
                activity?.SetTag("success", true);
                activity?.SetTag("found", true);
                activity?.SetTag("configuration_count", configurations.Count);

                _logger.LogInformation("Retrieved {Count} configurations for agent: {AgentId}",
                    configurations.Count, agentId);

                return Ok(configurations);
            }
            catch (Exception ex)
            {
                activity?.SetTag("success", false);
                activity?.SetTag("error.message", ex.Message);
                activity?.SetTag("error.type", ex.GetType().Name);

                _logger.LogError(ex, "Error occurred while retrieving configurations for agent: {AgentId}", agentId);
                return CreateErrorResponse<List<MetricsConfigurationMetadataDto>>(
                     $"Failed to retrieve configurations for agent: {agentId}", 500);
            }
            finally
            {
                stopwatch.Stop();
                _telemetryService.TrackMetricsConfigOperation("GetConfigurationsByAgent", agentId, success, stopwatch.Elapsed);
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
        [ProducesResponseType(typeof(ConfigurationSaveResponseDto), StatusCodes.Status409Conflict)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ConfigurationSaveResponseDto>> CreateConfiguration([FromBody] CreateConfigurationRequestDto createConfigDto)
        {
            using var activity = _telemetryService.StartActivity("EvalConfigs.CreateConfiguration");
            var stopwatch = Stopwatch.StartNew();
            bool success = false;

            try
            {
                _logger.LogInformation("Request to create new configuration: {ConfigName} for agent: {AgentId}",
                          createConfigDto.ConfigurationName, createConfigDto.AgentId);

                activity?.SetTag("operation", "CreateConfiguration");
                activity?.SetTag("agent_id", createConfigDto.AgentId);
                activity?.SetTag("configuration_name", createConfigDto.ConfigurationName);
                activity?.SetTag("environment_name", createConfigDto.EnvironmentName ?? "");
                return await CreateOrUpdateMetricsConfiguration(createConfigDto, activity);
            }
            catch (ValidationException vex)
            {
                activity?.SetTag("success", false);
                activity?.SetTag("error.message", vex.Message);
                activity?.SetTag("error.type", vex.GetType().Name);
                _logger.LogError(vex, $"Data Validation error occurred while creating configuration for agent: {createConfigDto.AgentId}, configuration: {createConfigDto.ConfigurationName}");
                return CreateErrorResponse<ConfigurationSaveResponseDto>(
                   vex.Message, 400);
            }
            catch (Exception ex)
            {
                activity?.SetTag("success", false);
                activity?.SetTag("error.message", ex.Message);
                activity?.SetTag("error.type", ex.GetType().Name);

                _logger.LogError(ex, $"Error occurred while creating configuration for agent: {createConfigDto.AgentId}, configuration: {createConfigDto.ConfigurationName}");
                return CreateErrorResponse<ConfigurationSaveResponseDto>(
                   "Failed to create evaluation configuration", 500);
            }
            finally
            {
                stopwatch.Stop();
                _telemetryService.TrackMetricsConfigOperation("CreateConfiguration", createConfigDto.ConfigurationName, success, stopwatch.Elapsed);
                activity.Stop();
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
        public async Task<ActionResult<ConfigurationSaveResponseDto>> UpdateConfiguration([FromRoute, Required] Guid configurationId,
                                                                                          [FromBody, Required] CreateConfigurationRequestDto updateConfigDto)
        {

            using var activity = _telemetryService.StartActivity("EvalConfigs.UpdateConfiguration");
            var stopwatch = Stopwatch.StartNew();
            bool success = false;

            try
            {
                _logger.LogInformation("Request to update configuration: {ConfigId}",
               configurationId);

                activity?.SetTag("operation", "UpdateConfiguration");
                activity?.SetTag("configuration_id", configurationId.ToString());

                var getEntity = await _metricsConfigurationRequestHandler.GetMetricsConfigurationByConfigurationIdAsync(configurationId.ToString());

                if(getEntity == null || !getEntity.Any())
                {
                    _logger.LogWarning("Configuration not found for update: {ConfigId}", configurationId);
                    activity?.SetTag("success", false);
                    activity?.SetTag("found", false);
                    return NotFound($"Configuration with ID '{configurationId}' not found");
                }


                var updateMetricsConfigurationRequestDto = new UpdateMetricsConfigurationRequestDto
                {
                    ConfigurationId = configurationId.ToString(),
                    MetricsConfiguration = updateConfigDto.MetricsConfiguration,
                    ConfigurationName = updateConfigDto.ConfigurationName,
                    Description = updateConfigDto.Description,
                    EnvironmentName = updateConfigDto.EnvironmentName,
                    AgentId = updateConfigDto.AgentId
                };

                return await CreateOrUpdateMetricsConfiguration(updateMetricsConfigurationRequestDto, activity);
            }
            catch (Exception ex)
            {
                activity?.SetTag("success", false);
                activity?.SetTag("error.message", ex.Message);
                activity?.SetTag("error.type", ex.GetType().Name);

                _logger.LogError(ex, "Error occurred while updating configuration: {ConfigId}", configurationId);
                return CreateErrorResponse<ConfigurationSaveResponseDto>(
                        "Failed to update evaluation configuration", 500);
            }
            finally
            {
                stopwatch.Stop();
                _telemetryService.TrackMetricsConfigOperation("UpdateConfiguration", configurationId.ToString(), success, stopwatch.Elapsed);
                activity.Stop(); 

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
            using var activity = _telemetryService.StartActivity("EvalConfigs.DeleteConfiguration");
            var stopwatch = Stopwatch.StartNew();
            bool success = false;

            try
            {
                _logger.LogInformation("Request to delete configuration: {ConfigId}", configurationId);

                activity?.SetTag("operation", "DeleteConfiguration");
                activity?.SetTag("configuration_id", configurationId.ToString());

                bool deleted = await _metricsConfigurationRequestHandler.DeleteConfigurationAsync(configurationId.ToString());

                if (!deleted)
                {
                    _logger.LogWarning("Configuration not found for deletion: {ConfigId}", configurationId);
                    activity?.SetTag("success", false);
                    activity?.SetTag("found", false);
                    return NotFound($"Configuration with ID '{configurationId}' not found");
                }

                success = true;
                activity?.SetTag("success", true);
                activity?.SetTag("deleted", true);

                _logger.LogInformation("Configuration deleted successfully: {ConfigId}", configurationId);
                return Ok(new { message = $"Configuration '{configurationId}' deleted successfully" });
            }
            catch (Exception ex)
            {
                activity?.SetTag("success", false);
                activity?.SetTag("error.message", ex.Message);
                activity?.SetTag("error.type", ex.GetType().Name);

                _logger.LogError(ex, "Error occurred while deleting configuration: {ConfigId}", configurationId);
                return StatusCode(500, new { message = "Failed to delete evaluation configuration", error = ex.Message });
            }
            finally
            {
                stopwatch.Stop();
                _telemetryService.TrackMetricsConfigOperation("DeleteConfiguration", configurationId.ToString(), success, stopwatch.Elapsed);
            }
        }

        #endregion

        private async Task<ActionResult<ConfigurationSaveResponseDto>> CreateOrUpdateMetricsConfiguration(CreateConfigurationRequestDto createConfigDto, Activity activity)
        {
            if (!ModelState.IsValid)
            {
                _logger.LogWarning("Invalid model state for configuration creation");
                activity?.SetTag("success", false);
                activity?.SetTag("error.message", "Invalid model state");
                return CreateValidationErrorResponse<ConfigurationSaveResponseDto>();
            }

            var result = await _metricsConfigurationRequestHandler.CreateOrSaveConfigurationAsync(createConfigDto);

            if (result.Status == "error")
            {
                _logger.LogError("Configuration creation failed: {Message}", result.Message);
                activity?.SetTag("success", false);
                activity?.SetTag("error.message", result.Message);

                if (result.Message.Contains("already exists"))
                {
                    return Conflict(result);
                }

                return StatusCode(500, result);
            }


            activity?.SetTag("success", true);
            activity?.SetTag("configuration_id", result.ConfigurationId);

            return CreatedAtAction(nameof(GetConfigurationsByMetricsConfigurationId),
                                   new { configurationId = result.ConfigurationId },
                                   result);
        }

    }
}
