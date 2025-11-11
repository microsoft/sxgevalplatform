using AutoMapper;
using Sxg.EvalPlatform.API.Storage;
using Sxg.EvalPlatform.API.Storage.Services;
using Sxg.EvalPlatform.API.Storage.TableEntities;
using SXG.EvalPlatform.Common;
using SxgEvalPlatformApi.Models;
using System.Text.Json;
using static SXG.EvalPlatform.Common.CommonConstants;

namespace SxgEvalPlatformApi.RequestHandlers
{
    /// <summary>
    /// Request handler for evaluation result operations with caching support
    /// </summary>
    public class EvaluationResultRequestHandler : IEvaluationResultRequestHandler
    {
        private readonly IAzureBlobStorageService _blobService;
        private readonly IEvalRunRequestHandler _evalRunRequestHandler;
        private readonly ILogger<EvaluationResultRequestHandler> _logger;
        private readonly IMapper _mapper;
        private readonly IConfigHelper _configHelper;
        private readonly ICacheManager _cacheManager;

        // Cache key patterns
        private const string EVAL_RESULT_CACHE_KEY = "eval_result:{0}"; // eval_result:evalRunId
        private const string EVAL_RUNS_BY_AGENT_CACHE_KEY = "eval_runs_agent:{0}:{1}:{2}"; // eval_runs_agent:agentId:startDate:endDate
        private const string EVAL_RESULTS_BY_DATE_CACHE_KEY = "eval_results_date:{0}:{1}:{2}"; // eval_results_date:agentId:startDate:endDate

        public EvaluationResultRequestHandler(
            IAzureBlobStorageService blobService,
            IEvalRunRequestHandler evalRunRequestHandler,
            ILogger<EvaluationResultRequestHandler> logger,
            IConfigHelper configHelper,
            IMapper mapper,
            ICacheManager cacheManager)
        {
            _blobService = blobService;
            _evalRunRequestHandler = evalRunRequestHandler;
            _logger = logger;
            _mapper = mapper;
            _configHelper = configHelper;
            _cacheManager = cacheManager;
        }

