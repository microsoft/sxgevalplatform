using Microsoft.AspNetCore.Mvc;
using SxgEvalPlatformApi.Models;
using SxgEvalPlatformApi.Services;
using Azure;

namespace SxgEvalPlatformApi.Controllers;

/// <summary>
/// Controller for evaluation result operations
/// </summary>
[ApiController]
[Route("api/v1/eval/results")]
public class EvalResultController : BaseController
{
    private readonly IEvaluationResultService _evaluationResultService;

    public EvalResultController(
        IEvaluationResultService evaluationResultService, 
        ILogger<EvalResultController> logger)
        : base(logger)
    {
        _evaluationResultService = evaluationResultService;
    }

    /// <summary>
    /// Save evaluation results for a specific evaluation run
    /// </summary>
    /// <param name="saveDto">Evaluation result data containing EvalRunId, FileName, and EvaluationRecords</param>
    /// <returns>Save operation result</returns>
    /// <response code="200">Evaluation results saved successfully</response>
    /// <response code="400">Invalid input data, EvalRunId not found, or evaluation run status is not terminal (must be 'Completed' or 'Failed')</response>
    /// <response code="403">Access denied - authorization failed</response>
    /// <response code="500">Internal server error</response>
    [HttpPost]
    [ProducesResponseType(typeof(EvaluationResultSaveResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<EvaluationResultSaveResponseDto>> SaveEvaluationResult([FromBody] SaveEvaluationResultDto saveDto)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return CreateValidationErrorResponse<EvaluationResultSaveResponseDto>();
            }

            _logger.LogInformation("Saving evaluation results for EvalRunId: {EvalRunId}", saveDto.EvalRunId);

            var result = await _evaluationResultService.SaveEvaluationResultAsync(saveDto);
            
            if (!result.Success)
            {
                if (result.Message.Contains("not found"))
                {
                    return BadRequest(result);
                }
                if (result.Message.Contains("Cannot save evaluation results") && result.Message.Contains("status"))
                {
                    return BadRequest(result);
                }
                return StatusCode(StatusCodes.Status500InternalServerError, result);
            }

            return Ok(result);
        }
        catch (RequestFailedException ex)
        {
            _logger.LogError(ex, "Azure error occurred while saving evaluation results for EvalRunId: {EvalRunId}", 
                saveDto.EvalRunId);
            return HandleAzureException<EvaluationResultSaveResponseDto>(ex, "Failed to save evaluation results");
        }
        catch (Exception ex)
        {
            if (IsAuthorizationError(ex))
            {
                _logger.LogWarning(ex, "Authorization error occurred while saving evaluation results for EvalRunId: {EvalRunId}", 
                    saveDto.EvalRunId);
                return CreateErrorResponse<EvaluationResultSaveResponseDto>("Access denied. Authorization failed.", StatusCodes.Status403Forbidden);
            }
            
            _logger.LogError(ex, "Error occurred while saving evaluation results for EvalRunId: {EvalRunId}", 
                saveDto.EvalRunId);
            return CreateErrorResponse<EvaluationResultSaveResponseDto>(
                "Failed to save evaluation results", StatusCodes.Status500InternalServerError);
        }
    }

