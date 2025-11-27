using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.DurableTask;
using Microsoft.DurableTask;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;

namespace EvalPurgeJob
{
    public  class EvalPurgeOrchestrator
    {
        private readonly ILogger _logger;
        private readonly IConfiguration _config;

        public EvalPurgeOrchestrator(ILoggerFactory loggerFactory, IConfiguration config)
        {
            _logger = loggerFactory.CreateLogger<EvalPurgeOrchestrator>();
            _config = config;
        }
        [Function("EvalPurgeOrchestrator")]
        public  async Task RunOrchestrato([OrchestrationTrigger] TaskOrchestrationContext ctx)
        {
            //var outputs = new List<string>();

            //// Replace "hello" with the name of your Durable Activity Function.
            //outputs.Add(await context.CallActivityAsync<string>(nameof(SayHello), "Tokyo"));
            //outputs.Add(await context.CallActivityAsync<string>(nameof(SayHello), "Seattle"));
            //outputs.Add(await context.CallActivityAsync<string>(nameof(SayHello), "London"));

            //// returns ["Hello Tokyo!", "Hello Seattle!", "Hello London!"]
            //return outputs;

            var retentionDays = _config.GetValue<int>("Purge:RetentionDays", 3);

            var cutoff = DateTimeOffset.UtcNow.AddDays(-retentionDays);
          
            var tableStorageHelper = new TableStorageHelper(
                _config["TableServiceUri"],
                _config["TableName"],
                _logger
            );
            var recentEntities = await tableStorageHelper.GetEntitiesModifiedAfterAsync(cutoff);
            _logger.LogInformation("Fetched {count} table entities with ModifiedDate > {cutoff}", recentEntities.Count, cutoff);

        }
    
       
    }
}