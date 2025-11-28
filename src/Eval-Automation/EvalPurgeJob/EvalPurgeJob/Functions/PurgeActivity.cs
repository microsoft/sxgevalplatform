using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
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
            var uniqueAgentIds = new HashSet<string>();
            foreach (var row in recentEntities)
            {
                if (row.TryGetValue("AgentId", out var agentIdObj) && agentIdObj is string agentId && !string.IsNullOrEmpty(agentId))
                {
                   uniqueAgentIds.Add(agentId);
                }
            }

            _logger.LogInformation("Purging dataset files for older than {cutoff}", cutoff);


            foreach (var agentId in uniqueAgentIds)
            {
                var blobServiceUrl = Environment.GetEnvironmentVariable("BlobServiceUrl");
                var containerName = agentId.ToLower();
                var blobStorageHelper = new BlobStorageHelper(blobServiceUrl, containerName);
                await blobStorageHelper.GetBlobsNewerThanAsync(cutoff);
            }
           
            //var managedIdentityClientId = Environment.GetEnvironmentVariable("ManagedIdentityClientId");
           
            //await blobStorageHelper.DeleteBlobsNewerThanAsync(input.Item2);
        }
    }
}