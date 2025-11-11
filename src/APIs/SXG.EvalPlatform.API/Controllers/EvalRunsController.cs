using Azure;
using Microsoft.AspNetCore.Mvc;
using Sxg.EvalPlatform.API.Storage.Services;
using SXG.EvalPlatform.Common;
using SxgEvalPlatformApi.Models;
using SxgEvalPlatformApi.Models.Dtos;
using SxgEvalPlatformApi.RequestHandlers;
using System.ComponentModel.DataAnnotations;
using System.Runtime.InteropServices;

namespace SxgEvalPlatformApi.Controllers;

/// <summary>
/// Controller for evaluation run operations
/// </summary>
[ApiController]
[Route("api/v1/eval/runs")]
public class EvalRunsController : BaseController
{
    private readonly IEvalRunRequestHandler _evalRunRequestHandler;
    private readonly IDataSetTableService _dataSetTableService;
    private readonly IMetricsConfigTableService _metricsConfigTableService;
    private readonly IEvalArtifactsRequestHandler _evalArtifactsRequestHandler;
    private readonly IEvaluationResultRequestHandler _evaluationResultRequestHandler;

    public EvalRunsController(
        IEvalRunRequestHandler evalRunRequestHandler, 
        IDataSetTableService dataSetTableService,
        IMetricsConfigTableService metricsConfigTableService,
        IEvalArtifactsRequestHandler evalArtifactsRequestHandler,
        IEvaluationResultRequestHandler evaluationResultRequestHandler,
    ILogger<EvalRunsController> logger)
        : base(logger)
    {
        _evalRunRequestHandler = evalRunRequestHandler;
        _dataSetTableService = dataSetTableService;
        _metricsConfigTableService = metricsConfigTableService;
        _evalArtifactsRequestHandler = evalArtifactsRequestHandler;
        _evaluationResultRequestHandler = evaluationResultRequestHandler; 
    }

