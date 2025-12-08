
using Azure.Storage.Blobs;
using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace EvalPurgeJob
{

    public class PurgeStarter
    {
        private readonly ILogger _logger;
        private readonly IConfiguration _config;

        public PurgeStarter(ILoggerFactory loggerFactory, IConfiguration config)
        {
            _logger = loggerFactory.CreateLogger<PurgeStarter>();
            _config = config;
        }

        [Function("EvalPurgeStarter")]
        public async Task RunPurge(
            [TimerTrigger("0 26 17 * * *")] TimerInfo myTimer,
            [DurableClient] DurableTaskClient client)
        {
            try { 
                _logger.LogInformation("EvalPurgeStarter function executed at: {time}", DateTime.UtcNow);
                await StartPurgeProcessAsync(client);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in EvalPurgeStarter: {message}", ex.Message);
                throw;
            }
           
        }
        private async Task StartPurgeProcessAsync(DurableTaskClient client)
        {
            
            _logger.LogInformation("Starting purge process...");
            var retentionDays = _config.GetValue<int>("Purge:RetentionDays", 30);

            var cutoff = DateTime.UtcNow.AddDays(-retentionDays);

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
                await client.ScheduleNewOrchestrationInstanceAsync("EvalPurgeOrchestrator", new Tuple<DateTime, string>(cutoff, agentId));
            }

            _logger.LogInformation("Purge process started.");
        }
    }
}
