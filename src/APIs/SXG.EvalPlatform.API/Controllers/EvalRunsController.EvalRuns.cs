using Azure;
using Microsoft.AspNetCore.Mvc;
using SxgEvalPlatformApi.Models;
using SxgEvalPlatformApi.Models.Dtos;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace SxgEvalPlatformApi.Controllers;

/// <summary>
/// Partial class containing evaluation run CRUD operations
/// </summary>
public partial class EvalRunsController
{
    #region Eval Run CRUD Endpoints

    /// <summary>
    /// Create a new evaluation run
    /// </summary>
    /// <param name="createDto">Evaluation run creation data containing AgentId, DataSetId, MetricsConfigurationId, Type, EnvironmentId, and AgentSchemaName (all required)</param>
    /// <returns>Created evaluation run with generated EvalRunId</returns>
    /// <response code="201">Evaluation run created successfully</response>
    /// <response code="400">Invalid input data</response>
    /// <response code="500">Internal server error</response>
    [HttpPost]
    [ProducesResponseType(typeof(EvalRunDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<EvalRunDto>> CreateEvalRun([FromBody] CreateEvalRunDto createDto)
    {
        using var activity = _telemetryService?.StartActivity("EvalRunsController.CreateEvalRun");

        var stopwatch = Stopwatch.StartNew();

        try
        {
            // Add telemetry tags for request parameters
            activity?.SetTag("agentId", createDto.AgentId);
            activity?.SetTag("dataSetId", createDto.DataSetId.ToString());
            activity?.SetTag("metricsConfigurationId", createDto.MetricsConfigurationId.ToString());
            activity?.SetTag("type", createDto.Type);
            activity?.SetTag("environmentId", createDto.EnvironmentId);
            activity?.SetTag("agentSchemaName", createDto.AgentSchemaName);
            activity?.SetTag("EvalRunName", createDto.EvalRunName);

            if (!ModelState.IsValid)
            {
                activity?.SetTag("validation.failed", true);
                activity?.SetTag("validation.errorCount", ModelState.ErrorCount);
                return CreateValidationErrorResponse<EvalRunDto>();
            }

            
            _logger.LogInformation($"Creating evaluation run for AgentId: {createDto.AgentId}, DataSetId: {createDto.DataSetId}, Type: {createDto.Type}, EnvironmentId: {createDto.EnvironmentId}");

            var evalRun = await _evalRunRequestHandler.CreateEvalRunAsync(createDto);

            // Add telemetry tags for successful creation
            activity?.SetTag("evalRunId", evalRun.EvalRunId.ToString());
            activity?.SetTag("success", true);
            activity?.SetTag("http.status_code", 201);

            stopwatch.Stop();
            activity?.SetTag("duration_ms", stopwatch.ElapsedMilliseconds);

            _logger.LogInformation("Successfully created evaluation run with ID: {EvalRunId} in {Duration}ms",
                  evalRun.EvalRunId, stopwatch.ElapsedMilliseconds);

            return CreatedAtAction(nameof(GetEvalRun), new { evalRunId = evalRun.EvalRunId }, evalRun);
        }
        catch(ValidationException ex)
        {
            stopwatch.Stop();
            activity?.SetTag("success", false);
            activity?.SetTag("error.type", "ValidationException");
            activity?.SetTag("error.message", ex.Message);
            activity?.SetTag("duration_ms", stopwatch.ElapsedMilliseconds);
            _logger.LogWarning(ex, $"Validation error occurred while creating evaluation run. Duration: {stopwatch.ElapsedMilliseconds}ms");
            return CreateErrorResponse<EvalRunDto>(ex.Message, StatusCodes.Status400BadRequest);
        }
        catch (RequestFailedException ex)
        {
            stopwatch.Stop();
            activity?.SetTag("success", false);
            activity?.SetTag("error.type", "AzureRequestFailed");
            activity?.SetTag("error.message", ex.Message);
            activity?.SetTag("error.status", ex.Status);
            activity?.SetTag("duration_ms", stopwatch.ElapsedMilliseconds);
            activity?.SetTag("http.status_code", ex.Status);
            
            _logger.LogError(ex, $"Azure error occurred while creating evaluation run. Status: {ex.Status}, Duration: {stopwatch.ElapsedMilliseconds}ms");

            return HandleAzureException<EvalRunDto>(ex, "Failed to create evaluation run");
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            activity?.SetTag("success", false);
            activity?.SetTag("error.type", ex.GetType().Name);
            activity?.SetTag("error.message", ex.Message);
            activity?.SetTag("duration_ms", stopwatch.ElapsedMilliseconds);

            if (ex is RequestFailedException azEx && (azEx.Status == 401 || azEx.Status == 403))
            {
                activity?.SetTag("error.category", "Authorization");
                activity?.SetTag("http.status_code", 403);

                _logger.LogWarning(ex, $"Authorization error occurred while creating evaluation run. Duration: {stopwatch.ElapsedMilliseconds}ms");

                return CreateErrorResponse<EvalRunDto>("Access denied. Authorization failed.", StatusCodes.Status403Forbidden);
            }

            activity?.SetTag("error.category", "UnexpectedError");
            activity?.SetTag("http.status_code", 500);

            _logger.LogError(ex, $"Error occurred while creating evaluation run. Duration: {stopwatch.ElapsedMilliseconds}ms");

            return CreateErrorResponse<EvalRunDto>("Failed to create evaluation run", StatusCodes.Status500InternalServerError);
        }
    }

    /// <summary>
    /// Get evaluation run by ID
    /// </summary>
    /// <param name="evalRunId">Evaluation run ID</param>
    /// <returns>Evaluation run details</returns>
    /// <response code="200">Evaluation run found</response>
    /// <response code="404">Evaluation run not found</response>
    /// <response code="500">Internal server error</response>
    [HttpGet("{evalRunId}")]
    [ProducesResponseType(typeof(EvalRunDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<EvalRunDto>> GetEvalRun(Guid evalRunId)
    {
        using var activity = _telemetryService?.StartActivity("EvalRunsController.GetEvalRun");

        var stopwatch = Stopwatch.StartNew();

        try
        {
             // Validate EvalRunId
             if (evalRunId == Guid.Empty)
             {
                return CreateFieldValidationError<EvalRunDto>("evalRunId", "Evaluation run ID is required and must be a valid GUID");
                }

            _logger.LogInformation("Retrieving evaluation run with ID: {EvalRunId}", evalRunId);

            var evalRun = await _evalRunRequestHandler.GetEvalRunByIdAsync(evalRunId);

            if (evalRun == null)
            {
                return NotFound($"Evaluation run with ID {evalRunId} not found");
            }

            return Ok(evalRun);
        }
        catch (RequestFailedException ex)
        {
            _logger.LogError(ex, "Azure error occurred while retrieving evaluation run with ID: {EvalRunId}", evalRunId);
            return HandleAzureException<EvalRunDto>(ex, "Failed to retrieve evaluation run");
        }
        catch (Exception ex)
        {
            if (ex is RequestFailedException azEx && (azEx.Status == 401 || azEx.Status == 403))
            {
                _logger.LogWarning(ex, "Authorization error occurred while retrieving evaluation run with ID: {EvalRunId}", evalRunId);
                return CreateErrorResponse<EvalRunDto>("Access denied. Authorization failed.", StatusCodes.Status403Forbidden);
            }

            _logger.LogError(ex, "Error occurred while retrieving evaluation run with ID: {EvalRunId}", evalRunId);
            return CreateErrorResponse<EvalRunDto>("Failed to retrieve evaluation run", StatusCodes.Status500InternalServerError);
        }
    }

    /// <summary>
    /// Get all evaluation runs for a specific agent
    /// </summary>
    /// <param name="agentId">Agent ID</param>
    /// <param name="startDateTime">Optional: Start date and time (ISO 8601 format)</param>
    /// <param name="endDateTime">Optional: End date and time (ISO 8601 format)</param>
    /// <returns>List of evaluation runs for the agent</returns>
    /// <response code="200">Evaluation runs retrieved successfully</response>
    /// <response code="400">Invalid AgentId</response>
    /// <response code="500">Internal server error</response>
    [HttpGet]
    [ProducesResponseType(typeof(List<EvalRunDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<List<EvalRunDto>>> GetEvalRunsByAgent([FromQuery, Required] string agentId,
                                                                         [FromQuery, Optional] DateTime? startDateTime,
                                                                         [FromQuery, Optional] DateTime? endDateTime)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(agentId))
            {
                return CreateFieldValidationError<List<EvalRunDto>>("agentId", "AgentId is required and cannot be empty");
            }

            if (startDateTime.HasValue && endDateTime.HasValue && startDateTime > endDateTime)
            {
                return CreateFieldValidationError<List<EvalRunDto>>("dateRange", "StartDateTime cannot be later than EndDateTime");
            }

            _logger.LogInformation("Retrieving evaluation runs for AgentId: {AgentId}", agentId);

            var evalRuns = await _evalRunRequestHandler.GetEvalRunsByAgentIdAsync(agentId, startDateTime, endDateTime);

            return Ok(evalRuns);
        }
        catch (RequestFailedException ex)
        {
            _logger.LogError(ex, "Azure error occurred while retrieving evaluation runs for AgentId: {AgentId}", agentId);
            return HandleAzureException<List<EvalRunDto>>(ex, "Failed to retrieve evaluation runs");
        }
        catch (Exception ex)
        {
            if (ex is RequestFailedException azEx && (azEx.Status == 401 || azEx.Status == 403))
            {
                _logger.LogWarning(ex, "Authorization error occurred while retrieving evaluation runs for AgentId: {AgentId}", agentId);
                return CreateErrorResponse<List<EvalRunDto>>("Access denied. Authorization failed.", StatusCodes.Status403Forbidden);
            }

            _logger.LogError(ex, "Error occurred while retrieving evaluation runs for AgentId: {AgentId}", agentId);
            return CreateErrorResponse<List<EvalRunDto>>("Failed to retrieve evaluation runs", StatusCodes.Status500InternalServerError);
        }
    }

    #endregion
}
