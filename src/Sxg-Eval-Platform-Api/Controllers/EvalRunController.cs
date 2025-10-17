using Microsoft.AspNetCore.Mvc;
using SxgEvalPlatformApi.Models;
using SxgEvalPlatformApi.Services;
using Azure;
using Sxg.EvalPlatform.API.Storage.Services;

namespace SxgEvalPlatformApi.Controllers;

/// <summary>
/// Controller for evaluation run operations
/// </summary>
[ApiController]
[Route("api/v1/eval/runs")]
public class EvalRunController : BaseController
{
    private readonly IEvalRunService _evalRunService;
    private readonly IDataSetTableService _dataSetTableService;
    private readonly IMetricsConfigTableService _metricsConfigTableService;

    public EvalRunController(
        IEvalRunService evalRunService, 
        IDataSetTableService dataSetTableService,
        IMetricsConfigTableService metricsConfigTableService,
        ILogger<EvalRunController> logger)
        : base(logger)
    {
        _evalRunService = evalRunService;
        _dataSetTableService = dataSetTableService;
        _metricsConfigTableService = metricsConfigTableService;
    }

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

            var evalRun = await _evalRunService.CreateEvalRunAsync(createDto);
            
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
    [HttpPut("{evalRunId}")]
    [ProducesResponseType(typeof(UpdateResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
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
            var currentEvalRun = await _evalRunService.GetEvalRunByIdAsync(evalRunId);
            if (currentEvalRun == null)
            {
                return NotFound(new UpdateResponseDto 
                { 
                    Success = false, 
                    Message = $"Evaluation run with ID {evalRunId} not found" 
                });
            }

            // Check if the current status is already in a terminal state
            var terminalStatuses = new[] { EvalRunStatusConstants.Completed, EvalRunStatusConstants.Failed };
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
                EvalRunStatusConstants.Queued, 
                EvalRunStatusConstants.Running, 
                EvalRunStatusConstants.Completed, 
                EvalRunStatusConstants.Failed 
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

            var updatedEvalRun = await _evalRunService.UpdateEvalRunStatusAsync(serviceUpdateDto);
            
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
    /// Get evaluation run by ID
    /// </summary>
    /// <param name="evalRunId">Evaluation run ID</param>
    /// <returns>Evaluation run details</returns>
    /// <response code="200">Evaluation run found</response>
    /// <response code="404">Evaluation run not found</response>
    /// <response code="500">Internal server error</response>
    [HttpGet("{evalRunId}")]
    [ProducesResponseType(typeof(EvalRunDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
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

            var evalRun = await _evalRunService.GetEvalRunByIdAsync(evalRunId);
            
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
    /// Validates that referenced entities (dataset, metrics configuration) exist and belong to the specified agent
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
            _logger.LogError(ex, "Error validating metrics configuration ID: {MetricsConfigurationId}", 
                createDto.MetricsConfigurationId);
            ModelState.AddModelError(nameof(createDto.MetricsConfigurationId), 
                "Unable to validate metrics configuration. Please check the configuration ID.");
        }
    }


}