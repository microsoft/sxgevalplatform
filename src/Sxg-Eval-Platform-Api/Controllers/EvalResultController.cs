using Microsoft.AspNetCore.Mvc;
using SxgEvalPlatformApi.Models;
using SxgEvalPlatformApi.Models.Dtos;
using SxgEvalPlatformApi.RequestHandlers;
using Sxg.EvalPlatform.API.Storage.Services;
using Sxg.EvalPlatform.API.Storage;
using Sxg.EvalPlatform.API.Storage.TableEntities;
using SxgEvalPlatformApi.Services.Cache;
using System.Text.Json;
using Azure;

namespace SxgEvalPlatformApi.Controllers
{
    /// <summary>
    /// Controller for evaluation result operations with smart caching
    /// </summary>
    [Route("api/v1/eval/results")]
    public class EvalResultController : BaseController
    {
        private readonly IConfiguration _configuration;
        private readonly IConfigHelper _configHelper;
        private readonly IGenericCacheService _cacheService;
        private readonly IEvaluationResultRequestHandler _evaluationResultRequestHandler;
        private readonly IEvalRunTableService _evalRunTableService;
        private readonly IAzureBlobStorageService _azureBlobStorageService;

        public EvalResultController(
            IConfiguration configuration,
            IConfigHelper configHelper,
            IGenericCacheService cacheService,
            IEvaluationResultRequestHandler evaluationResultRequestHandler,
            IEvalRunTableService evalRunTableService,
            IAzureBlobStorageService azureBlobStorageService,
            ILogger<EvalResultController> logger)
            : base(logger)
        {
            _configuration = configuration;
            _configHelper = configHelper;
            _cacheService = cacheService;
            _evaluationResultRequestHandler = evaluationResultRequestHandler;
            _evalRunTableService = evalRunTableService;
            _azureBlobStorageService = azureBlobStorageService;

            _logger.LogInformation("EvalResultController (hybrid: caching + RequestHandler) initialized");
        }

