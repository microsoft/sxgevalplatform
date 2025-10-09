using Azure.Core;
using Azure.Identity;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using SxgEvalPlatformApi.Models;
using System.Text.Json;

namespace SxgEvalPlatformApi.Services;

/// <summary>
/// Implementation of evaluation configuration service using Azure Blob Storage
/// </summary>
public class EvaluationConfigurationService : IEvaluationConfigurationService
{
    private readonly ILogger<EvaluationConfigurationService> _logger;
    private readonly BlobServiceClient _blobServiceClient;
    private readonly BlobContainerClient _containerClient;
    private readonly IConfiguration _configuration;
    private readonly string _containerName;
    private readonly string _defaultConfigBlobName;

    public EvaluationConfigurationService(
        ILogger<EvaluationConfigurationService> logger, 
        IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
        
        var storageAccountName = _configuration["AzureStorage:AccountName"] 
            ?? throw new ArgumentException("AzureStorage:AccountName configuration is missing");
        
        _containerName = _configuration["AzureStorage:ConfigurationContainer"] 
            ?? throw new ArgumentException("AzureStorage:ConfigurationContainer configuration is missing");
        
        _defaultConfigBlobName = _configuration["AzureStorage:DefaultConfigurationBlob"] 
            ?? "default-metric-configuration.json";

        // Use Managed Identity for authentication based on environment
        // - Development: Uses AzureCliCredential for local development
        // - Production/Staging: Uses DefaultAzureCredential (Managed Identity preferred)
        // Default to Production if ASPNETCORE_ENVIRONMENT is not configured
        var environment = _configuration.GetValue<string>("ASPNETCORE_ENVIRONMENT") ?? "Production";
        var isDevelopment = environment.Equals("Development", StringComparison.OrdinalIgnoreCase);
        
        TokenCredential credential = isDevelopment
            ? new AzureCliCredential() 
            : new DefaultAzureCredential();

        var blobServiceUri = new Uri($"https://{storageAccountName}.blob.core.windows.net");
        
        _blobServiceClient = new BlobServiceClient(blobServiceUri, credential);
        _containerClient = _blobServiceClient.GetBlobContainerClient(_containerName);
        
        _logger.LogInformation("EvaluationConfigurationService initialized with storage account: {StorageAccount}, container: {Container}, environment: {Environment}", 
            storageAccountName, _containerName, environment);
    }

