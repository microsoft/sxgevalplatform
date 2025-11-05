using SxgEvalPlatformApi.Models;
using Sxg.EvalPlatform.API.Storage.Services;
using Sxg.EvalPlatform.API.Storage;
using SXG.EvalPlatform.Common;
using System.Text.Json;

namespace SxgEvalPlatformApi.RequestHandlers
{
    /// <summary>
    /// Request handler for evaluation artifacts operations
    /// </summary>
    public class EvalArtifactsRequestHandler : IEvalArtifactsRequestHandler
    {
        private readonly IEvalRunRequestHandler _evalRunRequestHandler;
        private readonly IDataSetTableService _dataSetTableService;
        private readonly IMetricsConfigTableService _metricsConfigTableService;
        private readonly IAzureBlobStorageService _blobStorageService;
        private readonly IAzureQueueStorageService _queueStorageService;
        private readonly IConfigHelper _configHelper;
        private readonly ILogger<EvalArtifactsRequestHandler> _logger;

        public EvalArtifactsRequestHandler(
            IEvalRunRequestHandler evalRunRequestHandler,
            IDataSetTableService dataSetTableService,
            IMetricsConfigTableService metricsConfigTableService,
            IAzureBlobStorageService blobStorageService,
            IAzureQueueStorageService queueStorageService,
            IConfigHelper configHelper,
            ILogger<EvalArtifactsRequestHandler> logger)
        {
            _evalRunRequestHandler = evalRunRequestHandler;
            _dataSetTableService = dataSetTableService;
            _metricsConfigTableService = metricsConfigTableService;
            _blobStorageService = blobStorageService;
            _queueStorageService = queueStorageService;
            _configHelper = configHelper;
            _logger = logger;
        }