        #region GET Methods

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
        [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<EvaluationResultResponseDto>> GetEvaluationResult(Guid evalRunId)
        {
            try
            {
                // Use proper validation method from BaseController
                var evalRunIdValidation = ValidateEvalRunId(evalRunId);
                if (evalRunIdValidation != null)
                {
                    return evalRunIdValidation;
                }

                _logger.LogInformation("Optimized request to retrieve evaluation results for EvalRunId: {EvalRunId}", evalRunId);

                // First check if this is a known "not found" result to avoid expensive blob operations
                var notFoundCacheKey = $"EvalResult:NotFound:{evalRunId}";
                var isKnownNotFound = await _cacheService.ExistsAsync(notFoundCacheKey);
                if (isKnownNotFound)
                {
                    _logger.LogInformation("EvalRunId {EvalRunId} is cached as 'not found', returning 404 immediately", evalRunId);
                    return NotFound("Evaluation results not found");
                }

                // Generate cache key for successful results
                var cacheKey = $"EvalResult:{evalRunId}";
                
                // Try to get from cache first using GetOrSetAsync pattern
                var result = await _cacheService.GetOrSetAsync(cacheKey, async () =>
                {
                    // Cache miss, fetch from storage
                    _logger.LogInformation("Optimized cache miss, fetching evaluation results from storage for ID: {EvalRunId}", evalRunId);
                    
                    // First, quickly check if the EvalRun exists in cache
                    var evalRunCacheKey = $"EvalRun:{evalRunId}";
                    var cachedEvalRun = await _cacheService.GetOrSetAsync<EvalRunDto>(evalRunCacheKey, async () => null, TimeSpan.FromMinutes(1));

                    // Get EvalRun entity (use cached data if available)
                    EvalRunTableEntity? evalRunEntity;
                    if (cachedEvalRun != null)
                    {
                        _logger.LogInformation("Using cached EvalRun data for ID: {EvalRunId}", evalRunId);
                        // We have cached data, but we still need the entity for container/path info
                        evalRunEntity = await _evalRunTableService.GetEvalRunByIdAsync(evalRunId);
                        if (evalRunEntity == null)
                        {
                            _logger.LogWarning("EvalRun cache inconsistency - cached but not in storage: {EvalRunId}", evalRunId);
                            await _cacheService.InvalidateAsync(evalRunCacheKey); // Clean up stale cache
                            return null; // This will trigger not found caching
                        }
                    }
                    else
                    {
                        // First, verify that the EvalRunId exists and get internal details
                        evalRunEntity = await _evalRunTableService.GetEvalRunByIdAsync(evalRunId);
                        if (evalRunEntity == null)
                        {
                            _logger.LogWarning("EvalRunId not found: {EvalRunId}", evalRunId);
                            return null; // This will trigger not found caching
                        }
                    }

                    // Handle container name and blob path
                    string containerName;
                    string blobPath;
                    
                    if (!string.IsNullOrEmpty(evalRunEntity.ContainerName))
                    {
                        containerName = evalRunEntity.ContainerName;
                        if (!string.IsNullOrEmpty(evalRunEntity.BlobFilePath) && evalRunEntity.BlobFilePath.EndsWith('/'))
                        {
                            // Search for evaluation results files in the folder
                            var blobs = await _azureBlobStorageService.ListBlobsAsync(containerName, evalRunEntity.BlobFilePath);
                            var evaluationResultBlob = blobs.FirstOrDefault(b => 
                                b.Contains("evaluation_results_") && b.EndsWith(".json"));
                            
                            if (evaluationResultBlob != null)
                            {
                                blobPath = evaluationResultBlob;
                            }
                            else
                            {
                                blobPath = $"{evalRunEntity.BlobFilePath}results.json";
                            }
                        }
                        else
                        {
                            blobPath = evalRunEntity.BlobFilePath ?? $"evalresults/{evalRunId}/results.json";
                        }
                    }
                    else
                    {
                        // Legacy format fallback
                        containerName = evalRunEntity.AgentId.Trim().Replace(" ", "");
                        blobPath = $"evalresults/{evalRunId}/results.json";
                    }

                    _logger.LogInformation("Searching for evaluation results at path: {ContainerName}/{BlobPath}", containerName, blobPath);

                    // Check if the blob exists
                    var blobExists = await _azureBlobStorageService.BlobExistsAsync(containerName, blobPath);
                    if (!blobExists)
                    {
                        _logger.LogInformation("Evaluation results not found for EvalRunId: {EvalRunId} in path {ContainerName}/{BlobPath}. This could mean the evaluation run hasn't completed yet or something went wrong.", 
                            evalRunId, containerName, blobPath);
                        return null; // This will trigger not found caching
                    }

                    // Get the blob content
                    var jsonContent = await _azureBlobStorageService.ReadBlobContentAsync(containerName, blobPath);
                    if (string.IsNullOrEmpty(jsonContent))
                    {
                        _logger.LogWarning("Evaluation results file is empty for EvalRunId: {EvalRunId}", evalRunId);
                        return null; // This will trigger not found caching
                    }

                    // Parse and return the results
                    var evaluationRecords = JsonSerializer.Deserialize<JsonElement>(jsonContent);
                    
                    return new EvaluationResultResponseDto
                    {
                        Success = true,
                        Message = "Evaluation results retrieved successfully",
                        EvalRunId = evalRunId,
                        EvaluationRecords = evaluationRecords
                    };

                }, TimeSpan.FromMinutes(30)); // Cache for 30 minutes since results don't change often

                if (result == null)
                {
                    // Cache the "not found" result for 5 minutes to avoid repeated expensive blob lookups
                    await _cacheService.UpdateCacheAsync(notFoundCacheKey, new { NotFound = true }, TimeSpan.FromMinutes(5));
                    _logger.LogInformation("Cached 'not found' result for EvalRunId: {EvalRunId}", evalRunId);
                    return NotFound("Evaluation results not found");
                }

                if (!result.Success)
                {
                    if (result.Message.Contains("not found") && result.Message.Contains("EvalRunId"))
                    {
                        // This is an EvalRun not found case, cache it longer
                        await _cacheService.UpdateCacheAsync(notFoundCacheKey, new { NotFound = true }, TimeSpan.FromMinutes(15));
                        return CreateBadRequestResponse<EvaluationResultResponseDto>("evalRunId", "Invalid evaluation run identifier");
                    }
                    else if (result.Message.Contains("not found"))
                    {
                        // This is an evaluation result not found case, cache it shorter
                        await _cacheService.UpdateCacheAsync(notFoundCacheKey, new { NotFound = true }, TimeSpan.FromMinutes(5));
                        return NotFound("Evaluation results not found");
                    }
                    return CreateErrorResponse<EvaluationResultResponseDto>(
                        "Failed to retrieve evaluation results", StatusCodes.Status500InternalServerError);
                }

                _logger.LogInformation("Optimized successfully retrieved evaluation results for EvalRunId: {EvalRunId}", evalRunId);
                return Ok(result);
            }
            catch (RequestFailedException ex)
            {
                _logger.LogError(ex, "Azure error occurred while retrieving evaluation results for EvalRunId: {EvalRunId}", evalRunId);
                return HandleAzureException<EvaluationResultResponseDto>(ex, "Failed to retrieve evaluation results");
            }
            catch (Exception ex)
            {
                if (IsAuthorizationError(ex))
                {
                    _logger.LogWarning(ex, "Authorization error occurred while retrieving evaluation results for EvalRunId: {EvalRunId}", evalRunId);
                    return CreateErrorResponse<EvaluationResultResponseDto>("Access denied. Authorization failed.", StatusCodes.Status403Forbidden);
                }
                
                _logger.LogError(ex, "Optimized error occurred while retrieving evaluation results for EvalRunId: {EvalRunId}", evalRunId);
                return CreateErrorResponse<EvaluationResultResponseDto>(
                    "Failed to retrieve evaluation results", StatusCodes.Status500InternalServerError);
            }
        }

        #endregion

        #region POST Methods

        /// <summary>
        /// Save evaluation results for a specific evaluation run - Optimized version with cache invalidation
        /// </summary>
        /// <param name="saveDto">Evaluation result data containing EvalRunId and EvaluationRecords</param>
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
        public async Task<ActionResult<EvaluationResultSaveResponseDto>> SaveEvaluationResult([FromBody] SaveEvaluationResultDto saveDto)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return CreateValidationErrorResponse<EvaluationResultSaveResponseDto>();
                }

