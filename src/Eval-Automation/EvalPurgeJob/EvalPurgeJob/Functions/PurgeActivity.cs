using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Threading.Tasks;
namespace EvalPurgeJob
{
    public class PurgeBlobsActivity
    {
        private readonly ILogger _logger;
        private readonly IConfiguration _config;

        public PurgeBlobsActivity(ILoggerFactory loggerFactory,IConfiguration config)
        {
            _logger = loggerFactory.CreateLogger<EvalPurgeOrchestrator>();
            _config = config;
        }

        [Function("PurgeDataSetFiles")]
        public async Task Run(
            [ActivityTrigger] DateTimeOffset cutoff)
        {
       
            var tableStorageHelper = new TableStorageHelper(
                _config["TableServiceUri"],
                _config["TableName"],
                _logger
            );
            var recentEntities = await tableStorageHelper.GetEntitiesModifiedAfterAsync(cutoff);
            foreach (var entity in recentEntities)
            {
                _logger.LogInformation("Entity PartitionKey: {partitionKey}, RowKey: {rowKey}, LastUpdatedOn: {lastUpdatedOn}",
                    entity.PartitionKey,
                    entity.RowKey,
                    entity.GetDateTimeOffset("LastUpdatedOn"));
                var e = entity.ToDictionary();
            }
            _logger.LogInformation("Fetched {count} table entities with LastUpdatedOn > {cutoff}", recentEntities.Count, cutoff);

            _logger.LogInformation("Purging dataset files for older than {cutoff}", cutoff);

            var uniqueAgentIds = recentEntities
                                   .Select(e => e.GetString("AgentId"))
                                  .Where(id => !string.IsNullOrEmpty(id))
                                  .Distinct()
                                  .ToList();

            //foreach (var agentId in uniqueAgentIds)
            //{
            //    var input = new Tuple<string, DateTimeOffset>(agentId, cutoff);
            //    //await ctx.CallActivityAsync("PurgeDataSetFiles", input);
            //}
            //var blobServiceUrl = Environment.GetEnvironmentVariable("BlobServiceUrl");
            //var containerName = input.Item1.ToLower();
            ////var managedIdentityClientId = Environment.GetEnvironmentVariable("ManagedIdentityClientId");
            //var blobStorageHelper = new BlobStorageHelper(blobServiceUrl, containerName, managedIdentityClientId);
            //await blobStorageHelper.DeleteBlobsNewerThanAsync(input.Item2);
        }
    }
}