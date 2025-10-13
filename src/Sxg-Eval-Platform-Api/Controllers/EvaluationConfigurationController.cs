//using Microsoft.AspNetCore.Mvc;
//using SxgEvalPlatformApi.Models;
//using SxgEvalPlatformApi.Services;

//namespace SxgEvalPlatformApi.Controllers;

///// <summary>
///// Controller for evaluation configuration operations
///// </summary>
//[ApiController]
//[Route("api/v1/eval")]
//public class EvaluationConfigurationController : BaseController
//{
//    private readonly IEvaluationConfigurationService _configurationService;
//    private readonly IConfiguration _configuration;

//    public EvaluationConfigurationController(
//        IEvaluationConfigurationService configurationService,
//        IConfiguration configuration,
//        ILogger<EvaluationConfigurationController> logger)
//        : base(logger)
//    {
//        _configurationService = configurationService;
//        _configuration = configuration;

//        // Log controller initialization for debugging
//        _logger.LogInformation("EvaluationConfigurationController initialized");
//    }

//    #region  GET Methods 

//    /// <summary>
//    /// Test endpoint to verify debugging is working
//    /// </summary>
//    /// <returns>Simple test response</returns>
//    [HttpGet("test")]
//    public IActionResult TestEndpoint()
//    {
//        _logger.LogInformation("TEST: Test endpoint was called - debugging should work here!");

//        var testResponse = new
//        {
//            message = "Test endpoint working",
//            timestamp = DateTime.Now,
//            environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT"),
//            debuggerAttached = System.Diagnostics.Debugger.IsAttached
//        };

//        _logger.LogInformation("TEST: Returning response: {@Response}", testResponse);
//        return Ok(testResponse);
//    }

//    /// <summary>
//    /// Retrieve specific configuration for an agent by configuration name
//    /// </summary>
//    /// <param name="agentId">Unique ID of the agent</param>
//    /// <param name="configurationName">Name of the configuration</param>
//    /// <returns>Configuration associated with the agent and configuration name</returns>
//    /// <response code="200">Configuration retrieved successfully</response>
//    /// <response code="404">Configuration not found</response>
//    /// <response code="500">Internal server error</response>
//    [HttpGet("configurations/{agentId}/{configurationName}")]
//    [ProducesResponseType(typeof(CreateMetricsConfigurationDto), StatusCodes.Status200OK)]
//    [ProducesResponseType(StatusCodes.Status404NotFound)]
//    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
//    public async Task<ActionResult<CreateMetricsConfigurationDto>> GetConfigurationByAgentIdAndName(string agentId, string configurationName)
//    {
//        try
//        {
//            _logger.LogInformation("Request to retrieve configuration for agent: {AgentId}, config: {ConfigName}",
//                agentId, configurationName);

//            if (string.IsNullOrWhiteSpace(agentId))
//            {
//                _logger.LogWarning("Agent ID is null or empty");
//                return BadRequest("Agent ID is required");
//            }

//            if (string.IsNullOrWhiteSpace(configurationName))
//            {
//                _logger.LogWarning("Configuration name is null or empty");
//                return BadRequest("Configuration name is required");
//            }

//            var configuration = await _configurationService.GetConfigurationsByAgentIdAsync(agentId, configurationName);

//            if (configuration == null)
//            {
//                _logger.LogInformation("No configuration found for agent: {AgentId}, config: {ConfigName}",
//                    agentId, configurationName);
//                return NotFound($"No configuration found for agent: {agentId} with name: {configurationName}");
//            }

//            _logger.LogInformation("Retrieved configuration for agent: {AgentId}, config: {ConfigName}",
//                agentId, configurationName);

//            return Ok(configuration);
//        }
//        catch (Exception ex)
//        {
//            _logger.LogError(ex, "Error occurred while retrieving configuration for agent: {AgentId}, config: {ConfigName}",
//                agentId, configurationName);
//            return CreateErrorResponse<CreateMetricsConfigurationDto>(
//                $"Failed to retrieve configuration for agent: {agentId} with name: {configurationName}", 500);
//        }
//    }

