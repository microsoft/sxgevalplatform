using Microsoft.AspNetCore.Mvc;
using SxgEvalPlatformApi.Models;
using SxgEvalPlatformApi.Services;

namespace SxgEvalPlatformApi.Controllers;

/// <summary>
/// Controller for evaluation configuration operations
/// </summary>
[ApiController]
[Route("api/v1/eval")]
public class EvaluationConfigurationController : BaseController
{
    private readonly IEvaluationConfigurationService _configurationService;
    private readonly IConfiguration _configuration;

    public EvaluationConfigurationController(
        IEvaluationConfigurationService configurationService,
        IConfiguration configuration,
        ILogger<EvaluationConfigurationController> logger)
        : base(logger)
    {
        _configurationService = configurationService;
        _configuration = configuration;
    }

    #region  GET Methods 

    /// <summary>
    /// Retrieve all configurations for an agent
    /// </summary>
    /// <param name="agentId">Unique ID of the agent</param>
    /// <returns>All configurations associated with the agent</returns>
    /// <response code="200">Configurations retrieved successfully</response>
    /// <response code="404">No configurations found for this agent</response>
    /// <response code="500">Internal server error</response>
    [HttpGet("configurations/{agentId}")]
    [ProducesResponseType(typeof(CreateMetricsConfigurationDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<CreateMetricsConfigurationDto>> GetConfigurationsByAgentId(string agentId)
    {
        try
        {
            _logger.LogInformation("Request to retrieve configuration for agent: {AgentId}", agentId);

            if (string.IsNullOrWhiteSpace(agentId))
            {
                _logger.LogWarning("Agent ID is null or empty");
                return BadRequest("Agent ID is required");
            }

            var configuration = await _configurationService.GetMetricsConfigurationByAgentIdAsync(agentId);

            if (configuration == null)
            {
                _logger.LogInformation("No configuration found for agent: {AgentId}", agentId);
                return NotFound($"No configuration found for agent: {agentId}");
            }

            _logger.LogInformation("Retrieved configuration for agent: {AgentId}", agentId);

            return Ok(configuration);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred while retrieving configuration for agent: {AgentId}", agentId);
            return CreateErrorResponse<CreateMetricsConfigurationDto>(
                $"Failed to retrieve configuration for agent: {agentId}", 500);
        }
    }

    /// <summary>
    /// Get default metric configuration from Azure Blob Storage
    /// </summary>
    /// <returns>Default metric configuration JSON content</returns>
    /// <response code="200">Default configuration retrieved successfully</response>
    /// <response code="404">Default configuration not found</response>
    /// <response code="500">Internal server error</response>
    [HttpGet("configurations")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetDefaultConfiguration()
    {
        try
        {
            _logger.LogInformation("Request to retrieve default metric configuration from Azure Blob Storage");

            var jsonContent = await _configurationService.GetDefaultConfigurationAsync();

            if (string.IsNullOrEmpty(jsonContent))
            {
                _logger.LogWarning("Default metric configuration not found in Azure Blob Storage");
                return NotFound("Default metric configuration not found");
            }

            // Parse JSON to validate it's properly formatted
            var configObject = System.Text.Json.JsonSerializer.Deserialize<object>(jsonContent);

            _logger.LogInformation("Successfully retrieved default metric configuration from Azure Blob Storage");

            // Return the parsed JSON content
            return Ok(configObject);
        }
        catch (System.Text.Json.JsonException jsonEx)
        {
            _logger.LogError(jsonEx, "Invalid JSON format in default metric configuration");
            return CreateErrorResponse("Default metric configuration contains invalid JSON", 500);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred while retrieving default metric configuration");
            return CreateErrorResponse("Failed to retrieve default metric configuration", 500);
        }
    }

    #endregion

    #region  POST Methods     
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
                createConfigDto.ConfigName, createConfigDto.AgentId);

            if (!ModelState.IsValid)
            {
                _logger.LogWarning("Invalid model state for configuration creation");
                return BadRequest(ModelState);
            }

            // Check if configuration already exists
            var exists = await _configurationService.ConfigurationExistsAsync(
                createConfigDto.AgentId, createConfigDto.ConfigName);

            var result = await _configurationService.CreateOrSaveConfigurationAsync(createConfigDto);

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