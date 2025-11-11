using AutoMapper;
using Sxg.EvalPlatform.API.Storage;
using Sxg.EvalPlatform.API.Storage.Entities;
using Sxg.EvalPlatform.API.Storage.Services;
using Sxg.EvalPlatform.API.Storage.TableEntities;
using SXG.EvalPlatform.Common;
using SXG.EvalPlatform.Common.Exceptions;
using SxgEvalPlatformApi.Models.Dtos;
using System.Text.Json;

namespace SxgEvalPlatformApi.RequestHandlers
{
    /// <summary>
    /// Request handler for Metrics configuration operations using the storage project services with caching support
    /// </summary>
    public class MetricsConfigurationRequestHandler : IMetricsConfigurationRequestHandler
    {
        private readonly IMetricsConfigTableService _metricsConfigTableService;
        private readonly IAzureBlobStorageService _blobStorageService;
        IConfigHelper _configHelper;
        private readonly ILogger<MetricsConfigurationRequestHandler> _logger;
        private readonly IMapper _mapper;
        private readonly ICacheManager _cacheManager;

        // Cache key patterns
        private const string METRICS_CONFIG_BY_ID_CACHE_KEY = "metrics_config:{0}"; // metrics_config:metricConfigurationEntity
        private const string METRICS_CONFIG_METADATA_BY_ID_CACHE_KEY = "metrics_config_metadata:{0}"; // metrics_config_metadata:agentId:environmentName
        private const string DEFAULT_METRICS_CONFIG_CACHE_KEY = "default_metrics_config";
        private const string METRICS_CONFIG_LIST_BY_AGENT_ID_CACHE_KEY = "metrics_config_list:{0}"; // metrics_config_list:agentId



        public MetricsConfigurationRequestHandler(IMetricsConfigTableService metricsConfigTableService,
                                                  IAzureBlobStorageService blobStorageService,
                                                  ILogger<MetricsConfigurationRequestHandler> logger,
                                                  IMapper mapper,
                                                  IConfigHelper configHelper,
                                                  ICacheManager cacheManager)
        {
            _metricsConfigTableService = metricsConfigTableService;
            _logger = logger;
            _mapper = mapper;
            _blobStorageService = blobStorageService;
            _configHelper = configHelper;
            _cacheManager = cacheManager;
        }

        /// <summary>
        /// Get default Metrics configuration with caching support and fast fallback
        /// </summary>
        public async Task<DefaultMetricsConfiguration> GetDefaultMetricsConfigurationAsync()
        {
            try
            {
                // Check cache first with FAST timeout
                var cacheKey = DEFAULT_METRICS_CONFIG_CACHE_KEY;
                _logger.LogDebug("Checking cache for key: {CacheKey}", cacheKey);

                DefaultMetricsConfiguration? cachedResult = null;

                try
                {
                    // Cache should be FAST - 1 second timeout max!
                    cachedResult = await _cacheManager.GetAsync<DefaultMetricsConfiguration>(cacheKey);
                }
                catch (OperationCanceledException)
                {
                    _logger.LogWarning("Cache timeout after 1s for key: {CacheKey} - proceeding without cache", cacheKey);
                }
                catch (Exception cacheEx)
                {
                    _logger.LogWarning(cacheEx, "Cache error for key: {CacheKey} - proceeding without cache", cacheKey);
                }

                if (cachedResult != null)
                {
                    _logger.LogInformation("Cache HIT - returning cached Metrics config");
                    return cachedResult;
                }

                _logger.LogDebug("Cache MISS - fetching from storage");

                // If not in cache, fetch from storage
                string containerName = _configHelper.GetPlatformConfigurationsContainer();
                string blobFilePath = _configHelper.GetDefaultMetricsConfiguration();

                var blobContent = await _blobStorageService.ReadBlobContentAsync(containerName, blobFilePath);

                if (blobContent == null)
                {
                    throw new Exception($"Default Metrics configuration blob not found: {containerName}/{blobFilePath}");
                }

                DefaultMetricsConfiguration metrics;

                // Parse the JSON to handle the wrapper structure
                using var document = JsonDocument.Parse(blobContent);
                var root = document.RootElement;

                // Check if the JSON has a "metricConfiguration" wrapper
                if (root.TryGetProperty("metricConfiguration", out var metricsElement))
                {
                    metrics = JsonSerializer.Deserialize<DefaultMetricsConfiguration>(metricsElement.GetRawText());
                    if (metrics == null)
                    {
                        throw new Exception("Failed to deserialize default Metrics configuration from wrapped JSON structure.");
                    }
                }
                else
                {
                    // Try direct deserialization for backward compatibility
                    metrics = JsonSerializer.Deserialize<DefaultMetricsConfiguration>(blobContent);
                    if (metrics == null)
                    {
                        throw new Exception("Failed to deserialize default Metrics configuration from blob content.");
                    }
                }

                _logger.LogDebug("Successfully parsed metrics config with {Count} categories", metrics.Categories?.Count ?? 0);

                // Try to cache the result with FAST timeout
                try
                {
                    await _cacheManager.SetAsync(cacheKey, metrics, TimeSpan.FromHours(2));
                    _logger.LogInformation("Successfully cached Metrics config");
                }
                catch (OperationCanceledException)
                {
                    _logger.LogWarning("Cache SET timeout after 1s - continuing without caching");
                }
                catch (Exception cacheEx)
                {
                    _logger.LogWarning(cacheEx, "Cache SET failed - continuing without caching");
                }

                return metrics;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving default Metrics configuration");
                throw;
            }
        }

