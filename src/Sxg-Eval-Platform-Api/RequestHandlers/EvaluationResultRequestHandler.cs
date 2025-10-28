using AutoMapper;
using Sxg.EvalPlatform.API.Storage;
using Sxg.EvalPlatform.API.Storage.Services;
using Sxg.EvalPlatform.API.Storage.TableEntities;
using SXG.EvalPlatform.Common;
using SxgEvalPlatformApi.Models;
using System.Linq;
using System.Text.Json;
using static SXG.EvalPlatform.Common.CommonConstants;

namespace SxgEvalPlatformApi.RequestHandlers
{
    /// <summary>
    /// Request handler for evaluation result operations
    /// </summary>
    public class EvaluationResultRequestHandler : IEvaluationResultRequestHandler
    {
        private readonly IAzureBlobStorageService _blobService;
        private readonly IEvalRunRequestHandler _evalRunRequestHandler;
        private readonly ILogger<EvaluationResultRequestHandler> _logger;
        private readonly IMapper _mapper;
        private readonly IConfigHelper _configHelper;

        public EvaluationResultRequestHandler(
            IAzureBlobStorageService blobService,
            IEvalRunRequestHandler evalRunRequestHandler,
            ILogger<EvaluationResultRequestHandler> logger,
            IConfigHelper configHelper, 
            IMapper mapper)
        {
            _blobService = blobService;
            _evalRunRequestHandler = evalRunRequestHandler;
            _logger = logger;
            _mapper = mapper;
            _configHelper = configHelper;
        }

        /// <summary>
        /// Save evaluation results to blob storage
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
                var evaluationRecordsJson = saveDto.EvaluationRecords;

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
                    EvaluationRecords = evaluationRecordsJson
                };

                var jsonContent = JsonSerializer.Serialize(storageModel, new JsonSerializerOptions 
                { 
                    WriteIndented = true 
                });

                // Save to blob storage
                await _blobService.WriteBlobContentAsync(containerName, blobPath, jsonContent);

                _logger.LogInformation("Successfully saved evaluation results for EvalRunId: {EvalRunId} to {BlobPath}", 
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
        /// Get evaluation results by EvalRunId
        /// </summary>
        public async Task<EvaluationResultResponseDto> GetEvaluationResultByIdAsync(Guid evalRunId)
        {
            try
            {
                _logger.LogInformation("Retrieving evaluation results for EvalRunId: {EvalRunId}", evalRunId);

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

                _logger.LogInformation("Successfully retrieved evaluation results for EvalRunId: {EvalRunId}", evalRunId);

                return new EvaluationResultResponseDto
                {
                    Success = true,
                    Message = "Evaluation results retrieved successfully",
                    EvalRunId = evalRunId,
                    FileName = evaluationResult?.FileName ?? "Unknown",
                    EvaluationRecords = evaluationResult?.EvaluationRecords
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