//    /// <summary>
//    /// Retrieve all configurations for an agent
//    /// </summary>
//    /// <param name="agentId">Unique ID of the agent</param>
//    /// <returns>All configurations associated with the agent</returns>
//    /// <response code="200">Configurations retrieved successfully</response>
//    /// <response code="404">No configurations found for this agent</response>
//    /// <response code="500">Internal server error</response>
//    [HttpGet("configurations/{agentId}")]
//    [ProducesResponseType(typeof(List<CreateMetricsConfigurationDto>), StatusCodes.Status200OK)]
//    [ProducesResponseType(StatusCodes.Status404NotFound)]
//    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
//    public async Task<ActionResult<List<CreateMetricsConfigurationDto>>> GetConfigurationsByAgentId(string agentId)
//    {
//        try
//        {
//            _logger.LogInformation("Request to retrieve all configurations for agent: {AgentId}", agentId);

//            if (string.IsNullOrWhiteSpace(agentId))
//            {
//                _logger.LogWarning("Agent ID is null or empty");
//                return BadRequest("Agent ID is required");
//            }

//            var configurations = await _configurationService.GetConfigurationsByAgentIdAsync(agentId);

//            if (!configurations.Any())
//            {
//                _logger.LogInformation("No configurations found for agent: {AgentId}", agentId);
//                return NotFound($"No configurations found for agent: {agentId}");
//            }

//            _logger.LogInformation("Retrieved {Count} configurations for agent: {AgentId}",
//                configurations.Count, agentId);

//            return Ok(configurations);
//        }
//        catch (Exception ex)
//        {
//            _logger.LogError(ex, "Error occurred while retrieving configurations for agent: {AgentId}", agentId);
//            return CreateErrorResponse<List<CreateMetricsConfigurationDto>>(
//                $"Failed to retrieve configurations for agent: {agentId}", 500);
//        }
//    }

//    /// <summary>
//    /// Get platform configuration (default configuration)
//    /// </summary>
//    /// <returns>Platform configuration</returns>
//    /// <response code="200">Platform configuration retrieved successfully</response>
//    /// <response code="404">Platform configuration not found</response>
//    /// <response code="500">Internal server error</response>
//    [HttpGet("configurations/platform")]
//    [ProducesResponseType(typeof(CreateMetricsConfigurationDto), StatusCodes.Status200OK)]
//    [ProducesResponseType(StatusCodes.Status404NotFound)]
//    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
//    public async Task<ActionResult<CreateMetricsConfigurationDto>> GetPlatformConfiguration()
//    {
//        try
//        {
//            _logger.LogInformation("Request to retrieve platform configuration");

//            var configuration = await _configurationService.GetConfigurationsByAgentIdAsync();

//            if (configuration == null)
//            {
//                _logger.LogInformation("No platform configuration found");
//                return NotFound("Platform configuration not found");
//            }

//            _logger.LogInformation("Retrieved platform configuration successfully");

//            return Ok(configuration);
//        }
//        catch (Exception ex)
//        {
//            _logger.LogError(ex, "Error occurred while retrieving platform configuration");
//            return CreateErrorResponse<CreateMetricsConfigurationDto>(
//                "Failed to retrieve platform configuration", 500);
//        }
//    }

//    /// <summary>
//    /// Get default metric configuration from Azure Blob Storage
//    /// </summary>
//    /// <returns>Default metric configuration JSON content</returns>
//    /// <response code="200">Default configuration retrieved successfully</response>
//    /// <response code="404">Default configuration not found</response>
//    /// <response code="500">Internal server error</response>
//    [HttpGet("configurations/default")]
//    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
//    [ProducesResponseType(StatusCodes.Status404NotFound)]
//    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
//    public async Task<IActionResult> GetDefaultConfiguration()
//    {
//        try
//        {
//            _logger.LogInformation("DEFAULT CONFIG: Starting GetDefaultConfiguration method");
//            _logger.LogInformation("DEFAULT CONFIG: Debugger attached: {DebuggerAttached}", System.Diagnostics.Debugger.IsAttached);

