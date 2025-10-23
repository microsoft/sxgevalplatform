using Microsoft.AspNetCore.Mvc;
using SxgEvalPlatformApi.Models;
using SxgEvalPlatformApi.RequestHandlers;
using Azure;
using System.ComponentModel.DataAnnotations;

namespace SxgEvalPlatformApi.Controllers;

/// <summary>
/// Controller for evaluation artifacts operations
/// </summary>
[ApiController]
[Route("api/v1/eval/artifacts")]
public class EvalArtifactsController : BaseController
{
    private readonly IEvalArtifactsRequestHandler _evalArtifactsRequestHandler;

    public EvalArtifactsController(
        IEvalArtifactsRequestHandler evalArtifactsRequestHandler,
        ILogger<EvalArtifactsController> logger)
        : base(logger)
    {
        _evalArtifactsRequestHandler = evalArtifactsRequestHandler;
    }

    /// <summary>
    /// Get both metrics configuration and dataset content for an evaluation run
    /// </summary>
    /// <param name="evalRunId">Evaluation run ID</param>
    /// <returns>Combined artifacts including metrics configuration and dataset content</returns>
    /// <response code="200">Artifacts retrieved successfully</response>
    /// <response code="400">Invalid evaluation run ID</response>
    /// <response code="404">Evaluation run not found</response>
    /// <response code="500">Internal server error</response>
    [HttpGet]
    [ProducesResponseType(typeof(EvalArtifactsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<EvalArtifactsDto>> GetEvalArtifacts([FromQuery, Required] Guid evalRunId)
    {
        try
        {
            var evalRunIdValidation = ValidateEvalRunId(evalRunId);
            if (evalRunIdValidation != null)
            {
                return evalRunIdValidation;
            }

            _logger.LogInformation("Retrieving evaluation artifacts for EvalRunId: {EvalRunId}", evalRunId);

            var artifacts = await _evalArtifactsRequestHandler.GetEvalArtifactsAsync(evalRunId);
            
            if (artifacts == null)
            {
                return NotFound($"Evaluation run with ID {evalRunId} not found");
            }

            return Ok(artifacts);
        }
        catch (RequestFailedException ex)
        {
            _logger.LogError(ex, "Azure error occurred while retrieving evaluation artifacts for EvalRunId: {EvalRunId}", evalRunId);
            return HandleAzureException<EvalArtifactsDto>(ex, "Failed to retrieve evaluation artifacts");
        }
        catch (Exception ex)
        {
            if (IsAuthorizationError(ex))
            {
                _logger.LogWarning(ex, "Authorization error occurred while retrieving evaluation artifacts for EvalRunId: {EvalRunId}", evalRunId);
                return CreateErrorResponse<EvalArtifactsDto>("Access denied. Authorization failed.", StatusCodes.Status403Forbidden);
            }
            
            _logger.LogError(ex, "Error occurred while retrieving evaluation artifacts for EvalRunId: {EvalRunId}", evalRunId);
            return CreateErrorResponse<EvalArtifactsDto>("Failed to retrieve evaluation artifacts", StatusCodes.Status500InternalServerError);
        }
    }

    /// <summary>
    /// Get only metrics configuration for an evaluation run
    /// </summary>
    /// <param name="evalRunId">Evaluation run ID</param>
    /// <returns>Metrics configuration artifact</returns>
    /// <response code="200">Metrics configuration retrieved successfully</response>
    /// <response code="400">Invalid evaluation run ID</response>
    /// <response code="404">Evaluation run or metrics configuration not found</response>
    /// <response code="500">Internal server error</response>
    [HttpGet("metricsconfiguration")]
    [ProducesResponseType(typeof(MetricsConfigurationArtifactDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<MetricsConfigurationArtifactDto>> GetMetricsConfigurationArtifact([FromQuery, Required] Guid evalRunId)
    {
        try
        {
            var evalRunIdValidation = ValidateEvalRunId(evalRunId);
            if (evalRunIdValidation != null)
            {
                return evalRunIdValidation;
            }

            _logger.LogInformation("Retrieving metrics configuration artifact for EvalRunId: {EvalRunId}", evalRunId);

            var artifact = await _evalArtifactsRequestHandler.GetMetricsConfigurationArtifactAsync(evalRunId);
            
            if (artifact == null)
            {
                return NotFound($"Evaluation run with ID {evalRunId} not found or metrics configuration not available");
            }

            return Ok(artifact);
        }
        catch (RequestFailedException ex)
        {
            _logger.LogError(ex, "Azure error occurred while retrieving metrics configuration artifact for EvalRunId: {EvalRunId}", evalRunId);
            return HandleAzureException<MetricsConfigurationArtifactDto>(ex, "Failed to retrieve metrics configuration artifact");
        }
        catch (Exception ex)
        {
            if (IsAuthorizationError(ex))
            {
                _logger.LogWarning(ex, "Authorization error occurred while retrieving metrics configuration artifact for EvalRunId: {EvalRunId}", evalRunId);
                return CreateErrorResponse<MetricsConfigurationArtifactDto>("Access denied. Authorization failed.", StatusCodes.Status403Forbidden);
            }
            
            _logger.LogError(ex, "Error occurred while retrieving metrics configuration artifact for EvalRunId: {EvalRunId}", evalRunId);
            return CreateErrorResponse<MetricsConfigurationArtifactDto>("Failed to retrieve metrics configuration artifact", StatusCodes.Status500InternalServerError);
        }
    }

    /// <summary>
    /// Get only dataset content for an evaluation run
    /// </summary>
    /// <param name="evalRunId">Evaluation run ID</param>
    /// <returns>Dataset artifact</returns>
    /// <response code="200">Dataset retrieved successfully</response>
    /// <response code="400">Invalid evaluation run ID</response>
    /// <response code="404">Evaluation run or dataset not found</response>
    /// <response code="500">Internal server error</response>
    [HttpGet("dataset")]
    [ProducesResponseType(typeof(DatasetArtifactDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<DatasetArtifactDto>> GetDatasetArtifact([FromQuery, Required] Guid evalRunId)
    {
        try
        {
            var evalRunIdValidation = ValidateEvalRunId(evalRunId);
            if (evalRunIdValidation != null)
            {
                return evalRunIdValidation;
            }

            _logger.LogInformation("Retrieving dataset artifact for EvalRunId: {EvalRunId}", evalRunId);

            var artifact = await _evalArtifactsRequestHandler.GetDatasetArtifactAsync(evalRunId);
            
            if (artifact == null)
            {
                return NotFound($"Evaluation run with ID {evalRunId} not found or dataset not available");
            }

            return Ok(artifact);
        }
        catch (RequestFailedException ex)
        {
            _logger.LogError(ex, "Azure error occurred while retrieving dataset artifact for EvalRunId: {EvalRunId}", evalRunId);
            return HandleAzureException<DatasetArtifactDto>(ex, "Failed to retrieve dataset artifact");
        }
        catch (Exception ex)
        {
            if (IsAuthorizationError(ex))
            {
                _logger.LogWarning(ex, "Authorization error occurred while retrieving dataset artifact for EvalRunId: {EvalRunId}", evalRunId);
                return CreateErrorResponse<DatasetArtifactDto>("Access denied. Authorization failed.", StatusCodes.Status403Forbidden);
            }
            
            _logger.LogError(ex, "Error occurred while retrieving dataset artifact for EvalRunId: {EvalRunId}", evalRunId);
            return CreateErrorResponse<DatasetArtifactDto>("Failed to retrieve dataset artifact", StatusCodes.Status500InternalServerError);
        }
    }

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
    [HttpPost("enriched-dataset")]
    [ProducesResponseType(typeof(EnrichedDatasetResponseDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<EnrichedDatasetResponseDto>> CreateEnrichedDataset(
        [FromQuery, Required] Guid evalRunId, 
        [FromBody] CreateEnrichedDatasetDto createDto)
    {
        try
        {
            var evalRunIdValidation = ValidateEvalRunId(evalRunId);
            if (evalRunIdValidation != null)
            {
                return evalRunIdValidation;
            }

            if (!ModelState.IsValid)
            {
                return CreateValidationErrorResponse<EnrichedDatasetResponseDto>();
            }

            // Validate that the enriched dataset is not empty
            if (createDto.EnrichedDataset.ValueKind == System.Text.Json.JsonValueKind.Null ||
                createDto.EnrichedDataset.ValueKind == System.Text.Json.JsonValueKind.Undefined)
            {
                return CreateBadRequestResponse<EnrichedDatasetResponseDto>("EnrichedDataset", "Enriched dataset cannot be null or undefined");
            }

            _logger.LogInformation("Storing enriched dataset for EvalRunId: {EvalRunId}", evalRunId);

            var response = await _evalArtifactsRequestHandler.StoreEnrichedDatasetAsync(evalRunId, createDto.EnrichedDataset);
            
            return CreatedAtAction(
                nameof(GetEnrichedDatasetArtifact), 
                new { evalRunId = evalRunId }, 
                response);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "Invalid operation while storing enriched dataset for EvalRunId: {EvalRunId}", evalRunId);
            
            if (ex.Message.Contains("not found"))
            {
                return NotFound<EnrichedDatasetResponseDto>($"Evaluation run with ID {evalRunId} not found");
            }
            
            return CreateErrorResponse<EnrichedDatasetResponseDto>(ex.Message, StatusCodes.Status400BadRequest);
        }
        catch (RequestFailedException ex)
        {
            _logger.LogError(ex, "Azure error occurred while storing enriched dataset for EvalRunId: {EvalRunId}", evalRunId);
            return HandleAzureException<EnrichedDatasetResponseDto>(ex, "Failed to store enriched dataset");
        }
        catch (Exception ex)
        {
            if (IsAuthorizationError(ex))
            {
                _logger.LogWarning(ex, "Authorization error occurred while storing enriched dataset for EvalRunId: {EvalRunId}", evalRunId);
                return CreateErrorResponse<EnrichedDatasetResponseDto>("Access denied. Authorization failed.", StatusCodes.Status403Forbidden);
            }
            
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
    [HttpGet("enriched-dataset")]
    [ProducesResponseType(typeof(EnrichedDatasetArtifactDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<EnrichedDatasetArtifactDto>> GetEnrichedDatasetArtifact([FromQuery, Required] Guid evalRunId)
    {
        try
        {
            var evalRunIdValidation = ValidateEvalRunId(evalRunId);
            if (evalRunIdValidation != null)
            {
                return evalRunIdValidation;
            }

            _logger.LogInformation("Retrieving enriched dataset artifact for EvalRunId: {EvalRunId}", evalRunId);

            var artifact = await _evalArtifactsRequestHandler.GetEnrichedDatasetArtifactAsync(evalRunId);
            
            if (artifact == null)
            {
                return NotFound($"Enriched dataset not found for evaluation run with ID {evalRunId}");
            }

            return Ok(artifact);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "Invalid operation while retrieving enriched dataset artifact for EvalRunId: {EvalRunId}", evalRunId);
            return CreateErrorResponse<EnrichedDatasetArtifactDto>(ex.Message, StatusCodes.Status400BadRequest);
        }
        catch (RequestFailedException ex)
        {
            _logger.LogError(ex, "Azure error occurred while retrieving enriched dataset artifact for EvalRunId: {EvalRunId}", evalRunId);
            return HandleAzureException<EnrichedDatasetArtifactDto>(ex, "Failed to retrieve enriched dataset artifact");
        }
        catch (Exception ex)
        {
            if (IsAuthorizationError(ex))
            {
                _logger.LogWarning(ex, "Authorization error occurred while retrieving enriched dataset artifact for EvalRunId: {EvalRunId}", evalRunId);
                return CreateErrorResponse<EnrichedDatasetArtifactDto>("Access denied. Authorization failed.", StatusCodes.Status403Forbidden);
            }
            
            _logger.LogError(ex, "Error occurred while retrieving enriched dataset artifact for EvalRunId: {EvalRunId}", evalRunId);
            return CreateErrorResponse<EnrichedDatasetArtifactDto>("Failed to retrieve enriched dataset artifact", StatusCodes.Status500InternalServerError);
        }
    }
}