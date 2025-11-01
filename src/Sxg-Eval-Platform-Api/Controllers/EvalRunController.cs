using Microsoft.AspNetCore.Mvc;
using SxgEvalPlatformApi.Models;
using SxgEvalPlatformApi.Models.Dtos;
using SxgEvalPlatformApi.RequestHandlers;
using Sxg.EvalPlatform.API.Storage.Services;
using Sxg.EvalPlatform.API.Storage;
using Sxg.EvalPlatform.API.Storage.TableEntities;
using SxgEvalPlatformApi.Services.Cache;

namespace SxgEvalPlatformApi.Controllers
{
    /// <summary>
    /// Controller for evaluation run operations
    /// </summary>
    [Route("api/v1/eval/runs")]
    public class EvalRunController : BaseController
    {
        private readonly IConfiguration _configuration;
        private readonly IConfigHelper _configHelper;
        private readonly IGenericCacheService _cacheService;
        private readonly IEvalRunRequestHandler _evalRunRequestHandler;
        private readonly IDataSetTableService _dataSetTableService;
        private readonly IMetricsConfigTableService _metricsConfigTableService;

        public EvalRunController(
            IConfiguration configuration,
            IConfigHelper configHelper,
            IGenericCacheService cacheService,
            IEvalRunRequestHandler evalRunRequestHandler,
            IDataSetTableService dataSetTableService,
            IMetricsConfigTableService metricsConfigTableService,
            ILogger<EvalRunController> logger)
            : base(logger)
        {
            _configuration = configuration;
            _configHelper = configHelper;
            _cacheService = cacheService;
            _evalRunRequestHandler = evalRunRequestHandler;
            _dataSetTableService = dataSetTableService;
            _metricsConfigTableService = metricsConfigTableService;

            // Log controller initialization for debugging
            _logger.LogInformation("EvalRunController (high-performance with RequestHandler) initialized");
        }

        #region GET Methods