        /// <summary>
        /// Get all Metrics configurations by agent ID and environment with caching support
        /// </summary>
        public async Task<IList<MetricsConfigurationMetadataDto>> GetAllMetricsConfigurationsByAgentIdAndEnvironmentAsync(string agentId, string environmentName = "")
        {
            try
            {
                _logger.LogInformation($"Retrieving all configurations for Agent: {agentId} and EnvironmentName: {environmentName}");

                // Check cache first
                var cacheKey = string.Format(METRICS_CONFIG_LIST_BY_AGENT_ID_CACHE_KEY, agentId);
                var cachedResult = await _cacheManager.GetAsync<IList<MetricsConfigurationMetadataDto>>(cacheKey);

                if (cachedResult != null)
                {
                    _logger.LogDebug("Returning cached Metrics configurations for Agent: {AgentId}, Environment: {EnvironmentName}", agentId, environmentName);
                    return cachedResult.Where(p=> string.IsNullOrEmpty(environmentName) || p.EnvironmentName == environmentName).ToList();
                }

                // If not in cache, fetch from storage
                var entities = await _metricsConfigTableService.GetAllMetricsConfigurations(agentId);

                var configurations = entities.Select(ToMetricsConfigurationMetadataDto).ToList();

                // Cache the result (cache for 30 minutes for list queries)
                await _cacheManager.SetAsync(cacheKey, configurations, TimeSpan.FromMinutes(30));
                _logger.LogDebug("Cached Metrics configurations for Agent: {AgentId}, Environment: {EnvironmentName}", agentId, environmentName);

                _logger.LogInformation($"Retrieved {configurations.Count} configurations for Agent: {agentId} and EnvironmentName: {environmentName}",
           configurations.Count, agentId);

                return configurations.Where(p=> string.IsNullOrEmpty(environmentName) || p.EnvironmentName == environmentName).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retrieve configurations for Agent: {AgentId}", agentId);
                throw;
            }
        }

        /// <summary>
        /// Get Metrics configuration by configuration ID with caching support
        /// </summary>
        public async Task<IList<SelectedMetricsConfiguration>?> GetMetricsConfigurationByConfigurationIdAsync(string configurationId)
        {
            try
            {
                _logger.LogInformation("Retrieving configuration for ConfigId: {ConfigId}", configurationId);

                // Check cache first
                var cacheKey = string.Format(METRICS_CONFIG_BY_ID_CACHE_KEY, configurationId);
                var cachedResult = await _cacheManager.GetAsync<IList<SelectedMetricsConfiguration>>(cacheKey);

                if (cachedResult != null)
                {
                    _logger.LogDebug("Returning cached Metrics configuration for ConfigId: {ConfigId}", configurationId);
                    return cachedResult;
                }

                // If not in cache, fetch from storage
                var entity = await _metricsConfigTableService.GetMetricsConfigurationByConfigurationIdAsync(configurationId);
                if (entity == null)
                {
                    _logger.LogInformation("Configuration not found for ConfigId: {ConfigId}", configurationId);
                    return null;
                }

                var blobPath = entity.BlobFilePath;
                var blobContainer = entity.ConainerName;

                var blobContent = await _blobStorageService.ReadBlobContentAsync(blobContainer, blobPath);

                if (string.IsNullOrEmpty(blobContent))
                {
                    throw new Exception($"Metrics configuration blob not found: {blobContainer}/{blobPath}");
                }

                IList<SelectedMetricsConfiguration>? metrics = null;

                // Handle both old array format and new object format
                try
                {
                    var jsonDocument = JsonDocument.Parse(blobContent);
                    var rootElement = jsonDocument.RootElement;

                    if (rootElement.ValueKind == JsonValueKind.Array)
                    {
                        // Old format: direct array
                        metrics = JsonSerializer.Deserialize<IList<SelectedMetricsConfiguration>>(blobContent);
                    }
                    else if (rootElement.ValueKind == JsonValueKind.Object && rootElement.TryGetProperty("metricsConfiguration", out var metricsConfigElement))
                    {
                        // New format: object with metricsConfiguration property
                        var metricsConfigJson = metricsConfigElement.GetRawText();
                        metrics = JsonSerializer.Deserialize<IList<SelectedMetricsConfiguration>>(metricsConfigJson);
                    }
                    else
                    {
                        throw new Exception("Blob content does not contain Metrics configuration in expected format");
                    }
                }
                catch (JsonException ex)
                {
                    _logger.LogError(ex, "Failed to parse JSON from blob content for ConfigId: {ConfigId}", configurationId);
                    throw new Exception("Invalid JSON format in Metrics configuration blob", ex);
                }

                if (metrics == null)
                {
                    throw new Exception("Failed to deserialize Metrics configuration from blob content.");
                }

                // Cache the result (cache for 60 minutes)
                await _cacheManager.SetAsync(cacheKey, metrics, TimeSpan.FromMinutes(60));
                _logger.LogDebug("Cached Metrics configuration for ConfigId: {ConfigId}", configurationId);

                _logger.LogInformation("Retrieved configuration for ConfigId: {ConfigId}", configurationId);
                return metrics;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retrieve configuration for ConfigId: {ConfigId}", configurationId);
                throw;
            }
        }

