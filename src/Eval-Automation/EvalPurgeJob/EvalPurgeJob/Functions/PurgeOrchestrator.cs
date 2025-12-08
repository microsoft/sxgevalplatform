using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.DurableTask;
using Microsoft.DurableTask;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace EvalPurgeJob
{
    public class EvalPurgeOrchestrator
    {
        private readonly ILogger _logger;
        private readonly IConfiguration _config;

        public EvalPurgeOrchestrator(ILoggerFactory loggerFactory, IConfiguration config)
        {
            _logger = loggerFactory.CreateLogger<EvalPurgeOrchestrator>();
            _config = config;
        }
        [Function("EvalPurgeOrchestrator")]
        public async Task RunOrchestrato([OrchestrationTrigger] TaskOrchestrationContext ctx, Tuple<DateTime, string> purgeParams)
        {
            try
            {

                if (ctx != null)
                {
                    await ctx.CallActivityAsync<string>("PurgeDataSetFiles", purgeParams);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in EvalPurgeOrchestrator: {message}", ex.Message);
                throw;
            }

        }


    }
}