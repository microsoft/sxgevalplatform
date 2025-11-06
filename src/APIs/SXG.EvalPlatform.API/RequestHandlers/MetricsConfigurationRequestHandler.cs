using AutoMapper;
using Sxg.EvalPlatform.API.Storage;
using Sxg.EvalPlatform.API.Storage.Entities;
using Sxg.EvalPlatform.API.Storage.Services;
using Sxg.EvalPlatform.API.Storage.TableEntities;
using SXG.EvalPlatform.Common;
using SxgEvalPlatformApi.Models.Dtos;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory;

namespace SxgEvalPlatformApi.RequestHandlers
{
    /// <summary>
    /// Request handler for metrics configuration operations using the storage project services
    /// </summary>
    public class MetricsConfigurationRequestHandler : IMetricsConfigurationRequestHandler
    {
        private readonly IMetricsConfigTableService _metricsConfigTableService;
        private readonly IAzureBlobStorageService _blobStorageService;
        IConfigHelper _configHelper;
        private readonly ILogger<MetricsConfigurationRequestHandler> _logger;
        private readonly IMapper _mapper;
        private readonly StorageFactory _factory;

        public MetricsConfigurationRequestHandler(
            IMetricsConfigTableService metricsConfigTableService,
            IAzureBlobStorageService blobStorageService,
            ILogger<MetricsConfigurationRequestHandler> logger,
            IMapper mapper,
            IConfigHelper configHelper,
            StorageFactory factory)
        {
            _metricsConfigTableService = metricsConfigTableService;
            _logger = logger;
            _mapper = mapper;
            _blobStorageService = blobStorageService;
            _configHelper = configHelper;
            _factory = factory;
        }

        public async Task<MetricsConfiguration> GetDefaultMetricsConfigurationAsync()
        {
            string containerName = _configHelper.GetPlatformConfigurationsContainer();
            string blobFilePath = _configHelper.GetDefaultMetricsConfiguration();
            string storageProvider = _configHelper.GetStorageProvider();
            //var blobContent = await _blobStorageService.ReadBlobContentAsync(containerName, blobFilePath);

            var storage = _factory.GetProvider(storageProvider);
            var content = await storage.ReadAsync(containerName, blobFilePath);

            if (content == null)
            {
                throw new Exception($"Default metrics configuration blob not found: {containerName}/{blobFilePath}");
            }

            // Parse the JSON to handle the wrapper structure
            using var document = JsonDocument.Parse(content);
            var root = document.RootElement;
            
            // Check if the JSON has a "metricConfiguration" wrapper
            if (root.TryGetProperty("metricConfiguration", out var metricsElement))
            {
                var metrics = System.Text.Json.JsonSerializer.Deserialize<MetricsConfiguration>(metricsElement.GetRawText());
                if (metrics == null)
                {
                    throw new Exception("Failed to deserialize default metrics configuration from wrapped JSON structure.");
                }
                return metrics;
            }
            else
            {
                // Try direct deserialization for backward compatibility
                var metrics = System.Text.Json.JsonSerializer.Deserialize<MetricsConfiguration>(content);
                if (metrics == null)
                {
                    throw new Exception("Failed to deserialize default metrics configuration from blob content.");
                }
                return metrics;
            }
        }

        public async Task<IList<MetricsConfigurationMetadataDto>> GetAllMetricsConfigurationsByAgentIdAndEnvironmentAsync(string agentId, string enviornmentName)
        {
            try
            {
                _logger.LogInformation($"Retrieving all configurations for Agent: {agentId} and EnvironmentName: {enviornmentName}");
                                
                var entities = await _metricsConfigTableService.GetAllMetricsConfigurations(agentId, enviornmentName);

                var configurations = entities.Select(ToMetricsConfigurationMetadataDto).ToList();

                _logger.LogInformation($"Retrieved {configurations.Count} configurations for Agent: {agentId} and EnvironmentName: {enviornmentName}",
                    configurations.Count, agentId);

                return configurations;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retrieve configurations for Agent: {AgentId}", agentId);
                throw;
            }
        }

