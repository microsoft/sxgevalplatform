using Azure;
using Azure.Core;
using Azure.Data.Tables;
using Azure.Identity;
using SXG.EvalPlatform.Common;

namespace SxgEvalPlatformApi.Services;

/// <summary>
/// Base class for Azure Table Storage operations with standardized read/write handling
/// </summary>
public abstract class BaseTableService
{
    protected readonly ILogger _logger;
    protected readonly IConfiguration _configuration;

    protected BaseTableService(ILogger logger, IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
    }

    /// <summary>
    /// Creates a configured TableClient for the specified table
    /// </summary>
    /// <param name="tableName">Name of the table</param>
    /// <returns>Configured TableClient</returns>
    protected TableClient CreateTableClient(string tableName)
    {
        var accountName = _configuration["AzureStorage:AccountName"];
        
        if (string.IsNullOrEmpty(accountName))
        {
            throw new ArgumentException("Azure Storage account name is not configured");
        }

        var tableUri = $"https://{accountName}.table.core.windows.net";
        var environment = _configuration.GetValue<string>("ASPNETCORE_ENVIRONMENT") ?? "Production";
        
        TokenCredential credential = CommonUtils.GetTokenCredential(environment);
        
        var serviceClient = new TableServiceClient(new Uri(tableUri), credential);
        var tableClient = serviceClient.GetTableClient(tableName);
        
        // Ensure table exists
        try
        {
            tableClient.CreateIfNotExists();
            _logger.LogInformation("Table client initialized for table: {TableName}", tableName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create or access table: {TableName}", tableName);
            throw;
        }
        
        return tableClient;
    }

    /// <summary>
    /// Standardized read operation with proper error handling and logging
    /// </summary>
    /// <typeparam name="T">Entity type</typeparam>
    /// <param name="tableClient">Table client</param>
    /// <param name="partitionKey">Partition key</param>
    /// <param name="rowKey">Row key</param>
    /// <param name="operationName">Name of the operation for logging</param>
    /// <returns>Entity or null if not found</returns>
    protected async Task<T?> ReadEntityAsync<T>(TableClient tableClient, string partitionKey, string rowKey, string operationName) 
        where T : class, ITableEntity, new()
    {
        try
        {
            _logger.LogInformation("Reading entity: {OperationName} - PartitionKey: {PartitionKey}, RowKey: {RowKey}", 
                operationName, partitionKey, rowKey);

            var response = await tableClient.GetEntityAsync<T>(partitionKey, rowKey);
            
            _logger.LogInformation("Successfully read entity: {OperationName}", operationName);
            return response.Value;
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            _logger.LogWarning("Entity not found: {OperationName} - PartitionKey: {PartitionKey}, RowKey: {RowKey}", 
                operationName, partitionKey, rowKey);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reading entity: {OperationName} - PartitionKey: {PartitionKey}, RowKey: {RowKey}", 
                operationName, partitionKey, rowKey);
            throw;
        }
    }

    /// <summary>
    /// Standardized write operation (upsert) with proper error handling and logging
    /// </summary>
    /// <typeparam name="T">Entity type</typeparam>
    /// <param name="tableClient">Table client</param>
    /// <param name="entity">Entity to write</param>
    /// <param name="operationName">Name of the operation for logging</param>
    /// <returns>The written entity</returns>
    protected async Task<T> WriteEntityAsync<T>(TableClient tableClient, T entity, string operationName) 
        where T : class, ITableEntity
    {
        try
        {
            _logger.LogInformation("Writing entity: {OperationName} - PartitionKey: {PartitionKey}, RowKey: {RowKey}", 
                operationName, entity.PartitionKey, entity.RowKey);

            var response = await tableClient.UpsertEntityAsync(entity);
            
            _logger.LogInformation("Successfully wrote entity: {OperationName}", operationName);
            return entity;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error writing entity: {OperationName} - PartitionKey: {PartitionKey}, RowKey: {RowKey}", 
                operationName, entity.PartitionKey, entity.RowKey);
            throw;
        }
    }

    /// <summary>
    /// Standardized query operation with proper error handling and logging
    /// </summary>
    /// <typeparam name="T">Entity type</typeparam>
    /// <param name="tableClient">Table client</param>
    /// <param name="filter">OData filter expression</param>
    /// <param name="operationName">Name of the operation for logging</param>
    /// <returns>List of entities</returns>
    protected async Task<List<T>> QueryEntitiesAsync<T>(TableClient tableClient, string filter, string operationName) 
        where T : class, ITableEntity, new()
    {
        try
        {
            _logger.LogInformation("Querying entities: {OperationName} - Filter: {Filter}", operationName, filter);

            var entities = new List<T>();
            
            await foreach (var entity in tableClient.QueryAsync<T>(filter))
            {
                entities.Add(entity);
            }
            
            _logger.LogInformation("Successfully queried {Count} entities: {OperationName}", entities.Count, operationName);
            return entities;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error querying entities: {OperationName} - Filter: {Filter}", operationName, filter);
            throw;
        }
    }

    /// <summary>
    /// Standardized delete operation with proper error handling and logging
    /// </summary>
    /// <param name="tableClient">Table client</param>
    /// <param name="partitionKey">Partition key</param>
    /// <param name="rowKey">Row key</param>
    /// <param name="operationName">Name of the operation for logging</param>
    /// <returns>True if deleted, false if not found</returns>
    protected async Task<bool> DeleteEntityAsync(TableClient tableClient, string partitionKey, string rowKey, string operationName)
    {
        try
        {
            _logger.LogInformation("Deleting entity: {OperationName} - PartitionKey: {PartitionKey}, RowKey: {RowKey}", 
                operationName, partitionKey, rowKey);

            await tableClient.DeleteEntityAsync(partitionKey, rowKey);
            
            _logger.LogInformation("Successfully deleted entity: {OperationName}", operationName);
            return true;
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            _logger.LogWarning("Entity not found for deletion: {OperationName} - PartitionKey: {PartitionKey}, RowKey: {RowKey}", 
                operationName, partitionKey, rowKey);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting entity: {OperationName} - PartitionKey: {PartitionKey}, RowKey: {RowKey}", 
                operationName, partitionKey, rowKey);
            throw;
        }
    }
}