using Azure;
using Microsoft.AspNetCore.Mvc;
using SXG.EvalPlatform.Common;
using SxgEvalPlatformApi.Models;
using SxgEvalPlatformApi.Models.Dtos;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;

namespace SxgEvalPlatformApi.Controllers;

/// <summary>
/// Partial class containing evaluation run status operations
/// </summary>
public partial class EvalRunsController
{
    #region Eval Run Status Endpoints

    /// <summary>
    /// Update evaluation run status
    /// Note: Once an evaluation run reaches a terminal state (Completed or Failed), 
    /// its status cannot be updated anymore.
    /// </summary>
    /// <param name="evalRunId">Evaluation run ID from route parameter</param>
    /// <param name="updateDto">Status update data containing only the new Status</param>
    /// <returns>Updated evaluation run</returns>
    /// <response code="200">Status updated successfully</response>
    /// <response code="400">Invalid input data or evaluation run is in terminal state</response>
    /// <response code="404">Evaluation run not found</response>
    /// <response code="500">Internal server error</response>
    [HttpPut("{evalRunId}/status")]
    [ProducesResponseType(typeof(UpdateResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<UpdateResponseDto>> UpdateEvalRun([FromRoute, Required] Guid evalRunId, [FromBody] UpdateStatusDto updateDto)
    {
        using var activity = _telemetryService?.StartActivity("EvalRunsController.UpdateEvalRunStatus");
        var stopwatch = Stopwatch.StartNew();

        try
        {
            // Add telemetry tags for request parameters
            activity?.SetTag("evalRunId", CommonUtils.SanitizeForLog(evalRunId.ToString()));
            activity?.SetTag("newStatus", CommonUtils.SanitizeForLog(updateDto.Status));
            activity?.SetTag("operation", "UpdateEvalRunStatus");

            if (!ModelState.IsValid)
            {
                activity?.SetTag("validation.failed", true);
                activity?.SetTag("validation.errorCount", ModelState.ErrorCount);
                return CreateValidationErrorResponse<UpdateResponseDto>();
            }

            // First, get the current evaluation run to check its status
            // Use the cross-partition search since we don't have AgentId in the request
            var currentEvalRun = await _evalRunRequestHandler.GetEvalRunByIdAsync(evalRunId);
            if (currentEvalRun == null)
            {
                stopwatch.Stop();
                activity?.SetTag("success", false);
                activity?.SetTag("error.type", "NotFound");
                activity?.SetTag("http.status_code", 404);
                activity?.SetTag("duration_ms", stopwatch.ElapsedMilliseconds);

                _logger.LogWarning("Evaluation run not found for status update - EvalRunId: {EvalRunId}, Duration: {Duration}ms",
                    CommonUtils.SanitizeForLog(evalRunId.ToString()), stopwatch.ElapsedMilliseconds);

                return NotFound(new UpdateResponseDto
                {
                    Success = false,
                    Message = $"Evaluation run with ID {evalRunId} not found"
                });
            }

            // Add current status to telemetry
            activity?.SetTag("currentStatus", CommonUtils.SanitizeForLog(currentEvalRun.Status));
            activity?.SetTag("agentId", CommonUtils.SanitizeForLog(currentEvalRun.AgentId));

            // Check if the current status is already in a terminal state
            var terminalStatuses = new[] { CommonConstants.EvalRunStatus.EvalRunCompleted };
            if (terminalStatuses.Any(status => string.Equals(status, currentEvalRun.Status, StringComparison.OrdinalIgnoreCase)))
            {
                stopwatch.Stop();
                activity?.SetTag("success", false);
                activity?.SetTag("error.type", "TerminalStateViolation");
                activity?.SetTag("error.message", "Cannot update terminal state");
                activity?.SetTag("terminalState", CommonUtils.SanitizeForLog(currentEvalRun.Status));
                activity?.SetTag("http.status_code", 400);
                activity?.SetTag("duration_ms", stopwatch.ElapsedMilliseconds);

                _logger.LogWarning("Attempted to update terminal state - EvalRunId: {EvalRunId}, CurrentStatus: {CurrentStatus}, Duration: {Duration}ms",
                    CommonUtils.SanitizeForLog(evalRunId.ToString()), 
                    CommonUtils.SanitizeForLog(currentEvalRun.Status), 
                    stopwatch.ElapsedMilliseconds);

                return BadRequest(new UpdateResponseDto
                {
                    Success = false,
                    Message = $"Cannot update status for evaluation run with ID {evalRunId}. " +
                              $"The evaluation run is already in a terminal state '{currentEvalRun.Status}' and cannot be modified."
                });
            }

            // Validate status value
            var validStatuses = new[]
            {
                CommonConstants.EvalRunStatus.RequestSubmitted,
                CommonConstants.EvalRunStatus.EnrichingDataset,
                CommonConstants.EvalRunStatus.DatasetEnrichmentCompleted,
                CommonConstants.EvalRunStatus.EvalRunStarted,
                CommonConstants.EvalRunStatus.EvalRunCompleted,
                CommonConstants.EvalRunStatus.EvalRunFailed
            };

            if (!validStatuses.Any(status => string.Equals(status, updateDto.Status, StringComparison.OrdinalIgnoreCase)))
            {
                stopwatch.Stop();
                activity?.SetTag("success", false);
                activity?.SetTag("error.type", "InvalidStatus");
                activity?.SetTag("error.message", "Invalid status value");
                activity?.SetTag("http.status_code", 400);
                activity?.SetTag("duration_ms", stopwatch.ElapsedMilliseconds);

                _logger.LogWarning("Invalid status value - EvalRunId: {EvalRunId}, InvalidStatus: {InvalidStatus}, Duration: {Duration}ms",
                    CommonUtils.SanitizeForLog(evalRunId.ToString()),
                    CommonUtils.SanitizeForLog(updateDto.Status),
                    stopwatch.ElapsedMilliseconds);

                return CreateFieldValidationError<UpdateResponseDto>("Status", $"Invalid status. Valid values are: {string.Join(", ", validStatuses)}");
            }

            _logger.LogInformation("Updating evaluation run status to {Status} for ID: {EvalRunId}",
                CommonUtils.SanitizeForLog(updateDto.Status),
                CommonUtils.SanitizeForLog(evalRunId.ToString()));

            // Create the service DTO with the information we have
            var serviceUpdateDto = new UpdateEvalRunStatusDto
            {
                EvalRunId = evalRunId,
                Status = updateDto.Status,
                AgentId = currentEvalRun.AgentId // Get AgentId from the current evaluation run
            };

            var updatedEvalRun = await _evalRunRequestHandler.UpdateEvalRunStatusAsync(serviceUpdateDto);

            // Since we already validated the evaluation run exists above, this should not be null
            // But we'll still check for safety
            if (updatedEvalRun == null)
            {
                stopwatch.Stop();
                activity?.SetTag("success", false);
                activity?.SetTag("error.type", "UnexpectedNullResult");
                activity?.SetTag("error.message", "Null result from update operation");
                activity?.SetTag("http.status_code", 500);
                activity?.SetTag("duration_ms", stopwatch.ElapsedMilliseconds);

                _logger.LogError("Unexpected null result from UpdateEvalRunStatusAsync for EvalRunId: {EvalRunId}, Duration: {Duration}ms",
                    CommonUtils.SanitizeForLog(evalRunId.ToString()),
                    stopwatch.ElapsedMilliseconds);

                return StatusCode(StatusCodes.Status500InternalServerError, new UpdateResponseDto
                {
                    Success = false,
                    Message = "An unexpected error occurred while updating the evaluation run status"
                });
            }

            // Success telemetry
            stopwatch.Stop();
            activity?.SetTag("success", true);
            activity?.SetTag("http.status_code", 200);
            activity?.SetTag("statusTransition", CommonUtils.SanitizeForLog($"{currentEvalRun.Status} → {updateDto.Status}"));
            activity?.SetTag("duration_ms", stopwatch.ElapsedMilliseconds);

            _logger.LogInformation("Successfully updated evaluation run status - EvalRunId: {EvalRunId}, Transition: {Transition}, Duration: {Duration}ms",
                CommonUtils.SanitizeForLog(evalRunId.ToString()),
                CommonUtils.SanitizeForLog($"{currentEvalRun.Status} → {updateDto.Status}"),
                stopwatch.ElapsedMilliseconds);

            return Ok(new UpdateResponseDto
            {
                Success = true,
                Message = $"Evaluation run status updated successfully to {updateDto.Status}"
            });
        }
        catch (RequestFailedException ex)
        {
            stopwatch.Stop();
            activity?.SetTag("success", false);
            activity?.SetTag("error.type", "AzureRequestFailed");
            activity?.SetTag("error.message", CommonUtils.SanitizeForLog(ex.Message));
            activity?.SetTag("error.status", ex.Status);
            activity?.SetTag("duration_ms", stopwatch.ElapsedMilliseconds);
            activity?.SetTag("http.status_code", ex.Status);

            _logger.LogError(ex, "Azure error occurred while updating evaluation run status - EvalRunId: {EvalRunId}, Status: {Status}, Duration: {Duration}ms",
                CommonUtils.SanitizeForLog(evalRunId.ToString()),
                ex.Status,
                stopwatch.ElapsedMilliseconds);

            return HandleAzureException<UpdateResponseDto>(ex, "Failed to update evaluation run status");
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            activity?.SetTag("success", false);
            activity?.SetTag("error.type", ex.GetType().Name);
            activity?.SetTag("error.message", CommonUtils.SanitizeForLog(ex.Message));
            activity?.SetTag("duration_ms", stopwatch.ElapsedMilliseconds);

            if (ex is RequestFailedException azEx && (azEx.Status == 401 || azEx.Status == 403))
            {
                activity?.SetTag("error.category", "Authorization");
      activity?.SetTag("http.status_code", 403);

                _logger.LogWarning(ex, "Authorization error occurred while updating evaluation run status - EvalRunId: {EvalRunId}, Duration: {Duration}ms",
                    CommonUtils.SanitizeForLog(evalRunId.ToString()),
                    stopwatch.ElapsedMilliseconds);

                return CreateErrorResponse<UpdateResponseDto>("Access denied. Authorization failed.", StatusCodes.Status403Forbidden);
   }

            activity?.SetTag("error.category", "UnexpectedError");
            activity?.SetTag("http.status_code", 500);

            _logger.LogError(ex, "Error occurred while updating evaluation run status - EvalRunId: {EvalRunId}, Duration: {Duration}ms",
                CommonUtils.SanitizeForLog(evalRunId.ToString()),
                stopwatch.ElapsedMilliseconds);

            return CreateErrorResponse<UpdateResponseDto>("Failed to update evaluation run status", StatusCodes.Status500InternalServerError);
        }
    }

    /// <summary>
    /// Get evaluation run status by ID
    /// </summary>
    /// <param name="evalRunId">Evaluation run ID</param>
    /// <returns>Evaluation run status information</returns>
    /// <response code="200">Status retrieved successfully</response>
    /// <response code="404">Evaluation run not found</response>
    /// <response code="500">Internal server error</response>
    [HttpGet("{evalRunId}/status")]
    [ProducesResponseType(typeof(EvalRunStatusDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<EvalRunStatusDto>> GetEvalRunStatus([FromRoute, Required] Guid evalRunId)
    {
        using var activity = _telemetryService?.StartActivity("EvalRunsController.GetEvalRunStatus");
        var stopwatch = Stopwatch.StartNew();

        try
        {
            // Add telemetry tags for request parameters
            activity?.SetTag("evalRunId", CommonUtils.SanitizeForLog(evalRunId.ToString()));
            activity?.SetTag("operation", "GetEvalRunStatus");

            _logger.LogInformation("Retrieving evaluation run status for ID: {EvalRunId}",
                CommonUtils.SanitizeForLog(evalRunId.ToString()));

            var evalRun = await _evalRunRequestHandler.GetEvalRunByIdAsync(evalRunId);

            if (evalRun == null)
            {
                stopwatch.Stop();
                activity?.SetTag("success", false);
                activity?.SetTag("error.type", "NotFound");
                activity?.SetTag("http.status_code", 404);
                activity?.SetTag("duration_ms", stopwatch.ElapsedMilliseconds);

                _logger.LogInformation("Evaluation run status not found - EvalRunId: {EvalRunId}, Duration: {Duration}ms",
                    CommonUtils.SanitizeForLog(evalRunId.ToString()),
                    stopwatch.ElapsedMilliseconds);

                return NotFound($"Evaluation run with ID {evalRunId} not found");
            }

            var statusDto = new EvalRunStatusDto
            {
                EvalRunId = evalRun.EvalRunId,
                Status = evalRun.Status,
                LastUpdatedBy = evalRun.LastUpdatedBy,
                StartedDatetime = evalRun.StartedDatetime,
                CompletedDatetime = evalRun.CompletedDatetime
            };

            // Add status details to telemetry
            stopwatch.Stop();
            activity?.SetTag("success", true);
            activity?.SetTag("http.status_code", 200);
            activity?.SetTag("status", CommonUtils.SanitizeForLog(evalRun.Status));
            activity?.SetTag("agentId", CommonUtils.SanitizeForLog(evalRun.AgentId));
            activity?.SetTag("hasStartedDatetime", evalRun.StartedDatetime.HasValue);
            activity?.SetTag("hasCompletedDatetime", evalRun.CompletedDatetime.HasValue);
            activity?.SetTag("duration_ms", stopwatch.ElapsedMilliseconds);

            _logger.LogInformation("Successfully retrieved evaluation run status - EvalRunId: {EvalRunId}, Status: {Status}, Duration: {Duration}ms",
                CommonUtils.SanitizeForLog(evalRunId.ToString()),
                CommonUtils.SanitizeForLog(evalRun.Status),
                stopwatch.ElapsedMilliseconds);

            return Ok(statusDto);
        }
        catch (RequestFailedException ex)
        {
            stopwatch.Stop();
            activity?.SetTag("success", false);
            activity?.SetTag("error.type", "AzureRequestFailed");
            activity?.SetTag("error.message", CommonUtils.SanitizeForLog(ex.Message));
            activity?.SetTag("error.status", ex.Status);
            activity?.SetTag("duration_ms", stopwatch.ElapsedMilliseconds);
            activity?.SetTag("http.status_code", ex.Status);

            _logger.LogError(ex, "Azure error occurred while retrieving evaluation run status - EvalRunId: {EvalRunId}, Status: {Status}, Duration: {Duration}ms",
                CommonUtils.SanitizeForLog(evalRunId.ToString()),
                ex.Status,
                stopwatch.ElapsedMilliseconds);

            return HandleAzureException<EvalRunStatusDto>(ex, "Failed to retrieve evaluation run status");
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            activity?.SetTag("success", false);
            activity?.SetTag("error.type", ex.GetType().Name);
            activity?.SetTag("error.message", CommonUtils.SanitizeForLog(ex.Message));
            activity?.SetTag("duration_ms", stopwatch.ElapsedMilliseconds);

            if (ex is RequestFailedException azEx && (azEx.Status == 401 || azEx.Status == 403))
            {
                activity?.SetTag("error.category", "Authorization");
                activity?.SetTag("http.status_code", 403);

                _logger.LogWarning(ex, "Authorization error occurred while retrieving evaluation run status - EvalRunId: {EvalRunId}, Duration: {Duration}ms",
                    CommonUtils.SanitizeForLog(evalRunId.ToString()),
                    stopwatch.ElapsedMilliseconds);

                return CreateErrorResponse<EvalRunStatusDto>("Access denied. Authorization failed.", StatusCodes.Status403Forbidden);
            }

            activity?.SetTag("error.category", "UnexpectedError");
            activity?.SetTag("http.status_code", 500);

            _logger.LogError(ex, "Error occurred while retrieving evaluation run status - EvalRunId: {EvalRunId}, Duration: {Duration}ms",
         evalRunId, stopwatch.ElapsedMilliseconds);

            return CreateErrorResponse<EvalRunStatusDto>("Failed to retrieve evaluation run status", StatusCodes.Status500InternalServerError);
        }
    }

    #endregion
}
