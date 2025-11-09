using Azure;
using Azure.Data.Tables;
using Azure.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Sxg.EvalPlatform.API.Storage.TableEntities;

namespace Sxg.EvalPlatform.API.Storage.Services;

/// <summary>
/// Service for EvalRun table operations using Azure Table Storage
/// </summary>
public class EvalRunTableService : IEvalRunTableService
{
    private readonly TableClient _tableClient;
    private readonly ILogger<EvalRunTableService> _logger;
    private const string TableName = "EvalRun";

    public EvalRunTableService(IConfiguration configuration, ILogger<EvalRunTableService> logger)
    {
        _logger = logger;
        
        var accountName = configuration["AzureStorage:AccountName"];
        
        if (string.IsNullOrEmpty(accountName))
        {
            throw new ArgumentException("Azure Storage account name is not configured");
        }

        var tableUri = $"https://{accountName}.table.core.windows.net";
        var environment = configuration.GetValue<string>("ASPNETCORE_ENVIRONMENT") ?? "Production";
        
        if (environment == "Development")
        {
            // Use default Azure credentials for development
            var serviceClient = new TableServiceClient(new Uri(tableUri), new DefaultAzureCredential());
            _tableClient = serviceClient.GetTableClient(TableName);
        }
        else
        {
            // Use managed identity in production
            var serviceClient = new TableServiceClient(new Uri(tableUri), new ManagedIdentityCredential());
            _tableClient = serviceClient.GetTableClient(TableName);
        }
        
        // Ensure table exists
        try
        {
            _tableClient.CreateIfNotExists();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create or access table {TableName}", TableName);
            throw;
        }
    }

    /// <summary>
    /// Create a new evaluation run
    /// </summary>
    public async Task<EvalRunTableEntity> CreateEvalRunAsync(EvalRunTableEntity entity)
    {
        try
        {
            await _tableClient.AddEntityAsync(entity);
            _logger.LogInformation("Created evaluation run with ID: {EvalRunId}", entity.EvalRunId);
            return entity;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating evaluation run for AgentId: {AgentId}, EvalRunId: {EvalRunId}", 
                entity.AgentId, entity.EvalRunId);
            throw;
        }
    }

    /// <summary>
    /// Update evaluation run status
    /// </summary>
    public async Task<EvalRunTableEntity?> UpdateEvalRunStatusAsync(string agentId, Guid evalRunId, string status, string? lastUpdatedBy = null)
    {
        try
        {
            // First, get the existing entity
            var response = await _tableClient.GetEntityAsync<EvalRunTableEntity>(agentId, evalRunId.ToString());
            var entity = response.Value;
            
            if (entity == null)
            {
                _logger.LogWarning("Evaluation run not found with ID: {EvalRunId}", evalRunId);
                return null;
            }

            // Update the status and timestamp
            entity.Status = status;
            entity.LastUpdatedOn = DateTime.UtcNow;
            entity.LastUpdatedBy = lastUpdatedBy ?? "System";
            
            // Set completion datetime if status is Completed or Failed
            if (string.Equals(status, EvalRunStatusConstants.Completed, StringComparison.OrdinalIgnoreCase) || 
                string.Equals(status, EvalRunStatusConstants.Failed, StringComparison.OrdinalIgnoreCase))
            {
                entity.CompletedDatetime = DateTime.UtcNow;
            }

            await _tableClient.UpdateEntityAsync(entity, entity.ETag);
            
            _logger.LogInformation("Updated evaluation run status to {Status} for ID: {EvalRunId}", 
                status, evalRunId);
            
            return entity;
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            _logger.LogWarning("Evaluation run not found with ID: {EvalRunId}", evalRunId);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating evaluation run status for ID: {EvalRunId}", evalRunId);
            throw;
        }
    }

    /// <summary>
    /// Get evaluation run by agent ID and evaluation run ID
    /// </summary>
    public async Task<EvalRunTableEntity?> GetEvalRunByIdAsync(string agentId, Guid evalRunId)
    {
        try
        {
            var response = await _tableClient.GetEntityAsync<EvalRunTableEntity>(agentId, evalRunId.ToString());
            return response.Value;
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            _logger.LogWarning("Evaluation run not found with ID: {EvalRunId}", evalRunId);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving evaluation run with ID: {EvalRunId}", evalRunId);
            throw;
        }
    }

    /// <summary>
    /// Get evaluation run by ID (searches across all partitions)
    /// </summary>
    public async Task<EvalRunTableEntity?> GetEvalRunByIdAsync(Guid evalRunId)
    {
        try
        {
            // Query across all partitions to find the entity by EvalRunId
            var query = _tableClient.QueryAsync<EvalRunTableEntity>(
                filter: $"EvalRunId eq guid'{evalRunId}'");
            
            await foreach (var entity in query)
            {
                return entity;
            }
            
            _logger.LogWarning("Evaluation run not found with ID: {EvalRunId}", evalRunId);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving evaluation run with ID: {EvalRunId}", evalRunId);
            throw;
        }
    }

    /// <summary>
    /// Get all evaluation runs for an agent
    /// </summary>
    public async Task<List<EvalRunTableEntity>> GetEvalRunsByAgentIdAsync(string agentId)
    {
        try
        {
            var query = _tableClient.QueryAsync<EvalRunTableEntity>(
                filter: $"PartitionKey eq '{agentId}'");
            
            var results = new List<EvalRunTableEntity>();
            
            await foreach (var entity in query)
            {
                results.Add(entity);
            }
            
            _logger.LogInformation("Retrieved {Count} evaluation runs for AgentId: {AgentId}", 
                results.Count, agentId);
            
            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving evaluation runs for AgentId: {AgentId}", agentId);
            throw;
        }
    }

    /// <summary>
    /// Update an evaluation run entity
    /// </summary>
    public async Task<EvalRunTableEntity> UpdateEvalRunAsync(EvalRunTableEntity entity)
    {
        try
        {
            await _tableClient.UpdateEntityAsync(entity, entity.ETag);
            _logger.LogInformation("Updated evaluation run with ID: {EvalRunId}", entity.EvalRunId);
            return entity;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating evaluation run with ID: {EvalRunId}", entity.EvalRunId);
            throw;
        }
    }
}