        private void ValidateConfigurationAsync(CreateConfigurationRequestDto createConfigDto)
        {
            if (string.IsNullOrWhiteSpace(createConfigDto.AgentId))
                throw new DataValidationException("AgentId is required");

            if (string.IsNullOrWhiteSpace(createConfigDto.ConfigurationName))
                throw new DataValidationException("ConfigurationName is required");

            if (createConfigDto.MetricsConfiguration == null || !createConfigDto.MetricsConfiguration.Any())
                throw new DataValidationException("MetricsConfiguration cannot be empty");
        }

        public async Task<ConfigurationSaveResponseDto> CreateOrSaveConfigurationAsync(CreateConfigurationRequestDto createConfigDto)
        {
            try
            {
                _logger.LogInformation("Creating/saving configuration for Agent: {AgentId}, Config: {ConfigName}, Environment: {Environment}",
                    createConfigDto.AgentId, createConfigDto.ConfigurationName, createConfigDto.EnvironmentName);

                ValidateConfigurationAsync(createConfigDto);

                string configurationId = Guid.NewGuid().ToString();

                bool isExistingConfig = false;
                MetricsConfigurationTableEntity metricsConfigTableEntityInstance = new();
                string blobContainer = string.Empty;
                string blobFileName = string.Empty;
                string blobFilePath = string.Empty;

                if (createConfigDto is UpdateMetricsConfigurationRequestDto)
                {
                    isExistingConfig = true;
                    configurationId = (createConfigDto as UpdateMetricsConfigurationRequestDto)!.ConfigurationId;
                    var result = await _metricsConfigTableService.GetMetricsConfigurationByConfigurationIdAsync(configurationId);
                    if (result != null)
                    {
                        metricsConfigTableEntityInstance = result;
                        blobContainer = metricsConfigTableEntityInstance.ConainerName;
                        blobFilePath = metricsConfigTableEntityInstance.BlobFilePath;
                    }
                }
                else
                {
                    // Check if configuration already exists
                    var existingConfig = await _metricsConfigTableService.GetAllMetricsConfigurations(
                        createConfigDto.AgentId, createConfigDto.ConfigurationName, createConfigDto.EnvironmentName);

                    if (existingConfig != null && existingConfig.Count > 0)
                    {
                        isExistingConfig = true;
                        metricsConfigTableEntityInstance = existingConfig.First();
                        blobContainer = metricsConfigTableEntityInstance.ConainerName;
                        blobFilePath = metricsConfigTableEntityInstance.BlobFilePath;

                    }

                }

                if (!isExistingConfig)
                {
                    blobContainer = CommonUtils.TrimAndRemoveSpaces(createConfigDto.AgentId);

                    blobFileName = $"{createConfigDto.ConfigurationName}_{createConfigDto.EnvironmentName}_{configurationId}.json";
                    blobFilePath = $"{_configHelper.GetMetricsConfigurationsFolderName()}/{blobFileName}";

                    _mapper.Map(createConfigDto, metricsConfigTableEntityInstance);
                    metricsConfigTableEntityInstance.BlobFilePath = blobFilePath;
                    metricsConfigTableEntityInstance.ConainerName = blobContainer;
                    metricsConfigTableEntityInstance.ConfigurationId = configurationId;
                }

                metricsConfigTableEntityInstance.LastUpdatedOn = DateTime.UtcNow;
                var blobWriteResult = await _blobStorageService.WriteBlobContentAsync(blobContainer,
                                                                                      blobFilePath,
                                                                                      JsonSerializer.Serialize(createConfigDto.MetricsConfiguration));

                // Save to storage
                var savedEntity = await _metricsConfigTableService.SaveMetricsConfigurationAsync(metricsConfigTableEntityInstance);


                // Update Cache
                var cacheKey = string.Format(METRICS_CONFIG_BY_ID_CACHE_KEY, savedEntity.ConfigurationId);
                await _cacheManager.SetAsync(cacheKey, createConfigDto.MetricsConfiguration, TimeSpan.FromMinutes(60));

                cacheKey = string.Format(METRICS_CONFIG_METADATA_BY_ID_CACHE_KEY, savedEntity.ConfigurationId);
                var metadataDto = ToMetricsConfigurationMetadataDto(savedEntity);
                await _cacheManager.SetAsync(cacheKey, metadataDto, TimeSpan.FromMinutes(30));

                var response = new ConfigurationSaveResponseDto
                {
                    ConfigurationId = savedEntity.ConfigurationId,
                    Status = "success",
                    Message = isExistingConfig ? "Configuration updated successfully" : "Configuration created successfully."
                };

                await InvalidateAgentConfigurationCaches(savedEntity.AgentId);

                _logger.LogInformation("Successfully {Action} configuration with ID: {ConfigId}",
                    isExistingConfig ? "updated" : "created", savedEntity.ConfigurationId);

                return response;

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create/save configuration for Agent: {AgentId}",
                    createConfigDto.AgentId);

                return new ConfigurationSaveResponseDto
                {
                    ConfigurationId = string.Empty,
                    Status = "error",
                    Message = $"Failed to save configuration: {ex.Message}"
                };
            }
        }

