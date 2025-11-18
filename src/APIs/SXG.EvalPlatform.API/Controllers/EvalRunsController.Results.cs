using Azure;
using Microsoft.AspNetCore.Mvc;
using SxgEvalPlatformApi.Models;
using SxgEvalPlatformApi.Models.Dtos;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;

namespace SxgEvalPlatformApi.Controllers;

/// <summary>
/// Partial class containing evaluation results operations
/// </summary>
public partial class EvalRunsController
{
    #region Evaluation Results Endpoints

    /// <summary>
    /// Save evaluation results for a specific evaluation run
    /// </summary>
    /// <param name="evalRunId">Evaluation run ID from route</param>
    /// <param name="saveDto">Evaluation result data containing EvaluationRecords</param>
    /// <returns>Save operation result</returns>
    /// <response code="200">Evaluation results saved successfully</response>
    /// <response code="400">Invalid input data, EvalRunId not found, or evaluation run status is not terminal (must be 'Completed' or 'Failed')</response>
    /// <response code="403">Access denied - authorization failed</response>
    /// <response code="500">Internal server error</response>
    [HttpPost("{evalRunId}/results")]
    [ProducesResponseType(typeof(EvaluationResultSaveResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<EvaluationResultSaveResponseDto>> SaveEvaluationResult([FromRoute, Required] Guid evalRunId, [FromBody, Required] SaveEvaluationResultDto saveDto)
    {
        using var activity = _telemetryService?.StartActivity("EvalRunsController.SaveEvaluationResult");
        var stopwatch = Stopwatch.StartNew();

        try
        {
            // Add telemetry tags
            activity?.SetTag("evalRunId", evalRunId.ToString());
            activity?.SetTag("operation", "SaveEvaluationResult");
            activity?.SetTag("hasSummary", saveDto.EvaluationResultSummary.ValueKind != System.Text.Json.JsonValueKind.Undefined);
            activity?.SetTag("hasDataset", saveDto.EvaluationResultDataset.ValueKind != System.Text.Json.JsonValueKind.Undefined);

            if (!ModelState.IsValid)
            {
                stopwatch.Stop();
                activity?.SetTag("success", false);
                activity?.SetTag("error.type", "ValidationFailed");
                activity?.SetTag("validation.errorCount", ModelState.ErrorCount);
                activity?.SetTag("http.status_code", 400);
                activity?.SetTag("duration_ms", stopwatch.ElapsedMilliseconds);

                _logger.LogWarning("Invalid model state for save evaluation result - EvalRunId: {EvalRunId}, Duration: {Duration}ms",
                      evalRunId, stopwatch.ElapsedMilliseconds);

                return CreateValidationErrorResponse<EvaluationResultSaveResponseDto>();
            }

            _logger.LogInformation("Saving evaluation results for EvalRunId: {EvalRunId}", evalRunId);

            var result = await _evaluationResultRequestHandler.SaveEvaluationResultAsync(evalRunId, saveDto);

            bool isSuccessful = result.RequestProcesingResult!.IsSuccessful;

            if (!isSuccessful)
            {
                stopwatch.Stop();
                activity?.SetTag("success", false);
                activity?.SetTag("error.type", "SaveFailed");
                activity?.SetTag("error.message", result.RequestProcesingResult.Message);
                activity?.SetTag("http.status_code", (int)result.RequestProcesingResult.StatusCode);
                activity?.SetTag("duration_ms", stopwatch.ElapsedMilliseconds);

                _logger.LogError("Failed to save evaluation results - EvalRunId: {EvalRunId}, Message: {Message}, Duration: {Duration}ms",
       evalRunId, result.RequestProcesingResult.Message, stopwatch.ElapsedMilliseconds);

                return CreateErrorResponse<EvaluationResultSaveResponseDto>(
      result.RequestProcesingResult.Message, (int)result.RequestProcesingResult.StatusCode);
            }

            // Success telemetry
            stopwatch.Stop();
            activity?.SetTag("success", true);
            activity?.SetTag("http.status_code", 201);
            activity?.SetTag("result.evalRunId", result.EvalResponse.EvalRunId.ToString());
            activity?.SetTag("duration_ms", stopwatch.ElapsedMilliseconds);

            _logger.LogInformation("Successfully saved evaluation results - EvalRunId: {EvalRunId}, Duration: {Duration}ms",
  evalRunId, stopwatch.ElapsedMilliseconds);

            // FIX: Use evalRunId instead of configurationId to match the GetEvaluationResult route parameter
            return CreatedAtAction(nameof(GetEvaluationResult), new { evalRunId = result.EvalResponse.EvalRunId }, result);

        }
        catch (RequestFailedException ex)
        {
            stopwatch.Stop();
            activity?.SetTag("success", false);
            activity?.SetTag("error.type", "AzureRequestFailed");
            activity?.SetTag("error.message", ex.Message);
            activity?.SetTag("error.status", ex.Status);
            activity?.SetTag("http.status_code", ex.Status);
            activity?.SetTag("duration_ms", stopwatch.ElapsedMilliseconds);

            _logger.LogError(ex, "Azure error occurred while saving evaluation results for EvalRunId: {EvalRunId}, Status: {Status}, Duration: {Duration}ms",
                    evalRunId, ex.Status, stopwatch.ElapsedMilliseconds);

            return HandleAzureException<EvaluationResultSaveResponseDto>(ex, "Failed to save evaluation results");
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

   _logger.LogWarning(ex, "Authorization error occurred while saving evaluation results for EvalRunId: {EvalRunId}, Duration: {Duration}ms",
         evalRunId, stopwatch.ElapsedMilliseconds);

       return CreateErrorResponse<EvaluationResultSaveResponseDto>("Access denied. Authorization failed.", StatusCodes.Status403Forbidden);
            }

            activity?.SetTag("error.category", "UnexpectedError");
            activity?.SetTag("http.status_code", 500);

            _logger.LogError(ex, "Error occurred while saving evaluation results for EvalRunId: {EvalRunId}, Duration: {Duration}ms",
          evalRunId, stopwatch.ElapsedMilliseconds);

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
    /// <response code="404">Evaluation results not found or evaluation run not completed</response>
    /// <response code="500">Internal server error</response>
    [HttpGet("{evalRunId}/results")]
    [ProducesResponseType(typeof(EvaluationResultResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<EvaluationResultResponseDto>> GetEvaluationResult([FromRoute, Required] Guid evalRunId)
    {
        using var activity = _telemetryService?.StartActivity("EvalRunsController.GetEvaluationResult");
        var stopwatch = Stopwatch.StartNew();

        try
        {
            // Add telemetry tags
            activity?.SetTag("evalRunId", evalRunId.ToString());
            activity?.SetTag("operation", "GetEvaluationResult");

            // Validate EvalRunId
            if (evalRunId == Guid.Empty)
            {
                stopwatch.Stop();
                activity?.SetTag("success", false);
                activity?.SetTag("error.type", "ValidationFailed");
                activity?.SetTag("error.message", "Invalid EvalRunId");
                activity?.SetTag("http.status_code", 400);
                activity?.SetTag("duration_ms", stopwatch.ElapsedMilliseconds);

                _logger.LogWarning("Invalid EvalRunId validation failed - EvalRunId: {EvalRunId}, Duration: {Duration}ms",
             evalRunId, stopwatch.ElapsedMilliseconds);

                return CreateFieldValidationError<EvaluationResultResponseDto>("evalRunId", "Evaluation run ID is required and must be a valid GUID");
            }

            _logger.LogInformation("Retrieving evaluation results for EvalRunId: {EvalRunId}", evalRunId);

            var result = await _evaluationResultRequestHandler.GetEvaluationResultByIdAsync(evalRunId);
            bool isSuccessful = result.RequestProcesingResult!.IsSuccessful;

            if (!isSuccessful)
            {
                stopwatch.Stop();
                activity?.SetTag("success", false);
                activity?.SetTag("error.type", "RetrievalFailed");
                activity?.SetTag("error.message", result.RequestProcesingResult.Message);
                activity?.SetTag("http.status_code", (int)result.RequestProcesingResult.StatusCode);
                activity?.SetTag("duration_ms", stopwatch.ElapsedMilliseconds);

                _logger.LogWarning("Failed to retrieve evaluation results - EvalRunId: {EvalRunId}, Message: {Message}, StatusCode: {StatusCode}, Duration: {Duration}ms",
           evalRunId, result.RequestProcesingResult.Message, (int)result.RequestProcesingResult.StatusCode, stopwatch.ElapsedMilliseconds);

                return CreateErrorResponse<EvaluationResultResponseDto>(
              result.RequestProcesingResult.Message, (int)result.RequestProcesingResult.StatusCode);
            }

            // Success telemetry
            stopwatch.Stop();
            activity?.SetTag("success", true);
            activity?.SetTag("http.status_code", 200);
            activity?.SetTag("result.agentId", result.EvalResponse.AgentId);
            activity?.SetTag("result.datasetId", result.EvalResponse.DataSetId);
            activity?.SetTag("result.status", result.EvalResponse.Status);
            activity?.SetTag("result.hasRecords", result.EvalResponse.EvaluationRecords.HasValue);
            activity?.SetTag("duration_ms", stopwatch.ElapsedMilliseconds);

            _logger.LogInformation("Successfully retrieved evaluation results - EvalRunId: {EvalRunId}, AgentId: {AgentId}, Status: {Status}, Duration: {Duration}ms",
       evalRunId, result.EvalResponse.AgentId, result.EvalResponse.Status, stopwatch.ElapsedMilliseconds);

            return Ok(result.EvalResponse);
        }
        catch (RequestFailedException ex)
        {
            stopwatch.Stop();
            activity?.SetTag("success", false);
            activity?.SetTag("error.type", "AzureRequestFailed");
            activity?.SetTag("error.message", ex.Message);
            activity?.SetTag("error.status", ex.Status);
            activity?.SetTag("http.status_code", ex.Status);
            activity?.SetTag("duration_ms", stopwatch.ElapsedMilliseconds);

            _logger.LogError(ex, "Azure error occurred while retrieving evaluation results for EvalRunId: {EvalRunId}, Status: {Status}, Duration: {Duration}ms",
          evalRunId, ex.Status, stopwatch.ElapsedMilliseconds);

            return HandleAzureException<EvaluationResultResponseDto>(ex, "Failed to retrieve evaluation results");
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

   _logger.LogWarning(ex, "Authorization error occurred while retrieving evaluation results for EvalRunId: {EvalRunId}, Duration: {Duration}ms",
                    evalRunId, stopwatch.ElapsedMilliseconds);

                return CreateErrorResponse<EvaluationResultResponseDto>("Access denied. Authorization failed.", StatusCodes.Status403Forbidden);
            }

            activity?.SetTag("error.category", "UnexpectedError");
            activity?.SetTag("http.status_code", 500);

            _logger.LogError(ex, "Error occurred while retrieving evaluation results for EvalRunId: {EvalRunId}, Duration: {Duration}ms",
                      evalRunId, stopwatch.ElapsedMilliseconds);

            return CreateErrorResponse<EvaluationResultResponseDto>(
             "Failed to retrieve evaluation results", StatusCodes.Status500InternalServerError);
        }
    }

    #endregion
}
