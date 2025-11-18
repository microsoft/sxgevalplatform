using SxgEvalPlatformApi.Models;
using Sxg.EvalPlatform.API.Storage.Services;
using Sxg.EvalPlatform.API.Storage;
using SXG.EvalPlatform.Common;
using System.Text.Json;

namespace SxgEvalPlatformApi.RequestHandlers
{
    /// <summary>
    /// Request handler for evaluation artifacts operations with caching support
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
        private readonly ICacheManager _cacheManager;

        // Cache key patterns
        private const string EVAL_ARTIFACTS_CACHE_KEY = "eval_artifacts:{0}"; // eval_artifacts:evalRunId
        private const string METRICS_CONFIG_ARTIFACT_CACHE_KEY = "metrics_config_artifact:{0}"; // metrics_config_artifact:evalRunId
        private const string DATASET_ARTIFACT_CACHE_KEY = "dataset_artifact:{0}"; // dataset_artifact:evalRunId
        private const string ENRICHED_DATASET_ARTIFACT_CACHE_KEY = "enriched_dataset_artifact:{0}"; // enriched_dataset_artifact:evalRunId

        public EvalArtifactsRequestHandler(
            IEvalRunRequestHandler evalRunRequestHandler,
            IDataSetTableService dataSetTableService,
            IMetricsConfigTableService metricsConfigTableService,
            IAzureBlobStorageService blobStorageService,
            IAzureQueueStorageService queueStorageService,
            IConfigHelper configHelper,
            ILogger<EvalArtifactsRequestHandler> logger,
            ICacheManager cacheManager)
        {
            _evalRunRequestHandler = evalRunRequestHandler;
            _dataSetTableService = dataSetTableService;
            _metricsConfigTableService = metricsConfigTableService;
            _blobStorageService = blobStorageService;
            _queueStorageService = queueStorageService;
            _configHelper = configHelper;
            _logger = logger;
            _cacheManager = cacheManager;
        }

        /// <summary>
        /// Get both Metrics configuration and dataset content for an evaluation run with caching support
        /// </summary>
        public async Task<EvalArtifactsDto?> GetEvalArtifactsAsync(Guid evalRunId)
        {
            try
            {
                // Check cache first
                var cacheKey = string.Format(EVAL_ARTIFACTS_CACHE_KEY, evalRunId);
                var cachedResult = await _cacheManager.GetAsync<EvalArtifactsDto>(cacheKey);

                if (cachedResult != null)
                {
                    _logger.LogDebug("Returning cached evaluation artifacts for EvalRunId: {EvalRunId}", evalRunId);
                    return cachedResult;
                }

                // If not in cache, fetch from storage
                // Get evaluation run details first (this uses caching from EvalRunRequestHandler)
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

                var result = new EvalArtifactsDto
                {
                    EvalRunId = evalRunId,
                    AgentId = evalRun.AgentId,
                    MetricsConfiguration = metricsConfig,
                    DatasetContent = datasetContent,
                    LastUpdated = evalRun.LastUpdatedOn
                };

                // Cache the result (cache for 1 hour)
                await _cacheManager.SetAsync(cacheKey, result, TimeSpan.FromHours(1));
                _logger.LogDebug("Cached evaluation artifacts for EvalRunId: {EvalRunId}", evalRunId);

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving evaluation artifacts for EvalRunId: {EvalRunId}", evalRunId);
                throw;
            }
        }

        /// <summary>
        /// Get only Metrics configuration for an evaluation run with caching support
        /// </summary>
        public async Task<MetricsConfigurationArtifactDto?> GetMetricsConfigurationArtifactAsync(Guid evalRunId)
        {
            try
            {
                // Check cache first
                var cacheKey = string.Format(METRICS_CONFIG_ARTIFACT_CACHE_KEY, evalRunId);
                var cachedResult = await _cacheManager.GetAsync<MetricsConfigurationArtifactDto>(cacheKey);

                if (cachedResult != null)
                {
                    _logger.LogDebug("Returning cached Metrics configuration artifact for EvalRunId: {EvalRunId}", evalRunId);
                    return cachedResult;
                }

                // If not in cache, fetch from storage
                var evalRun = await _evalRunRequestHandler.GetEvalRunByIdAsync(evalRunId);
                if (evalRun == null)
                {
                    _logger.LogWarning("Evaluation run not found with ID: {EvalRunId}", evalRunId);
                    return null;
                }

                var metricsConfig = await GetMetricsConfigurationContentAsync(evalRun.MetricsConfigurationId);

                var result = new MetricsConfigurationArtifactDto
                {
                    EvalRunId = evalRunId,
                    AgentId = evalRun.AgentId,
                    MetricsConfigurationId = evalRun.MetricsConfigurationId,
                    MetricsConfiguration = metricsConfig,
                    LastUpdated = evalRun.LastUpdatedOn
                };

                // Cache the result (cache for 1 hour)
                await _cacheManager.SetAsync(cacheKey, result, TimeSpan.FromHours(1));
                _logger.LogDebug("Cached Metrics configuration artifact for EvalRunId: {EvalRunId}", evalRunId);

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving Metrics configuration artifact for EvalRunId: {EvalRunId}", evalRunId);
                throw;
            }
        }