        /// <summary>
        /// Get evaluation run by ID
        /// </summary>
        /// <param name="evalRunId">Evaluation run ID from route parameter</param>
        /// <returns>Evaluation run details</returns>
        /// <response code="200">Evaluation run retrieved successfully</response>
        /// <response code="400">Invalid evaluation run ID format</response>
        /// <response code="404">Evaluation run not found</response>
        /// <response code="500">Internal server error</response>
        [HttpGet("{evalRunId}")]
        [ProducesResponseType(typeof(EvalRunDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<EvalRunDto>> GetEvalRun(Guid evalRunId)
        {
            try
            {
                _logger.LogInformation("High-performance request to retrieve evaluation run with ID: {EvalRunId}", evalRunId);

                var evalRunIdValidation = ValidateEvalRunId(evalRunId);
                if (evalRunIdValidation != null)
                {
                    return evalRunIdValidation;
                }

                // Generate cache key
                var cacheKey = $"EvalRun:{evalRunId}";
                
                // Try to get from cache first using GetOrSetAsync pattern
                var evalRun = await _cacheService.GetOrSetAsync(cacheKey, async () =>
                {
                    // Cache miss, fetch from storage using RequestHandler
                    _logger.LogInformation("High-performance cache miss, fetching from storage for ID: {EvalRunId}", evalRunId);
                    
                    // Use RequestHandler instead of creating service directly
                    var evalRunDto = await _evalRunRequestHandler.GetEvalRunByIdAsync(evalRunId);
                    return evalRunDto;
                    
                }, TimeSpan.FromMinutes(60));

                if (evalRun == null)
                {
                    _logger.LogInformation("Evaluation run with ID {EvalRunId} not found", evalRunId);
                    return CreateErrorResponse<EvalRunDto>($"Evaluation run with ID {evalRunId} not found", StatusCodes.Status404NotFound);
                }

                _logger.LogInformation("High-performance successfully retrieved evaluation run with ID: {EvalRunId}", evalRunId);
                return Ok(evalRun);
            }
            catch (Azure.RequestFailedException ex)
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
                
                _logger.LogError(ex, "High-performance error occurred while retrieving evaluation run with ID: {EvalRunId}", evalRunId);
                return CreateErrorResponse<EvalRunDto>("Failed to retrieve evaluation run", StatusCodes.Status500InternalServerError);
            }
        }

        #endregion

        #region POST Methods

        /// <summary>
        /// Create a new evaluation run
        /// </summary>
        /// <param name="createDto">Evaluation run creation data</param>
        /// <returns>Created evaluation run</returns>
        /// <response code="201">Evaluation run created successfully</response>
        /// <response code="400">Invalid input data</response>
        /// <response code="500">Internal server error</response>
        [HttpPost]
        [ProducesResponseType(typeof(EvalRunDto), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<EvalRunDto>> CreateEvalRun([FromBody] CreateEvalRunDto createDto)
        {
            try
            {
                _logger.LogInformation("High-performance request to create evaluation run for AgentId: {AgentId}", createDto.AgentId);

                // Basic validation
                ValidateAndAddToModelState(createDto.AgentId, "AgentId", "agentid");
                ValidateAndAddToModelState(createDto.Type, "Type", "type");
                ValidateAndAddToModelState(createDto.AgentSchemaName, "AgentSchemaName", "agentschemaname");
                
                // Validate that the referenced entities exist
                await ValidateReferencedEntitiesAsync(createDto);
                
                if (!ModelState.IsValid)
                {
                    _logger.LogWarning("Invalid input data for evaluation run creation");
                    return CreateValidationErrorResponse<EvalRunDto>();
                }

                // Use RequestHandler instead of creating service directly
                var evalRun = await _evalRunRequestHandler.CreateEvalRunAsync(createDto);

                _logger.LogInformation("High-performance successfully created evaluation run with ID: {EvalRunId}", evalRun.EvalRunId);
                
                return CreatedAtAction(
                    nameof(GetEvalRun), 
                    new { evalRunId = evalRun.EvalRunId }, 
                    evalRun);
            }
            catch (Azure.RequestFailedException ex)
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
                
                _logger.LogError(ex, "High-performance error occurred while creating evaluation run");
                return CreateErrorResponse<EvalRunDto>("Failed to create evaluation run", StatusCodes.Status500InternalServerError);
            }
        }

        #endregion

        #region PUT Methods

        /// <summary>
        /// Update evaluation run status
        /// </summary>
        /// <param name="evalRunId">Evaluation run ID</param>
        /// <param name="updateDto">Status update data</param>
        /// <returns>Success response</returns>
        /// <response code="200">Status updated successfully</response>
        /// <response code="400">Invalid input data</response>
        /// <response code="404">Evaluation run not found</response>
        /// <response code="500">Internal server error</response>
        [HttpPut("{evalRunId}")]
        [ProducesResponseType(typeof(UpdateResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<UpdateResponseDto>> UpdateEvalRun(Guid evalRunId, [FromBody] UpdateStatusDto updateDto)
        {
            try
            {
                _logger.LogInformation("High-performance request to update evaluation run status for ID: {EvalRunId}", evalRunId);

                var evalRunIdValidation = ValidateEvalRunId(evalRunId);
                if (evalRunIdValidation != null)
                {
                    return evalRunIdValidation;
                }

                if (!ModelState.IsValid)
                {
                    return CreateValidationErrorResponse<UpdateResponseDto>();
                }

                // First get the eval run using RequestHandler + cache to check current status
                var cacheKey = $"EvalRun:{evalRunId}";
                var existingEvalRun = await _cacheService.GetOrSetAsync(cacheKey, async () =>
                {
                    // Cache miss, fetch from storage using RequestHandler
                    _logger.LogInformation("Cache miss during status update, fetching eval run from storage for ID: {EvalRunId}", evalRunId);
                    return await _evalRunRequestHandler.GetEvalRunByIdAsync(evalRunId);
                }, TimeSpan.FromMinutes(60));

                if (existingEvalRun == null)
                {
                    _logger.LogWarning("Evaluation run with ID {EvalRunId} not found for status update", evalRunId);
                    return NotFound(new UpdateResponseDto 
                    { 
                        Success = false, 
                        Message = $"Evaluation run with ID {evalRunId} not found" 
                    });
                }

                // Check if the current status is already in a terminal state
                var terminalStatuses = new[] { Sxg.EvalPlatform.API.Storage.TableEntities.EvalRunStatusConstants.Completed, 
                                             Sxg.EvalPlatform.API.Storage.TableEntities.EvalRunStatusConstants.Failed };
                if (terminalStatuses.Any(status => string.Equals(status, existingEvalRun.Status, StringComparison.OrdinalIgnoreCase)))
                {
                    return BadRequest(new UpdateResponseDto
                    {
                        Success = false,
                        Message = $"Cannot update status for evaluation run with ID {evalRunId}. " +
                                 $"The evaluation run is already in a terminal state '{existingEvalRun.Status}' and cannot be modified."
                    });
                }

                // Validate status value
                var validStatuses = new[] 
                { 
                    Sxg.EvalPlatform.API.Storage.TableEntities.EvalRunStatusConstants.Queued, 
                    Sxg.EvalPlatform.API.Storage.TableEntities.EvalRunStatusConstants.Running, 
                    Sxg.EvalPlatform.API.Storage.TableEntities.EvalRunStatusConstants.Completed, 
                    Sxg.EvalPlatform.API.Storage.TableEntities.EvalRunStatusConstants.Failed 
                };

                if (!validStatuses.Any(status => string.Equals(status, updateDto.Status, StringComparison.OrdinalIgnoreCase)))
                {
                    return CreateBadRequestResponse<UpdateResponseDto>("Status", $"Invalid status. Valid values are: {string.Join(", ", validStatuses)}");
                }

                // Use RequestHandler to update the status
                var updateRequestDto = new UpdateEvalRunStatusDto
                {
                    EvalRunId = evalRunId,
                    Status = updateDto.Status,
                    AgentId = existingEvalRun.AgentId  // CRITICAL: Must include AgentId for storage lookup
                };

                var updatedEvalRun = await _evalRunRequestHandler.UpdateEvalRunStatusAsync(updateRequestDto);
                
                if (updatedEvalRun == null)
                {
                    return CreateErrorResponse<UpdateResponseDto>($"Failed to update evaluation run with ID {evalRunId}", StatusCodes.Status500InternalServerError);
                }

                // Invalidate cache since data changed
                await _cacheService.InvalidateAsync(cacheKey);

                _logger.LogInformation("High-performance successfully updated evaluation run status for ID: {EvalRunId} to {Status}", evalRunId, updateDto.Status);

                return Ok(new UpdateResponseDto
                {
                    Success = true,
                    Message = $"Evaluation run status updated successfully to {updateDto.Status}"
                });
            }
            catch (Azure.RequestFailedException ex)
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
                
                _logger.LogError(ex, "High-performance error occurred while updating evaluation run status for ID: {EvalRunId}", evalRunId);
                return CreateErrorResponse<UpdateResponseDto>("Failed to update evaluation run status", StatusCodes.Status500InternalServerError);
            }
        }

        #endregion

        #region Private Helper Methods

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

        #endregion
    }
}