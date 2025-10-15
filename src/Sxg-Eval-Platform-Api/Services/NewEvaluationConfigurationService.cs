//using Sxg.EvalPlatform.API.Storage.Services;
//using SxgEvalPlatformApi.Models;
//using SxgEvalPlatformApi.Models.Dtos;
//using System.Text.Json;

//namespace SxgEvalPlatformApi.Services;

///// <summary>
///// New implementation of evaluation configuration service using Azure Table Storage for metadata and Blob Storage for content
///// </summary>
//public class NewEvaluationConfigurationService : IEvaluationConfigurationService
//{
//    //private readonly IAzureTableService _tableService;
//    //private readonly IAzureBlobStorageService _blobService;

//    private readonly IMetricsConfigTableService _tableService;
//    private readonly IAzureBlobStorageService _blobService;
//    private readonly ILogger<NewEvaluationConfigurationService> _logger;

//    public NewEvaluationConfigurationService(
//        IMetricsConfigTableService tableService,
//        IAzureBlobStorageService blobService,
//        ILogger<NewEvaluationConfigurationService> logger)
//    {
//        _tableService = tableService;
//        _blobService = blobService;
//        _logger = logger;
//    }

//    /// <inheritdoc />
//    public async Task<ConfigurationSaveResponseDto> CreateOrSaveConfigurationAsync(CreateMetricsConfigurationDto createConfigDto)
//    {
//        try
//        {
//            _logger.LogInformation("Creating/saving configuration: {ConfigName} for agent: {AgentId}", 
//                createConfigDto.ConfigurationName, createConfigDto.AgentId);

//            // Check if configuration already exists
//            //var existingMetadata = await _tableService.GetConfigurationMetadataAsync(
//            //    createConfigDto.AgentId, createConfigDto.ConfigurationName);

//            var existingMetadata = await _tableService.GetMetricsConfigurationAsync(
//               createConfigDto.AgentId, createConfigDto.ConfigurationName);

//            string containerName;
//            string blobFilePath;
//            string configurationId;
//            bool isUpdate = existingMetadata != null;

//            if (isUpdate)
//            {
//                // Use existing metadata
//                containerName = existingMetadata!.;
//                blobFilePath = existingMetadata.BlobFilePath;
//                configurationId = existingMetadata.ConfigurationId;
                
//                _logger.LogInformation("Updating existing configuration with ID: {ConfigId}", configurationId);
//            }
//            else
//            {
//                // Create new configuration
//                configurationId = Guid.NewGuid().ToString();
//                containerName = createConfigDto.AgentId.ToLowerInvariant();
//                blobFilePath = $"configurations/{configurationId}.json";
                
//                _logger.LogInformation("Creating new configuration with ID: {ConfigId}", configurationId);
//            }

//            // Serialize configuration to JSON
//            var configJson = JsonSerializer.Serialize(createConfigDto, new JsonSerializerOptions 
//            { 
//                WriteIndented = true,
//                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
//            });

//            // Save to blob storage
//            var blobSuccess = await _blobService.WriteBlobContentAsync(containerName, blobFilePath, configJson);
            
//            if (!blobSuccess)
//            {
//                throw new InvalidOperationException("Failed to save configuration to blob storage");
//            }

//            // Create or update metadata in table storage
//            var metadata = new ConfigurationMetadataEntity
//            {
//                ConfigurationId = configurationId,
//                ConfigurationType = ConfigurationTypes.ApplicationConfiguration,
//                ContainerName = containerName,
//                BlobFilePath = blobFilePath,
//                AgentId = createConfigDto.AgentId,
//                ConfigurationName = createConfigDto.ConfigurationName,
//                LastUpdatedOn = DateTime.UtcNow
//            };

//            await _tableService.SaveConfigurationMetadataAsync(metadata);

