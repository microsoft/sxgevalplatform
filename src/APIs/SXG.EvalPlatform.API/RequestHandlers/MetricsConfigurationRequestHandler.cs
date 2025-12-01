using AutoMapper;
using Sxg.EvalPlatform.API.Storage;
using Sxg.EvalPlatform.API.Storage.Entities;
using Sxg.EvalPlatform.API.Storage.Services;
using Sxg.EvalPlatform.API.Storage.TableEntities;
using SXG.EvalPlatform.Common;
using SXG.EvalPlatform.Common.Exceptions;
using SxgEvalPlatformApi.Models.Dtos;
using SxgEvalPlatformApi.Services;
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
        private readonly IConfigHelper _configHelper;
        private readonly ILogger<MetricsConfigurationRequestHandler> _logger;
        private readonly IMapper _mapper;
        private readonly ICacheManager _cacheManager;
        private readonly ICallerIdentificationService _callerService;

        // Cache key patterns
        //private const string METRICS_CONFIG_BY_ID_CACHE_KEY = "metrics_config:{0}";
        //private const string METRICS_CONFIG_METADATA_BY_ID_CACHE_KEY = "metrics_config_metadata:{0}";
        //private const string DEFAULT_METRICS_CONFIG_CACHE_KEY = "default_metrics_config";
        //private const string METRICS_CONFIG_LIST_BY_AGENT_ID_CACHE_KEY = "metrics_config_list:{0}";

        // Cache expiration constants
        //private static readonly TimeSpan DefaultConfigCacheDuration = TimeSpan.FromHours(2);
        //private static readonly TimeSpan ConfigByIdCacheDuration = TimeSpan.FromMinutes(60);
        //private static readonly TimeSpan ConfigListCacheDuration = TimeSpan.FromMinutes(30);
        //private static readonly TimeSpan MetadataCacheDuration = TimeSpan.FromMinutes(30);

        public MetricsConfigurationRequestHandler(IMetricsConfigTableService metricsConfigTableService,
                                                  IAzureBlobStorageService blobStorageService,
                                                  ILogger<MetricsConfigurationRequestHandler> logger,
                                                  IMapper mapper,
                                                  IConfigHelper configHelper,
                                                  ICacheManager cacheManager,
                                                  ICallerIdentificationService callerService)
        {
            _metricsConfigTableService = metricsConfigTableService;
            _logger = logger;
            _mapper = mapper;
            _blobStorageService = blobStorageService;
            _configHelper = configHelper;
            _cacheManager = cacheManager;
            _callerService = callerService;
        }

        /// <summary>
        /// Get the audit user based on authentication flow
        /// For AppToApp (service principal with no user context): use application name
        /// For DirectUser/DelegatedAppToApp (user context): use UPN or email
        /// </summary>
        private string GetAuditUser()
        {
            try
            {
                var callerInfo = _callerService.GetCallerInfo();
                
                _logger.LogDebug("GetAuditUser - IsServicePrincipal: {IsServicePrincipal}, HasDelegatedUser: {HasDelegatedUser}, UserEmail: {UserEmail}, AppName: {AppName}",
                    callerInfo.IsServicePrincipal, callerInfo.HasDelegatedUser, callerInfo.UserEmail, callerInfo.ApplicationName);

                if (_callerService.IsServicePrincipalCall() && !_callerService.HasDelegatedUserContext())
                {
                    // AppToApp flow - use application name
                    var appName = _callerService.GetCallingApplicationName();
                    var auditUser = !string.IsNullOrWhiteSpace(appName) && appName != "unknown" ? appName : "System";
                    _logger.LogInformation("Audit user (AppToApp): {AuditUser}", auditUser);
                    return auditUser;
                }
                else
                {
                    // DirectUser or DelegatedAppToApp - use UPN/email
                    var userEmail = _callerService.GetCurrentUserEmail();
                    
                    // Check if we got a meaningful value
                    if (!string.IsNullOrWhiteSpace(userEmail) && userEmail != "unknown")
                    {
                        _logger.LogInformation("Audit user (User): {AuditUser}", userEmail);
                        return userEmail;
                    }
                    
                    // Try to get user ID as fallback
                    var userId = _callerService.GetCurrentUserId();
                    if (!string.IsNullOrWhiteSpace(userId) && userId != "unknown")
                    {
                        _logger.LogInformation("Audit user (User ID fallback): {AuditUser}", userId);
                        return userId;
                    }
                    
                    _logger.LogWarning("Could not determine audit user, using 'System'. UserEmail: {UserEmail}, UserId: {UserId}", userEmail, userId);
                    return "System";
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to get caller information, defaulting to 'System'");
                return "System";
            }
        }

        /// <summary>
        /// Get default Metrics configuration with caching support and fast fallback
        /// </summary>
        public async Task<DefaultMetricsConfiguration> GetDefaultMetricsConfigurationAsync()
        {
            try
            {
                _logger.LogDebug("Retrieving default Metrics configuration");

                var metrics = await FetchDefaultMetricsFromStorageAsync();
                
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
                _logger.LogInformation("Retrieving all configurations for Agent: {AgentId} and Environment: {EnvironmentName}",
                       agentId, environmentName);

                var entities = await _metricsConfigTableService.GetAllMetricsConfigurations(agentId);
                IList<MetricsConfigurationMetadataDto>  configurations = entities.Select(ToMetricsConfigurationMetadataDto).ToList();

                var filteredConfigurations = FilterByEnvironment(configurations, environmentName);

                _logger.LogInformation("Retrieved {Count} configurations for Agent: {AgentId} and Environment: {EnvironmentName}",
                filteredConfigurations.Count, agentId, environmentName);

                return filteredConfigurations;
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
                                

                var entity = await _metricsConfigTableService.GetMetricsConfigurationByConfigurationIdAsync(configurationId);

                if (entity == null)
                {
                    _logger.LogInformation("Configuration not found for ConfigId: {ConfigId}", configurationId);
                    return null;
                }

                var metrics = await FetchMetricsFromBlobAsync(entity.ConainerName, entity.BlobFilePath, configurationId);
                                
                _logger.LogInformation("Retrieved configuration for ConfigId: {ConfigId}", configurationId);

                return metrics;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retrieve configuration for ConfigId: {ConfigId}", configurationId);
                throw;
            }
        }

        /// <summary>
        /// Create or update a metrics configuration based on AgentId, ConfigurationName, and EnvironmentName
        /// </summary>
        public async Task<ConfigurationSaveResponseDto> CreateConfigurationAsync(CreateConfigurationRequestDto createConfigDto)
        {
            try
            {
                _logger.LogInformation("Creating configuration for Agent: {AgentId}, Config: {ConfigName}, Environment: {Environment}",
                        createConfigDto.AgentId, createConfigDto.ConfigurationName, createConfigDto.EnvironmentName);

                ValidateConfigurationAsync(createConfigDto);

                var (isExistingConfig, configurationId, entity) = await DetermineConfigurationStateAsync(createConfigDto);
                var (blobContainer, blobFilePath) = GetOrCreateBlobPaths(entity, createConfigDto, configurationId, isExistingConfig);
                var auditUser = GetAuditUser();

                entity.ConfigurationId = configurationId;
                entity.BlobFilePath = blobFilePath;
                entity.ConainerName = blobContainer;

                if (!isExistingConfig)
                {
                    _mapper.Map(createConfigDto, entity);
                }

                // Set audit properties (automatically detects create vs update based on CreatedOn)
                entity.SetAudit(auditUser);

                await _blobStorageService.WriteBlobContentAsync(blobContainer, blobFilePath, JsonSerializer.Serialize(createConfigDto.MetricsConfiguration));

                var savedEntity = await _metricsConfigTableService.SaveMetricsConfigurationAsync(entity);

                //await UpdateCachesAfterSave(savedEntity, createConfigDto.MetricsConfiguration);

                _logger.LogInformation("Successfully {Action} configuration with ID: {ConfigId} by {AuditUser}",
                   isExistingConfig ? "updated" : "created", savedEntity.ConfigurationId, auditUser);

                return CreateSuccessResponse(savedEntity, isExistingConfig);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create configuration for Agent: {AgentId}", createConfigDto.AgentId);
                return CreateErrorResponse(ex);
            }
        }

        /// <summary>
        /// Update an existing metrics configuration by ConfigurationId
        /// </summary>
        public async Task<ConfigurationSaveResponseDto> UpdateConfigurationAsync(string configurationId, CreateConfigurationRequestDto updateConfigDto)
        {
            try
            {
                _logger.LogInformation("Updating configuration with ID: {ConfigId}", configurationId);

                ValidateConfigurationAsync(updateConfigDto);

                // Verify the configuration exists
                var existingEntity = await _metricsConfigTableService.GetMetricsConfigurationByConfigurationIdAsync(configurationId);
                if (existingEntity == null)
                {
                    return new ConfigurationSaveResponseDto
                    {
                        ConfigurationId = configurationId,
                        Status = "not_found",
                        Message = $"Configuration with ID '{configurationId}' not found"
                    };
                }

                var auditUser = GetAuditUser();

                // Set audit properties for update
                existingEntity.SetUpdateAudit(auditUser);

                // Update blob storage
                await _blobStorageService.WriteBlobContentAsync(
                     existingEntity.ConainerName,
                    existingEntity.BlobFilePath,
 JsonSerializer.Serialize(updateConfigDto.MetricsConfiguration));

                var savedEntity = await _metricsConfigTableService.SaveMetricsConfigurationAsync(existingEntity);

                //await UpdateCachesAfterSave(savedEntity, updateConfigDto.MetricsConfiguration);

                _logger.LogInformation("Successfully updated configuration with ID: {ConfigId} by {AuditUser}", savedEntity.ConfigurationId, auditUser);

                return CreateSuccessResponse(savedEntity, true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update configuration with ID: {ConfigId}", configurationId);
                return CreateErrorResponse(ex);
            }
        }

        /// <summary>
        /// Create or update a metrics configuration (legacy method for backward compatibility)
        /// </summary>
        [Obsolete("Use CreateConfigurationAsync or UpdateConfigurationAsync instead")]
        public async Task<ConfigurationSaveResponseDto> CreateOrSaveConfigurationAsync(CreateConfigurationRequestDto createConfigDto)
        {
            // Check if this is an update request
            if (createConfigDto is UpdateMetricsConfigurationRequestDto updateDto)
            {
                return await UpdateConfigurationAsync(updateDto.ConfigurationId, createConfigDto);
            }

            // Otherwise, it's a create request
            return await CreateConfigurationAsync(createConfigDto);
        }

        /// <summary>
        /// Delete configuration and update cache
        /// </summary>
        public async Task<bool> DeleteConfigurationAsync(string configurationId)
        {
            try
            {
                _logger.LogInformation("Deleting configuration with ID: {ConfigurationId}", configurationId);

                var existingConfig = await _metricsConfigTableService.GetMetricsConfigurationByConfigurationIdAsync(configurationId);

                if (existingConfig == null)
                {
                    _logger.LogWarning("Configuration with ID: {ConfigurationId} not found", configurationId);
                    return false;
                }

                bool deleted = await _metricsConfigTableService.DeleteMetricsConfigurationByIdAsync(
        existingConfig.AgentId,
                  configurationId);

                if (deleted)
                {
                    //await RemoveConfigurationFromCacheAsync(configurationId, existingConfig.AgentId);
                    await TryDeleteBlobAsync(existingConfig.ConainerName, existingConfig.BlobFilePath, configurationId);

                    _logger.LogInformation("Configuration with ID: {ConfigurationId} deleted successfully", configurationId);
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

        #region Private Helper Methods

        /// <summary>
        /// Try to get item from cache with timeout handling
        /// </summary>
        private async Task<T?> TryGetFromCacheAsync<T>(string cacheKey) where T : class
        {
            try
            {
                _logger.LogDebug("Checking cache for key: {CacheKey}", cacheKey);
                return await _cacheManager.GetAsync<T>(cacheKey);
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("Cache timeout for key: {CacheKey} - proceeding without cache", cacheKey);
                return null;
            }
            catch (Exception cacheEx)
            {
                _logger.LogWarning(cacheEx, "Cache error for key: {CacheKey} - proceeding without cache", cacheKey);
                return null;
            }
        }

        /// <summary>
        /// Try to set item in cache with timeout handling
        /// </summary>
        //private async Task TrySetCacheAsync<T>(string cacheKey, T value, TimeSpan expiration) where T : class
        //{
        //    try
        //    {
        //        await _cacheManager.SetAsync(cacheKey, value, expiration);
        //        _logger.LogInformation("Successfully cached item with key: {CacheKey}", cacheKey);
        //    }
        //    catch (OperationCanceledException)
        //    {
        //        _logger.LogWarning("Cache SET timeout for key: {CacheKey} - continuing without caching", cacheKey);
        //    }
        //    catch (Exception cacheEx)
        //    {
        //        _logger.LogWarning(cacheEx, "Cache SET failed for key: {CacheKey} - continuing without caching", cacheKey);
        //    }
        //}

        /// <summary>
        /// Fetch default metrics configuration from blob storage
        /// </summary>
        private async Task<DefaultMetricsConfiguration> FetchDefaultMetricsFromStorageAsync()
        {
            string containerName = _configHelper.GetPlatformConfigurationsContainer();
            string blobFilePath = _configHelper.GetDefaultMetricsConfiguration();

            var blobContent = await _blobStorageService.ReadBlobContentAsync(containerName, blobFilePath);

            if (blobContent == null)
            {
                throw new Exception($"Default Metrics configuration blob not found: {containerName}/{blobFilePath}");
            }

            var metrics = DeserializeDefaultMetricsConfiguration(blobContent);
            _logger.LogDebug("Successfully parsed metrics config with {Count} categories", metrics.Categories?.Count ?? 0);

            return metrics;
        }

        /// <summary>
        /// Deserialize default metrics configuration handling both wrapped and unwrapped JSON formats
        /// </summary>
        private DefaultMetricsConfiguration DeserializeDefaultMetricsConfiguration(string blobContent)
        {
            using var document = JsonDocument.Parse(blobContent);
            var root = document.RootElement;

            // Check if the JSON has a "metricConfiguration" wrapper
            if (root.TryGetProperty("metricConfiguration", out var metricsElement))
            {
                var metrics = JsonSerializer.Deserialize<DefaultMetricsConfiguration>(metricsElement.GetRawText());
                if (metrics == null)
                {
                    throw new Exception("Failed to deserialize default Metrics configuration from wrapped JSON structure.");
                }
                return metrics;
            }

            // Try direct deserialization for backward compatibility
            var directMetrics = JsonSerializer.Deserialize<DefaultMetricsConfiguration>(blobContent);
            if (directMetrics == null)
            {
                throw new Exception("Failed to deserialize default Metrics configuration from blob content.");
            }

            return directMetrics;
        }

        /// <summary>
        /// Fetch metrics configuration from blob storage
        /// </summary>
        private async Task<IList<SelectedMetricsConfiguration>> FetchMetricsFromBlobAsync(string blobContainer,
                                                                                          string blobPath,  
                                                                                          string configurationId)
        {
            var blobContent = await _blobStorageService.ReadBlobContentAsync(blobContainer, blobPath);

            if (string.IsNullOrEmpty(blobContent))
            {
                throw new Exception($"Metrics configuration blob not found: {blobContainer}/{blobPath}");
            }

            return DeserializeMetricsConfiguration(blobContent, configurationId);
        }

        /// <summary>
        /// Deserialize metrics configuration handling both array and object formats
        /// </summary>
        private IList<SelectedMetricsConfiguration> DeserializeMetricsConfiguration(string blobContent, string configurationId)
        {
            try
            {
                var jsonDocument = JsonDocument.Parse(blobContent);
                var rootElement = jsonDocument.RootElement;

                IList<SelectedMetricsConfiguration>? metrics = null;

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

                if (metrics == null)
                {
                    throw new Exception("Failed to deserialize Metrics configuration from blob content.");
                }

                return metrics;
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Failed to parse JSON from blob content for ConfigId: {ConfigId}", configurationId);
                throw new Exception("Invalid JSON format in Metrics configuration blob", ex);
            }
        }

        /// <summary>
        /// Filter configurations by environment name
        /// </summary>
        private IList<MetricsConfigurationMetadataDto> FilterByEnvironment(IList<MetricsConfigurationMetadataDto> configurations, string environmentName)
        {
            if (string.IsNullOrEmpty(environmentName))
            {
                return configurations;
            }

            return configurations.Where(p => p.EnvironmentName == environmentName).ToList();
        }

        /// <summary>
        /// Validate configuration request DTO
        /// </summary>
        private void ValidateConfigurationAsync(CreateConfigurationRequestDto createConfigDto)
        {
            if (string.IsNullOrWhiteSpace(createConfigDto.AgentId))
                throw new DataValidationException("AgentId is required");

            if (string.IsNullOrWhiteSpace(createConfigDto.ConfigurationName))
                throw new DataValidationException("ConfigurationName is required");

            if (createConfigDto.MetricsConfiguration == null || !createConfigDto.MetricsConfiguration.Any())
                throw new DataValidationException("MetricsConfiguration cannot be empty");
        }

        /// <summary>
        /// Determine if this is a new or existing configuration
        /// </summary>
        private async Task<(bool isExisting, string configId, MetricsConfigurationTableEntity entity)> DetermineConfigurationStateAsync(CreateConfigurationRequestDto createConfigDto)
        {
            if (createConfigDto is UpdateMetricsConfigurationRequestDto updateDto)
            {
                var configId = updateDto.ConfigurationId;
                var result = await _metricsConfigTableService.GetMetricsConfigurationByConfigurationIdAsync(configId);

                if (result != null)
                {
                    return (true, configId, result);
                }
            }

            // Check if configuration already exists
            var existingConfigs = await _metricsConfigTableService.GetAllMetricsConfigurations(createConfigDto.AgentId, createConfigDto.ConfigurationName, createConfigDto.EnvironmentName);

            if (existingConfigs != null && existingConfigs.Count > 0)
            {
                var existingEntity = existingConfigs.First();
                return (true, existingEntity.ConfigurationId, existingEntity);
            }

            return (false, Guid.NewGuid().ToString(), new MetricsConfigurationTableEntity());
        }

        /// <summary>
        /// Get or create blob storage paths
        /// </summary>
        private (string container, string filePath) GetOrCreateBlobPaths(MetricsConfigurationTableEntity entity, CreateConfigurationRequestDto createConfigDto, string configurationId, bool isExisting)
        {
            if (isExisting && !string.IsNullOrEmpty(entity.BlobFilePath))
            {
                return (entity.ConainerName, entity.BlobFilePath);
            }

            var container = CommonUtils.TrimAndRemoveSpaces(createConfigDto.AgentId);
            var fileName = $"{createConfigDto.ConfigurationName}_{createConfigDto.EnvironmentName}_{configurationId}.json";
            var filePath = $"{_configHelper.GetMetricsConfigurationsFolderName()}/{fileName}";

            return (container, filePath);
        }

        /// <summary>
        /// Update all relevant caches after saving configuration
        /// </summary>
        //private async Task UpdateCachesAfterSave(MetricsConfigurationTableEntity savedEntity, IList<SelectedMetricsConfigurationDto> metricsConfiguration)
        //{
        //    // Cache the configuration itself
        //    var configCacheKey = string.Format(METRICS_CONFIG_BY_ID_CACHE_KEY, savedEntity.ConfigurationId);
        //    await TrySetCacheAsync(configCacheKey, metricsConfiguration, ConfigByIdCacheDuration);

        //    // Cache the metadata
        //    var metadataCacheKey = string.Format(METRICS_CONFIG_METADATA_BY_ID_CACHE_KEY, savedEntity.ConfigurationId);
        //    var metadataDto = ToMetricsConfigurationMetadataDto(savedEntity);
        //    await TrySetCacheAsync(metadataCacheKey, metadataDto, MetadataCacheDuration);

        //    // Invalidate agent configuration list cache
        //    await InvalidateAgentConfigurationCaches(savedEntity.AgentId);
        //}

        /// <summary>
        /// Remove configuration from all caches
        /// </summary>
        //private async Task RemoveConfigurationFromCacheAsync(string configurationId, string agentId)
        //{
        //    var cacheKey = string.Format(METRICS_CONFIG_BY_ID_CACHE_KEY, configurationId);
        //    await _cacheManager.RemoveAsync(cacheKey);
        //    await InvalidateAgentConfigurationCaches(agentId);
        //}

        /// <summary>
        /// Invalidate agent-level configuration caches
        /// </summary>
        //private async Task InvalidateAgentConfigurationCaches(string agentId)
        //{
        //    var cacheKeyPattern = string.Format(METRICS_CONFIG_LIST_BY_AGENT_ID_CACHE_KEY, agentId);
        //    await _cacheManager.RemoveAsync(cacheKeyPattern);
        //}

        /// <summary>
        /// Try to delete blob file with error handling
        /// </summary>
        private async Task TryDeleteBlobAsync(string containerName, string blobPath, string configurationId)
        {
            try
            {
                bool blobExists = await _blobStorageService.BlobExistsAsync(containerName, blobPath);
                if (blobExists)
                {
                    await _blobStorageService.DeleteBlobAsync(containerName, blobPath);
                    _logger.LogInformation("Configuration blob file deleted: {ContainerName}/{BlobPath}", containerName, blobPath);
                }
            }
            catch (Exception blobEx)
            {
                _logger.LogWarning(blobEx,
                    "Failed to delete blob file for configuration ID: {ConfigurationId}, but table record was deleted",
                configurationId);
                // Continue - table deletion was successful, blob deletion failure is not critical
            }
        }

        /// <summary>
        /// Create success response DTO
        /// </summary>
        private ConfigurationSaveResponseDto CreateSuccessResponse(
            MetricsConfigurationTableEntity savedEntity,
           bool isExisting)
        {
            return new ConfigurationSaveResponseDto
            {
                ConfigurationId = savedEntity.ConfigurationId,
                Status = "success",
                Message = isExisting ? "Configuration updated successfully" : "Configuration created successfully."
            };
        }

        /// <summary>
        /// Create error response DTO
        /// </summary>
        private ConfigurationSaveResponseDto CreateErrorResponse(Exception ex)
        {
            return new ConfigurationSaveResponseDto
            {
                ConfigurationId = string.Empty,
                Status = "error",
                Message = $"Failed to save configuration: {ex.Message}"
            };
        }

        /// <summary>
        /// Map entity to metadata DTO
        /// </summary>
        private MetricsConfigurationMetadataDto ToMetricsConfigurationMetadataDto(MetricsConfigurationTableEntity entity)
        {
            return _mapper.Map<MetricsConfigurationMetadataDto>(entity);
        }

        #endregion
    }
}