    /// <inheritdoc />
    public async Task<ConfigurationSaveResponseDto> CreateOrSaveConfigurationAsync(CreateMetricsConfigurationDto createConfigDto)
    {
        _logger.LogInformation("Creating/saving evaluation configuration: {ConfigName} for agent: {AgentId}", 
            createConfigDto.ConfigName, createConfigDto.AgentId);

        try
        {
            // Check if configuration already exists
            var (existingConfig, existingConfigId) = await GetMetricsConfigurationByAgentAndNameAsync(createConfigDto.AgentId, createConfigDto.ConfigName);
            
            bool isUpdate = existingConfig != null;
            string configId;
            string status;
            string message;

            if (isUpdate)
            {
                // Use existing config ID or generate new one
                configId = existingConfigId ?? Guid.NewGuid().ToString();
                status = "updated";
                message = "Evaluation configuration updated successfully.";
                
                _logger.LogInformation("Updated configuration with ID: {ConfigId}", configId);
            }
            else
            {
                // Create new configuration ID
                configId = Guid.NewGuid().ToString();
                status = "saved";
                message = "Evaluation configuration saved successfully.";
                
                _logger.LogInformation("Created new configuration with ID: {ConfigId}", configId);
            }

            // Save CreateMetricsConfigurationDto directly to blob storage
            await SaveMetricsConfigurationToBlobAsync(createConfigDto, configId);

            return new ConfigurationSaveResponseDto
            {
                ConfigId = configId,
                Status = status,
                Message = message
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating/saving evaluation configuration for agent: {AgentId}", createConfigDto.AgentId);
            return new ConfigurationSaveResponseDto
            {
                ConfigId = string.Empty,
                Status = "error",
                Message = $"Failed to save configuration: {ex.Message}"
            };
        }
    }

    /// <inheritdoc />
    public async Task<ConfigurationsResponseDto?> GetConfigurationsByAgentIdAsync(string agentId)
    {
        _logger.LogInformation("Retrieving configurations for agent: {AgentId}", agentId);

        try
        {
            var agentConfigs = await GetAllMetricsConfigurationsForAgentAsync(agentId);

            if (!agentConfigs.Any())
            {
                _logger.LogWarning("No configurations found for agent: {AgentId}", agentId);
                return null;
            }

            var response = new ConfigurationsResponseDto
            {
                AgentId = agentId,
                Configurations = agentConfigs
                    .OrderByDescending(c => c.LastUpdated ?? c.CreatedAt)
                    .Select(c => new ConfigurationSummaryDto
                    {
                        ConfigId = c.ConfigId!,
                        ConfigName = c.ConfigName,
                        Dataset = new DatasetSummaryDto
                        {
                            Type = "Metrics", 
                            Source = "CreateMetricsConfiguration"
                        },
                        Evaluators = new List<string> { "MetricsConfiguration" },
                        PassingThreshold = 0.0,
                        LastUpdated = c.LastUpdated ?? c.CreatedAt
                    }).ToList()
            };

            _logger.LogInformation("Retrieved {Count} configurations for agent: {AgentId}", 
                response.Configurations.Count, agentId);

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving configurations for agent: {AgentId}", agentId);
            return null;
        }
    }

    /// <inheritdoc />
    public async Task<List<CreateMetricsConfigurationDto>> GetMetricsConfigurationsByAgentIdAsync(string agentId)
    {
        _logger.LogInformation("Retrieving metrics configurations for agent: {AgentId}", agentId);

        try
        {
            var configurations = new List<CreateMetricsConfigurationDto>();
            var blobs = _containerClient.GetBlobsAsync(prefix: $"agent-{agentId}/");
            
            await foreach (var blobItem in blobs)
            {
                if (blobItem.Name == _defaultConfigBlobName) continue;
                
                try
                {
                    var blobClient = _containerClient.GetBlobClient(blobItem.Name);
                    var response = await blobClient.DownloadContentAsync();
                    var json = response.Value.Content.ToString();
                    
                    var config = JsonSerializer.Deserialize<CreateMetricsConfigurationDto>(json, new JsonSerializerOptions
                    {
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                    });
                    
                    if (config != null)
                    {
                        configurations.Add(config);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error reading blob {BlobName} as CreateMetricsConfigurationDto", blobItem.Name);
                    continue;
                }
            }

            return configurations;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving metrics configurations for agent: {AgentId}", agentId);
            return new List<CreateMetricsConfigurationDto>();
        }
    }

    /// <inheritdoc />
    public async Task<CreateMetricsConfigurationDto?> GetMetricsConfigurationByAgentIdAsync(string agentId)
    {
        _logger.LogInformation("Retrieving single metrics configuration for agent: {AgentId}", agentId);

        try
        {
            var blobs = _containerClient.GetBlobsAsync(prefix: $"agent-{agentId}/");
            CreateMetricsConfigurationDto? latestConfig = null;
            DateTime latestModified = DateTime.MinValue;
            
            await foreach (var blobItem in blobs)
            {
                if (blobItem.Name == _defaultConfigBlobName) continue;
                
                try
                {
                    var blobClient = _containerClient.GetBlobClient(blobItem.Name);
                    var response = await blobClient.DownloadContentAsync();
                    var json = response.Value.Content.ToString();
                    
                    var config = JsonSerializer.Deserialize<CreateMetricsConfigurationDto>(json, new JsonSerializerOptions
                    {
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                    });
                    
                    if (config != null)
                    {
                        var properties = await blobClient.GetPropertiesAsync();
                        var lastModified = properties.Value.LastModified.DateTime;
                        
                        if (latestConfig == null || lastModified > latestModified)
                        {
                            latestConfig = config;
                            latestModified = lastModified;
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error reading blob {BlobName}", blobItem.Name);
                    continue;
                }
            }

            return latestConfig;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving metrics configuration for agent: {AgentId}", agentId);
            return null;
        }
    }

    /// <inheritdoc />
    public async Task<EvaluationConfigurationDto?> GetConfigurationByIdAsync(string configId)
    {
        _logger.LogInformation("Retrieving configuration by ID: {ConfigId}", configId);

        try
        {
            var config = await GetConfigurationFromBlobByIdAsync(configId);
            
            if (config == null)
            {
                _logger.LogWarning("Configuration not found with ID: {ConfigId}", configId);
            }

            return config;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving configuration by ID: {ConfigId}", configId);
            return null;
        }
    }

    /// <inheritdoc />
    public async Task<bool> ConfigurationExistsAsync(string agentId, string configName)
    {
        _logger.LogInformation("Checking if configuration exists: {ConfigName} for agent: {AgentId}", 
            configName, agentId);

        try
        {
            var (config, _) = await GetMetricsConfigurationByAgentAndNameAsync(agentId, configName);
            return config != null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking if configuration exists: {ConfigName} for agent: {AgentId}", configName, agentId);
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<string?> GetDefaultConfigurationAsync()
    {
        _logger.LogInformation("Retrieving default configuration from blob: {BlobName}", _defaultConfigBlobName);

        try
        {
            var blobClient = _containerClient.GetBlobClient(_defaultConfigBlobName);
            
            if (await blobClient.ExistsAsync())
            {
                var response = await blobClient.DownloadContentAsync();
                var content = response.Value.Content.ToString();
                
                _logger.LogInformation("Successfully retrieved default configuration from Azure Blob Storage");
                return content;
            }
            else
            {
                _logger.LogWarning("Default configuration blob not found: {BlobName}", _defaultConfigBlobName);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Unable to retrieve default configuration from Azure Blob Storage, falling back to local file");
        }

        try
        {
            var localConfigPath = Path.Combine(AppContext.BaseDirectory, "metrics-configuration", "default-metric-configuration.json");
            
            if (File.Exists(localConfigPath))
            {
                var content = await File.ReadAllTextAsync(localConfigPath);
                _logger.LogInformation("Successfully retrieved default configuration from local file: {FilePath}", localConfigPath);
                return content;
            }
            else
            {
                _logger.LogWarning("Local default configuration file not found: {FilePath}", localConfigPath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reading local default configuration file");
        }

        return null;
    }

    #region Private Helper Methods

    private async Task SaveMetricsConfigurationToBlobAsync(CreateMetricsConfigurationDto config, string configId)
    {
        try
        {
            var blobName = GetBlobNameForConfiguration(config.AgentId, configId);
            var blobClient = _containerClient.GetBlobClient(blobName);
            
            var json = JsonSerializer.Serialize(config, new JsonSerializerOptions 
            { 
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });
            
            using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(json));
            
            await blobClient.UploadAsync(stream, overwrite: true);
            
            var metadata = new Dictionary<string, string>
            {
                ["AgentId"] = config.AgentId,
                ["ConfigName"] = config.ConfigName,
                ["ConfigId"] = configId,
                ["LastUpdated"] = DateTime.UtcNow.ToString("O"),
                ["CreatedAt"] = DateTime.UtcNow.ToString("O")
            };
            
            await blobClient.SetMetadataAsync(metadata);
            
            _logger.LogInformation("Configuration saved to blob: {BlobName}", blobName);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to save configuration to Azure Blob Storage, falling back to local storage");
            
            try
            {
                var localDir = Path.Combine(AppContext.BaseDirectory, "local-configs", $"agent-{config.AgentId}");
                Directory.CreateDirectory(localDir);
                
                var localFilePath = Path.Combine(localDir, $"{configId}.json");
                var json = JsonSerializer.Serialize(config, new JsonSerializerOptions 
                { 
                    WriteIndented = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });
                
                await File.WriteAllTextAsync(localFilePath, json);
                _logger.LogInformation("Configuration saved to local file: {FilePath}", localFilePath);
            }
            catch (Exception localEx)
            {
                _logger.LogError(localEx, "Failed to save configuration to local storage as well");
                throw;
            }
        }
    }

    private async Task<(CreateMetricsConfigurationDto? Config, string? ConfigId)> GetMetricsConfigurationByAgentAndNameAsync(string agentId, string configName)
    {
        try
        {
            var blobs = _containerClient.GetBlobsAsync(prefix: $"agent-{agentId}/");
            
            await foreach (var blobItem in blobs)
            {
                if (blobItem.Name == _defaultConfigBlobName) continue;
                
                try
                {
                    var blobClient = _containerClient.GetBlobClient(blobItem.Name);
                    var propertiesResponse = await blobClient.GetPropertiesAsync();
                    
                    if (propertiesResponse.Value.Metadata.TryGetValue("ConfigName", out var metadataConfigName) &&
                        metadataConfigName.Equals(configName, StringComparison.OrdinalIgnoreCase))
                    {
                        var response = await blobClient.DownloadContentAsync();
                        var json = response.Value.Content.ToString();
                        
                        var config = JsonSerializer.Deserialize<CreateMetricsConfigurationDto>(json, new JsonSerializerOptions
                        {
                            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                        });
                        
                        if (config != null)
                        {
                            var configId = propertiesResponse.Value.Metadata.TryGetValue("ConfigId", out var id) ? id : null;
                            return (config, configId);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error reading blob {BlobName}", blobItem.Name);
                    continue;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving configuration by agent and name: {AgentId}, {ConfigName}", agentId, configName);
        }
        
        return (null, null);
    }

    private async Task<List<MetricsConfigurationWithMetadata>> GetAllMetricsConfigurationsForAgentAsync(string agentId)
    {
        var configurations = new List<MetricsConfigurationWithMetadata>();
        
        try
        {
            var blobs = _containerClient.GetBlobsAsync(prefix: $"agent-{agentId}/");
            
            await foreach (var blobItem in blobs)
            {
                if (blobItem.Name == _defaultConfigBlobName) continue;
                
                try
                {
                    var blobClient = _containerClient.GetBlobClient(blobItem.Name);
                    var response = await blobClient.DownloadContentAsync();
                    var json = response.Value.Content.ToString();
                    
                    var metricsConfig = JsonSerializer.Deserialize<CreateMetricsConfigurationDto>(json, new JsonSerializerOptions
                    {
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                    });
                    
                    if (metricsConfig != null)
                    {
                        var propertiesResponse = await blobClient.GetPropertiesAsync();
                        var metadata = propertiesResponse.Value.Metadata;
                        
                        var configWithMetadata = new MetricsConfigurationWithMetadata
                        {
                            ConfigId = metadata.TryGetValue("ConfigId", out var configId) ? configId : null,
                            AgentId = metricsConfig.AgentId,
                            ConfigName = metricsConfig.ConfigName,
                            Description = metricsConfig.Description,
                            MetricsConfiguration = metricsConfig.MetricsConfiguration,
                            CreatedAt = metadata.TryGetValue("CreatedAt", out var createdAt) && DateTime.TryParse(createdAt, out var createdAtDate) 
                                ? createdAtDate : DateTime.UtcNow,
                            LastUpdated = metadata.TryGetValue("LastUpdated", out var lastUpdated) && DateTime.TryParse(lastUpdated, out var lastUpdatedDate) 
                                ? lastUpdatedDate : null
                        };
                        
                        configurations.Add(configWithMetadata);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error reading blob {BlobName}", blobItem.Name);
                    continue;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving configurations for agent: {AgentId}", agentId);
        }
        
        return configurations;
    }

    private async Task<EvaluationConfigurationDto?> GetConfigurationFromBlobByIdAsync(string configId)
    {
        try
        {
            var blobs = _containerClient.GetBlobsAsync();
            
            await foreach (var blobItem in blobs)
            {
                if (blobItem.Name == _defaultConfigBlobName) continue;
                
                try
                {
                    var blobClient = _containerClient.GetBlobClient(blobItem.Name);
                    var propertiesResponse = await blobClient.GetPropertiesAsync();
                    var metadata = propertiesResponse.Value.Metadata;
                    
                    if (metadata.TryGetValue("ConfigId", out var metadataConfigId) && metadataConfigId == configId)
                    {
                        var response = await blobClient.DownloadContentAsync();
                        var json = response.Value.Content.ToString();
                        
                        var metricsConfig = JsonSerializer.Deserialize<CreateMetricsConfigurationDto>(json, new JsonSerializerOptions
                        {
                            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                        });
                        
                        if (metricsConfig != null)
                        {
                            return new EvaluationConfigurationDto
                            {
                                ConfigId = configId,
                                AgentId = metricsConfig.AgentId,
                                ConfigName = metricsConfig.ConfigName,
                                Description = metricsConfig.Description,
                                CreatedAt = metadata.TryGetValue("CreatedAt", out var createdAt) && DateTime.TryParse(createdAt, out var createdAtDate) 
                                    ? createdAtDate : DateTime.UtcNow,
                                LastUpdated = metadata.TryGetValue("LastUpdated", out var lastUpdated) && DateTime.TryParse(lastUpdated, out var lastUpdatedDate) 
                                    ? lastUpdatedDate : null,
                                Dataset = new DatasetDto { Type = "Metrics", Source = "Configuration" },
                                Evaluators = new List<EvaluatorDto>(),
                                PassingThreshold = 0.0
                            };
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error reading blob {BlobName}", blobItem.Name);
                    continue;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching for configuration by ID: {ConfigId}", configId);
        }
        
        return null;
    }

    private static string GetBlobNameForConfiguration(string agentId, string configId)
    {
        return $"agent-{agentId}/{configId}.json";
    }

    private class MetricsConfigurationWithMetadata
    {
        public string? ConfigId { get; set; }
        public string AgentId { get; set; } = string.Empty;
        public string ConfigName { get; set; } = string.Empty;
        public string? Description { get; set; }
        public JsonElement MetricsConfiguration { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? LastUpdated { get; set; }
    }

    #endregion
}