//            return new ConfigurationSaveResponseDto
//            {
//                ConfigId = configurationId,
//                Status = isUpdate ? "updated" : "created",
//                Message = isUpdate ? "Configuration updated successfully" : "Configuration created successfully"
//            };
//        }
//        catch (Exception ex)
//        {
//            _logger.LogError(ex, "Error creating/saving configuration for agent: {AgentId}", createConfigDto.AgentId);
//            return new ConfigurationSaveResponseDto
//            {
//                ConfigId = string.Empty,
//                Status = "error",
//                Message = $"Failed to save configuration: {ex.Message}"
//            };
//        }
//    }

//    /// <inheritdoc />
//    public async Task<CreateMetricsConfigurationDto?> GetConfigurationsByAgentIdAsync(string agentId, string configurationName)
//    {
//        try
//        {
//            _logger.LogInformation("Retrieving configuration for agent: {AgentId}, config: {ConfigName}", 
//                agentId, configurationName);

//            // Get metadata from table storage
//            var metadata = await _tableService.GetConfigurationMetadataAsync(agentId, configurationName);
            
//            if (metadata == null)
//            {
//                _logger.LogInformation("Configuration not found for agent: {AgentId}, config: {ConfigName}", 
//                    agentId, configurationName);
//                return null;
//            }

//            // Read configuration from blob storage
//            var configJson = await _blobService.ReadBlobContentAsync(metadata.ContainerName, metadata.BlobFilePath);
            
//            if (string.IsNullOrEmpty(configJson))
//            {
//                _logger.LogWarning("Blob content is empty for agent: {AgentId}, config: {ConfigName}", 
//                    agentId, configurationName);
//                return null;
//            }

//            var configuration = JsonSerializer.Deserialize<CreateMetricsConfigurationDto>(configJson, new JsonSerializerOptions
//            {
//                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
//            });

//            _logger.LogInformation("Successfully retrieved configuration for agent: {AgentId}, config: {ConfigName}", 
//                agentId, configurationName);

//            return configuration;
//        }
//        catch (Exception ex)
//        {
//            _logger.LogError(ex, "Error retrieving configuration for agent: {AgentId}, config: {ConfigName}", 
//                agentId, configurationName);
//            return null;
//        }
//    }

//    /// <inheritdoc />
//    public async Task<List<CreateMetricsConfigurationDto>> GetConfigurationsByAgentIdAsync(string agentId)
//    {
//        try
//        {
//            _logger.LogInformation("Retrieving all configurations for agent: {AgentId}", agentId);

//            // Get all metadata for the agent
//            var metadataList = await _tableService.GetAllConfigurationMetadataByAgentIdAsync(agentId);
            
//            var configurations = new List<CreateMetricsConfigurationDto>();

//            foreach (var metadata in metadataList)
//            {
//                try
//                {
//                    // Read configuration from blob storage
//                    var configJson = await _blobService.ReadBlobContentAsync(metadata.ContainerName, metadata.BlobFilePath);
                    
//                    if (string.IsNullOrEmpty(configJson))
//                    {
//                        _logger.LogWarning("Blob content is empty for configuration: {ConfigId}", metadata.ConfigurationId);
//                        continue;
//                    }

//                    var configuration = JsonSerializer.Deserialize<CreateMetricsConfigurationDto>(configJson, new JsonSerializerOptions
//                    {
//                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
//                    });

//                    if (configuration != null)
//                    {
//                        configurations.Add(configuration);
//                    }
//                }
//                catch (Exception ex)
//                {
//                    _logger.LogWarning(ex, "Error reading configuration blob for ID: {ConfigId}", metadata.ConfigurationId);
//                    continue;
//                }
//            }

//            _logger.LogInformation("Retrieved {Count} configurations for agent: {AgentId}", 
//                configurations.Count, agentId);

//            return configurations;
//        }
//        catch (Exception ex)
//        {
//            _logger.LogError(ex, "Error retrieving configurations for agent: {AgentId}", agentId);
//            return new List<CreateMetricsConfigurationDto>();
//        }
//    }