                _logger.LogInformation("Optimized saving evaluation results for EvalRunId: {EvalRunId}", saveDto.EvalRunId);

                // First verify that the EvalRunId exists using our fast table service
                var evalRun = await _evalRunTableService.GetEvalRunByIdAsync(saveDto.EvalRunId);
                if (evalRun == null)
                {
                    _logger.LogWarning("EvalRunId not found: {EvalRunId}", saveDto.EvalRunId);
                    return CreateBadRequestResponse<EvaluationResultSaveResponseDto>("EvalRunId", "Invalid evaluation run identifier or evaluation run not found");
                }

                // Check if status allows saving (must be terminal: Completed or Failed)
                if (evalRun.Status != "Completed" && evalRun.Status != "Failed")
                {
                    _logger.LogWarning("Cannot save results for EvalRunId {EvalRunId} with status {Status}", saveDto.EvalRunId, evalRun.Status);
                    return CreateBadRequestResponse<EvaluationResultSaveResponseDto>("EvalRunId", "Unable to save results - evaluation run status does not allow saving");
                }

                // Save the evaluation results to blob storage
                var containerName = evalRun.ContainerName ?? evalRun.AgentId.Replace(" ", "");
                var blobPath = $"{evalRun.BlobFilePath}evaluation_results_{DateTime.UtcNow:yyyyMMddHHmmss}.json";
                
                var jsonContent = System.Text.Json.JsonSerializer.Serialize(saveDto.EvaluationRecords, 
                    new System.Text.Json.JsonSerializerOptions 
                    { 
                        PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase,
                        WriteIndented = true
                    });

                await _azureBlobStorageService.WriteBlobContentAsync(containerName, blobPath, jsonContent);

                // Invalidate caches for this EvalRunId
                var cacheKey = $"EvalResult:{saveDto.EvalRunId}";
                var notFoundKey = $"EvalResult:NotFound:{saveDto.EvalRunId}";
                await _cacheService.InvalidateAsync(cacheKey);
                await _cacheService.InvalidateAsync(notFoundKey);

                _logger.LogInformation("Optimized successfully saved evaluation results for EvalRunId: {EvalRunId}", saveDto.EvalRunId);

                return Ok(new EvaluationResultSaveResponseDto
                {
                    Success = true,
                    Message = "Evaluation results saved successfully",
                    EvalRunId = saveDto.EvalRunId
                });
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

        #endregion

        #region GET Methods - Additional

        /// <summary>
        /// Get all evaluation runs for a specific agent - Optimized version with caching
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
                    return CreateBadRequestResponse<List<EvalRunDto>>("agentId", "AgentId is required and cannot be empty");
                }

