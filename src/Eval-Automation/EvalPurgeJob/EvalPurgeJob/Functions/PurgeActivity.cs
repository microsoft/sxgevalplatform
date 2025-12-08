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

        public PurgeBlobsActivity(ILoggerFactory loggerFactory, IConfiguration config)
        {
            _logger = loggerFactory.CreateLogger<EvalPurgeOrchestrator>();
            _config = config;
        }

        [Function("PurgeDataSetFiles")]
        public async Task Run(
            [ActivityTrigger] Tuple<DateTime, string> purgeParams)
        {
            try
            {
                var blobServiceUrl = Environment.GetEnvironmentVariable("BlobServiceUrl");
                var containerName = purgeParams.Item2.ToLower();
                var blobStorageHelper = new BlobStorageHelper(blobServiceUrl, containerName);
                await blobStorageHelper.GetBlobsNewerThanAsync(purgeParams.Item1);

                //var managedIdentityClientId = Environment.GetEnvironmentVariable("ManagedIdentityClientId");

                //await blobStorageHelper.DeleteBlobsNewerThanAsync(purgeParams.Item1);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in PurgeDataSetFiles: {message}", ex.Message);
                throw;
            }

           
        }
    }
}