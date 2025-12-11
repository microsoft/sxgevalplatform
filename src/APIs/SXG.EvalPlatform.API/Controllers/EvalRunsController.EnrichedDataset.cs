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
            activity?.SetTag("evalRunId", evalRunId.ToString());
            activity?.SetTag("operation", "CreateEnrichedDataset");

            // Validate EvalRunId
            if (evalRunId == Guid.Empty)
            {
                stopwatch.Stop();
                activity?.SetTag("success", false);
                activity?.SetTag("error.type", "ValidationFailed");
                activity?.SetTag("http.status_code", 400);
                return CreateFieldValidationError<EnrichedDatasetResponseDto>("evalRunId", "Evaluation run ID is required and must be a valid GUID");
            }

            if (!ModelState.IsValid)
            {
                stopwatch.Stop();
                activity?.SetTag("success", false);
                return CreateValidationErrorResponse<EnrichedDatasetResponseDto>();
            }

            // Validate that the enriched dataset is not empty
            if (createDto.EnrichedDataset.ValueKind == System.Text.Json.JsonValueKind.Null ||
                createDto.EnrichedDataset.ValueKind == System.Text.Json.JsonValueKind.Undefined)
            {
                stopwatch.Stop();
                activity?.SetTag("success", false);
                activity?.SetTag("http.status_code", 400);
                return CreateFieldValidationError<EnrichedDatasetResponseDto>("EnrichedDataset", "Enriched dataset cannot be null or undefined");
            }

            _logger.LogInformation("Storing enriched dataset for EvalRunId: {EvalRunId}", evalRunId);

            var response = await _evalArtifactsRequestHandler.StoreEnrichedDatasetAsync(evalRunId, createDto.EnrichedDataset);

            // Success telemetry
            stopwatch.Stop();
            activity?.SetTag("success", true);
            activity?.SetTag("http.status_code", 201);

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

            _logger.LogError(ex, "Invalid operation while storing enriched dataset for EvalRunId: {EvalRunId}", evalRunId);

            if (ex.Message.Contains("not found"))
            {
                activity?.SetTag("http.status_code", 404);
                return CreateNotFoundResponse<EnrichedDatasetResponseDto>($"Evaluation run with ID {evalRunId} not found");
            }

            activity?.SetTag("http.status_code", 400);
            return CreateErrorResponse<EnrichedDatasetResponseDto>(ex.Message, StatusCodes.Status400BadRequest);
        }
        catch (RequestFailedException ex)
        {
            stopwatch.Stop();
            activity?.SetTag("success", false);

            _logger.LogError(ex, "Azure error occurred while storing enriched dataset for EvalRunId: {EvalRunId}", evalRunId);

            return HandleAzureException<EnrichedDatasetResponseDto>(ex, "Failed to store enriched dataset");
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            activity?.SetTag("success", false);

            // Check if it's an authorization error
            if (ex is RequestFailedException azEx && (azEx.Status == 401 || azEx.Status == 403))
            {
                activity?.SetTag("http.status_code", 403);
                _logger.LogWarning(ex, "Authorization error occurred while storing enriched dataset for EvalRunId: {EvalRunId}", evalRunId);
                return CreateErrorResponse<EnrichedDatasetResponseDto>("Access denied. Authorization failed.", StatusCodes.Status403Forbidden);
            }

            activity?.SetTag("http.status_code", 500);
            _logger.LogError(ex, "Error occurred while storing enriched dataset for EvalRunId: {EvalRunId}", evalRunId);
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
            activity?.SetTag("evalRunId", evalRunId.ToString());
            activity?.SetTag("operation", "GetEnrichedDatasetArtifact");

            // Validate EvalRunId
            if (evalRunId == Guid.Empty)
            {
                stopwatch.Stop();
                activity?.SetTag("success", false);
                activity?.SetTag("http.status_code", 400);
                return CreateFieldValidationError<EnrichedDatasetArtifactDto>("evalRunId", "Evaluation run ID is required and must be a valid GUID");
            }

            _logger.LogInformation("Retrieving enriched dataset artifact for EvalRunId: {EvalRunId}", evalRunId);

            var artifact = await _evalArtifactsRequestHandler.GetEnrichedDatasetArtifactAsync(evalRunId);

            if (artifact == null)
            {
                stopwatch.Stop();
                activity?.SetTag("success", false);
                activity?.SetTag("http.status_code", 404);
                return CreateNotFoundResponse<EnrichedDatasetArtifactDto>($"Enriched dataset not found for evaluation run with ID {evalRunId}");
            }

            // Success telemetry
            stopwatch.Stop();
            activity?.SetTag("success", true);
            activity?.SetTag("http.status_code", 200);

            _logger.LogInformation("Successfully retrieved enriched dataset artifact - EvalRunId: {EvalRunId}, AgentId: {AgentId}, Duration: {Duration}ms",
                evalRunId, artifact.AgentId, stopwatch.ElapsedMilliseconds);

            return Ok(artifact);
        }
        catch (InvalidOperationException ex)
        {
            stopwatch.Stop();
            activity?.SetTag("success", false);
            activity?.SetTag("http.status_code", 400);

            _logger.LogError(ex, "Invalid operation while retrieving enriched dataset artifact for EvalRunId: {EvalRunId}", evalRunId);
            return CreateErrorResponse<EnrichedDatasetArtifactDto>(ex.Message, StatusCodes.Status400BadRequest);
        }
        catch (RequestFailedException ex)
        {
            stopwatch.Stop();
            activity?.SetTag("success", false);

            _logger.LogError(ex, "Azure error occurred while retrieving enriched dataset artifact for EvalRunId: {EvalRunId}", evalRunId);
            return HandleAzureException<EnrichedDatasetArtifactDto>(ex, "Failed to retrieve enriched dataset artifact");
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            activity?.SetTag("success", false);

            // Check if it's an authorization error
            if (ex is RequestFailedException azEx && (azEx.Status == 401 || azEx.Status == 403))
            {
                activity?.SetTag("http.status_code", 403);
                _logger.LogWarning(ex, "Authorization error occurred while retrieving enriched dataset artifact for EvalRunId: {EvalRunId}", evalRunId);
                return CreateErrorResponse<EnrichedDatasetArtifactDto>("Access denied. Authorization failed.", StatusCodes.Status403Forbidden);
            }

            activity?.SetTag("http.status_code", 500);
            _logger.LogError(ex, "Error occurred while retrieving enriched dataset artifact for EvalRunId: {EvalRunId}", evalRunId);
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
            activity?.SetTag("evalRunId", evalRunId.ToString());
            activity?.SetTag("operation", "EnqueueDatasetEnrichment");

            // Validate EvalRunId
            if (evalRunId == Guid.Empty)
            {
                stopwatch.Stop();
                activity?.SetTag("success", false);
                activity?.SetTag("http.status_code", 400);
                return CreateFieldValidationError("evalRunId", "Evaluation run ID is required and must be a valid GUID");
            }

            _logger.LogInformation("Enqueuing dataset enrichment for EvalRunId: {EvalRunId}", evalRunId);

            var (isSuccessful, httpStatusCode, message) = await _evalRunRequestHandler.PlaceEnrichmentRequestToDataVerseAPI(evalRunId);

            if (!isSuccessful)
            {
                stopwatch.Stop();
                activity?.SetTag("success", false);

                if (httpStatusCode == "404")
                {
                    activity?.SetTag("http.status_code", 404);
                    return CreateNotFoundResponse($"Evaluation run with ID {evalRunId} not found");
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

            _logger.LogInformation("Successfully enqueued dataset enrichment - EvalRunId: {EvalRunId}, Duration: {Duration}ms",
                evalRunId, stopwatch.ElapsedMilliseconds);

            return Accepted();
        }
        catch (InvalidOperationException ex)
        {
            stopwatch.Stop();
            activity?.SetTag("success", false);
            activity?.SetTag("http.status_code", 400);

            _logger.LogError(ex, "Invalid operation while enqueuing dataset enrichment for EvalRunId: {EvalRunId}", evalRunId);
            return CreateErrorResponse(ex.Message, StatusCodes.Status400BadRequest);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            activity?.SetTag("success", false);

            // Check if it's an authorization error
            if (ex is RequestFailedException azEx && (azEx.Status == 401 || azEx.Status == 403))
            {
                activity?.SetTag("http.status_code", 403);
                _logger.LogWarning(ex, "Authorization error occurred while enqueuing dataset enrichment for EvalRunId: {EvalRunId}", evalRunId);
                return CreateErrorResponse("Access denied. Authorization failed.", StatusCodes.Status403Forbidden);
            }

            activity?.SetTag("http.status_code", 500);
            _logger.LogError(ex, "Error occurred while enqueuing dataset enrichment for EvalRunId: {EvalRunId}", evalRunId);
            return CreateErrorResponse("Failed to enqueue dataset enrichment", StatusCodes.Status500InternalServerError);
        }
    }

    #endregion
}
