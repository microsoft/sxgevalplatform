using AutoMapper;
using Sxg.EvalPlatform.API.Storage;
using Sxg.EvalPlatform.API.Storage.TableEntities;
using SXG.EvalPlatform.Common;
using SxgEvalPlatformApi.Models;
using System.Text.Json;
using static SXG.EvalPlatform.Common.CommonConstants;

namespace SxgEvalPlatformApi.RequestHandlers
{
    /// <summary>
    /// Request handler for evaluation result operations
    /// </summary>
    public class EvaluationResultRequestHandler : IEvaluationResultRequestHandler
    {
        private readonly IEvalRunRequestHandler _evalRunRequestHandler;
        private readonly ILogger<EvaluationResultRequestHandler> _logger;
        private readonly IMapper _mapper;
        private readonly IConfigHelper _configHelper;
        private readonly StorageFactory _factory;

        public EvaluationResultRequestHandler(
            IEvalRunRequestHandler evalRunRequestHandler,
            ILogger<EvaluationResultRequestHandler> logger,
            IConfigHelper configHelper, 
            IMapper mapper,
            StorageFactory factory)
        {
            _evalRunRequestHandler = evalRunRequestHandler;
            _logger = logger;
            _mapper = mapper;
            _configHelper = configHelper;
            _factory = factory;
        }

        /// <summary>
        /// Save evaluation results to storage
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

                // Get internal entity details from storage
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

                // Handle container name and results path with support for folder structure
                string containerName;
                string filePath;
                string fileName = $"evaluation_results_{DateTime.UtcNow:yyyyMMdd_HHmmss}.json";
                