        /// <summary>
        /// Get only dataset content for an evaluation run with caching support
        /// </summary>
        public async Task<DatasetArtifactDto?> GetDatasetArtifactAsync(Guid evalRunId)
        {
            try
            {
                // Check cache first
                var cacheKey = string.Format(DATASET_ARTIFACT_CACHE_KEY, evalRunId);
                var cachedResult = await _cacheManager.GetAsync<DatasetArtifactDto>(cacheKey);

                if (cachedResult != null)
                {
                    _logger.LogDebug("Returning cached dataset artifact for EvalRunId: {EvalRunId}", evalRunId);
                    return cachedResult;
                }

                // If not in cache, fetch from storage
                var evalRun = await _evalRunRequestHandler.GetEvalRunByIdAsync(evalRunId);
                if (evalRun == null)
                {
                    _logger.LogWarning("Evaluation run not found with ID: {EvalRunId}", evalRunId);
                    return null;
                }

                var datasetContent = await GetDatasetContentAsync(evalRun.AgentId, evalRun.DataSetId);

                var result = new DatasetArtifactDto
                {
                    EvalRunId = evalRunId,
                    AgentId = evalRun.AgentId,
                    DataSetId = evalRun.DataSetId,
                    DatasetContent = datasetContent,
                    LastUpdated = evalRun.LastUpdatedOn
                };

                // Cache the result (cache for 1 hour)
                await _cacheManager.SetAsync(cacheKey, result, TimeSpan.FromHours(1));
                _logger.LogDebug("Cached dataset artifact for EvalRunId: {EvalRunId}", evalRunId);

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving dataset artifact for EvalRunId: {EvalRunId}", evalRunId);
                throw;
            }
        }

        /// <summary>
        /// Store enriched dataset content for an evaluation run and update cache
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

                // Store in blob storage first (write to backend store)
                var success = await _blobStorageService.WriteBlobContentAsync(containerName, blobPath, jsonContent);

                if (!success)
                {
                    throw new InvalidOperationException("Failed to store enriched dataset in blob storage");
                }

                // After successful write, update cache
                var enrichedDatasetArtifact = new EnrichedDatasetArtifactDto
                {
                    EvalRunId = evalRunId,
                    AgentId = evalRunEntity.AgentId,
                    EnrichedDataset = enrichedDataset,
                    CreatedAt = evalRunEntity.StartedDatetime,
                    LastUpdated = DateTime.UtcNow
                };

                var cacheKey = string.Format(ENRICHED_DATASET_ARTIFACT_CACHE_KEY, evalRunId);
                await _cacheManager.SetAsync(cacheKey, enrichedDatasetArtifact, TimeSpan.FromHours(2));

                // Invalidate related caches (artifacts cache should be refreshed)
                await InvalidateEvalArtifactsCaches(evalRunId);

                _logger.LogInformation("Successfully stored enriched dataset for EvalRunId: {EvalRunId} at path: {BlobPath} and updated cache", 
                    evalRunId, blobPath);

                // Update evaluation run entity to mark enriched dataset as stored
                await _evalRunRequestHandler.UpdateEvalRunStatusAsync(new UpdateEvalRunStatusDto() 
                { 
                    AgentId = evalRunEntity.AgentId, 
                    EvalRunId = evalRunEntity.EvalRunId, 
                    Status = CommonConstants.EvalRunStatus.DatasetEnrichmentCompleted 
                });

