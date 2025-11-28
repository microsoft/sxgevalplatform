
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

        public PurgeStarter(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<PurgeStarter>();
        }

        [Function("EvalPurgeStarter")]
        public async Task RunPurge(
            [TimerTrigger("0 0 9 * * *")] TimerInfo myTimer,
            [DurableClient] DurableTaskClient client)
        {
            await client.ScheduleNewOrchestrationInstanceAsync("EvalPurgeOrchestrator");
        }
    }
}
