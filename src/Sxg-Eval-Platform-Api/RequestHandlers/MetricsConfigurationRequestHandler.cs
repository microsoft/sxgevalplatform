using AutoMapper;
using Sxg.EvalPlatform.API.Storage;
using Sxg.EvalPlatform.API.Storage.Entities;
using Sxg.EvalPlatform.API.Storage.Services;
using Sxg.EvalPlatform.API.Storage.TableEntities;
using SXG.EvalPlatform.Common;
using SxgEvalPlatformApi.Models.Dtos;
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

        public MetricsConfigurationRequestHandler(
            IMetricsConfigTableService metricsConfigTableService,
            IAzureBlobStorageService blobStorageService,
            ILogger<MetricsConfigurationRequestHandler> logger,
            IMapper mapper,
            IConfigHelper configHelper)
        {
            _metricsConfigTableService = metricsConfigTableService;
            _logger = logger;
            _mapper = mapper;
            _blobStorageService = blobStorageService;
            _configHelper = configHelper;
        }

        public async Task<MetricsConfiguration> GetDefaultMetricsConfigurationAsync()
        {
            string containerName = _configHelper.GetPlatformConfigurationsContainer();
            string blobFilePath = _configHelper.GetDefaultMetricsConfiguration();
            var blobContent = await _blobStorageService.ReadBlobContentAsync(containerName, blobFilePath);

            if (blobContent == null)
            {
                throw new Exception($"Default metrics configuration blob not found: {containerName}/{blobFilePath}");
            }

            var metrics = System.Text.Json.JsonSerializer.Deserialize<MetricsConfiguration>(blobContent);

            if (metrics == null)
            {
                throw new Exception("Failed to deserialize default metrics configuration from blob content default metrics configuration.");
            }


            return metrics; 
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

        public async Task<IList<SelectedMetricsConfiguration>> GetMetricsConfigurationByConfigurationIdAsync(string configurationId)
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

                IList<SelectedMetricsConfiguration>? metrics = System.Text.Json.JsonSerializer.Deserialize<IList<SelectedMetricsConfiguration>>(blobContent);

                if (metrics == null)
                {
                    throw new Exception("Failed to deserialize metrics configuration from blob content.");
                }

                var result = ToSelectedMetricsConfigurationDto(metrics);

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

        //public async Task<bool> ConfigurationExistsAsync(string agentId, string configurationName, string environmentName)
        //{
        //    try
        //    {
        //        _logger.LogInformation("Checking if configuration exists for Agent: {AgentId}, Config: {ConfigName}, Environment: {Environment}",
        //            agentId, configurationName, environmentName);

        //        var exists = await _metricsConfigTableService.MetricsConfigurationExistsAsync(agentId, configurationName, environmentName);

        //        _logger.LogInformation("Configuration exists check for Agent: {AgentId}, Config: {ConfigName}, Environment: {Environment} = {Exists}",
        //            agentId, configurationName, environmentName, exists);

        //        return exists;
        //    }
        //    catch (Exception ex)
        //    {
        //        _logger.LogError(ex, "Failed to check configuration existence for Agent: {AgentId}, Config: {ConfigName}, Environment: {Environment}",
        //            agentId, configurationName, environmentName);
        //        throw;
        //    }
        //}

        private IList<SelectedMetricsConfigurationDto> ToSelectedMetricsConfigurationDto(IList<SelectedMetricsConfiguration> entity)
        {
            return _mapper.Map<IList<SelectedMetricsConfigurationDto>>(entity);

        }

        private MetricsConfigurationTableEntity ToMetricsConfigurationEntity(CreateMetricsConfigurationDto dto)
        {
            return _mapper.Map<MetricsConfigurationTableEntity>(dto);
        }

        public async Task<ConfigurationSaveResponseDto> CreateOrSaveConfigurationAsync(CreateMetricsConfigurationDto createConfigDto)
        {
            try
            {
                _logger.LogInformation("Creating/saving configuration for Agent: {AgentId}, Config: {ConfigName}, Environment: {Environment}",
                    createConfigDto.AgentId, createConfigDto.ConfigurationName, createConfigDto.EnvironmentName);

                // Check if configuration already exists
                var existingConfig = await _metricsConfigTableService.GetAllMetricsConfigurations(
                    createConfigDto.AgentId, createConfigDto.ConfigurationName, createConfigDto.EnvironmentName);

                MetricsConfigurationTableEntity entity;
                bool isUpdate = false;

                var blobContainer = CommonUtils.TrimAndRemoveSpaces(createConfigDto.AgentId);
                
                var blobFileName = $"{createConfigDto.ConfigurationName}_{createConfigDto.EnvironmentName}.json";
                var blobFilePath = $"{_configHelper.GetMetricsConfigurationsFolderName()}/{blobFileName}";

                if (existingConfig != null && existingConfig.Count > 0)
                {
                    entity = existingConfig.First();
                    blobFileName = entity.BlobFilePath; // Reuse existing blob file name
                    isUpdate = true; 
                }
                else
                {
                    entity = ToMetricsConfigurationEntity(createConfigDto);
                    entity.BlobFilePath = blobFilePath;
                    entity.ConainerName = blobContainer; 
                }

                entity.LastUpdatedOn = DateTime.UtcNow;
                //TODO: Set LastUpdatedBy from Auth Token


                // Save metrics configuration to blob
                var blobWriteResult = await _blobStorageService.WriteBlobContentAsync(blobContainer, blobFilePath,
                    System.Text.Json.JsonSerializer.Serialize(createConfigDto.MetricsConfiguration));

                
                // Save to storage
                var savedEntity = await _metricsConfigTableService.SaveMetricsConfigurationAsync(entity);


                var response = new ConfigurationSaveResponseDto
                {
                    ConfigId = savedEntity.ConfigurationId,
                    Status = "success",
                    Message = isUpdate ? "Configuration updated successfully" : "Configuration created successfully"
                };

                _logger.LogInformation("Successfully {Action} configuration with ID: {ConfigId}",
                    isUpdate ? "updated" : "created", savedEntity.ConfigurationId);

                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create/save configuration for Agent: {AgentId}",
                    createConfigDto.AgentId);

                return new ConfigurationSaveResponseDto
                {
                    ConfigId = string.Empty,
                    Status = "error",
                    Message = $"Failed to save configuration: {ex.Message}"
                };
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