                // Send message to evaluation processing queue (no caching for queue operations)
                await SendEvalProcessingRequestAsync(evalRunId, evalRunEntity.MetricsConfigurationId);

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
        /// Get enriched dataset content for an evaluation run with caching support
        /// </summary>
        public async Task<EnrichedDatasetArtifactDto?> GetEnrichedDatasetArtifactAsync(Guid evalRunId)
        {
            try
            {
                // Check cache first
                var cacheKey = string.Format(ENRICHED_DATASET_ARTIFACT_CACHE_KEY, evalRunId);
                var cachedResult = await _cacheManager.GetAsync<EnrichedDatasetArtifactDto>(cacheKey);

                if (cachedResult != null)
                {
                    _logger.LogDebug("Returning cached enriched dataset artifact for EvalRunId: {EvalRunId}", evalRunId);
                    return cachedResult;
                }

                // If not in cache, fetch from storage
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

                var result = new EnrichedDatasetArtifactDto
                {
                    EvalRunId = evalRunId,
                    AgentId = evalRunEntity.AgentId,
                    EnrichedDataset = enrichedDataset,
                    CreatedAt = evalRunEntity.StartedDatetime,
                    LastUpdated = evalRunEntity.LastUpdatedOn
                };

                // Cache the result (cache for 2 hours)
                await _cacheManager.SetAsync(cacheKey, result, TimeSpan.FromHours(2));
                _logger.LogDebug("Cached enriched dataset artifact for EvalRunId: {EvalRunId}", evalRunId);

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving enriched dataset artifact for EvalRunId: {EvalRunId}", evalRunId);
                throw;
            }
        }

        /// <summary>
        /// Invalidate evaluation artifacts caches when data is modified
        /// </summary>
        private async Task InvalidateEvalArtifactsCaches(Guid evalRunId)
        {
            try
            {
                _logger.LogDebug("Invalidating evaluation artifacts caches for EvalRunId: {EvalRunId}", evalRunId);

                // Remove specific cache entries related to this evaluation run
                var artifactsCacheKey = string.Format(EVAL_ARTIFACTS_CACHE_KEY, evalRunId);
                var metricsArtifactCacheKey = string.Format(METRICS_CONFIG_ARTIFACT_CACHE_KEY, evalRunId);
                var datasetArtifactCacheKey = string.Format(DATASET_ARTIFACT_CACHE_KEY, evalRunId);

                await _cacheManager.RemoveAsync(artifactsCacheKey);
                await _cacheManager.RemoveAsync(metricsArtifactCacheKey);
                await _cacheManager.RemoveAsync(datasetArtifactCacheKey);

                var statistics = await _cacheManager.GetStatisticsAsync();
                _logger.LogDebug("Cache statistics after invalidation - Type: {CacheType}, Items: {ItemCount}",
                    statistics.CacheType, statistics.ItemCount);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error invalidating evaluation artifacts caches for EvalRunId: {EvalRunId}", evalRunId);
                // Don't throw - cache invalidation failure shouldn't break the main operation
            }
        }

        /// <summary>
        /// Send evaluation processing request to Azure Storage Queue (no caching for queue operations)
        /// </summary>
        /// <param name="evalRunId">Evaluation run ID</param>
        /// <param name="metricsConfigurationId">Metrics configuration ID</param>
        /// <param name="enrichedDatasetId">Enriched dataset ID</param>
        /// <param name="agentId">Agent ID</param>
        /// <param name="datasetId">Original dataset ID</param>
        /// <param name="enrichedDatasetBlobPath">Path to the enriched dataset blob</param>
        private async Task SendEvalProcessingRequestAsync(Guid evalRunId, string metricsConfigurationId)
        {
            try
            {
                _logger.LogInformation("Sending evaluation processing request to queue for EvalRunId: {EvalRunId}", evalRunId);

                var processingRequest = new EvalProcessingRequest
                {
                    EvalRunId = evalRunId,
                    MetricsConfigurationId = metricsConfigurationId,
                    RequestedAt = DateTime.UtcNow,
                    Priority = "Normal",
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
        /// Helper method to get Metrics configuration content (leverages MetricsConfigurationRequestHandler caching)
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

                // Read blob content for Metrics configuration
                var configContent = await _blobStorageService.ReadBlobContentAsync(metricsConfig.ConainerName, metricsConfig.BlobFilePath);
                if (string.IsNullOrEmpty(configContent))
                {
                    _logger.LogWarning("Empty Metrics configuration content for ID: {MetricsConfigurationId}", metricsConfigurationId);
                    return null;
                }

                return JsonSerializer.Deserialize<JsonElement>(configContent);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving Metrics configuration content for ID: {MetricsConfigurationId}", metricsConfigurationId);
                return null;
            }
        }

        /// <summary>
        /// Helper method to get dataset content (leverages DataSetRequestHandler caching)
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