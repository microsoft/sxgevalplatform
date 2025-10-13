using Microsoft.AspNetCore.Mvc;
using SxgEvalPlatformApi.Models;
using SxgEvalPlatformApi.Services;

namespace SxgEvalPlatformApi.Controllers
{
    [Route("api/v1/eval")]
    public class EvalConfigController : BaseController
    {
        private readonly IEvaluationConfigurationService _configurationService;
        private readonly IConfiguration _configuration;

        public EvalConfigController(
            IEvaluationConfigurationService configurationService,
            IConfiguration configuration,
            ILogger<EvalConfigController> logger)
            : base(logger)
        {
            _configurationService = configurationService;
            _configuration = configuration;

            // Log controller initialization for debugging
            _logger.LogInformation("EvalConfigController initialized");
        }

        #region GET Methods

        /// <summary>
        /// Retrieve all configurations for an agent
        /// </summary>
        /// <param name="agentId">Unique ID of the agent</param>
        /// <returns>All configurations associated with the agent</returns>
        /// <response code="200">Configurations retrieved successfully</response>
        /// <response code="404">No configurations found for this agent</response>
        /// <response code="500">Internal server error</response>
        [HttpGet("configurations/{agentId}")]
        [ProducesResponseType(typeof(List<CreateMetricsConfigurationDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<List<CreateMetricsConfigurationDto>>> GetConfigurationsByAgentId(string agentId)
        {
            try
            {
                _logger.LogInformation("Request to retrieve all configurations for agent: {AgentId}", agentId);

                if (string.IsNullOrWhiteSpace(agentId))
                {
                    _logger.LogWarning("Agent ID is null or empty");
                    return BadRequest("Agent ID is required");
                }

                var configurations = await _configurationService.GetConfigurationsByAgentIdAsync(agentId);

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
                return CreateErrorResponse<List<CreateMetricsConfigurationDto>>(
                    $"Failed to retrieve configurations for agent: {agentId}", 500);
            }
        }

        #endregion

        #region POST Methods

        /// <summary>
        /// Create or save evaluation configuration
        /// </summary>
        /// <param name="createConfigDto">Configuration creation data</param>
        /// <returns>Configuration save response</returns>
        /// <response code="201">Configuration created successfully</response>
        /// <response code="200">Configuration updated successfully</response>
        /// <response code="400">Invalid input</response>
        /// <response code="500">Internal server error</response>
        [HttpPost("configurations")]
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
                    return BadRequest(ModelState);
                }

                // Check if configuration already exists
                var exists = await _configurationService.ConfigurationExistsAsync(
                    createConfigDto.AgentId, createConfigDto.ConfigurationName);

                var result = await _configurationService.CreateOrSaveConfigurationAsync(createConfigDto);

                if (result.Status == "error")
                {
                    _logger.LogError("Configuration creation/update failed: {Message}", result.Message);
                    return StatusCode(500, result);
                }

                if (exists)
                {
                    _logger.LogInformation("Configuration updated successfully: {ConfigId}", result.ConfigId);
                    return Ok(result);
                }
                else
                {
                    _logger.LogInformation("Configuration created successfully: {ConfigId}", result.ConfigId);
                    return CreatedAtAction(
                        nameof(GetConfigurationsByAgentId),
                        new { agentId = createConfigDto.AgentId },
                        result);
                }
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