//            var jsonContent = await _configurationService.GetDefaultConfigurationAsync();

//            _logger.LogInformation("DEFAULT CONFIG: Service call completed, jsonContent length: {Length}",
//                jsonContent?.Length ?? 0);

//            if (string.IsNullOrEmpty(jsonContent))
//            {
//                _logger.LogWarning("Default metric configuration not found");
//                return NotFound("Default metric configuration not found");
//            }

//            // Parse JSON to validate it's properly formatted
//            var configObject = System.Text.Json.JsonSerializer.Deserialize<object>(jsonContent);

//            _logger.LogInformation("Successfully retrieved default metric configuration");

//            // Return the parsed JSON content
//            return Ok(configObject);
//        }
//        catch (System.Text.Json.JsonException jsonEx)
//        {
//            _logger.LogError(jsonEx, "Invalid JSON format in default metric configuration");
//            return CreateErrorResponse("Default metric configuration contains invalid JSON", 500);
//        }
//        catch (Exception ex)
//        {
//            _logger.LogError(ex, "Error occurred while retrieving default metric configuration");
//            return CreateErrorResponse("Failed to retrieve default metric configuration", 500);
//        }
//    }

//    #endregion

//    #region  POST Methods     
//    /// <summary>
//    /// Create or save evaluation configuration
//    /// </summary>
//    /// <param name="createConfigDto">Configuration creation data</param>
//    /// <returns>Configuration save response</returns>
//    /// <response code="201">Configuration created successfully</response>
//    /// <response code="200">Configuration updated successfully</response>
//    /// <response code="400">Invalid input</response>
//    /// <response code="500">Internal server error</response>
//    [HttpPost("configurations")]
//    [ProducesResponseType(typeof(ConfigurationSaveResponseDto), StatusCodes.Status201Created)]
//    [ProducesResponseType(typeof(ConfigurationSaveResponseDto), StatusCodes.Status200OK)]
//    [ProducesResponseType(StatusCodes.Status400BadRequest)]
//    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
//    public async Task<ActionResult<ConfigurationSaveResponseDto>> CreateOrSaveConfiguration(
//        [FromBody] CreateMetricsConfigurationDto createConfigDto)
//    {
//        try
//        {
//            _logger.LogInformation("Request to create/save configuration: {ConfigName} for agent: {AgentId}",
//                createConfigDto.ConfigurationName, createConfigDto.AgentId);

//            if (!ModelState.IsValid)
//            {
//                _logger.LogWarning("Invalid model state for configuration creation");
//                return BadRequest(ModelState);
//            }

//            // Check if configuration already exists
//            var exists = await _configurationService.ConfigurationExistsAsync(
//                createConfigDto.AgentId, createConfigDto.ConfigurationName);

//            var result = await _configurationService.CreateOrSaveConfigurationAsync(createConfigDto);

//            if (result.Status == "error")
//            {
//                _logger.LogError("Configuration creation/update failed: {Message}", result.Message);
//                return StatusCode(500, result);
//            }

//            if (exists)
//            {
//                _logger.LogInformation("Configuration updated successfully: {ConfigId}", result.ConfigId);
//                return Ok(result);
//            }
//            else
//            {
//                _logger.LogInformation("Configuration created successfully: {ConfigId}", result.ConfigId);
//                return CreatedAtAction(
//                    nameof(GetConfigurationsByAgentId),
//                    new { agentId = createConfigDto.AgentId },
//                    result);
//            }
//        }
//        catch (Exception ex)
//        {
//            _logger.LogError(ex, "Error occurred while creating/saving configuration for agent: {AgentId}",
//                createConfigDto.AgentId);
//            return CreateErrorResponse<ConfigurationSaveResponseDto>(
//                "Failed to create or save evaluation configuration", 500);
//        }
//    }

//    #endregion
//}