                _logger.LogInformation("Optimized retrieving evaluation runs for AgentId: {AgentId}", agentId);

                // Generate cache key for agent's eval runs
                var cacheKey = $"EvalRuns:Agent:{agentId}";
                
                // Try to get from cache first using GetOrSetAsync pattern
                var evalRuns = await _cacheService.GetOrSetAsync(cacheKey, async () =>
                {
                    // Cache miss, fetch from storage using RequestHandler
                    _logger.LogInformation("Optimized cache miss, fetching eval runs from storage for AgentId: {AgentId}", agentId);
                    
                    // Use RequestHandler instead of creating service directly
                    var evalRunsList = await _evaluationResultRequestHandler.GetEvalRunsByAgentIdAsync(agentId);
                    return evalRunsList;
                    
                }, TimeSpan.FromMinutes(30)); // Cache for 30 minutes since this data changes less frequently

                _logger.LogInformation("Optimized successfully retrieved {Count} evaluation runs for AgentId: {AgentId}", evalRuns.Count, agentId);
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
        /// Get evaluation results for a specific agent within a date range - Optimized version with caching
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
                    return CreateBadRequestResponse<List<EvaluationResultResponseDto>>("agentId", "AgentId is required and cannot be empty");
                }

                if (startDateTime >= endDateTime)
                {
                    return CreateBadRequestResponse<List<EvaluationResultResponseDto>>("startDateTime", "StartDateTime must be earlier than EndDateTime");
                }

                if (endDateTime > DateTime.UtcNow)
                {
                    return CreateBadRequestResponse<List<EvaluationResultResponseDto>>("endDateTime", "EndDateTime cannot be in the future");
                }

                _logger.LogInformation("Optimized retrieving evaluation results for AgentId: {AgentId} between {StartDateTime} and {EndDateTime}", 
                    agentId, startDateTime, endDateTime);

                // Generate cache key for date range query (round to hour for better cache hits)
                var startKey = startDateTime.ToString("yyyyMMddHH");
                var endKey = endDateTime.ToString("yyyyMMddHH");
                var cacheKey = $"EvalResults:Agent:{agentId}:DateRange:{startKey}:{endKey}";
                
                // Try to get from cache first using GetOrSetAsync pattern
                var results = await _cacheService.GetOrSetAsync(cacheKey, async () =>
                {
                    // Cache miss, fetch from storage
                    _logger.LogInformation("Optimized cache miss, fetching date range results from storage for AgentId: {AgentId}", agentId);

                    // Get eval runs for agent within date range
                    var evalRuns = await _evalRunTableService.GetEvalRunsByAgentIdAsync(agentId);
                    var filteredRuns = evalRuns.Where(er => 
                        er.StartedDatetime >= startDateTime && 
                        er.StartedDatetime <= endDateTime &&
                        (er.Status == "Completed" || er.Status == "Failed")).ToList();

                    var resultsList = new List<EvaluationResultResponseDto>();

                    // For each eval run, try to get the evaluation results
                    foreach (var evalRun in filteredRuns)
                    {
                        try
                        {
                            var containerName = evalRun.ContainerName ?? evalRun.AgentId.Replace(" ", "");
                            var blobPath = $"{evalRun.BlobFilePath}results.json";

                            var content = await _azureBlobStorageService.ReadBlobContentAsync(containerName, blobPath);
                            if (!string.IsNullOrEmpty(content))
                            {
                                var evaluationRecords = System.Text.Json.JsonSerializer.Deserialize<JsonElement>(content,
                                    new System.Text.Json.JsonSerializerOptions
                                    {
                                        PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
                                    });

                                resultsList.Add(new EvaluationResultResponseDto
                                {
                                    Success = true,
                                    EvalRunId = evalRun.EvalRunId,
                                    EvaluationRecords = evaluationRecords,
                                    Message = "Evaluation results retrieved successfully"
                                });
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Could not retrieve results for EvalRunId: {EvalRunId}", evalRun.EvalRunId);
                            // Continue with other eval runs
                        }
                    }

                    return resultsList;
                    
                }, TimeSpan.FromMinutes(15)); // Cache for 15 minutes since date range queries are expensive

                _logger.LogInformation("Optimized successfully retrieved {Count} evaluation results for AgentId: {AgentId} in date range", results.Count, agentId);
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

        #endregion
    }
}