                if (!string.IsNullOrEmpty(evalRunEntity.ContainerName))
                {
                    // New format: use stored container name and results path
                    containerName = evalRunEntity.ContainerName;
                    // If FilePath is a folder (ends with /), append the filename
                    if (!string.IsNullOrEmpty(evalRunEntity.FilePath) && evalRunEntity.FilePath.EndsWith('/'))
                    {
                        filePath = $"{evalRunEntity.FilePath}{fileName}";
                    }
                    else
                    {
                        filePath = evalRunEntity.FilePath ?? $"{_configHelper.EvalResultsFolderName}/{evalRunId}/{fileName}";
                    }
                }
                else
                {
                    // Backward compatibility: parse the old format
                    containerName = CommonUtils.TrimAndRemoveSpaces(evalRunEntity.AgentId);
                    filePath = $"{_configHelper.EvalResultsFolderName}/{evalRunId}/{fileName}";
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

                // Save to storage
                string storageProvider = _configHelper.GetStorageProvider();
                var storage = _factory.GetProvider(storageProvider);
                await storage.WriteAsync(containerName, filePath, jsonContent);

                _logger.LogInformation("Successfully saved evaluation results for EvalRunId: {EvalRunId} to {FilePath}", 
                    evalRunId, $"{containerName}/{filePath}");

                return new EvaluationResultSaveResponseDto
                {
                    Success = true,
                    Message = "Evaluation results saved successfully",
                    EvalRunId = evalRunId,
                    ResultsPath = $"{containerName}/{filePath}"
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
        /// Get evaluation results by EvalRunId
        /// </summary>
        public async Task<EvaluationResultResponseDto> GetEvaluationResultByIdAsync(Guid evalRunId)
        {
            try
            {
                _logger.LogInformation("Retrieving evaluation results for EvalRunId: {EvalRunId}", evalRunId);
                var evalSummaryFilePath = $"evaluation-results/{evalRunId}_summary.json";
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

                // Get internal entity details from storage
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

                // Try to read summary first
                string summaryContent = null;
                string storageProvider = _configHelper.GetStorageProvider();
                var storage = _factory.GetProvider(storageProvider);
                var summaryExists = await storage.ExistsAsync(containerName, evalSummaryFilePath);
                if (summaryExists)
                {
                    summaryContent = await storage.ReadAsync(containerName, evalSummaryFilePath);
                    _logger.LogInformation("Successfully read summary from {ContainerName}/{FilePath}", 
                        containerName, evalSummaryFilePath);
                }
                else
                {
                    _logger.LogWarning("Summary not found at {ContainerName}/{FilePath}", 
                        containerName, evalSummaryFilePath);
                }

                // Try to read dataset results
                string datasetContent = null;
                var datasetExists = await storage.ExistsAsync(containerName, evalResultDatasetPath);
                if (datasetExists)
                {
                    datasetContent = await storage.ReadAsync(containerName, evalResultDatasetPath);
                    _logger.LogInformation("Successfully read dataset from {ContainerName}/{FilePath}", 
                        containerName, evalResultDatasetPath);
                }
                else
                {
                    _logger.LogWarning("Dataset not found at {ContainerName}/{FilePath}", 
                        containerName, evalResultDatasetPath);
                }

                // Check if summary and dataset exists
                if (!summaryExists && !datasetExists)
                {
                    _logger.LogInformation("No evaluation result data found for EvalRunId: {EvalRunId} in container: {ContainerName}. " +
                        "Tried paths: {SummaryPath}, {DatasetPath}.", 
                        evalRunId, containerName, evalSummaryFilePath, evalResultDatasetPath);
                    
                    // Fallback: try to find any evaluation results using the original logic
                    return await GetEvaluationResultByIdFallbackAsync(evalRunId, evalRun, evalRunEntity);
                }

                // Combine the results from both sources
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
                    SummaryPath = summaryExists ? evalSummaryFilePath : null,
                    DatasetPath = datasetExists ? evalResultDatasetPath : null,
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
                    FileName = summaryExists ? Path.GetFileName(evalSummaryFilePath) : Path.GetFileName(evalResultDatasetPath),
                    EvaluationRecords = combinedJsonElement
                };
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Invalid JSON format in evaluation results for EvalRunId: {EvalRunId}", evalRunId);
                return new EvaluationResultResponseDto
                {
                    Success = false,
                    Message = "Invalid evaluation results format",
                    EvalRunId = evalRunId
                };
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
        /// Fallback method to retrieve evaluation results using the original logic
        /// </summary>
        private async Task<EvaluationResultResponseDto> GetEvaluationResultByIdFallbackAsync(Guid evalRunId, EvalRunDto evalRun, EvalRunTableEntity evalRunEntity)
        {
            try
            {
                _logger.LogInformation("Using fallback method to retrieve evaluation results for EvalRunId: {EvalRunId}", evalRunId);

                // Handle container name and evalRun path with support for folder structure
                string containerName;
                string filePath;
                string storageProvider = _configHelper.GetStorageProvider();
                var storage = _factory.GetProvider(storageProvider);

                if (!string.IsNullOrEmpty(evalRunEntity.ContainerName))
                {
                    // New format: use stored container name and evalRun path
                    containerName = evalRunEntity.ContainerName;
                    // If FilePath is a folder (ends with /), look for evaluation results file dynamically
                    if (!string.IsNullOrEmpty(evalRunEntity.FilePath) && evalRunEntity.FilePath.EndsWith('/'))
                    {
                        // Search for evaluation results files in the folder
                        var evalResults = await storage.ListAsync(containerName, evalRunEntity.FilePath);
                        var evaluationResults = evalResults.FirstOrDefault(b => 
                            b.Contains("evaluation_results_") && b.EndsWith(".json"));
                        
                        if (evaluationResults != null)
                        {
                            filePath = evaluationResults;
                        }
                        else
                        {
                            // Fallback to looking for results.json for backward compatibility
                            filePath = $"{evalRunEntity.FilePath}results.json";
                        }
                    }
                    else
                    {
                        filePath = evalRunEntity.FilePath ?? $"evalresults/{evalRunId}/results.json";
                    }
                }
                else
                {
                    // Backward compatibility: parse the old format
                    containerName = CommonUtils.TrimAndRemoveSpaces(evalRunEntity.AgentId);
                    // Try to find the evaluation results file dynamically first
                    var folderPath = $"evalresults/{evalRunId}/";
                    var evalResults = await storage.ListAsync(containerName, folderPath);
                    var evaluationResults = evalResults.FirstOrDefault(b => 
                        b.Contains("evaluation_results_") && b.EndsWith(".json"));
                    
                    if (evaluationResults != null)
                    {
                        filePath = evaluationResults;
                    }
                    else
                    {
                        // Fallback to the old hardcoded name
                        filePath = $"evalresults/{evalRunId}/results.json";
                    }
                }

                // Check if evalResults exists
                var evalExists = await storage.ExistsAsync(containerName, filePath);
                if (!evalExists)
                {
                    _logger.LogInformation("Evaluation results not found for EvalRunId: {EvalRunId} in path {FilePath}. " +
                        "This could mean the evaluation run hasn't completed yet or something went wrong.", 
                        evalRunId, $"{containerName}/{filePath}");
                    
                    return new EvaluationResultResponseDto
                    {
                        Success = false,
                        Message = "Evaluation results not found. This could mean the evaluation run hasn't completed yet or something went wrong during the evaluation process.",
                        EvalRunId = evalRunId
                    };
                }

                // Read results content
                var jsonContent = await storage.ReadAsync(containerName, filePath);
                
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
                    FileName = evaluationResult?.FileName ?? Path.GetFileName(filePath),
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

        /// <summary>
        /// Get all evaluation runs for a specific agent
        /// </summary>
        public async Task<IList<EvalRunDto>> GetEvalRunsByAgentIdAsync(string agentId, DateTime? startDateTime, DateTime? endDateTime)
        {
            try
            {
                _logger.LogInformation("Retrieving evaluation runs for AgentId: {AgentId}", agentId);

                return await _evalRunRequestHandler.GetEvalRunsByAgentIdAsync(agentId, startDateTime, endDateTime);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving evaluation runs for AgentId: {AgentId}", agentId);
                throw;
            }
        }

        /// <summary>
        /// Get evaluation results for a specific agent within a date range
        /// </summary>
        public async Task<IList<EvaluationResultResponseDto>> GetEvaluationResultsByDateRangeAsync(string agentId, DateTime startDateTime, DateTime endDateTime)
        {
            try
            {
                _logger.LogInformation("Retrieving evaluation results for AgentId: {AgentId} between {StartDateTime} and {EndDateTime}", 
                    agentId, startDateTime, endDateTime);

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
                        // Get evaluation results for each run
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

       
    }
}