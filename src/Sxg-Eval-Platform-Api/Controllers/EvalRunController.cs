using Microsoft.AspNetCore.Mvc;
using SxgEvalPlatformApi.Models;
using SxgEvalPlatformApi.Models.Dtos;
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

        public EvalRunController(
            IConfiguration configuration,
            IConfigHelper configHelper,
            IGenericCacheService cacheService,
            ILogger<EvalRunController> logger)
            : base(logger)
        {
            _configuration = configuration;
            _configHelper = configHelper;
            _cacheService = cacheService;

            // Log controller initialization for debugging
            _logger.LogInformation("EvalRunController (high-performance) initialized");
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

                if (evalRunId == Guid.Empty)
                {
                    _logger.LogWarning("Invalid evaluation run ID: {EvalRunId}", evalRunId);
                    return CreateErrorResponse<EvalRunDto>("Invalid evaluation run ID format", StatusCodes.Status400BadRequest);
                }

                // Generate cache key
                var cacheKey = $"EvalRun:{evalRunId}";
                
                // Try to get from cache first using GetOrSetAsync pattern
                var evalRun = await _cacheService.GetOrSetAsync(cacheKey, async () =>
                {
                    // Cache miss, fetch from storage
                    _logger.LogInformation("High-performance cache miss, fetching from storage for ID: {EvalRunId}", evalRunId);
                    
                    // Create services directly without DI caching decorators
                    var evalRunTableService = new EvalRunTableService(
                        _configuration,
                        this.HttpContext.RequestServices.GetRequiredService<ILogger<EvalRunTableService>>());

                    var entity = await evalRunTableService.GetEvalRunByIdAsync(evalRunId);
                    
                    if (entity == null)
                    {
                        return null;
                    }

                    return new EvalRunDto
                    {
                        EvalRunId = entity.EvalRunId,
                        AgentId = entity.AgentId,
                        DataSetId = entity.DataSetId,
                        MetricsConfigurationId = entity.MetricsConfigurationId,
                        Status = entity.Status,
                        LastUpdatedBy = entity.LastUpdatedBy,
                        LastUpdatedOn = entity.LastUpdatedOn,
                        StartedDatetime = entity.StartedDatetime,
                        CompletedDatetime = entity.CompletedDatetime
                    };
                }, TimeSpan.FromMinutes(60));

                if (evalRun == null)
                {
                    _logger.LogInformation("Evaluation run with ID {EvalRunId} not found", evalRunId);
                    return CreateErrorResponse<EvalRunDto>($"Evaluation run with ID {evalRunId} not found", StatusCodes.Status404NotFound);
                }

                _logger.LogInformation("High-performance successfully retrieved evaluation run with ID: {EvalRunId}", evalRunId);
                return Ok(evalRun);
            }
            catch (Exception ex)
            {
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
                
                if (!ModelState.IsValid)
                {
                    _logger.LogWarning("Invalid input data for evaluation run creation");
                    return CreateValidationErrorResponse<EvalRunDto>();
                }

                // Create the eval run entity
                var evalRunId = Guid.NewGuid();
                var currentDateTime = DateTime.UtcNow;
                var containerName = createDto.AgentId.Trim().Replace(" ", "");
                var blobFilePath = $"evalresults/{evalRunId}/";
                
                var entity = new EvalRunTableEntity
                {
                    EvalRunId = evalRunId,
                    AgentId = createDto.AgentId,
                    DataSetId = createDto.DataSetId.ToString(),
                    MetricsConfigurationId = createDto.MetricsConfigurationId.ToString(),
                    Status = Sxg.EvalPlatform.API.Storage.TableEntities.EvalRunStatusConstants.Queued,
                    LastUpdatedBy = "System", // Default since UserMetadata is no longer required
                    LastUpdatedOn = currentDateTime,
                    StartedDatetime = currentDateTime,
                    ContainerName = containerName,
                    BlobFilePath = blobFilePath,
                    Type = createDto.Type,
                    EnvironmentId = createDto.EnvironmentId.ToString(),
                    AgentSchemaName = createDto.AgentSchemaName
                };
                entity.RowKey = evalRunId.ToString();

                // Create service directly
                var evalRunTableService = new EvalRunTableService(
                    _configuration,
                    this.HttpContext.RequestServices.GetRequiredService<ILogger<EvalRunTableService>>());

                var createdEntity = await evalRunTableService.CreateEvalRunAsync(entity);
                
                var evalRun = new EvalRunDto
                {
                    EvalRunId = createdEntity.EvalRunId,
                    AgentId = createdEntity.AgentId,
                    DataSetId = createdEntity.DataSetId,
                    MetricsConfigurationId = createdEntity.MetricsConfigurationId,
                    Status = createdEntity.Status,
                    LastUpdatedBy = createdEntity.LastUpdatedBy,
                    LastUpdatedOn = createdEntity.LastUpdatedOn,
                    StartedDatetime = createdEntity.StartedDatetime,
                    CompletedDatetime = createdEntity.CompletedDatetime
                };

                // Cache the newly created eval run
                var cacheKey = $"EvalRun:{evalRunId}";
                await _cacheService.UpdateCacheAsync(cacheKey, evalRun, TimeSpan.FromMinutes(60));

                _logger.LogInformation("High-performance successfully created evaluation run with ID: {EvalRunId}", evalRunId);
                
                return CreatedAtAction(
                    nameof(GetEvalRun), 
                    new { evalRunId = evalRun.EvalRunId }, 
                    evalRun);
            }
            catch (Exception ex)
            {
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

                if (evalRunId == Guid.Empty)
                {
                    _logger.LogWarning("Invalid evaluation run ID: {EvalRunId}", evalRunId);
                    return CreateErrorResponse<UpdateResponseDto>("Invalid evaluation run ID format", StatusCodes.Status400BadRequest);
                }

                if (!ModelState.IsValid)
                {
                    return CreateValidationErrorResponse<UpdateResponseDto>();
                }

                // Create service directly
                var evalRunTableService = new EvalRunTableService(
                    _configuration,
                    this.HttpContext.RequestServices.GetRequiredService<ILogger<EvalRunTableService>>());

                // First get the eval run to find the agentId
                var existingEntity = await evalRunTableService.GetEvalRunByIdAsync(evalRunId);
                if (existingEntity == null)
                {
                    _logger.LogWarning("Evaluation run with ID {EvalRunId} not found for status update", evalRunId);
                    return CreateErrorResponse<UpdateResponseDto>($"Evaluation run with ID {evalRunId} not found", StatusCodes.Status404NotFound);
                }

                // Update the status
                var updatedEntity = await evalRunTableService.UpdateEvalRunStatusAsync(
                    existingEntity.AgentId, 
                    evalRunId, 
                    updateDto.Status,
                    "System"); // Default since UserMetadata is no longer required
                
                if (updatedEntity == null)
                {
                    return CreateErrorResponse<UpdateResponseDto>($"Failed to update evaluation run with ID {evalRunId}", StatusCodes.Status500InternalServerError);
                }

                // Invalidate cache since data changed
                var cacheKey = $"EvalRun:{evalRunId}";
                await _cacheService.InvalidateAsync(cacheKey);

                _logger.LogInformation("High-performance successfully updated evaluation run status for ID: {EvalRunId}", evalRunId);

                return Ok(new UpdateResponseDto
                {
                    Success = true,
                    Message = $"Evaluation run status updated successfully to {updateDto.Status}"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "High-performance error occurred while updating evaluation run status for ID: {EvalRunId}", evalRunId);
                return CreateErrorResponse<UpdateResponseDto>("Failed to update evaluation run status", StatusCodes.Status500InternalServerError);
            }
        }

        #endregion
    }
}