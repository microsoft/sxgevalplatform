using Azure;
using Microsoft.AspNetCore.Mvc;
using SxgEvalPlatformApi.Models;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;

namespace SxgEvalPlatformApi.Controllers;

/// <summary>
/// Partial class containing enriched dataset operations
/// </summary>
public partial class EvalRunsController
{
    #region Enriched Dataset Endpoints

    /// <summary>
    /// Store enriched dataset content for an evaluation run
    /// </summary>
    /// <param name="evalRunId">Evaluation run ID</param>
    /// <param name="createDto">Enriched dataset JSON content</param>
    /// <returns>Success response with blob storage path</returns>
    /// <response code="201">Enriched dataset stored successfully</response>
    /// <response code="400">Invalid input data or evaluation run ID</response>
    /// <response code="404">Evaluation run not found</response>
    /// <response code="500">Internal server error</response>
    [HttpPost("{evalRunId}/enriched-dataset")]
    [ProducesResponseType(typeof(EnrichedDatasetResponseDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<EnrichedDatasetResponseDto>> CreateEnrichedDataset([FromRoute, Required] Guid evalRunId, [FromBody] CreateEnrichedDatasetDto createDto)
    {
        using var activity = _telemetryService?.StartActivity("EvalRunsController.CreateEnrichedDataset");
        var stopwatch = Stopwatch.StartNew();

        try
        {
            // Add telemetry tags
            activity?.SetTag("evalRunId", evalRunId.ToString());
            activity?.SetTag("operation", "CreateEnrichedDataset");
            activity?.SetTag("hasEnrichedDataset", createDto.EnrichedDataset.ValueKind != System.Text.Json.JsonValueKind.Undefined);

            var evalRunIdValidation = ValidateEvalRunId(evalRunId);
            if (evalRunIdValidation != null)
            {
                stopwatch.Stop();
                activity?.SetTag("success", false);
                activity?.SetTag("error.type", "ValidationFailed");
                activity?.SetTag("error.message", "Invalid EvalRunId");
                activity?.SetTag("http.status_code", 400);
                activity?.SetTag("duration_ms", stopwatch.ElapsedMilliseconds);

                _logger.LogWarning("Invalid EvalRunId validation failed - EvalRunId: {EvalRunId}, Duration: {Duration}ms",
                  evalRunId, stopwatch.ElapsedMilliseconds);

                return evalRunIdValidation;
            }

            if (!ModelState.IsValid)
            {
                stopwatch.Stop();
                activity?.SetTag("success", false);
                activity?.SetTag("error.type", "ValidationFailed");
                activity?.SetTag("validation.errorCount", ModelState.ErrorCount);
                activity?.SetTag("http.status_code", 400);
                activity?.SetTag("duration_ms", stopwatch.ElapsedMilliseconds);

                _logger.LogWarning("Invalid model state for create enriched dataset - EvalRunId: {EvalRunId}, Duration: {Duration}ms",
                evalRunId, stopwatch.ElapsedMilliseconds);

                return CreateValidationErrorResponse<EnrichedDatasetResponseDto>();
            }

            // Validate that the enriched dataset is not empty
            if (createDto.EnrichedDataset.ValueKind == System.Text.Json.JsonValueKind.Null ||
           createDto.EnrichedDataset.ValueKind == System.Text.Json.JsonValueKind.Undefined)
            {
                stopwatch.Stop();
                activity?.SetTag("success", false);
                activity?.SetTag("error.type", "NullOrUndefinedDataset");
                activity?.SetTag("error.message", "Enriched dataset cannot be null or undefined");
                activity?.SetTag("http.status_code", 400);
                activity?.SetTag("duration_ms", stopwatch.ElapsedMilliseconds);

                _logger.LogWarning("Enriched dataset is null or undefined - EvalRunId: {EvalRunId}, Duration: {Duration}ms",
             evalRunId, stopwatch.ElapsedMilliseconds);

                return CreateBadRequestResponse<EnrichedDatasetResponseDto>("EnrichedDataset", "Enriched dataset cannot be null or undefined");
            }

            _logger.LogInformation("Storing enriched dataset for EvalRunId: {EvalRunId}", evalRunId);

            var response = await _evalArtifactsRequestHandler.StoreEnrichedDatasetAsync(evalRunId, createDto.EnrichedDataset);

            // Success telemetry
            stopwatch.Stop();
            activity?.SetTag("success", true);
            activity?.SetTag("http.status_code", 201);
            activity?.SetTag("response.blobPath", response.BlobPath);
            activity?.SetTag("response.evalRunId", response.EvalRunId.ToString());
            activity?.SetTag("duration_ms", stopwatch.ElapsedMilliseconds);

            _logger.LogInformation("Successfully stored enriched dataset - EvalRunId: {EvalRunId}, BlobPath: {BlobPath}, Duration: {Duration}ms",
     evalRunId, response.BlobPath, stopwatch.ElapsedMilliseconds);

            return CreatedAtAction(
                 nameof(GetEnrichedDatasetArtifact),
            new { evalRunId = evalRunId },
           response);
        }
        catch (InvalidOperationException ex)
        {
            stopwatch.Stop();
            activity?.SetTag("success", false);
            activity?.SetTag("error.type", "InvalidOperation");
            activity?.SetTag("error.message", ex.Message);
            activity?.SetTag("duration_ms", stopwatch.ElapsedMilliseconds);

            _logger.LogError(ex, "Invalid operation while storing enriched dataset for EvalRunId: {EvalRunId}, Duration: {Duration}ms",
               evalRunId, stopwatch.ElapsedMilliseconds);

            if (ex.Message.Contains("not found"))
            {
                activity?.SetTag("http.status_code", 404);
                return NotFound<EnrichedDatasetResponseDto>($"Evaluation run with ID {evalRunId} not found");
            }

            activity?.SetTag("http.status_code", 400);
            return CreateErrorResponse<EnrichedDatasetResponseDto>(ex.Message, StatusCodes.Status400BadRequest);
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

            _logger.LogError(ex, "Azure error occurred while storing enriched dataset for EvalRunId: {EvalRunId}, Status: {Status}, Duration: {Duration}ms",
 evalRunId, ex.Status, stopwatch.ElapsedMilliseconds);

            return HandleAzureException<EnrichedDatasetResponseDto>(ex, "Failed to store enriched dataset");
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            activity?.SetTag("success", false);
            activity?.SetTag("error.type", ex.GetType().Name);
            activity?.SetTag("error.message", ex.Message);
            activity?.SetTag("duration_ms", stopwatch.ElapsedMilliseconds);

            if (IsAuthorizationError(ex))
            {
                activity?.SetTag("error.category", "Authorization");
                activity?.SetTag("http.status_code", 403);

                _logger.LogWarning(ex, "Authorization error occurred while storing enriched dataset for EvalRunId: {EvalRunId}, Duration: {Duration}ms",
        evalRunId, stopwatch.ElapsedMilliseconds);

                return CreateErrorResponse<EnrichedDatasetResponseDto>("Access denied. Authorization failed.", StatusCodes.Status403Forbidden);
            }

            activity?.SetTag("error.category", "UnexpectedError");
            activity?.SetTag("http.status_code", 500);

            _logger.LogError(ex, "Error occurred while storing enriched dataset for EvalRunId: {EvalRunId}, Duration: {Duration}ms",
          evalRunId, stopwatch.ElapsedMilliseconds);

            return CreateErrorResponse<EnrichedDatasetResponseDto>("Failed to store enriched dataset", StatusCodes.Status500InternalServerError);
        }
    }

    /// <summary>
    /// Get enriched dataset content for an evaluation run
    /// </summary>
    /// <param name="evalRunId">Evaluation run ID</param>
    /// <returns>Enriched dataset artifact</returns>
    /// <response code="200">Enriched dataset retrieved successfully</response>
    /// <response code="400">Invalid evaluation run ID</response>
    /// <response code="404">Evaluation run or enriched dataset not found</response>
    /// <response code="500">Internal server error</response>
    [HttpGet("{evalRunId}/enriched-dataset")]
    [ProducesResponseType(typeof(EnrichedDatasetArtifactDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<EnrichedDatasetArtifactDto>> GetEnrichedDatasetArtifact([FromRoute, Required] Guid evalRunId)
    {
        using var activity = _telemetryService?.StartActivity("EvalRunsController.GetEnrichedDatasetArtifact");
        var stopwatch = Stopwatch.StartNew();

        try
        {
            // Add telemetry tags
            activity?.SetTag("evalRunId", evalRunId.ToString());
            activity?.SetTag("operation", "GetEnrichedDatasetArtifact");

            var evalRunIdValidation = ValidateEvalRunId(evalRunId);
            if (evalRunIdValidation != null)
            {
                stopwatch.Stop();
                activity?.SetTag("success", false);
                activity?.SetTag("error.type", "ValidationFailed");
                activity?.SetTag("error.message", "Invalid EvalRunId");
                activity?.SetTag("http.status_code", 400);
                activity?.SetTag("duration_ms", stopwatch.ElapsedMilliseconds);

                _logger.LogWarning("Invalid EvalRunId validation failed - EvalRunId: {EvalRunId}, Duration: {Duration}ms",
            evalRunId, stopwatch.ElapsedMilliseconds);

                return evalRunIdValidation;
            }

            _logger.LogInformation("Retrieving enriched dataset artifact for EvalRunId: {EvalRunId}", evalRunId);

            var artifact = await _evalArtifactsRequestHandler.GetEnrichedDatasetArtifactAsync(evalRunId);

            if (artifact == null)
            {
                stopwatch.Stop();
                activity?.SetTag("success", false);
                activity?.SetTag("error.type", "NotFound");
                activity?.SetTag("http.status_code", 404);
                activity?.SetTag("duration_ms", stopwatch.ElapsedMilliseconds);

                _logger.LogInformation("Enriched dataset not found for EvalRunId: {EvalRunId}, Duration: {Duration}ms",
                       evalRunId, stopwatch.ElapsedMilliseconds);

                return NotFound($"Enriched dataset not found for evaluation run with ID {evalRunId}");
            }

            // Success telemetry
            stopwatch.Stop();
            activity?.SetTag("success", true);
            activity?.SetTag("http.status_code", 200);
            activity?.SetTag("artifact.agentId", artifact.AgentId);
            activity?.SetTag("artifact.hasEnrichedDataset", artifact.EnrichedDataset.HasValue);
            activity?.SetTag("artifact.createdAt", artifact.CreatedAt?.ToString("o"));
            activity?.SetTag("duration_ms", stopwatch.ElapsedMilliseconds);

            _logger.LogInformation("Successfully retrieved enriched dataset artifact - EvalRunId: {EvalRunId}, AgentId: {AgentId}, Duration: {Duration}ms",
    evalRunId, artifact.AgentId, stopwatch.ElapsedMilliseconds);

            return Ok(artifact);
        }
        catch (InvalidOperationException ex)
        {
            stopwatch.Stop();
            activity?.SetTag("success", false);
            activity?.SetTag("error.type", "InvalidOperation");
            activity?.SetTag("error.message", ex.Message);
            activity?.SetTag("http.status_code", 400);
            activity?.SetTag("duration_ms", stopwatch.ElapsedMilliseconds);

            _logger.LogError(ex, "Invalid operation while retrieving enriched dataset artifact for EvalRunId: {EvalRunId}, Duration: {Duration}ms",
evalRunId, stopwatch.ElapsedMilliseconds);

            return CreateErrorResponse<EnrichedDatasetArtifactDto>(ex.Message, StatusCodes.Status400BadRequest);
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

            _logger.LogError(ex, "Azure error occurred while retrieving enriched dataset artifact for EvalRunId: {EvalRunId}, Status: {Status}, Duration: {Duration}ms",
       evalRunId, ex.Status, stopwatch.ElapsedMilliseconds);

            return HandleAzureException<EnrichedDatasetArtifactDto>(ex, "Failed to retrieve enriched dataset artifact");
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            activity?.SetTag("success", false);
            activity?.SetTag("error.type", ex.GetType().Name);
            activity?.SetTag("error.message", ex.Message);
            activity?.SetTag("duration_ms", stopwatch.ElapsedMilliseconds);

            if (IsAuthorizationError(ex))
            {
                activity?.SetTag("error.category", "Authorization");
                activity?.SetTag("http.status_code", 403);

                _logger.LogWarning(ex, "Authorization error occurred while retrieving enriched dataset artifact for EvalRunId: {EvalRunId}, Duration: {Duration}ms",
                     evalRunId, stopwatch.ElapsedMilliseconds);

                return CreateErrorResponse<EnrichedDatasetArtifactDto>("Access denied. Authorization failed.", StatusCodes.Status403Forbidden);
            }

            activity?.SetTag("error.category", "UnexpectedError");
            activity?.SetTag("http.status_code", 500);

            _logger.LogError(ex, "Error occurred while retrieving enriched dataset artifact for EvalRunId: {EvalRunId}, Duration: {Duration}ms",
               evalRunId, stopwatch.ElapsedMilliseconds);

            return CreateErrorResponse<EnrichedDatasetArtifactDto>("Failed to retrieve enriched dataset artifact", StatusCodes.Status500InternalServerError);
        }
    }

    /// <summary>
    /// Enqueue dataset enrichment request to DataVerse API
    /// </summary>
    /// <param name="evalRunId">Evaluation run ID</param>
    /// <returns>Accepted response if successful</returns>
    /// <response code="202">Enrichment request enqueued successfully</response>
    /// <response code="400">Invalid input data</response>
    /// <response code="404">Evaluation run not found</response>
    /// <response code="500">Internal server error</response>
    [HttpPost("{evalRunId}/enrichment-queue")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> EnqueueDatasetEnrichment([FromRoute, Required] Guid evalRunId)
    {
        using var activity = _telemetryService?.StartActivity("EvalRunsController.EnqueueDatasetEnrichment");
        var stopwatch = Stopwatch.StartNew();

        try
        {
            // Add telemetry tags
            activity?.SetTag("evalRunId", evalRunId.ToString());
            activity?.SetTag("operation", "EnqueueDatasetEnrichment");

            var evalRunIdValidation = ValidateEvalRunId(evalRunId);

            if (evalRunIdValidation != null)
            {
                stopwatch.Stop();
                activity?.SetTag("success", false);
                activity?.SetTag("error.type", "ValidationFailed");
                activity?.SetTag("error.message", "Invalid EvalRunId");
                activity?.SetTag("http.status_code", 400);
                activity?.SetTag("duration_ms", stopwatch.ElapsedMilliseconds);

                _logger.LogWarning("Invalid EvalRunId validation failed - EvalRunId: {EvalRunId}, Duration: {Duration}ms",
                       evalRunId, stopwatch.ElapsedMilliseconds);

                return evalRunIdValidation;
            }

            _logger.LogInformation("Enqueuing dataset enrichment for EvalRunId: {EvalRunId}", evalRunId);

            var (isSuccessful, httpStatusCode, message) = await _evalRunRequestHandler.PlaceEnrichmentRequestToDataVerseAPI(evalRunId);

            if (!isSuccessful)
            {
                stopwatch.Stop();
                activity?.SetTag("success", false);
                activity?.SetTag("error.type", "EnqueueFailed");
                activity?.SetTag("error.message", message);
                activity?.SetTag("error.statusCode", httpStatusCode);
                activity?.SetTag("duration_ms", stopwatch.ElapsedMilliseconds);

                _logger.LogWarning("Failed to enqueue dataset enrichment - EvalRunId: {EvalRunId}, StatusCode: {StatusCode}, Message: {Message}, Duration: {Duration}ms",
                   evalRunId, httpStatusCode, message, stopwatch.ElapsedMilliseconds);

                if (httpStatusCode == "404")
                {
                    activity?.SetTag("http.status_code", 404);
                    return NotFound($"Evaluation run with ID {evalRunId} not found");
                }
                else if (httpStatusCode == "400")
                {
                    activity?.SetTag("http.status_code", 400);
                    return BadRequest(message);
                }
                else
                {
                    activity?.SetTag("http.status_code", 500);
                    return CreateErrorResponse(message, StatusCodes.Status500InternalServerError);
                }
            }

            // Success telemetry
            stopwatch.Stop();
            activity?.SetTag("success", true);
            activity?.SetTag("http.status_code", 202);
            activity?.SetTag("duration_ms", stopwatch.ElapsedMilliseconds);

            _logger.LogInformation("Successfully enqueued dataset enrichment - EvalRunId: {EvalRunId}, Duration: {Duration}ms",
                evalRunId, stopwatch.ElapsedMilliseconds);

            return Accepted();
        }
        catch (InvalidOperationException ex)
        {
            stopwatch.Stop();
            activity?.SetTag("success", false);
            activity?.SetTag("error.type", "InvalidOperation");
            activity?.SetTag("error.message", ex.Message);
            activity?.SetTag("http.status_code", 400);
            activity?.SetTag("duration_ms", stopwatch.ElapsedMilliseconds);

            _logger.LogError(ex, "Invalid operation while enqueuing dataset enrichment for EvalRunId: {EvalRunId}, Duration: {Duration}ms",
     evalRunId, stopwatch.ElapsedMilliseconds);

            return CreateErrorResponse(ex.Message, StatusCodes.Status400BadRequest);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            activity?.SetTag("success", false);
            activity?.SetTag("error.type", ex.GetType().Name);
            activity?.SetTag("error.message", ex.Message);
            activity?.SetTag("duration_ms", stopwatch.ElapsedMilliseconds);

            if (IsAuthorizationError(ex))
            {
                activity?.SetTag("error.category", "Authorization");
                activity?.SetTag("http.status_code", 403);

                _logger.LogWarning(ex, "Authorization error occurred while enqueuing dataset enrichment for EvalRunId: {EvalRunId}, Duration: {Duration}ms",
             evalRunId, stopwatch.ElapsedMilliseconds);

                return CreateErrorResponse("Access denied. Authorization failed.", StatusCodes.Status403Forbidden);
            }

            activity?.SetTag("error.category", "UnexpectedError");
            activity?.SetTag("http.status_code", 500);

            _logger.LogError(ex, "Error occurred while enqueuing dataset enrichment for EvalRunId: {EvalRunId}, Duration: {Duration}ms",
                 evalRunId, stopwatch.ElapsedMilliseconds);

            return CreateErrorResponse("Failed to enqueue dataset enrichment", StatusCodes.Status500InternalServerError);
        }
    }

    #endregion
}