    /// <summary>
    /// Get evaluation results by EvalRunId
    /// </summary>
    /// <param name="evalRunId">Evaluation run ID</param>
    /// <returns>Evaluation results data</returns>
    /// <response code="200">Evaluation results retrieved successfully</response>
    /// <response code="400">Invalid EvalRunId</response>
    /// <response code="404">Evaluation results not found</response>
    /// <response code="500">Internal server error</response>
    [HttpGet("{evalRunId}")]
    [ProducesResponseType(typeof(EvaluationResultResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<EvaluationResultResponseDto>> GetEvaluationResult(Guid evalRunId)
    {
        try
        {
            if (evalRunId == Guid.Empty)
            {
                return BadRequest("EvalRunId is required and must be a valid GUID");
            }

            _logger.LogInformation("Retrieving evaluation results for EvalRunId: {EvalRunId}", evalRunId);

            var result = await _evaluationResultService.GetEvaluationResultByIdAsync(evalRunId);
            
            if (!result.Success)
            {
                if (result.Message.Contains("not found") && result.Message.Contains("EvalRunId"))
                {
                    return BadRequest(result);
                }
                else if (result.Message.Contains("not found") || result.Message.Contains("hasn't completed"))
                {
                    return NotFound(result);
                }
                return StatusCode(StatusCodes.Status500InternalServerError, result);
            }

            return Ok(result);
        }
        catch (RequestFailedException ex)
        {
            _logger.LogError(ex, "Azure error occurred while retrieving evaluation results for EvalRunId: {EvalRunId}", 
                evalRunId);
            return HandleAzureException<EvaluationResultResponseDto>(ex, "Failed to retrieve evaluation results");
        }
        catch (Exception ex)
        {
            if (IsAuthorizationError(ex))
            {
                _logger.LogWarning(ex, "Authorization error occurred while retrieving evaluation results for EvalRunId: {EvalRunId}", 
                    evalRunId);
                return CreateErrorResponse<EvaluationResultResponseDto>("Access denied. Authorization failed.", StatusCodes.Status403Forbidden);
            }
            
            _logger.LogError(ex, "Error occurred while retrieving evaluation results for EvalRunId: {EvalRunId}", 
                evalRunId);
            return CreateErrorResponse<EvaluationResultResponseDto>(
                "Failed to retrieve evaluation results", StatusCodes.Status500InternalServerError);
        }
    }

    /// <summary>
    /// Get all evaluation runs for a specific agent
    /// </summary>
    /// <param name="agentId">Agent ID</param>
    /// <returns>List of evaluation runs for the agent</returns>
    /// <response code="200">Evaluation runs retrieved successfully</response>
    /// <response code="400">Invalid AgentId</response>
    /// <response code="500">Internal server error</response>
    [HttpGet("agent/{agentId}")]
    [ProducesResponseType(typeof(List<EvalRunDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<List<EvalRunDto>>> GetEvalRunsByAgent(string agentId)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(agentId))
            {
                return BadRequest("AgentId is required and cannot be empty");
            }

            _logger.LogInformation("Retrieving evaluation runs for AgentId: {AgentId}", agentId);

            var evalRuns = await _evaluationResultService.GetEvalRunsByAgentIdAsync(agentId);
            
            return Ok(evalRuns);
        }
        catch (RequestFailedException ex)
        {
            _logger.LogError(ex, "Azure error occurred while retrieving evaluation runs for AgentId: {AgentId}", agentId);
            return HandleAzureException<List<EvalRunDto>>(ex, "Failed to retrieve evaluation runs");
        }
        catch (Exception ex)
        {
            if (IsAuthorizationError(ex))
            {
                _logger.LogWarning(ex, "Authorization error occurred while retrieving evaluation runs for AgentId: {AgentId}", agentId);
                return CreateErrorResponse<List<EvalRunDto>>("Access denied. Authorization failed.", StatusCodes.Status403Forbidden);
            }
            
            _logger.LogError(ex, "Error occurred while retrieving evaluation runs for AgentId: {AgentId}", agentId);
            return CreateErrorResponse<List<EvalRunDto>>("Failed to retrieve evaluation runs", StatusCodes.Status500InternalServerError);
        }
    }

    /// <summary>
    /// Get evaluation results for a specific agent within a date range
    /// </summary>
    /// <param name="agentId">Agent ID</param>
    /// <param name="startDateTime">Start date and time (ISO 8601 format)</param>
    /// <param name="endDateTime">End date and time (ISO 8601 format)</param>
    /// <returns>List of evaluation results within the specified date range</returns>
    /// <response code="200">Evaluation results retrieved successfully</response>
    /// <response code="400">Invalid parameters</response>
    /// <response code="500">Internal server error</response>
    [HttpGet("agent/{agentId}/daterange")]
    [ProducesResponseType(typeof(List<EvaluationResultResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<List<EvaluationResultResponseDto>>> GetEvaluationResultsByDateRange(
        string agentId, 
        [FromQuery] DateTime startDateTime, 
        [FromQuery] DateTime endDateTime)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(agentId))
            {
                return BadRequest("AgentId is required and cannot be empty");
            }

            if (startDateTime >= endDateTime)
            {
                return BadRequest("StartDateTime must be earlier than EndDateTime");
            }

            if (endDateTime > DateTime.UtcNow)
            {
                return BadRequest("EndDateTime cannot be in the future");
            }

            _logger.LogInformation("Retrieving evaluation results for AgentId: {AgentId} between {StartDateTime} and {EndDateTime}", 
                agentId, startDateTime, endDateTime);

            var results = await _evaluationResultService.GetEvaluationResultsByDateRangeAsync(agentId, startDateTime, endDateTime);
            
            return Ok(results);
        }
        catch (RequestFailedException ex)
        {
            _logger.LogError(ex, "Azure error occurred while retrieving evaluation results for AgentId: {AgentId} between {StartDateTime} and {EndDateTime}", 
                agentId, startDateTime, endDateTime);
            return HandleAzureException<List<EvaluationResultResponseDto>>(ex, "Failed to retrieve evaluation results");
        }
        catch (Exception ex)
        {
            if (IsAuthorizationError(ex))
            {
                _logger.LogWarning(ex, "Authorization error occurred while retrieving evaluation results for AgentId: {AgentId} between {StartDateTime} and {EndDateTime}", 
                    agentId, startDateTime, endDateTime);
                return CreateErrorResponse<List<EvaluationResultResponseDto>>("Access denied. Authorization failed.", StatusCodes.Status403Forbidden);
            }
            
            _logger.LogError(ex, "Error occurred while retrieving evaluation results for AgentId: {AgentId} between {StartDateTime} and {EndDateTime}", 
                agentId, startDateTime, endDateTime);
            return CreateErrorResponse<List<EvaluationResultResponseDto>>("Failed to retrieve evaluation results", StatusCodes.Status500InternalServerError);
        }
    }
}