        public async Task<IList<SelectedMetricsConfiguration>?> GetMetricsConfigurationByConfigurationIdAsync(string configurationId)
        {
            try
            {
                _logger.LogInformation("Retrieving configuration for ConfigId: {ConfigId}",
                    configurationId);
                var entity = await _metricsConfigTableService.GetMetricsConfigurationByConfigurationIdAsync(configurationId);
                if (entity == null)
                {
                    _logger.LogInformation("Configuration not found for ConfigId: {ConfigId}",
                        configurationId);
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
                        throw new Exception("Blob content does not contain metrics configuration in expected format");
                    }
                }
                catch (JsonException ex)
                {
                    _logger.LogError(ex, "Failed to parse JSON from blob content for ConfigId: {ConfigId}", configurationId);
                    throw new Exception("Invalid JSON format in metrics configuration blob", ex);
                }

                if (metrics == null)
                {
                    throw new Exception("Failed to deserialize metrics configuration from blob content.");
                }

                _logger.LogInformation("Retrieved configuration for ConfigId: {ConfigId}",
                    configurationId);
                return metrics;

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retrieve configuration for ConfigId: {ConfigId}",
                    configurationId);
                throw;
            }
        }

        public async Task<ConfigurationSaveResponseDto> CreateConfigurationAsync(CreateConfigurationRequestDto createConfigDto)
        {
            try
            {
                _logger.LogInformation("Creating new configuration for Agent: {AgentId}, Config: {ConfigName}, Environment: {Environment}",
                    createConfigDto.AgentId, createConfigDto.ConfigurationName, createConfigDto.EnvironmentName);

                // Check if configuration already exists to prevent duplicates
                var existingConfig = await _metricsConfigTableService.GetAllMetricsConfigurations(
                    createConfigDto.AgentId, createConfigDto.ConfigurationName, createConfigDto.EnvironmentName);

                if (existingConfig != null && existingConfig.Count > 0)
                {
                    return new ConfigurationSaveResponseDto
                    {
                        ConfigurationId = string.Empty,
                        Status = "error",
                        Message = $"Configuration with name '{createConfigDto.ConfigurationName}' already exists for agent '{createConfigDto.AgentId}' in environment '{createConfigDto.EnvironmentName}'"
                    };
                }

                return await SaveConfigurationInternalAsync(createConfigDto, null, false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create configuration for Agent: {AgentId}",
                    createConfigDto.AgentId);

                return new ConfigurationSaveResponseDto
                {
                    ConfigurationId = string.Empty,
                    Status = "error",
                    Message = $"Failed to create configuration: {ex.Message}"
                };
            }
        }

        public async Task<ConfigurationSaveResponseDto> UpdateConfigurationAsync(string configurationId, UpdateConfigurationRequestDto updateConfigDto)
        {
            try
            {
                _logger.LogInformation("Updating configuration with ID: {ConfigurationId}", configurationId);

                // Get existing configuration by ID
                var existingConfig = await _metricsConfigTableService.GetMetricsConfigurationByConfigurationIdAsync(configurationId);

                if (existingConfig == null)
                {
                    return new ConfigurationSaveResponseDto
                    {
                        ConfigurationId = string.Empty,
                        Status = "error",
                        Message = $"Configuration with ID '{configurationId}' not found"
                    };
                }

                return await SaveConfigurationInternalAsync(updateConfigDto, existingConfig, true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update configuration with ID: {ConfigurationId}", configurationId);

                return new ConfigurationSaveResponseDto
                {
                    ConfigurationId = string.Empty,
                    Status = "error",
                    Message = $"Failed to update configuration: {ex.Message}"
                };
            }
        }

        /// <summary>
        /// Internal method to handle update operations with UpdateConfigurationRequestDto
        /// </summary>
        private async Task<ConfigurationSaveResponseDto> SaveConfigurationInternalAsync(
            UpdateConfigurationRequestDto updateConfigDto, 
            MetricsConfigurationTableEntity existingEntity, 
            bool isUpdate = true)
        {
            try
            {
                var currentTime = DateTime.UtcNow;

                // Only update user metadata and timestamp for tracking
                existingEntity.LastUpdatedBy = "System"; // Default since UserMetadata is no longer required
                existingEntity.LastUpdatedOn = currentTime;

                // Update blob with new metrics configuration
                var blobContainer = existingEntity.ConainerName;
                var blobFilePath = existingEntity.BlobFilePath;

                // Read existing blob content
                var existingBlobContent = await _blobStorageService.ReadBlobContentAsync(blobContainer, blobFilePath);
                
                if (string.IsNullOrEmpty(existingBlobContent))
                {
                    throw new InvalidOperationException("Existing blob content is empty or null");
                }
                
                // Parse existing JSON to preserve other fields
                JsonElement rootElement;
                var jsonObject = new Dictionary<string, object>();
                
                try
                {
                    var existingJson = JsonDocument.Parse(existingBlobContent);
                    rootElement = existingJson.RootElement;
                    
                    // Handle both object and array structures
                    if (rootElement.ValueKind == JsonValueKind.Object)
                    {
                        // Copy all existing properties from object structure
                        foreach (var property in rootElement.EnumerateObject())
                        {
                            var deserializedValue = JsonSerializer.Deserialize<object>(property.Value.GetRawText());
                            if (deserializedValue != null)
                            {
                                jsonObject[property.Name] = deserializedValue;
                            }
                        }
                    }
                    else if (rootElement.ValueKind == JsonValueKind.Array)
                    {
                        // If existing blob is just an array, treat it as metricsConfiguration
                        var existingMetrics = JsonSerializer.Deserialize<object>(rootElement.GetRawText());
                        if (existingMetrics != null)
                        {
                            jsonObject["metricsConfiguration"] = existingMetrics;
                        }
                        _logger.LogInformation("Converted array-based blob to object structure for ConfigurationId: {ConfigId}", existingEntity.ConfigurationId);
                    }
                    else
                    {
                        throw new InvalidOperationException($"Unexpected JSON structure: {rootElement.ValueKind}");
                    }
                }
                catch (JsonException ex)
                {
                    _logger.LogError(ex, "Failed to parse existing blob JSON for ConfigurationId: {ConfigId}", existingEntity.ConfigurationId);
                    throw new InvalidOperationException("Existing blob contains invalid JSON", ex);
                }
                
                // Update only the metricsConfiguration field
                jsonObject["metricsConfiguration"] = updateConfigDto.MetricsConfiguration;

                // Serialize and save updated JSON back to blob
                var updatedJsonContent = JsonSerializer.Serialize(jsonObject, new JsonSerializerOptions 
                { 
                    WriteIndented = true 
                });
                var blobWriteResult = await _blobStorageService.WriteBlobContentAsync(blobContainer, blobFilePath, updatedJsonContent);

                // Save to storage
                var savedEntity = await _metricsConfigTableService.SaveMetricsConfigurationAsync(existingEntity);

                var response = new ConfigurationSaveResponseDto
                {
                    ConfigurationId = savedEntity.ConfigurationId,
                    Status = "success",
                    Message = "Configuration updated successfully",
                    CreatedBy = savedEntity.CreatedBy,
                    CreatedOn = savedEntity.CreatedOn,
                    LastUpdatedBy = savedEntity.LastUpdatedBy,
                    LastUpdatedOn = savedEntity.LastUpdatedOn
                };

                _logger.LogInformation("Successfully updated configuration with ID: {ConfigId} by user: {UserEmail}",
                    savedEntity.ConfigurationId, "System");

                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save configuration during update");
                throw;
            }
        }

        /// <summary>
        /// Common method to handle both create and update operations
        /// </summary>
        private async Task<ConfigurationSaveResponseDto> SaveConfigurationInternalAsync(
            CreateConfigurationRequestDto configDto, 
            MetricsConfigurationTableEntity? existingEntity = null, 
            bool isUpdate = false)
        {
            try
            {
                var blobContainer = CommonUtils.TrimAndRemoveSpaces(configDto.AgentId);
                
                MetricsConfigurationTableEntity entity;
                var currentTime = DateTime.UtcNow;
                string configurationId;
                string blobFileName;
                string blobFilePath;

                if (isUpdate && existingEntity != null)
                {
                    // Update existing entity
                    entity = existingEntity;
                    entity.ConfigurationName = configDto.ConfigurationName;
                    entity.EnvironmentName = configDto.EnvironmentName;
                    entity.Description = configDto.Description;
                    entity.LastUpdatedBy = "System"; // Default since UserMetadata is no longer required
                    entity.LastUpdatedOn = currentTime;
                    
                    // Use existing ConfigurationId and blob path
                    configurationId = entity.ConfigurationId;
                    blobFileName = $"{configDto.ConfigurationName}_{configDto.EnvironmentName}_{configurationId}.json";
                    blobFilePath = $"{_configHelper.GetMetricsConfigurationsFolderName()}/{blobFileName}";
                    
                    // Use existing blob path if available, otherwise create new one with GUID
                    if (string.IsNullOrEmpty(entity.BlobFilePath))
                    {
                        entity.BlobFilePath = blobFilePath;
                    }
                    else
                    {
                        blobFilePath = entity.BlobFilePath;
                    }
                }
                else
                {
                    // Generate ConfigurationId first for new entity
                    configurationId = Guid.NewGuid().ToString();
                    blobFileName = $"{configDto.ConfigurationName}_{configDto.EnvironmentName}_{configurationId}.json";
                    blobFilePath = $"{_configHelper.GetMetricsConfigurationsFolderName()}/{blobFileName}";
                    
                    // Create new entity directly from CreateConfigurationRequestDto
                    entity = _mapper.Map<MetricsConfigurationTableEntity>(configDto);
                    entity.ConfigurationId = configurationId; // Override the AutoMapper generated GUID
                    entity.BlobFilePath = blobFilePath;
                    entity.ConainerName = blobContainer;
                    entity.CreatedBy = "System"; // Default since UserMetadata is no longer required
                    entity.CreatedOn = currentTime;
                    entity.LastUpdatedBy = "System"; // Default since UserMetadata is no longer required
                    entity.LastUpdatedOn = currentTime;
                }

                // Save metrics configuration to blob
                var blobWriteResult = await _blobStorageService.WriteBlobContentAsync(blobContainer, blobFilePath,
                    System.Text.Json.JsonSerializer.Serialize(configDto.MetricsConfiguration));

                // Save to storage
                var savedEntity = await _metricsConfigTableService.SaveMetricsConfigurationAsync(entity);

                var response = new ConfigurationSaveResponseDto
                {
                    ConfigurationId = savedEntity.ConfigurationId,
                    Status = "success",
                    Message = isUpdate ? "Configuration updated successfully" : "Configuration created successfully",
                    CreatedBy = savedEntity.CreatedBy,
                    CreatedOn = savedEntity.CreatedOn,
                    LastUpdatedBy = savedEntity.LastUpdatedBy,
                    LastUpdatedOn = savedEntity.LastUpdatedOn
                };

                _logger.LogInformation("Successfully {Action} configuration with ID: {ConfigId} by user: {UserEmail}",
                    isUpdate ? "updated" : "created", savedEntity.ConfigurationId, "System");

                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save configuration");
                throw;
            }
        }

        //public async Task<List<CreateMetricsConfigurationDto>> GetConfigurationsByAgentIdAsync(string agentId)
        //{
        //    try
        //    {
        //        _logger.LogInformation("Retrieving all configurations for Agent: {AgentId}", agentId);

        //        var entities = await _metricsConfigTableService.GetAllMetricsConfigurationsByAgentIdAsync(agentId);
        //        var configurations = entities.Select(ConvertToDto).ToList();

        //        _logger.LogInformation("Retrieved {Count} configurations for Agent: {AgentId}",
        //            configurations.Count, agentId);

        //        return configurations;
        //    }
        //    catch (Exception ex)
        //    {
        //        _logger.LogError(ex, "Failed to retrieve configurations for Agent: {AgentId}", agentId);
        //        throw;
        //    }
        //}

        //public async Task<List<CreateMetricsConfigurationDto>> GetConfigurationsByAgentIdAndEnvironmentAsync(string agentId, string environmentName)
        //{
        //    try
        //    {
        //        _logger.LogInformation("Retrieving configurations for Agent: {AgentId}, Environment: {Environment}",
        //            agentId, environmentName);

        //        var entities = await _metricsConfigTableService.GetAllMetricsConfigurationsByAgentIdAndEnvironmentAsync(agentId, environmentName);
        //        var configurations = entities.Select(ConvertToDto).ToList();

        //        _logger.LogInformation("Retrieved {Count} configurations for Agent: {AgentId}, Environment: {Environment}",
        //            configurations.Count, agentId, environmentName);

        //        return configurations;
        //    }
        //    catch (Exception ex)
        //    {
        //        _logger.LogError(ex, "Failed to retrieve configurations for Agent: {AgentId}, Environment: {Environment}",
        //            agentId, environmentName);
        //        throw;
        //    }
        //}

        //public async Task<CreateMetricsConfigurationDto?> GetConfigurationAsync(string agentId, string configurationName, string environmentName)
        //{
        //    try
        //    {
        //        _logger.LogInformation("Retrieving configuration for Agent: {AgentId}, Config: {ConfigName}, Environment: {Environment}",
        //            agentId, configurationName, environmentName);

        //        var entity = await _metricsConfigTableService.GetMetricsConfigurationAsync(agentId, configurationName, environmentName);

        //        if (entity == null)
        //        {
        //            _logger.LogInformation("Configuration not found for Agent: {AgentId}, Config: {ConfigName}, Environment: {Environment}",
        //                agentId, configurationName, environmentName);
        //            return null;
        //        }

        //        var dto = ConvertToDto(entity);
        //        _logger.LogInformation("Retrieved configuration for Agent: {AgentId}, Config: {ConfigName}, Environment: {Environment}",
        //            agentId, configurationName, environmentName);

        //        return dto;
        //    }
        //    catch (Exception ex)
        //    {
        //        _logger.LogError(ex, "Failed to retrieve configuration for Agent: {AgentId}, Config: {ConfigName}, Environment: {Environment}",
        //            agentId, configurationName, environmentName);
        //        throw;
        //    }
        //}

        //public async Task<CreateMetricsConfigurationDto?> GetConfigurationByIdAsync(string agentId, string configurationId)
        //{
        //    try
        //    {
        //        _logger.LogInformation("Retrieving configuration by ID for Agent: {AgentId}, ConfigId: {ConfigId}",
        //            agentId, configurationId);

        //        var entity = await _metricsConfigTableService.GetMetricsConfigurationByIdAsync(agentId, configurationId);

        //        if (entity == null)
        //        {
        //            _logger.LogInformation("Configuration not found for Agent: {AgentId}, ConfigId: {ConfigId}",
        //                agentId, configurationId);
        //            return null;
        //        }

        //        var dto = ConvertToDto(entity);
        //        _logger.LogInformation("Retrieved configuration by ID for Agent: {AgentId}, ConfigId: {ConfigId}",
        //            agentId, configurationId);

        //        return dto;
        //    }
        //    catch (Exception ex)
        //    {
        //        _logger.LogError(ex, "Failed to retrieve configuration by ID for Agent: {AgentId}, ConfigId: {ConfigId}",
        //            agentId, configurationId);
        //        throw;
        //    }
        //}



        //public async Task<bool> DeleteConfigurationAsync(string agentId, string configurationId)
        //{
        //    try
        //    {
        //        _logger.LogInformation("Deleting configuration for Agent: {AgentId}, ConfigId: {ConfigId}",
        //            agentId, configurationId);

        //        var deleted = await _metricsConfigTableService.DeleteMetricsConfigurationByIdAsync(agentId, configurationId);

        //        _logger.LogInformation("Configuration deletion for Agent: {AgentId}, ConfigId: {ConfigId} = {Deleted}",
        //            agentId, configurationId, deleted);

        //        return deleted;
        //    }
        //    catch (Exception ex)
        //    {
        //        _logger.LogError(ex, "Failed to delete configuration for Agent: {AgentId}, ConfigId: {ConfigId}",
        //            agentId, configurationId);
        //        throw;
        //    }
        //}

        //public async Task<CreateMetricsConfigurationDto?> UpdateConfigurationAsync(string agentId, string configurationId, CreateMetricsConfigurationDto updateDto)
        //{
        //    try
        //    {
        //        _logger.LogInformation("Updating configuration for Agent: {AgentId}, ConfigId: {ConfigId}",
        //            agentId, configurationId);

        //        var updatedEntity = await _metricsConfigTableService.UpdateMetricsConfigurationMetadataAsync(
        //            agentId, 
        //            configurationId, 
        //            entity =>
        //            {
        //                entity.ConfigurationName = updateDto.ConfigurationName;
        //                entity.EnvironmentName = updateDto.EnvironmentName;
        //                entity.Description = updateDto.Description;
        //                entity.LastUpdatedBy = updateDto.LastUpdatedBy;
        //                entity.MetricsConfiguration = ConvertMetricsConfiguration(updateDto.MetricsConfiguration);
        //            });

        //        if (updatedEntity == null)
        //        {
        //            _logger.LogInformation("Configuration not found for update - Agent: {AgentId}, ConfigId: {ConfigId}",
        //                agentId, configurationId);
        //            return null;
        //        }

        //        var dto = ConvertToDto(updatedEntity);
        //        _logger.LogInformation("Successfully updated configuration for Agent: {AgentId}, ConfigId: {ConfigId}",
        //            agentId, configurationId);

        //        return dto;
        //    }
        //    catch (Exception ex)
        //    {
        //        _logger.LogError(ex, "Failed to update configuration for Agent: {AgentId}, ConfigId: {ConfigId}",
        //            agentId, configurationId);
        //        throw;
        //    }
        //}

        //#region Private Helper Methods

        ///// <summary>
        ///// Convert storage entity to DTO
        ///// </summary>
        ///
        private MetricsConfigurationMetadataDto ToMetricsConfigurationMetadataDto(MetricsConfigurationTableEntity entity)
        {
            return _mapper.Map<MetricsConfigurationMetadataDto>(entity);
        }

        public async Task<bool> DeleteConfigurationAsync(string configurationId)
        {
            try
            {
                _logger.LogInformation("Deleting configuration with ID: {ConfigurationId}", configurationId);

                // First get the configuration to get the AgentId for deletion
                var existingConfig = await _metricsConfigTableService.GetMetricsConfigurationByConfigurationIdAsync(configurationId);

                if (existingConfig == null)
                {
                    _logger.LogWarning("Configuration with ID: {ConfigurationId} not found", configurationId);
                    return false;
                }

                // Delete from table storage
                bool deleted = await _metricsConfigTableService.DeleteMetricsConfigurationByIdAsync(existingConfig.AgentId, configurationId);

                if (deleted)
                {
                    _logger.LogInformation("Configuration with ID: {ConfigurationId} deleted successfully", configurationId);
                    
                    // Also delete the blob file if it exists
                    try
                    {
                        var containerName = $"agent-{CommonUtils.TrimAndRemoveSpaces(existingConfig.AgentId)}";
                        var blobPath = $"configurations/{configurationId}.json";
                        
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

        

        ///// <summary>
        ///// Convert DTO metrics configuration to storage format
        ///// </summary>
        //private IList<Sxg.EvalPlatform.API.Storage.TableEntities.MetricsConfiguration> ConvertMetricsConfiguration(IList<Models.MetricsConfiguration> dtoMetrics)
        //{
        //    return dtoMetrics.Select(m => new Sxg.Eval.Platform.API.Storage.TableEntities.MetricsConfiguration
        //    {
        //        MetricName = m.MetricName,
        //        Threshold = m.Threshold
        //    }).ToList();
        //}

        ///// <summary>
        ///// Convert storage metrics configuration to DTO format
        ///// </summary>
        //private IList<Models.MetricsConfiguration> ConvertMetricsConfigurationToDto(IList<Sxg.EvalPlatform.API.Storage.TableEntities.MetricsConfiguration> storageMetrics)
        //{
        //    return storageMetrics.Select(m => new Models.MetricsConfiguration
        //    {
        //        MetricName = m.MetricName,
        //        Threshold = m.Threshold
        //    }).ToList();
        //}

        //#endregion
    }
}