//    /// <inheritdoc />
//    public async Task<CreateMetricsConfigurationDto?> GetConfigurationsByAgentIdAsync()
//    {
//        try
//        {
//            _logger.LogInformation("Retrieving platform configuration");

//            // Get platform configuration metadata
//            var metadata = await _tableService.GetPlatformConfigurationMetadataAsync();
            
//            if (metadata == null)
//            {
//                _logger.LogInformation("No platform configuration found");
//                return null;
//            }

//            // Read configuration from blob storage
//            var configJson = await _blobService.ReadBlobContentAsync(metadata.ContainerName, metadata.BlobFilePath);
            
//            if (string.IsNullOrEmpty(configJson))
//            {
//                _logger.LogWarning("Platform configuration blob content is empty");
//                return null;
//            }

//            var configuration = JsonSerializer.Deserialize<CreateMetricsConfigurationDto>(configJson, new JsonSerializerOptions
//            {
//                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
//            });

//            _logger.LogInformation("Successfully retrieved platform configuration");
//            return configuration;
//        }
//        catch (Exception ex)
//        {
//            _logger.LogError(ex, "Error retrieving platform configuration");
//            return null;
//        }
//    }

//    /// <inheritdoc />
//    public async Task<CreateMetricsConfigurationDto?> GetMetricsConfigurationByAgentIdAsync(string agentId)
//    {
//        try
//        {
//            _logger.LogInformation("Retrieving latest metrics configuration for agent: {AgentId}", agentId);

//            // Get all configurations for the agent and return the latest one
//            var configurations = await GetConfigurationsByAgentIdAsync(agentId);
            
//            if (!configurations.Any())
//            {
//                _logger.LogInformation("No configurations found for agent: {AgentId}", agentId);
//                return null;
//            }

//            // Return the first configuration (you might want to add logic to determine which is "latest")
//            var latestConfig = configurations.First();
            
//            _logger.LogInformation("Retrieved latest configuration for agent: {AgentId}", agentId);
//            return latestConfig;
//        }
//        catch (Exception ex)
//        {
//            _logger.LogError(ex, "Error retrieving latest metrics configuration for agent: {AgentId}", agentId);
//            return null;
//        }
//    }

//    /// <inheritdoc />
//    public async Task<EvaluationConfigurationDto?> GetConfigurationByIdAsync(string configId)
//    {
//        try
//        {
//            _logger.LogInformation("Retrieving configuration by ID: {ConfigId}", configId);

//            // This method would require searching through all metadata to find the configuration by ID
//            // For now, return null as this is a complex query that would require additional table design considerations
//            _logger.LogWarning("GetConfigurationByIdAsync is not implemented in the new service design");
//            return null;
//        }
//        catch (Exception ex)
//        {
//            _logger.LogError(ex, "Error retrieving configuration by ID: {ConfigId}", configId);
//            return null;
//        }
//    }

//    /// <inheritdoc />
//    public async Task<bool> ConfigurationExistsAsync(string agentId, string configName)
//    {
//        try
//        {
//            return await _tableService.ConfigurationExistsAsync(agentId, configName);
//        }
//        catch (Exception ex)
//        {
//            _logger.LogError(ex, "Error checking if configuration exists for agent: {AgentId}, config: {ConfigName}", 
//                agentId, configName);
//            return false;
//        }
//    }

//    /// <inheritdoc />
//    public async Task<string?> GetDefaultConfigurationAsync()
//    {
//        try
//        {
//            _logger.LogInformation("Retrieving default configuration");

//            // Get platform configuration and return as JSON string
//            var platformConfig = await GetConfigurationsByAgentIdAsync();
            
//            if (platformConfig == null)
//            {
//                _logger.LogInformation("No platform configuration found for default");
//                return null;
//            }

//            var json = JsonSerializer.Serialize(platformConfig, new JsonSerializerOptions 
//            { 
//                WriteIndented = true,
//                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
//            });

//            return json;
//        }
//        catch (Exception ex)
//        {
//            _logger.LogError(ex, "Error retrieving default configuration");
//            return null;
//        }
//    }
//}