        /// <summary>
        /// Save evaluation results to blob storage and update cache
        /// </summary>
        public async Task<EvaluationResultSaveResponseDto> SaveEvaluationResultAsync(Guid evalRunId, SaveEvaluationResultDto saveDto)
        {
            try
            {
                _logger.LogInformation("Saving evaluation results for EvalRunId: {EvalRunId}", evalRunId);

                // First, verify that the EvalRunId exists and get internal details
                var evalRun = await _evalRunRequestHandler.GetEvalRunByIdAsync(evalRunId);
                if (evalRun == null)
                {
                    _logger.LogWarning("EvalRunId not found: {EvalRunId}", evalRunId);
                    return new EvaluationResultSaveResponseDto
                    {
                        Success = false,
                        Message = $"EvalRunId '{evalRunId}' not found. Please provide a valid EvalRunId.",
                        EvalRunId = evalRunId
                    };
                }

                // Get internal entity details for blob storage
                var evalRunEntity = await _evalRunRequestHandler.GetEvalRunEntityByIdAsync(evalRunId);
                if (evalRunEntity == null)
                {
                    _logger.LogError("Could not retrieve internal entity details for EvalRunId: {EvalRunId}", evalRunId);
                    return new EvaluationResultSaveResponseDto
                    {
                        Success = false,
                        Message = "Failed to retrieve evaluation run details",
                        EvalRunId = evalRunId
                    };
                }

                // Validate that the evaluation run has a terminal status (Completed or Failed)
                var allowedStatuses = new[] { EvalRunStatus.EvalRunCompleted, EvalRunStatus.EvalRunFailed };
                if (!allowedStatuses.Any(status => string.Equals(status, evalRun.Status, StringComparison.OrdinalIgnoreCase)))
                {
                    _logger.LogWarning("Cannot save evaluation results for EvalRunId: {EvalRunId} with status: {Status}. Results can only be saved for evaluations with status 'Completed' or 'Failed'.",
                        evalRunId, evalRun.Status);
                    return new EvaluationResultSaveResponseDto
                    {
                        Success = false,
                        Message = $"Cannot save evaluation results for evaluation run with status '{evalRun.Status}'. Results can only be saved for evaluations with status 'Completed' or 'Failed'.",
                        EvalRunId = evalRunId
                    };
                }

                // Since we're now receiving flexible JSON structure directly,
                // we can use it as-is without transformation
                var evaluationRecordsJson = saveDto.EvaluationResults;

                // Handle container name and blob path with support for folder structure
                string containerName;
                string blobPath;
                string fileName = $"evaluation_results_{DateTime.UtcNow:yyyyMMdd_HHmmss}.json";

                if (!string.IsNullOrEmpty(evalRunEntity.ContainerName))
                {
                    // New format: use stored container name and blob path
                    containerName = evalRunEntity.ContainerName;
                    // If BlobFilePath is a folder (ends with /), append the filename
                    if (!string.IsNullOrEmpty(evalRunEntity.BlobFilePath) && evalRunEntity.BlobFilePath.EndsWith('/'))
                    {
                        blobPath = $"{evalRunEntity.BlobFilePath}{fileName}";
                    }
                    else
                    {
                        blobPath = evalRunEntity.BlobFilePath ?? $"{_configHelper.EvalResultsFolderName}/{evalRunId}/{fileName}";
                    }
                }
                else
                {
                    // Backward compatibility: parse the old format
                    containerName = CommonUtils.TrimAndRemoveSpaces(evalRunEntity.AgentId);
                    blobPath = $"{_configHelper.EvalResultsFolderName}/{evalRunId}/{fileName}";
                }

                // Serialize evaluation records to JSON using the storage model
                var storageModel = new StoredEvaluationResultDto
                {
                    EvalRunId = evalRunId,
                    FileName = fileName,
                    AgentId = evalRun.AgentId,
                    DataSetId = evalRun.DataSetId,
                    MetricsConfigurationId = evalRun.MetricsConfigurationId,
                    SavedAt = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"),
                    EvaluationResults = evaluationRecordsJson
                };

                var jsonContent = JsonSerializer.Serialize(storageModel, new JsonSerializerOptions
                {
                    WriteIndented = true
                });

                // Save to blob storage first (write to backend store)
                await _blobService.WriteBlobContentAsync(containerName, blobPath, jsonContent);

                // After successful write, update cache
                var cacheKey = string.Format(EVAL_RESULT_CACHE_KEY, evalRunId);
                var cacheResponse = new EvaluationResultResponseDto
                {
                    Success = true,
                    Message = "Evaluation results retrieved from cache",
                    EvalRunId = evalRunId,
                    FileName = fileName,
                    EvaluationRecords = evaluationRecordsJson
                };

                await _cacheManager.SetAsync(cacheKey, cacheResponse, TimeSpan.FromMinutes(60));

                // Also invalidate related caches
                await InvalidateRelatedCaches(evalRun.AgentId);

                _logger.LogInformation("Successfully saved evaluation results for EvalRunId: {EvalRunId} to {BlobPath} and updated cache",
                    evalRunId, $"{containerName}/{blobPath}");

                return new EvaluationResultSaveResponseDto
                {
                    Success = true,
                    Message = "Evaluation results saved successfully",
                    EvalRunId = evalRunId,
                    BlobPath = $"{containerName}/{blobPath}"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving evaluation results for EvalRunId: {EvalRunId}", evalRunId);
                return new EvaluationResultSaveResponseDto
                {
                    Success = false,
                    Message = "Failed to save evaluation results",
                    EvalRunId = evalRunId
                };
            }
        }

        /// <summary>
        /// Get evaluation results by EvalRunId with caching support
        /// </summary>
        public async Task<EvaluationResultResponseDto> GetEvaluationResultByIdAsync(Guid evalRunId)
        {
            try
            {
                _logger.LogInformation("Retrieving evaluation results for EvalRunId: {EvalRunId}", evalRunId);

                // Check cache first
                var cacheKey = string.Format(EVAL_RESULT_CACHE_KEY, evalRunId);
                var cachedResult = await _cacheManager.GetAsync<EvaluationResultResponseDto>(cacheKey);

                if (cachedResult != null)
                {
                    _logger.LogDebug("Returning cached evaluation results for EvalRunId: {EvalRunId}", evalRunId);
                    return cachedResult;
                }

                // If not in cache, fetch from storage
                var result = await GetEvaluationResultFromStorageAsync(evalRunId);

                // Cache the result if successful (only cache successful results)
                if (result.Success)
                {
                    await _cacheManager.SetAsync(cacheKey, result, TimeSpan.FromMinutes(60));
                    _logger.LogDebug("Cached evaluation results for EvalRunId: {EvalRunId}", evalRunId);
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving evaluation results for EvalRunId: {EvalRunId}", evalRunId);
                return new EvaluationResultResponseDto
                {
                    Success = false,
                    Message = "Failed to retrieve evaluation results",
                    EvalRunId = evalRunId
                };
            }
        }

        /// <summary>
        /// Get evaluation results from storage (original logic without caching)
        /// </summary>
        private async Task<EvaluationResultResponseDto> GetEvaluationResultFromStorageAsync(Guid evalRunId)
        {
            var evalSummaryBlobPath = $"evaluation-results/{evalRunId}_summary.json";
            var evalResultDatasetPath = $"evaluation-results/{evalRunId}_dataset.json";

            // First, verify that the EvalRunId exists and get internal details
            var evalRun = await _evalRunRequestHandler.GetEvalRunByIdAsync(evalRunId);
            if (evalRun == null)
            {
                _logger.LogWarning("EvalRunId not found: {EvalRunId}", evalRunId);
                return new EvaluationResultResponseDto
                {
                    Success = false,
                    Message = $"EvalRunId '{evalRunId}' not found. Please provide a valid EvalRunId.",
                    EvalRunId = evalRunId
                };
            }

            // Check if the evaluation run status is EvalRunCompleted
            if (!string.Equals(evalRun.Status, EvalRunStatus.EvalRunCompleted, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogInformation("Evaluation results not available for EvalRunId: {EvalRunId} with status: {Status}. Results are only available for completed evaluations.",
             evalRunId, evalRun.Status);

                return new EvaluationResultResponseDto
                {
                    Success = false,
                    Message = "Evaluation results not available. Results are only available for completed evaluations.",
                    EvalRunId = evalRunId
                };
            }

            // Get internal entity details for blob storage
            var evalRunEntity = await _evalRunRequestHandler.GetEvalRunEntityByIdAsync(evalRunId);
            if (evalRunEntity == null)
            {
                _logger.LogError("Could not retrieve internal entity details for EvalRunId: {EvalRunId}", evalRunId);
                return new EvaluationResultResponseDto
                {
                    Success = false,
                    Message = "Failed to retrieve evaluation run details",
                    EvalRunId = evalRunId
                };
            }

            // Determine container name
            string containerName;
            if (!string.IsNullOrEmpty(evalRunEntity.ContainerName))
            {
                containerName = evalRunEntity.ContainerName;
            }
            else
            {
                // Backward compatibility: parse the old format
                containerName = CommonUtils.TrimAndRemoveSpaces(evalRunEntity.AgentId);
            }

            _logger.LogInformation("Reading evaluation results from container: {ContainerName} for EvalRunId: {EvalRunId}",
               containerName, evalRunId);

            // Try to read summary blob first
            string summaryContent = null;
            var summaryExists = await _blobService.BlobExistsAsync(containerName, evalSummaryBlobPath);
            if (summaryExists)
            {
                summaryContent = await _blobService.ReadBlobContentAsync(containerName, evalSummaryBlobPath);
                _logger.LogInformation("Successfully read summary blob from {ContainerName}/{BlobPath}",
                         containerName, evalSummaryBlobPath);
            }
            else
            {
                _logger.LogWarning("Summary blob not found at {ContainerName}/{BlobPath}",
                containerName, evalSummaryBlobPath);
            }

            // Try to read dataset results blob
            string datasetContent = null;
            var datasetExists = await _blobService.BlobExistsAsync(containerName, evalResultDatasetPath);
            if (datasetExists)
            {
                datasetContent = await _blobService.ReadBlobContentAsync(containerName, evalResultDatasetPath);
                _logger.LogInformation("Successfully read dataset blob from {ContainerName}/{BlobPath}",
                   containerName, evalResultDatasetPath);
            }
            else
            {
                _logger.LogWarning("Dataset blob not found at {ContainerName}/{BlobPath}",
           containerName, evalResultDatasetPath);
            }

            // Check if at least one blob exists
            if (!summaryExists && !datasetExists)
            {
                _logger.LogInformation("No evaluation result blobs found for EvalRunId: {EvalRunId} in container: {ContainerName}. " +
              "Tried paths: {SummaryPath}, {DatasetPath}.",
                    evalRunId, containerName, evalSummaryBlobPath, evalResultDatasetPath);

                // Fallback: try to find any evaluation results using the original logic
                return await GetEvaluationResultByIdFallbackAsync(evalRunId, evalRun, evalRunEntity);
            }

            // Combine the results from both blobs
            var combinedResults = new Dictionary<string, object>();

            // Add summary data if available
            if (!string.IsNullOrEmpty(summaryContent))
            {
                try
                {
                    var summaryData = JsonSerializer.Deserialize<JsonElement>(summaryContent);
                    combinedResults["summary"] = summaryData;
                }
                catch (JsonException ex)
                {
                    _logger.LogWarning(ex, "Failed to parse summary JSON content for EvalRunId: {EvalRunId}", evalRunId);
                    combinedResults["summary"] = summaryContent; // Store as raw string if JSON parsing fails
                }
            }

            // Add dataset data if available
            if (!string.IsNullOrEmpty(datasetContent))
            {
                try
                {
                    var datasetData = JsonSerializer.Deserialize<JsonElement>(datasetContent);
                    combinedResults["dataset"] = datasetData;
                }
                catch (JsonException ex)
                {
                    _logger.LogWarning(ex, "Failed to parse dataset JSON content for EvalRunId: {EvalRunId}", evalRunId);
                    combinedResults["dataset"] = datasetContent; // Store as raw string if JSON parsing fails
                }
            }

            // Add metadata
            combinedResults["metadata"] = new
            {
                EvalRunId = evalRunId,
                AgentId = evalRun.AgentId,
                DataSetId = evalRun.DataSetId,
                MetricsConfigurationId = evalRun.MetricsConfigurationId,
                Status = evalRun.Status,
                CompletedDatetime = evalRun.CompletedDatetime,
                SummaryBlobPath = summaryExists ? evalSummaryBlobPath : null,
                DatasetBlobPath = datasetExists ? evalResultDatasetPath : null,
                RetrievedAt = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ")
            };

            // Serialize the combined results
            var combinedJsonElement = JsonSerializer.SerializeToElement(combinedResults, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = true
            });

            _logger.LogInformation("Successfully retrieved and combined evaluation results for EvalRunId: {EvalRunId}", evalRunId);

            return new EvaluationResultResponseDto
            {
                Success = true,
                Message = "Evaluation results retrieved successfully",
                EvalRunId = evalRunId,
                FileName = summaryExists ? Path.GetFileName(evalSummaryBlobPath) : Path.GetFileName(evalResultDatasetPath),
                EvaluationRecords = combinedJsonElement
            };
        }

        /// <summary>
        /// Get all evaluation runs for a specific agent with caching support
        /// </summary>
        public async Task<IList<EvalRunDto>> GetEvalRunsByAgentIdAsync(string agentId, DateTime? startDateTime, DateTime? endDateTime)
        {
            try
            {
                _logger.LogInformation("Retrieving evaluation runs for AgentId: {AgentId}", agentId);

                // Create cache key
                var startDateStr = startDateTime?.ToString("yyyyMMdd") ?? "null";
                var endDateStr = endDateTime?.ToString("yyyyMMdd") ?? "null";
                var cacheKey = string.Format(EVAL_RUNS_BY_AGENT_CACHE_KEY, agentId, startDateStr, endDateStr);

                // Check cache first
                var cachedResult = await _cacheManager.GetAsync<IList<EvalRunDto>>(cacheKey);
                if (cachedResult != null)
                {
                    _logger.LogDebug("Returning cached evaluation runs for AgentId: {AgentId}", agentId);
                    return cachedResult;
                }

                // If not in cache, fetch from storage
                var result = await _evalRunRequestHandler.GetEvalRunsByAgentIdAsync(agentId, startDateTime, endDateTime);

                // Cache the result (cache for 30 minutes for list queries)
                await _cacheManager.SetAsync(cacheKey, result, TimeSpan.FromMinutes(30));
                _logger.LogDebug("Cached evaluation runs for AgentId: {AgentId}", agentId);

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving evaluation runs for AgentId: {AgentId}", agentId);
                throw;
            }
        }

        /// <summary>
        /// Get evaluation results for a specific agent within a date range with caching support
        /// </summary>
        public async Task<IList<EvaluationResultResponseDto>> GetEvaluationResultsByDateRangeAsync(string agentId, DateTime startDateTime, DateTime endDateTime)
        {
            try
            {
                _logger.LogInformation("Retrieving evaluation results for AgentId: {AgentId} between {StartDateTime} and {EndDateTime}",
                    agentId, startDateTime, endDateTime);

                // Create cache key
                var startDateStr = startDateTime.ToString("yyyyMMdd");
                var endDateStr = endDateTime.ToString("yyyyMMdd");
                var cacheKey = string.Format(EVAL_RESULTS_BY_DATE_CACHE_KEY, agentId, startDateStr, endDateStr);

                // Check cache first
                var cachedResult = await _cacheManager.GetAsync<IList<EvaluationResultResponseDto>>(cacheKey);
                if (cachedResult != null)
                {
                    _logger.LogDebug("Returning cached evaluation results by date range for AgentId: {AgentId}", agentId);
                    return cachedResult;
                }

                // If not in cache, fetch from storage
                // Get all evaluation runs for the agent
                var evalRuns = await _evalRunRequestHandler.GetEvalRunsByAgentIdAsync(agentId, startDateTime, endDateTime);

                // Filter runs within the date range and only include completed ones
                var filteredRuns = evalRuns
                .Where(run => run.StartedDatetime.HasValue &&
                         run.StartedDatetime.Value >= startDateTime &&
                  run.StartedDatetime.Value <= endDateTime &&
               string.Equals(run.Status, EvalRunStatus.EvalRunCompleted, StringComparison.OrdinalIgnoreCase))
                      .ToList();

                _logger.LogInformation("Found {Count} completed evaluation runs within date range for AgentId: {AgentId}",
              filteredRuns.Count, agentId);

                var results = new List<EvaluationResultResponseDto>();

                foreach (var evalRun in filteredRuns)
                {
                    try
                    {
                        // Get evaluation results for each run (this will use caching from GetEvaluationResultByIdAsync)
                        var result = await GetEvaluationResultByIdAsync(evalRun.EvalRunId);

                        if (result.Success)
                        {
                            results.Add(result);
                        }
                        else
                        {
                            _logger.LogWarning("Could not retrieve results for EvalRunId: {EvalRunId}. Reason: {Message}",
                                evalRun.EvalRunId, result.Message);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error retrieving results for EvalRunId: {EvalRunId}", evalRun.EvalRunId);
                        // Continue with other runs instead of failing completely
                    }
                }

                // Cache the result (cache for 20 minutes for aggregated queries)
                await _cacheManager.SetAsync(cacheKey, results, TimeSpan.FromMinutes(20));
                _logger.LogDebug("Cached evaluation results by date range for AgentId: {AgentId}", agentId);

                _logger.LogInformation("Successfully retrieved {Count} evaluation results for AgentId: {AgentId}",
                               results.Count, agentId);

                return results;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving evaluation results for AgentId: {AgentId} between {StartDateTime} and {EndDateTime}",
               agentId, startDateTime, endDateTime);
                throw;
            }
        }

        /// <summary>
        /// Invalidate related caches when data is modified
        /// </summary>
        private async Task InvalidateRelatedCaches(string agentId)
        {
            try
            {
                // We can't easily invalidate wildcard patterns, so we'll log this for now
                // In a production system, you might want to implement cache tagging or use a more sophisticated cache invalidation strategy
                _logger.LogDebug("Invalidating related caches for AgentId: {AgentId}", agentId);

                // For now, we just log the invalidation need
                // In a more sophisticated implementation, you could:
                // 1. Use cache tags to track related cache entries
                // 2. Store a list of active cache keys and iterate through them
                // 3. Use Redis SCAN command to find matching patterns (for Redis cache)

                var statistics = await _cacheManager.GetStatisticsAsync();
                _logger.LogDebug("Cache statistics after potential invalidation - Type: {CacheType}, Items: {ItemCount}",
                statistics.CacheType, statistics.ItemCount);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error invalidating related caches for AgentId: {AgentId}", agentId);
                // Don't throw - cache invalidation failure shouldn't break the main operation
            }
        }

        // ... rest of the existing methods remain the same (GetEvaluationResultByIdFallbackAsync, etc)

        /// <summary>
        /// Fallback method to retrieve evaluation results using the original logic
        /// </summary>
        private async Task<EvaluationResultResponseDto> GetEvaluationResultByIdFallbackAsync(Guid evalRunId, EvalRunDto evalRun, EvalRunTableEntity evalRunEntity)
        {
            try
            {
                _logger.LogInformation("Using fallback method to retrieve evaluation results for EvalRunId: {EvalRunId}", evalRunId);

                // Handle container name and blob path with support for folder structure
                string containerName;
                string blobPath;

                if (!string.IsNullOrEmpty(evalRunEntity.ContainerName))
                {
                    // New format: use stored container name and blob path
                    containerName = evalRunEntity.ContainerName;
                    // If BlobFilePath is a folder (ends with /), look for evaluation results file dynamically
                    if (!string.IsNullOrEmpty(evalRunEntity.BlobFilePath) && evalRunEntity.BlobFilePath.EndsWith('/'))
                    {
                        // Search for evaluation results files in the folder
                        var blobs = await _blobService.ListBlobsAsync(containerName, evalRunEntity.BlobFilePath);
                        var evaluationResultBlob = blobs.FirstOrDefault(b =>
                       b.Contains("evaluation_results_") && b.EndsWith(".json"));

                        if (evaluationResultBlob != null)
                        {
                            blobPath = evaluationResultBlob;
                        }
                        else
                        {
                            // Fallback to looking for results.json for backward compatibility
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
                    // Backward compatibility: parse the old format
                    containerName = CommonUtils.TrimAndRemoveSpaces(evalRunEntity.AgentId);
                    // Try to find the evaluation results file dynamically first
                    var folderPath = $"evalresults/{evalRunId}/";
                    var blobs = await _blobService.ListBlobsAsync(containerName, folderPath);
                    var evaluationResultBlob = blobs.FirstOrDefault(b =>
               b.Contains("evaluation_results_") && b.EndsWith(".json"));

                    if (evaluationResultBlob != null)
                    {
                        blobPath = evaluationResultBlob;
                    }
                    else
                    {
                        // Fallback to the old hardcoded name
                        blobPath = $"evalresults/{evalRunId}/results.json";
                    }
                }

                // Check if blob exists
                var blobExists = await _blobService.BlobExistsAsync(containerName, blobPath);
                if (!blobExists)
                {
                    _logger.LogInformation("Evaluation results not found for EvalRunId: {EvalRunId} in path {BlobPath}. " +
                           "This could mean the evaluation run hasn't completed yet or something went wrong.",
                         evalRunId, $"{containerName}/{blobPath}");

                    return new EvaluationResultResponseDto
                    {
                        Success = false,
                        Message = "Evaluation results not found. This could mean the evaluation run hasn't completed yet or something went wrong during the evaluation process.",
                        EvalRunId = evalRunId
                    };
                }

                // Read blob content
                var jsonContent = await _blobService.ReadBlobContentAsync(containerName, blobPath);

                if (string.IsNullOrEmpty(jsonContent))
                {
                    _logger.LogWarning("Empty evaluation results content for EvalRunId: {EvalRunId}", evalRunId);
                    return new EvaluationResultResponseDto
                    {
                        Success = false,
                        Message = "Evaluation results are empty",
                        EvalRunId = evalRunId
                    };
                }

                // Deserialize the content using the storage model
                var evaluationResult = JsonSerializer.Deserialize<StoredEvaluationResultDto>(jsonContent);

                _logger.LogInformation("Successfully retrieved evaluation results using fallback method for EvalRunId: {EvalRunId}", evalRunId);

                return new EvaluationResultResponseDto
                {
                    Success = true,
                    Message = "Evaluation results retrieved successfully (fallback)",
                    EvalRunId = evalRunId,
                    FileName = evaluationResult?.FileName ?? Path.GetFileName(blobPath),
                    EvaluationRecords = evaluationResult?.EvaluationResults
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in fallback method for EvalRunId: {EvalRunId}", evalRunId);
                return new EvaluationResultResponseDto
                {
                    Success = false,
                    Message = "Failed to retrieve evaluation results using fallback method",
                    EvalRunId = evalRunId
                };
            }
        }
    }
}