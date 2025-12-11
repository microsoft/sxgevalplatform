using AutoMapper;
using Sxg.EvalPlatform.API.Storage;
using Sxg.EvalPlatform.API.Storage.Services;
using Sxg.EvalPlatform.API.Storage.TableEntities;
using SXG.EvalPlatform.Common;
using SxgEvalPlatformApi.Models;
using SxgEvalPlatformApi.Services;
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
        private readonly IEvalRunTableService _evalRunTableService;
        private readonly IMetricsConfigTableService _metricsConfigTableService;
        private readonly IDataSetTableService _dataSetTableService;
        private readonly ILogger<EvaluationResultRequestHandler> _logger;
        private readonly IMapper _mapper;
        private readonly IConfigHelper _configHelper;
        private readonly ICacheManager _cacheManager;
        private readonly ICallerIdentificationService _callerService;
        private readonly IMessagePublisher _messagePublisher;

        public EvaluationResultRequestHandler(
            IAzureBlobStorageService blobService,
            IEvalRunTableService evalRunTableService,
            IMetricsConfigTableService metricsConfigTableService,
            IDataSetTableService dataSetTableService,
            ILogger<EvaluationResultRequestHandler> logger,
            IConfigHelper configHelper,
            IMapper mapper,
            ICacheManager cacheManager,
            ICallerIdentificationService callerService,
            IMessagePublisher messagePublisher)
        {
            _blobService = blobService;
            _evalRunTableService = evalRunTableService;
            _logger = logger;
            _mapper = mapper;
            _configHelper = configHelper;
            _cacheManager = cacheManager;
            _metricsConfigTableService = metricsConfigTableService;
            _dataSetTableService = dataSetTableService;
            _callerService = callerService;
            _messagePublisher = messagePublisher;
        }


        /// <summary>
        /// Save evaluation results to blob storage and update cache
        /// </summary>
        public async Task<(EvaluationResultSaveResponseDto? EvalResponse, APIRequestProcessingResultDto? RequestProcesingResult)> SaveEvaluationResultAsync(Guid evalRunId, SaveEvaluationResultDto saveDto)
        {
            // Get caller information from the service
            var callerId = _callerService.GetCurrentUserId();
            var callerEmail = _callerService.GetCurrentUserEmail();
            
            try
            {
                string evalSummaryFileName = $"evaluation-results/{evalRunId}_summary.json";
                string evalDatasetFileName = $"evaluation-results/{evalRunId}_dataset.json";

                _logger.LogInformation("Saving evaluation results for EvalRunId: {EvalRunId}, Caller: {CallerEmail} ({CallerId})", 
        evalRunId, callerEmail, callerId);

                // First, verify that the EvalRunId exists and get internal details
                var evalRunEntity = await _evalRunTableService.GetEvalRunByIdAsync(evalRunId);

                if (evalRunEntity == null)
                {
                    _logger.LogWarning("EvalRunId not found: {EvalRunId}, Requested by: {CallerEmail}", evalRunId, callerEmail);
                    return (null, new APIRequestProcessingResultDto
                    {
                        IsSuccessful = false,
                        Message = $"EvalRunId '{evalRunId}' not found. Please provide a valid EvalRunId.",
                        StatusCode = System.Net.HttpStatusCode.BadRequest
                    });

                }
                
                var evalResultSummary = saveDto.EvaluationResultSummary; 
                var evalResultDataset = saveDto.EvaluationResultDataset;

                // Handle container name and blob path with support for folder structure
                string containerName;

                if (!string.IsNullOrEmpty(evalRunEntity.ContainerName))
                {
                    containerName = evalRunEntity.ContainerName;
                }
                else
                {
                    containerName = CommonUtils.TrimAndRemoveSpaces(evalRunEntity.AgentId);
                }

                // Save to blob storage first (write to backend store)
                await _blobService.WriteBlobContentAsync(containerName, evalSummaryFileName, evalResultSummary.ToString());

                _logger.LogInformation("Successfully saved evaluation result summary for EvalRunId: {EvalRunId} to {BlobPath}, Saved by: {CallerEmail}",
                    evalRunId, $"{containerName}/{evalSummaryFileName}", callerEmail);

                //Write the same to service bus
                await _messagePublisher.SendMessageAsync("evalresults", evalResultDataset.ToString());
                
                _logger.LogInformation("Successfully pushed evaluation result details for EvalRunId: {EvalRunId} to the downstream",
                    evalRunId);

                await _blobService.WriteBlobContentAsync(containerName, evalDatasetFileName, evalResultDataset.ToString());
                                
                _logger.LogInformation("Successfully saved evaluation result dataset for EvalRunId: {EvalRunId} to {BlobPath}, Saved by: {CallerEmail}",
                    evalRunId, $"{containerName}/{evalDatasetFileName}", callerEmail);

                // Delete enriched dataset file after successfully saving evaluation results
                await DeleteEnrichedDatasetAsync(evalRunId, containerName);

                return (new EvaluationResultSaveResponseDto
                {
                    EvalRunId = evalRunId
                }, new APIRequestProcessingResultDto
                {
                    IsSuccessful = true,
                    Message = "Evaluation results saved successfully",
                    StatusCode = System.Net.HttpStatusCode.OK
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving evaluation results for EvalRunId: {EvalRunId}, Caller: {CallerEmail}", evalRunId, callerEmail);
                
                return (null, new APIRequestProcessingResultDto
                {
                    IsSuccessful = false,
                    Message = "Failed to save evaluation results",
                    StatusCode = System.Net.HttpStatusCode.InternalServerError
                });
            }
        }

        /// <summary>
        /// Get evaluation results by EvalRunId with caching support
        /// </summary>
        public async Task<(EvaluationResultResponseDto? EvalResponse, APIRequestProcessingResultDto? RequestProcesingResult)> GetEvaluationResultByIdAsync(Guid evalRunId)
        {
            try
            {
                _logger.LogInformation("Retrieving evaluation results for EvalRunId: {EvalRunId}", evalRunId);
                                
                var result = await GetEvaluationResultFromStorageAsync(evalRunId);
                                
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving evaluation results for EvalRunId: {EvalRunId}", evalRunId);
                throw; 
            }
        }

        /// <summary>
        /// Get evaluation results from storage (original logic without caching)
        /// </summary>
        private async Task<(EvaluationResultResponseDto?, APIRequestProcessingResultDto?)> GetEvaluationResultFromStorageAsync(Guid evalRunId)
        {
            var evalSummaryBlobPath = $"evaluation-results/{evalRunId}_summary.json";
            var evalResultDatasetPath = $"evaluation-results/{evalRunId}_dataset.json";

            // First, verify that the EvalRunId exists and get internal details
            var evalRunEntity = await _evalRunTableService.GetEvalRunByIdAsync(evalRunId);

            if (evalRunEntity == null)
            {
                _logger.LogWarning("EvalRunId not found: {EvalRunId}", evalRunId);
                return (null, new APIRequestProcessingResultDto
                {
                    IsSuccessful = false,
                    Message = $"EvalRunId '{evalRunId}' not found. Please provide a valid EvalRunId.",
                    StatusCode = System.Net.HttpStatusCode.NotFound
                });
            }

            // Check if the evaluation run status is EvalRunCompleted
            if (!string.Equals(evalRunEntity.Status, EvalRunStatus.EvalRunCompleted, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogInformation("Evaluation results not available for EvalRunId: {EvalRunId} with status: {Status}. Results are only available for completed evaluations.", evalRunId, evalRunEntity.Status);
                return (null, new APIRequestProcessingResultDto
                {
                    IsSuccessful = false,
                    Message = "Evaluation results not yet available. Results are only available for completed evaluations.",
                    StatusCode = System.Net.HttpStatusCode.BadRequest
                });
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
                _logger.LogWarning($"Dataset blob not found at {containerName}/{evalResultDatasetPath}"); 
            }

            // Check if at least one blob exists
            if (!summaryExists && !datasetExists)
            {
                _logger.LogInformation($"No evaluation result blobs found for EvalRunId: {evalRunId} in container: {containerName}. Tried paths: {evalSummaryBlobPath}, {evalResultDatasetPath}.");
                return (null, new APIRequestProcessingResultDto
                {
                    IsSuccessful = false,
                    Message = "Evaluation results not found. This could mean the evaluation run hasn't completed yet or something went wrong during the evaluation process.",
                    StatusCode = System.Net.HttpStatusCode.NotFound
                });
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

            // Serialize the combined results
            var combinedJsonElement = JsonSerializer.SerializeToElement(combinedResults, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = true
            });

            _logger.LogInformation("Successfully retrieved and combined evaluation results for EvalRunId: {EvalRunId}", evalRunId);

            var result =  new EvaluationResultResponseDto
            {
                                
                EvalRunId = evalRunId.ToString(),
                AgentId = evalRunEntity.AgentId.ToString(),
                MetricsConfigurationId = evalRunEntity.MetricsConfigurationId.ToString(),
                DataSetId = evalRunEntity.DataSetId.ToString(),
                Status = evalRunEntity.Status.ToString(),
                StartedAt = evalRunEntity.StartedDatetime,
                CompletedAt = evalRunEntity.CompletedDatetime,
                DataSetName = await GetDataSetName(evalRunEntity.DataSetId),
                MetricsConfigurationName = await GetMetricsConfigurationName(evalRunEntity.MetricsConfigurationId),
                EvaluationRecords = combinedJsonElement
            };

            return (result, new APIRequestProcessingResultDto() { IsSuccessful = true, Message = string.Empty, StatusCode = System.Net.HttpStatusCode.OK});
        }

        private async Task<string> GetMetricsConfigurationName(string metricsConfigurationId)
        {
            var result = await _metricsConfigTableService.GetMetricsConfigurationByConfigurationIdAsync(metricsConfigurationId);
            return result?.ConfigurationName ?? string.Empty;
        }

        private async Task<string> GetDataSetName(string dataSetId)
        {
            var result = await _dataSetTableService.GetDataSetByIdAsync(dataSetId);
            return result?.DatasetName ?? string.Empty;
        }

        /// <summary>
        /// Get all evaluation runs for a specific agent with caching support
        /// </summary>
        public async Task<IList<EvalRunDto>> GetEvalRunsByAgentIdAsync(string agentId, DateTime? startDateTime, DateTime? endDateTime)
        {
            try
            {
                _logger.LogInformation("Retrieving evaluation runs for AgentId: {AgentId}", agentId);
                var result = await _evalRunTableService.GetEvalRunsByAgentIdAndDateFilterAsync(agentId, startDateTime, endDateTime);

                return _mapper.Map<IList<EvalRunTableEntity>, IList<EvalRunDto>>(result); 
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
        //public async Task<IList<EvaluationResultResponseDto>> GetEvaluationResultsByDateRangeAsync(string agentId, DateTime startDateTime, DateTime endDateTime)
        //{
        //    try
        //    {
        //        _logger.LogInformation("Retrieving evaluation results for AgentId: {AgentId} between {StartDateTime} and {EndDateTime}",
        //            agentId, startDateTime, endDateTime);

                
        //        // If not in cache, fetch from storage
        //        // Get all evaluation runs for the agent
        //        var evalRuns = await _evalRunTableService.GetEvalRunsByAgentIdAndDateFilterAsync(agentId, startDateTime, endDateTime);

        //        // Filter runs within the date range and only include completed ones
        //        var filteredRuns = evalRuns
        //        .Where(run => run.StartedDatetime.HasValue &&
        //                 run.StartedDatetime.Value >= startDateTime &&
        //          run.StartedDatetime.Value <= endDateTime &&
        //       string.Equals(run.Status, EvalRunStatus.EvalRunCompleted, StringComparison.OrdinalIgnoreCase))
        //              .ToList();

        //        _logger.LogInformation("Found {Count} completed evaluation runs within date range for AgentId: {AgentId}",
        //      filteredRuns.Count, agentId);

        //        var results = new List<EvaluationResultResponseDto>();

        //        foreach (var evalRun in filteredRuns)
        //        {
        //            try
        //            {
        //                // Get evaluation results for each run (this will use caching from GetEvaluationResultByIdAsync)
        //                var result = await GetEvaluationResultByIdAsync(evalRun.EvalRunId);

        //                if (result.Success)
        //                {
        //                    results.Add(result);
        //                }
        //                else
        //                {
        //                    _logger.LogWarning("Could not retrieve results for EvalRunId: {EvalRunId}. Reason: {Message}",
        //                        evalRun.EvalRunId, result.Message);
        //                }
        //            }
        //            catch (Exception ex)
        //            {
        //                _logger.LogError(ex, "Error retrieving results for EvalRunId: {EvalRunId}", evalRun.EvalRunId);
        //                // Continue with other runs instead of failing completely
        //            }
        //        }

        //        // Cache the result (cache for 20 minutes for aggregated queries)
        //        await _cacheManager.SetAsync(cacheKey, results, TimeSpan.FromMinutes(20));
        //        _logger.LogDebug("Cached evaluation results by date range for AgentId: {AgentId}", agentId);

        //        _logger.LogInformation("Successfully retrieved {Count} evaluation results for AgentId: {AgentId}",
        //                       results.Count, agentId);

        //        return results;
        //    }
        //    catch (Exception ex)
        //    {
        //        _logger.LogError(ex, "Error retrieving evaluation results for AgentId: {AgentId} between {StartDateTime} and {EndDateTime}",
        //       agentId, startDateTime, endDateTime);
        //        throw;
        //    }
        //}

        /// <summary>
        /// Invalidate related caches when data is modified
        /// </summary>
        //private async Task InvalidateRelatedCaches(string agentId)
        //{
        //    try
        //    {
        //        // We can't easily invalidate wildcard patterns, so we'll log this for now
        //        // In a production system, you might want to implement cache tagging or use a more sophisticated cache invalidation strategy
        //        _logger.LogDebug("Invalidating related caches for AgentId: {AgentId}", agentId);

        //        // For now, we just log the invalidation need
        //        // In a more sophisticated implementation, you could:
        //        // 1. Use cache tags to track related cache entries
        //        // 2. Store a list of active cache keys and iterate through them
        //        // 3. Use Redis SCAN command to find matching patterns (for Redis cache)

        //        var statistics = await _cacheManager.GetStatisticsAsync();
        //        _logger.LogDebug("Cache statistics after potential invalidation - Type: {CacheType}, Items: {ItemCount}",
        //        statistics.CacheType, statistics.ItemCount);
        //    }
        //    catch (Exception ex)
        //    {
        //        _logger.LogWarning(ex, "Error invalidating related caches for AgentId: {AgentId}", agentId);
        //        // Don't throw - cache invalidation failure shouldn't break the main operation
        //    }
        //}

        // ... rest of the existing methods remain the same (GetEvaluationResultByIdFallbackAsync, etc)

        /// <summary>
        /// Fallback method to retrieve evaluation results using the original logic
        /// </summary>
        //private async Task<EvaluationResultResponseDto> GetEvaluationResultByIdFallbackAsync(Guid evalRunId, EvalRunDto evalRun, EvalRunTableEntity evalRunEntity)
        //{
        //    try
        //    {
        //        _logger.LogInformation("Using fallback method to retrieve evaluation results for EvalRunId: {EvalRunId}", evalRunId);

        //        // Handle container name and blob path with support for folder structure
        //        string containerName;
        //        string blobPath;

        //        if (!string.IsNullOrEmpty(evalRunEntity.ContainerName))
        //        {
        //            // New format: use stored container name and blob path
        //            containerName = evalRunEntity.ContainerName;
        //            // If BlobFilePath is a folder (ends with /), look for evaluation results file dynamically
        //            if (!string.IsNullOrEmpty(evalRunEntity.BlobFilePath) && evalRunEntity.BlobFilePath.EndsWith('/'))
        //            {
        //                // Search for evaluation results files in the folder
        //                var blobs = await _blobService.ListBlobsAsync(containerName, evalRunEntity.BlobFilePath);
        //                var evaluationResultBlob = blobs.FirstOrDefault(b =>
        //               b.Contains("evaluation_results_") && b.EndsWith(".json"));

        //                if (evaluationResultBlob != null)
        //                {
        //                    blobPath = evaluationResultBlob;
        //                }
        //                else
        //                {
        //                    // Fallback to looking for results.json for backward compatibility
        //                    blobPath = $"{evalRunEntity.BlobFilePath}results.json";
        //                }
        //            }
        //            else
        //            {
        //                blobPath = evalRunEntity.BlobFilePath ?? $"evalresults/{evalRunId}/results.json";
        //            }
        //        }
        //        else
        //        {
        //            // Backward compatibility: parse the old format
        //            containerName = CommonUtils.TrimAndRemoveSpaces(evalRunEntity.AgentId);
        //            // Try to find the evaluation results file dynamically first
        //            var folderPath = $"evalresults/{evalRunId}/";
        //            var blobs = await _blobService.ListBlobsAsync(containerName, folderPath);
        //            var evaluationResultBlob = blobs.FirstOrDefault(b =>
        //       b.Contains("evaluation_results_") && b.EndsWith(".json"));

        //            if (evaluationResultBlob != null)
        //            {
        //                blobPath = evaluationResultBlob;
        //            }
        //            else
        //            {
        //                // Fallback to the old hardcoded name
        //                blobPath = $"evalresults/{evalRunId}/results.json";
        //            }
        //        }

        //        // Check if blob exists
        //        var blobExists = await _blobService.BlobExistsAsync(containerName, blobPath);
        //        if (!blobExists)
        //        {
        //            _logger.LogInformation("Evaluation results not found for EvalRunId: {EvalRunId} in path {BlobPath}. " +
        //                   "This could mean the evaluation run hasn't completed yet or something went wrong.",
        //                 evalRunId, $"{containerName}/{blobPath}");

        //            return new EvaluationResultResponseDto
        //            {
        //                Success = false,
        //                Message = "Evaluation results not found. This could mean the evaluation run hasn't completed yet or something went wrong during the evaluation process.",
        //                EvalRunId = evalRunId
        //            };
        //        }

        //        // Read blob content
        //        var jsonContent = await _blobService.ReadBlobContentAsync(containerName, blobPath);

        //        if (string.IsNullOrEmpty(jsonContent))
        //        {
        //            _logger.LogWarning("Empty evaluation results content for EvalRunId: {EvalRunId}", evalRunId);
        //            return new EvaluationResultResponseDto
        //            {
        //                Success = false,
        //                Message = "Evaluation results are empty",
        //                EvalRunId = evalRunId
        //            };
        //        }

        //        // Deserialize the content using the storage model
        //        var evaluationResult = JsonSerializer.Deserialize<StoredEvaluationResultDto>(jsonContent);

        //        _logger.LogInformation("Successfully retrieved evaluation results using fallback method for EvalRunId: {EvalRunId}", evalRunId);

        //        return new EvaluationResultResponseDto
        //        {
        //            Success = true,
        //            Message = "Evaluation results retrieved successfully (fallback)",
        //            EvalRunId = evalRunId,
        //            FileName = evaluationResult?.FileName ?? Path.GetFileName(blobPath),
        //            EvaluationRecords = evaluationResult?.EvaluationResults
        //        };
        //    }
        //    catch (Exception ex)
        //    {
        //        _logger.LogError(ex, "Error in fallback method for EvalRunId: {EvalRunId}", evalRunId);
        //        return new EvaluationResultResponseDto
        //        {
        //            Success = false,
        //            Message = "Failed to retrieve evaluation results using fallback method",
        //            EvalRunId = evalRunId
        //        };
        //    }
        //}

        /// <summary>
        /// Delete enriched dataset file for the given evaluation run
        /// </summary>
        /// <param name="evalRunId">Evaluation run ID</param>
        /// <param name="containerName">Container name where the enriched dataset is stored</param>
        private async Task DeleteEnrichedDatasetAsync(Guid evalRunId, string containerName)
        {
            try
            {
                var enrichedDatasetPath = $"enriched-datasets/{evalRunId}.json";

                _logger.LogInformation("Attempting to delete enriched dataset for EvalRunId: {EvalRunId} at path: {BlobPath}",
                 evalRunId, $"{containerName}/{enrichedDatasetPath}");

                // Check if the enriched dataset blob exists
                var blobExists = await _blobService.BlobExistsAsync(containerName, enrichedDatasetPath);

                if (!blobExists)
                {
                    _logger.LogInformation("Enriched dataset not found for EvalRunId: {EvalRunId} at path: {BlobPath}. Skipping deletion.",
                 evalRunId, $"{containerName}/{enrichedDatasetPath}");
                    return;
                }

                // Delete the enriched dataset blob
                var deleteSuccess = await _blobService.DeleteBlobAsync(containerName, enrichedDatasetPath);

                if (deleteSuccess)
                {
                    _logger.LogInformation("Successfully deleted enriched dataset for EvalRunId: {EvalRunId} from {BlobPath}",
   evalRunId, $"{containerName}/{enrichedDatasetPath}");
                }
                else
                {
                    _logger.LogWarning("Failed to delete enriched dataset for EvalRunId: {EvalRunId} from {BlobPath}",
          evalRunId, $"{containerName}/{enrichedDatasetPath}");
                }
            }
            catch (Exception ex)
            {
                // Log the error but don't fail the save operation
                // The enriched dataset deletion is a cleanup operation and shouldn't block saving results
                _logger.LogError(ex, "Error deleting enriched dataset for EvalRunId: {EvalRunId}. " +
                "Evaluation results were saved successfully, but enriched dataset cleanup failed.", evalRunId);
            }
        }
    }
}