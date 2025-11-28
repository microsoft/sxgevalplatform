using Azure;
using Azure.Data.Tables;
using Azure.Identity;
using EvalPurgeJob;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TableEntity = Azure.Data.Tables.TableEntity;

public class TableStorageHelper
{
    private readonly TableServiceClient _tableServiceClient;
    private readonly IConfiguration _config;
    private readonly ILogger _logger;
    private readonly string _tableName;

    public TableStorageHelper(string storageAccountUri, string tableName, ILogger logger)
    {
        _logger = logger;
        _tableName = tableName;
        _tableServiceClient = new TableServiceClient(
            new Uri(storageAccountUri),
            new DefaultAzureCredential()
        );
    }

    public async Task<List<IDictionary<string, object?>>> GetEntitiesModifiedAfterAsync(DateTimeOffset cutoffDate)
    {
        var results = new List<IDictionary<string, object?>>();
        string filter = TableClient.CreateQueryFilter($"LastUpdatedOn gt {cutoffDate}");
        var tableClient = _tableServiceClient.GetTableClient(_tableName);
        await foreach (var entity in tableClient.QueryAsync<TableEntity>(filter))
        {
            results.Add(entity.ToDictionary());
        }
        return results;
    }
}