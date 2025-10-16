using System.Text.Json;
using SxgEvalPlatformApi.Models;
using Sxg.EvalPlatform.API.Storage.Services;

namespace SxgEvalPlatformApi.Services;

/// <summary>
/// Service for evaluation result operations
/// </summary>
public class EvaluationResultService : IEvaluationResultService
{
    private readonly IAzureBlobStorageService _blobService;
    private readonly IEvalRunService _evalRunService;
    private readonly ILogger<EvaluationResultService> _logger;

    public EvaluationResultService(
        IAzureBlobStorageService blobService,
        IEvalRunService evalRunService,
        ILogger<EvaluationResultService> logger)
    {
        _blobService = blobService;
        _evalRunService = evalRunService;
        _logger = logger;
    }

    /// <summary>
    /// Save evaluation results to blob storage
    /// </summary>
    public async Task<EvaluationResultSaveResponseDto> SaveEvaluationResultAsync(SaveEvaluationResultDto saveDto)
    {
        try
        {
            _logger.LogInformation("Saving evaluation results for EvalRunId: {EvalRunId}", saveDto.EvalRunId);

            // First, verify that the EvalRunId exists
            var evalRun = await _evalRunService.GetEvalRunByIdAsync(saveDto.EvalRunId);
            if (evalRun == null)
            {
                _logger.LogWarning("EvalRunId not found: {EvalRunId}", saveDto.EvalRunId);
                return new EvaluationResultSaveResponseDto
                {
                    Success = false,
                    Message = $"EvalRunId '{saveDto.EvalRunId}' not found. Please provide a valid EvalRunId.",
                    EvalRunId = saveDto.EvalRunId
                };
            }

            // Validate that the evaluation run has a terminal status (Completed or Failed)
            var allowedStatuses = new[] { EvalRunStatusConstants.Completed, EvalRunStatusConstants.Failed };
            if (!allowedStatuses.Contains(evalRun.Status))
            {
                _logger.LogWarning("Cannot save evaluation results for EvalRunId: {EvalRunId} with status: {Status}. Results can only be saved for evaluations with status 'Completed' or 'Failed'.", 
                    saveDto.EvalRunId, evalRun.Status);
                return new EvaluationResultSaveResponseDto
                {
                    Success = false,
                    Message = $"Cannot save evaluation results for evaluation run with status '{evalRun.Status}'. Results can only be saved for evaluations with status 'Completed' or 'Failed'.",
                    EvalRunId = saveDto.EvalRunId
                };
            }

            // Since we're now receiving flexible JSON structure directly,
            // we can use it as-is without transformation
            var evaluationRecordsJson = saveDto.EvaluationRecords;

            // Handle container name and blob path with backward compatibility
            string containerName;
            string blobPath;
            
            if (!string.IsNullOrEmpty(evalRun.ContainerName))
            {
                // New format: use stored container name and blob path
                containerName = evalRun.ContainerName;
                blobPath = evalRun.BlobFilePath ?? $"evaluations/{saveDto.EvalRunId}.json";
            }
            else
            {
                // Backward compatibility: parse the old format
                containerName = evalRun.AgentId;
                if (!string.IsNullOrEmpty(evalRun.BlobFilePath) && evalRun.BlobFilePath.Contains('/'))
                {
                    // Old format: "A001/evaluations/{evalRunId}.json"
                    var pathParts = evalRun.BlobFilePath.Split('/', 2);
                    if (pathParts.Length == 2)
                    {
                        containerName = pathParts[0];
                        blobPath = pathParts[1];
                    }
                    else
                    {
                        blobPath = evalRun.BlobFilePath;
                    }
                }
                else
                {
                    blobPath = evalRun.BlobFilePath ?? $"evaluations/{saveDto.EvalRunId}.json";
                }
            }

            // Serialize evaluation records to JSON using the storage model
            var storageModel = new StoredEvaluationResultDto
            {
                EvalRunId = saveDto.EvalRunId,
                FileName = saveDto.FileName,
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
                saveDto.EvalRunId, $"{containerName}/{blobPath}");

            return new EvaluationResultSaveResponseDto
            {
                Success = true,
                Message = "Evaluation results saved successfully",
                EvalRunId = saveDto.EvalRunId,
                BlobPath = $"{containerName}/{blobPath}"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving evaluation results for EvalRunId: {EvalRunId}", saveDto.EvalRunId);
            return new EvaluationResultSaveResponseDto
            {
                Success = false,
                Message = "Failed to save evaluation results",
                EvalRunId = saveDto.EvalRunId
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

            // First, verify that the EvalRunId exists
            var evalRun = await _evalRunService.GetEvalRunByIdAsync(evalRunId);
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

            // Handle container name and blob path with backward compatibility
            string containerName;
            string blobPath;
            
            if (!string.IsNullOrEmpty(evalRun.ContainerName))
            {
                // New format: use stored container name and blob path
                containerName = evalRun.ContainerName;
                blobPath = evalRun.BlobFilePath ?? $"evaluations/{evalRunId}.json";
            }
            else
            {
                // Backward compatibility: parse the old format
                containerName = evalRun.AgentId;
                if (!string.IsNullOrEmpty(evalRun.BlobFilePath) && evalRun.BlobFilePath.Contains('/'))
                {
                    // Old format: "A001/evaluations/{evalRunId}.json"
                    var pathParts = evalRun.BlobFilePath.Split('/', 2);
                    if (pathParts.Length == 2)
                    {
                        containerName = pathParts[0];
                        blobPath = pathParts[1];
                    }
                    else
                    {
                        blobPath = evalRun.BlobFilePath;
                    }
                }
                else
                {
                    blobPath = evalRun.BlobFilePath ?? $"evaluations/{evalRunId}.json";
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
    public async Task<List<EvalRunDto>> GetEvalRunsByAgentIdAsync(string agentId)
    {
        try
        {
            _logger.LogInformation("Retrieving evaluation runs for AgentId: {AgentId}", agentId);
            
            return await _evalRunService.GetEvalRunsByAgentIdAsync(agentId);
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
    public async Task<List<EvaluationResultResponseDto>> GetEvaluationResultsByDateRangeAsync(string agentId, DateTime startDateTime, DateTime endDateTime)
    {
        try
        {
            _logger.LogInformation("Retrieving evaluation results for AgentId: {AgentId} between {StartDateTime} and {EndDateTime}", 
                agentId, startDateTime, endDateTime);

            // Get all evaluation runs for the agent
            var evalRuns = await _evalRunService.GetEvalRunsByAgentIdAsync(agentId);
            
            // Filter runs within the date range and only include completed ones
            var filteredRuns = evalRuns
                .Where(run => run.StartedDatetime.HasValue &&
                             run.StartedDatetime.Value >= startDateTime && 
                             run.StartedDatetime.Value <= endDateTime && 
                             run.Status == EvalRunStatusConstants.Completed)
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