        /// <summary>
        /// Get both metrics configuration and dataset content for an evaluation run
        /// </summary>
        public async Task<EvalArtifactsDto?> GetEvalArtifactsAsync(Guid evalRunId)
        {
            try
            {
                // Get evaluation run details first
                var evalRun = await _evalRunRequestHandler.GetEvalRunByIdAsync(evalRunId);
                if (evalRun == null)
                {
                    _logger.LogWarning("Evaluation run not found with ID: {EvalRunId}", evalRunId);
                    return null;
                }

                var metricsConfigTask = GetMetricsConfigurationContentAsync(evalRun.MetricsConfigurationId);
                var datasetContentTask = GetDatasetContentAsync(evalRun.AgentId, evalRun.DataSetId);

                await Task.WhenAll(metricsConfigTask, datasetContentTask);

                var metricsConfig = await metricsConfigTask;
                var datasetContent = await datasetContentTask;

                return new EvalArtifactsDto
                {
                    EvalRunId = evalRunId,
                    AgentId = evalRun.AgentId,
                    MetricsConfiguration = metricsConfig,
                    DatasetContent = datasetContent,
                    LastUpdated = evalRun.LastUpdatedOn
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving evaluation artifacts for EvalRunId: {EvalRunId}", evalRunId);
                throw;
            }
        }

        /// <summary>
        /// Get only metrics configuration for an evaluation run
        /// </summary>
        public async Task<MetricsConfigurationArtifactDto?> GetMetricsConfigurationArtifactAsync(Guid evalRunId)
        {
            try
            {
                var evalRun = await _evalRunRequestHandler.GetEvalRunByIdAsync(evalRunId);
                if (evalRun == null)
                {
                    _logger.LogWarning("Evaluation run not found with ID: {EvalRunId}", evalRunId);
                    return null;
                }

                var metricsConfig = await GetMetricsConfigurationContentAsync(evalRun.MetricsConfigurationId);

                return new MetricsConfigurationArtifactDto
                {
                    EvalRunId = evalRunId,
                    AgentId = evalRun.AgentId,
                    MetricsConfigurationId = evalRun.MetricsConfigurationId,
                    MetricsConfiguration = metricsConfig,
                    LastUpdated = evalRun.LastUpdatedOn
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving metrics configuration artifact for EvalRunId: {EvalRunId}", evalRunId);
                throw;
            }
        }

        /// <summary>
        /// Get only dataset content for an evaluation run
        /// </summary>
        public async Task<DatasetArtifactDto?> GetDatasetArtifactAsync(Guid evalRunId)
        {
            try
            {
                var evalRun = await _evalRunRequestHandler.GetEvalRunByIdAsync(evalRunId);
                if (evalRun == null)
                {
                    _logger.LogWarning("Evaluation run not found with ID: {EvalRunId}", evalRunId);
                    return null;
                }

                var datasetContent = await GetDatasetContentAsync(evalRun.AgentId, evalRun.DataSetId);

                return new DatasetArtifactDto
                {
                    EvalRunId = evalRunId,
                    AgentId = evalRun.AgentId,
                    DataSetId = evalRun.DataSetId,
                    DatasetContent = datasetContent,
                    LastUpdated = evalRun.LastUpdatedOn
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving dataset artifact for EvalRunId: {EvalRunId}", evalRunId);
                throw;
            }
        }

        /// <summary>
        /// Store enriched dataset content for an evaluation run
        /// </summary>
        public async Task<EnrichedDatasetResponseDto> StoreEnrichedDatasetAsync(Guid evalRunId, JsonElement enrichedDataset)
        {
            try
            {
                // Get evaluation run details to get agent ID
                var evalRunEntity = await _evalRunRequestHandler.GetEvalRunEntityByIdAsync(evalRunId);
                if (evalRunEntity == null)
                {
                    throw new InvalidOperationException($"Evaluation run with ID {evalRunId} not found");
                }

                // Create container name from agent ID (using common utility for consistent naming)
                var containerName = CommonUtils.TrimAndRemoveSpaces(evalRunEntity.AgentId);
                var blobPath = $"enriched-datasets/{evalRunId}.json";

                
                // Serialize the enriched dataset to JSON string
                var jsonContent = JsonSerializer.Serialize(enrichedDataset, new JsonSerializerOptions 
                { 
                    WriteIndented = true 
                });

                // Store in blob storage
                var success = await _blobStorageService.WriteBlobContentAsync(containerName, blobPath, jsonContent);

                if (!success)
                {
                    throw new InvalidOperationException("Failed to store enriched dataset in blob storage");
                }

                _logger.LogInformation("Successfully stored enriched dataset for EvalRunId: {EvalRunId} at path: {BlobPath}", 
                    evalRunId, blobPath);

                //Update evaluation run entity to mark enriched dataset as stored
                await _evalRunRequestHandler.UpdateEvalRunStatusAsync(new UpdateEvalRunStatusDto() { AgentId = evalRunEntity.AgentId, EvalRunId = evalRunEntity.EvalRunId, Status = CommonConstants.EvalRunStatus.DatasetEnrichmentCompleted });

                // Send message to evaluation processing queue (now includes dataset ID)
                await SendEvalProcessingRequestAsync(evalRunId, evalRunEntity.MetricsConfigurationId, evalRunId.ToString(), evalRunEntity.AgentId, evalRunEntity.DataSetId, blobPath);

                return new EnrichedDatasetResponseDto
                {
                    Success = true,
                    Message = "Enriched dataset stored successfully",
                    EvalRunId = evalRunId,
                    BlobPath = blobPath
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error storing enriched dataset for EvalRunId: {EvalRunId}", evalRunId);
                throw;
            }
        }

        /// <summary>
        /// Send evaluation processing request to Azure Storage Queue
        /// </summary>
        /// <param name="evalRunId">Evaluation run ID</param>
        /// <param name="metricsConfigurationId">Metrics configuration ID</param>
        /// <param name="enrichedDatasetId">Enriched dataset ID</param>
        /// <param name="agentId">Agent ID</param>
        /// <param name="datasetId">Original dataset ID</param>
        /// <param name="enrichedDatasetBlobPath">Path to the enriched dataset blob</param>
        private async Task SendEvalProcessingRequestAsync(Guid evalRunId, string metricsConfigurationId, string enrichedDatasetId, string agentId, string datasetId, string enrichedDatasetBlobPath)
        {
            try
            {
                _logger.LogInformation("Sending evaluation processing request to queue for EvalRunId: {EvalRunId}", evalRunId);

                var processingRequest = new EvalProcessingRequest
                {
                    EvalRunId = evalRunId,
                    MetricsConfigurationId = metricsConfigurationId,
                    EnrichedDatasetId = enrichedDatasetId,
                    AgentId = agentId,
                    DatasetId = datasetId,
                    RequestedAt = DateTime.UtcNow,
                    Priority = "Normal",
                    EnrichedDatasetBlobPath = enrichedDatasetBlobPath
                };

                var messageContent = JsonSerializer.Serialize(processingRequest, new JsonSerializerOptions 
                { 
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase 
                });

                var queueName = _configHelper.GetEvalProcessingRequestsQueueName();
                var success = await _queueStorageService.SendMessageAsync(queueName, messageContent);

                if (success)
                {
                    _logger.LogInformation("Successfully sent evaluation processing request to queue {QueueName} for EvalRunId: {EvalRunId}", queueName, evalRunId);
                }
                else
                {
                    _logger.LogWarning("Failed to send evaluation processing request to queue {QueueName} for EvalRunId: {EvalRunId}", queueName, evalRunId);
                }
            }
            catch (Exception ex)
            {
                // Log the error but don't fail the enriched dataset storage
                // The processing can be retried or handled separately
                _logger.LogError(ex, "Error sending evaluation processing request to queue for EvalRunId: {EvalRunId}", evalRunId);
            }
        }

        /// <summary>
        /// Get enriched dataset content for an evaluation run
        /// </summary>
        public async Task<EnrichedDatasetArtifactDto?> GetEnrichedDatasetArtifactAsync(Guid evalRunId)
        {
            try
            {
                // Get evaluation run details to get agent ID
                var evalRunEntity = await _evalRunRequestHandler.GetEvalRunEntityByIdAsync(evalRunId);
                if (evalRunEntity == null)
                {
                    _logger.LogWarning("Evaluation run not found with ID: {EvalRunId}", evalRunId);
                    return null;
                }

                var containerName = CommonUtils.TrimAndRemoveSpaces(evalRunEntity.AgentId);
                var blobPath = $"enriched-datasets/{evalRunId}.json";

                // Check if blob exists first
                var blobExists = await _blobStorageService.BlobExistsAsync(containerName, blobPath);
                if (!blobExists)
                {
                    _logger.LogWarning("Enriched dataset not found for EvalRunId: {EvalRunId} at path: {BlobPath}", 
                        evalRunId, blobPath);
                    return null;
                }

                // Read blob content
                var jsonContent = await _blobStorageService.ReadBlobContentAsync(containerName, blobPath);
                if (string.IsNullOrEmpty(jsonContent))
                {
                    _logger.LogWarning("Empty enriched dataset content for EvalRunId: {EvalRunId}", evalRunId);
                    return null;
                }

                // Parse JSON content
                JsonElement enrichedDataset;
                try
                {
                    enrichedDataset = JsonSerializer.Deserialize<JsonElement>(jsonContent);
                }
                catch (JsonException ex)
                {
                    _logger.LogError(ex, "Invalid JSON content in enriched dataset for EvalRunId: {EvalRunId}", evalRunId);
                    throw new InvalidOperationException("Invalid JSON content in stored enriched dataset", ex);
                }

                return new EnrichedDatasetArtifactDto
                {
                    EvalRunId = evalRunId,
                    AgentId = evalRunEntity.AgentId,
                    EnrichedDataset = enrichedDataset,
                    CreatedAt = evalRunEntity.StartedDatetime,
                    LastUpdated = evalRunEntity.LastUpdatedOn
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving enriched dataset artifact for EvalRunId: {EvalRunId}", evalRunId);
                throw;
            }
        }

        /// <summary>
        /// Helper method to get metrics configuration content
        /// </summary>
        private async Task<JsonElement?> GetMetricsConfigurationContentAsync(string metricsConfigurationId)
        {
            try
            {
                var metricsConfig = await _metricsConfigTableService.GetMetricsConfigurationByConfigurationIdAsync(metricsConfigurationId);
                if (metricsConfig == null)
                {
                    _logger.LogWarning("Metrics configuration not found with ID: {MetricsConfigurationId}", metricsConfigurationId);
                    return null;
                }

                // Read blob content for metrics configuration
                var configContent = await _blobStorageService.ReadBlobContentAsync(metricsConfig.ConainerName, metricsConfig.BlobFilePath);
                if (string.IsNullOrEmpty(configContent))
                {
                    _logger.LogWarning("Empty metrics configuration content for ID: {MetricsConfigurationId}", metricsConfigurationId);
                    return null;
                }

                return JsonSerializer.Deserialize<JsonElement>(configContent);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving metrics configuration content for ID: {MetricsConfigurationId}", metricsConfigurationId);
                return null;
            }
        }

        /// <summary>
        /// Helper method to get dataset content
        /// </summary>
        private async Task<JsonElement?> GetDatasetContentAsync(string agentId, string dataSetId)
        {
            try
            {
                var dataset = await _dataSetTableService.GetDataSetAsync(agentId, dataSetId);
                if (dataset == null)
                {
                    _logger.LogWarning("Dataset not found with ID: {DataSetId} for agent: {AgentId}", dataSetId, agentId);
                    return null;
                }

                // Read blob content for dataset
                var datasetContent = await _blobStorageService.ReadBlobContentAsync(dataset.ContainerName, dataset.BlobFilePath);
                if (string.IsNullOrEmpty(datasetContent))
                {
                    _logger.LogWarning("Empty dataset content for ID: {DataSetId}", dataSetId);
                    return null;
                }

                return JsonSerializer.Deserialize<JsonElement>(datasetContent);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving dataset content for ID: {DataSetId}, agent: {AgentId}", dataSetId, agentId);
                return null;
            }
        }
    }
}