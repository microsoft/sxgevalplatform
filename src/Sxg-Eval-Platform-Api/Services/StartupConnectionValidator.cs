using Azure.Data.Tables;
using Azure.Identity;
using Azure.Storage.Blobs;
using Microsoft.Extensions.Options;
using Sxg.EvalPlatform.API.Storage;
using StackExchange.Redis;
using SxgEvalPlatformApi.Configuration;

namespace SxgEvalPlatformApi.Services;

public interface IStartupConnectionValidator
{
    Task ValidateConnectionsAsync();
}

public class StartupConnectionValidator : IStartupConnectionValidator
{
    private readonly IConnectionMultiplexer _redisConnection;
    private readonly IConfiguration _configuration;
    private readonly ILogger<StartupConnectionValidator> _logger;
    private readonly IOptions<RedisConfiguration> _redisConfig;

    public StartupConnectionValidator(
        IConnectionMultiplexer redisConnection,
        IConfiguration configuration,
        ILogger<StartupConnectionValidator> logger,
        IOptions<RedisConfiguration> redisConfig)
    {
        _redisConnection = redisConnection;
        _configuration = configuration;
        _logger = logger;
        _redisConfig = redisConfig;
    }

    public async Task ValidateConnectionsAsync()
    {
        _logger.LogInformation("Validating connections...");

        var validationTasks = new[]
        {
            ValidateRedisConnectionAsync(),
            ValidateAzureStorageConnectionAsync()
        };

        try
        {
            await Task.WhenAll(validationTasks);
            _logger.LogInformation("All connections validated successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Connection validation failed");
            throw;
        }
    }

    private async Task ValidateRedisConnectionAsync()
    {
        try
        {
            var database = _redisConnection.GetDatabase();
            
            // Test basic connectivity with a ping
            var pingResult = await database.PingAsync();
            
            // Test read/write operations
            var testKey = "startup:validation:test";
            var testValue = $"startup-test-{DateTime.UtcNow:yyyy-MM-dd-HH-mm-ss}";
            
            // Test SET operation
            var setResult = await database.StringSetAsync(testKey, testValue, TimeSpan.FromMinutes(1));
            if (!setResult)
            {
                throw new InvalidOperationException("Failed to SET test key in Redis");
            }
            
            // Test GET operation
            var getValue = await database.StringGetAsync(testKey);
            if (!getValue.HasValue || getValue != testValue)
            {
                throw new InvalidOperationException("Failed to GET test key from Redis or value mismatch");
            }
            
            // Clean up test key
            await database.KeyDeleteAsync(testKey);
            
            var authMethod = string.IsNullOrEmpty(_redisConfig.Value.AccessKey) 
                ? "Microsoft Entra Auth" 
                : "Access Key Auth";
            
            _logger.LogInformation("Redis connected ({PingTime}ms) - {AuthMethod}", pingResult.TotalMilliseconds, authMethod);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Redis connection validation failed");
            throw new InvalidOperationException("Redis connection validation failed. Please check your Redis configuration and authentication.", ex);
        }
    }

    private async Task ValidateAzureStorageConnectionAsync()
    {
        try
        {
            var storageAccountName = _configuration.GetValue<string>("AzureStorage:AccountName");
            if (string.IsNullOrEmpty(storageAccountName))
            {
                throw new InvalidOperationException("AzureStorage:AccountName is not configured");
            }

            var credential = new DefaultAzureCredential();
            
            // Test Blob Storage connection
            var blobServiceClient = new BlobServiceClient(
                new Uri($"https://{storageAccountName}.blob.core.windows.net"), 
                credential);
            
            var blobProperties = await blobServiceClient.GetPropertiesAsync();
            
            // Test Table Storage connection
            var tableServiceClient = new TableServiceClient(
                new Uri($"https://{storageAccountName}.table.core.windows.net"), 
                credential);
            
            var tableProperties = await tableServiceClient.GetPropertiesAsync();
            
            // Test specific tables exist (silently)
            var requiredTables = new[]
            {
                _configuration.GetValue<string>("AzureStorage:MetricsConfigurationsTable") ?? "MetricsConfigurationsTable",
                _configuration.GetValue<string>("AzureStorage:DataSetsTable") ?? "DataSetsTable",
                _configuration.GetValue<string>("AzureStorage:EvalRunTable") ?? "EvalRunsTable"
            };

            foreach (var tableName in requiredTables)
            {
                try
                {
                    var tableClient = tableServiceClient.GetTableClient(tableName);
                    await tableClient.CreateIfNotExistsAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Could not verify table '{TableName}', but continuing", tableName);
                }
            }
            
            _logger.LogInformation("Azure Storage connected - Managed Identity");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Azure Storage connection validation failed");
            throw new InvalidOperationException("Azure Storage connection validation failed. Please check your storage configuration and managed identity permissions.", ex);
        }
    }
}