        private async Task InvalidateAgentConfigurationCaches(string agentId)
        {
            var cacheKeyPattern = string.Format(METRICS_CONFIG_LIST_BY_AGENT_ID_CACHE_KEY, agentId);
            await _cacheManager.RemoveAsync(cacheKeyPattern);

        }


        /// <summary>
        /// Delete configuration and update cache
        /// </summary>
        public async Task<bool> DeleteConfigurationAsync(string configurationId)
        {
            try
            {
                _logger.LogInformation("Deleting configuration with ID: {ConfigurationId}", configurationId);

                // First get the configuration to get the AgentId for cache invalidation
                var existingConfig = await _metricsConfigTableService.GetMetricsConfigurationByConfigurationIdAsync(configurationId);

                if (existingConfig == null)
                {
                    _logger.LogWarning("Configuration with ID: {ConfigurationId} not found", configurationId);
                    return false;
                }

                // Delete from storage first
                bool deleted = await _metricsConfigTableService.DeleteMetricsConfigurationByIdAsync(existingConfig.AgentId, configurationId);

                if (deleted)
                {
                    // After successful deletion, remove from cache
                    var cacheKey = string.Format(METRICS_CONFIG_BY_ID_CACHE_KEY, configurationId);
                    await _cacheManager.RemoveAsync(cacheKey);

                    // Invalidate agent-based caches
                    await InvalidateAgentConfigurationCaches(existingConfig.AgentId);

                    _logger.LogInformation("Configuration with ID: {ConfigurationId} deleted successfully and removed from cache", configurationId);

                    // Also delete the blob file if it exists
                    try
                    {
                        var containerName = existingConfig.ConainerName;
                        var blobPath = existingConfig.BlobFilePath;

                        // Check if blob exists before attempting to delete
                        bool blobExists = await _blobStorageService.BlobExistsAsync(containerName, blobPath);
                        if (blobExists)
                        {
                            await _blobStorageService.DeleteBlobAsync(containerName, blobPath);
                            _logger.LogInformation("Configuration blob file deleted: {ContainerName}/{BlobPath}", containerName, blobPath);
                        }
                    }
                    catch (Exception blobEx)
                    {
                        _logger.LogWarning(blobEx, "Failed to delete blob file for configuration ID: {ConfigurationId}, but table record was deleted", configurationId);
                        // Continue - table deletion was successful, blob deletion failure is not critical
                    }
                }
                else
                {
                    _logger.LogWarning("Failed to delete configuration with ID: {ConfigurationId}", configurationId);
                }

                return deleted;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while deleting configuration: {ConfigurationId}", configurationId);
                return false;
            }
        }

        private MetricsConfigurationMetadataDto ToMetricsConfigurationMetadataDto(MetricsConfigurationTableEntity entity)
        {
            return _mapper.Map<MetricsConfigurationMetadataDto>(entity);
        }
        //private MetricsConfigurationTableEntity ToMetricsConfigurationTableEntity(CreateMetricsConfigurationDto dto)
        //{
        //    return _mapper.Map<MetricsConfigurationTableEntity>(dto);
        //}

    }
}