    #region Eval Run Endpoints


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
        try
        {
            // Add custom validation for string fields (DataSetId and MetricsConfigurationId are now Guid types)
            ValidateAndAddToModelState(createDto.AgentId, "AgentId", "agentid");
            ValidateAndAddToModelState(createDto.Type, "Type", "type");
            ValidateAndAddToModelState(createDto.AgentSchemaName, "AgentSchemaName", "agentschemaname");

            // Validate that the referenced entities exist
            await ValidateReferencedEntitiesAsync(createDto);

            if (!ModelState.IsValid)
            {
                return CreateValidationErrorResponse<EvalRunDto>();
            }

            _logger.LogInformation("Creating evaluation run for AgentId: {AgentId}, DataSetId: {DataSetId}, Type: {Type}, EnvironmentId: {EnvironmentId}",
                createDto.AgentId, createDto.DataSetId, createDto.Type, createDto.EnvironmentId);

            var evalRun = await _evalRunRequestHandler.CreateEvalRunAsync(createDto);

            return CreatedAtAction(
                nameof(GetEvalRun),
                new { evalRunId = evalRun.EvalRunId },
                evalRun);
        }
        catch (RequestFailedException ex)
        {
            _logger.LogError(ex, "Azure error occurred while creating evaluation run");
            return HandleAzureException<EvalRunDto>(ex, "Failed to create evaluation run");
        }
        catch (Exception ex)
        {
            if (IsAuthorizationError(ex))
            {
                _logger.LogWarning(ex, "Authorization error occurred while creating evaluation run");
                return CreateErrorResponse<EvalRunDto>("Access denied. Authorization failed.", StatusCodes.Status403Forbidden);
            }

            _logger.LogError(ex, "Error occurred while creating evaluation run");
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
        try
        {
            var evalRunIdValidation = ValidateEvalRunId(evalRunId);
            if (evalRunIdValidation != null)
            {
                return evalRunIdValidation;
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
            if (IsAuthorizationError(ex))
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
    //[HttpGet("agent/{agentId}")]
    [HttpGet]
    [ProducesResponseType(typeof(List<EvalRunDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<List<EvalRunDto>>> GetEvalRunsByAgent([FromQuery, Required]string agentId, [FromQuery, Optional] DateTime? startDateTime, [FromQuery, Optional] DateTime? endDateTime)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(agentId))
            {
                return CreateBadRequestResponse<List<EvalRunDto>>("agentId", "AgentId is required and cannot be empty");
            }

            if (startDateTime.HasValue && endDateTime.HasValue && startDateTime > endDateTime)
            {
                return CreateBadRequestResponse<List<EvalRunDto>>("dateRange", "StartDateTime cannot be later than EndDateTime");
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
            if (IsAuthorizationError(ex))
            {
                _logger.LogWarning(ex, "Authorization error occurred while retrieving evaluation runs for AgentId: {AgentId}", agentId);
                return CreateErrorResponse<List<EvalRunDto>>("Access denied. Authorization failed.", StatusCodes.Status403Forbidden);
            }

            _logger.LogError(ex, "Error occurred while retrieving evaluation runs for AgentId: {AgentId}", agentId);
            return CreateErrorResponse<List<EvalRunDto>>("Failed to retrieve evaluation runs", StatusCodes.Status500InternalServerError);
        }
    }

       

    #endregion


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
    public async Task<ActionResult<UpdateResponseDto>> UpdateEvalRun(Guid evalRunId, [FromBody] UpdateStatusDto updateDto)
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
                return CreateValidationErrorResponse<UpdateResponseDto>();
            }

            // First, get the current evaluation run to check its status
            // Use the cross-partition search since we don't have AgentId in the request
            var currentEvalRun = await _evalRunRequestHandler.GetEvalRunByIdAsync(evalRunId);
            if (currentEvalRun == null)
            {
                return NotFound(new UpdateResponseDto
                {
                    Success = false,
                    Message = $"Evaluation run with ID {evalRunId} not found"
                });
            }

            // Check if the current status is already in a terminal state
            var terminalStatuses = new[] { CommonConstants.EvalRunStatus.EvalRunCompleted, CommonConstants.EvalRunStatus.EvalRunFailed };
            if (terminalStatuses.Any(status => string.Equals(status, currentEvalRun.Status, StringComparison.OrdinalIgnoreCase)))
            {
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
                CommonConstants.EvalRunStatus.EvalRunCompleted
            };

            if (!validStatuses.Any(status => string.Equals(status, updateDto.Status, StringComparison.OrdinalIgnoreCase)))
            {
                return CreateBadRequestResponse<UpdateResponseDto>("Status", $"Invalid status. Valid values are: {string.Join(", ", validStatuses)}");
            }

            _logger.LogInformation("Updating evaluation run status to {Status} for ID: {EvalRunId}",
                updateDto.Status, evalRunId);

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
                _logger.LogError("Unexpected null result from UpdateEvalRunStatusAsync for EvalRunId: {EvalRunId}", evalRunId);
                return StatusCode(StatusCodes.Status500InternalServerError, new UpdateResponseDto
                {
                    Success = false,
                    Message = "An unexpected error occurred while updating the evaluation run status"
                });
            }

            return Ok(new UpdateResponseDto
            {
                Success = true,
                Message = $"Evaluation run status updated successfully to {updateDto.Status}"
            });
        }
        catch (RequestFailedException ex)
        {
            _logger.LogError(ex, "Azure error occurred while updating evaluation run status");
            return HandleAzureException<UpdateResponseDto>(ex, "Failed to update evaluation run status");
        }
        catch (Exception ex)
        {
            if (IsAuthorizationError(ex))
            {
                _logger.LogWarning(ex, "Authorization error occurred while updating evaluation run status");
                return CreateErrorResponse<UpdateResponseDto>("Access denied. Authorization failed.", StatusCodes.Status403Forbidden);
            }

            _logger.LogError(ex, "Error occurred while updating evaluation run status");
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
    public async Task<ActionResult<EvalRunStatusDto>> GetEvalRunStatus(Guid evalRunId)
    {
        try
        {
            var evalRunIdValidation = ValidateEvalRunId(evalRunId);
            if (evalRunIdValidation != null)
            {
                return evalRunIdValidation;
            }

            _logger.LogInformation("Retrieving evaluation run status for ID: {EvalRunId}", evalRunId);

            var evalRun = await _evalRunRequestHandler.GetEvalRunByIdAsync(evalRunId);

            if (evalRun == null)
            {
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

            return Ok(statusDto);
        }
        catch (RequestFailedException ex)
        {
            _logger.LogError(ex, "Azure error occurred while retrieving evaluation run status for ID: {EvalRunId}", evalRunId);
            return HandleAzureException<EvalRunStatusDto>(ex, "Failed to retrieve evaluation run status");
        }
        catch (Exception ex)
        {
            if (IsAuthorizationError(ex))
            {
                _logger.LogWarning(ex, "Authorization error occurred while retrieving evaluation run status for ID: {EvalRunId}", evalRunId);
                return CreateErrorResponse<EvalRunStatusDto>("Access denied. Authorization failed.", StatusCodes.Status403Forbidden);
            }

            _logger.LogError(ex, "Error occurred while retrieving evaluation run status for ID: {EvalRunId}", evalRunId);
            return CreateErrorResponse<EvalRunStatusDto>("Failed to retrieve evaluation run status", StatusCodes.Status500InternalServerError);
        }
    }

    #endregion


    /// <summary>
    /// Validates that referenced entities (dataset, Metrics configuration) exist and belong to the specified agent
    /// </summary>
    /// <param name="createDto">The evaluation run creation DTO</param>
    private async Task ValidateReferencedEntitiesAsync(CreateEvalRunDto createDto)
    {
        // Validate DataSet exists and belongs to the agent
        try
        {
            var dataset = await _dataSetTableService.GetDataSetAsync(createDto.AgentId, createDto.DataSetId.ToString());
            if (dataset == null)
            {
                ModelState.AddModelError(nameof(createDto.DataSetId), 
                    $"Dataset with ID '{createDto.DataSetId}' not found for agent '{createDto.AgentId}'");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating dataset ID: {DataSetId} for agent: {AgentId}", 
                createDto.DataSetId, createDto.AgentId);
            ModelState.AddModelError(nameof(createDto.DataSetId), 
                "Unable to validate dataset. Please check the dataset ID.");
        }

        // Validate Metrics Configuration exists
        try
        {
            var metricsConfig = await _metricsConfigTableService.GetMetricsConfigurationByConfigurationIdAsync(createDto.MetricsConfigurationId.ToString());
            if (metricsConfig == null)
            {
                ModelState.AddModelError(nameof(createDto.MetricsConfigurationId), 
                    $"Metrics configuration with ID '{createDto.MetricsConfigurationId}' not found");
            }
            else if (metricsConfig.PartitionKey != createDto.AgentId)
            {
                ModelState.AddModelError(nameof(createDto.MetricsConfigurationId), 
                    $"Metrics configuration '{createDto.MetricsConfigurationId}' does not belong to agent '{createDto.AgentId}'");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating Metrics configuration ID: {MetricsConfigurationId}", 
                createDto.MetricsConfigurationId);
            ModelState.AddModelError(nameof(createDto.MetricsConfigurationId), 
                "Unable to validate Metrics configuration. Please check the configuration ID.");
        }
    }

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
    public async Task<ActionResult<EnrichedDatasetResponseDto>> CreateEnrichedDataset(
        [FromRoute, Required] Guid evalRunId,
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
    [HttpGet("{evalRunId}/enriched-dataset")]
    [ProducesResponseType(typeof(EnrichedDatasetArtifactDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<EnrichedDatasetArtifactDto>> GetEnrichedDatasetArtifact([FromRoute, Required] Guid evalRunId)
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


    #endregion

    #region Evaluation Results Endpoints


    /// <summary>
    /// Save evaluation results for a specific evaluation run
    /// </summary>
    /// <param name="saveDto">Evaluation result data containing EvalRunId and EvaluationRecords</param>
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
    public async Task<ActionResult<EvaluationResultSaveResponseDto>> SaveEvaluationResult([FromRoute] Guid evalRunId, [FromBody] SaveEvaluationResultDto saveDto)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return CreateValidationErrorResponse<EvaluationResultSaveResponseDto>();
            }

            _logger.LogInformation("Saving evaluation results for EvalRunId: {EvalRunId}", evalRunId);

            var result = await _evaluationResultRequestHandler.SaveEvaluationResultAsync(evalRunId, saveDto);

            if (!result.Success)
            {
                // Use generic error messages to prevent information disclosure
                if (result.Message.Contains("not found"))
                {
                    return CreateBadRequestResponse<EvaluationResultSaveResponseDto>("EvalRunId", "Invalid evaluation run identifier or evaluation run not found");
                }
                if (result.Message.Contains("Cannot save evaluation results") && result.Message.Contains("status"))
                {
                    return CreateBadRequestResponse<EvaluationResultSaveResponseDto>("EvalRunId", "Unable to save results - evaluation run status does not allow saving");
                }
                return CreateErrorResponse<EvaluationResultSaveResponseDto>(
                    "Failed to save evaluation results", StatusCodes.Status500InternalServerError);
            }

            return Ok(result);
        }
        catch (RequestFailedException ex)
        {
            _logger.LogError(ex, "Azure error occurred while saving evaluation results for EvalRunId: {EvalRunId}",
                evalRunId);
            return HandleAzureException<EvaluationResultSaveResponseDto>(ex, "Failed to save evaluation results");
        }
        catch (Exception ex)
        {
            if (IsAuthorizationError(ex))
            {
                _logger.LogWarning(ex, "Authorization error occurred while saving evaluation results for EvalRunId: {EvalRunId}",
                    evalRunId);
                return CreateErrorResponse<EvaluationResultSaveResponseDto>("Access denied. Authorization failed.", StatusCodes.Status403Forbidden);
            }

            _logger.LogError(ex, "Error occurred while saving evaluation results for EvalRunId: {EvalRunId}",
                evalRunId);
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
    public async Task<ActionResult<EvaluationResultResponseDto>> GetEvaluationResult(Guid evalRunId)
    {
        try
        {
            var evalRunIdValidation = ValidateEvalRunId(evalRunId);
            if (evalRunIdValidation != null)
            {
                return evalRunIdValidation;
            }

            _logger.LogInformation("Retrieving evaluation results for EvalRunId: {EvalRunId}", evalRunId);

            var result = await _evaluationResultRequestHandler.GetEvaluationResultByIdAsync(evalRunId);

            if (!result.Success)
            {
                // Check if this is a status-related issue (evaluation not completed)
                if (result.Message.Contains("Results are only available for completed evaluations"))
                {
                    return NotFound("Evaluation results not available. Results are only available for completed evaluations.");
                }
                
                // Use generic error messages to prevent information disclosure
                if (result.Message.Contains("not found") && result.Message.Contains("EvalRunId"))
                {
                    return CreateBadRequestResponse<EvaluationResultResponseDto>("evalRunId", "Invalid evaluation run identifier");
                }
                else if (result.Message.Contains("not found") || result.Message.Contains("hasn't completed"))
                {
                    return NotFound("Evaluation results not found");
                }
                return CreateErrorResponse<EvaluationResultResponseDto>(
                    "Failed to retrieve evaluation results", StatusCodes.Status500InternalServerError);
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

    #endregion

}