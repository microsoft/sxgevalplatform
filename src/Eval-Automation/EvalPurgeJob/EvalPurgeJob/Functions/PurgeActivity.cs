//using Microsoft.Azure.Functions.Worker;
//using Microsoft.Extensions.Logging;
//using System;
//using System.Threading.Tasks;
//namespace EvalPurgeJob
//{
//    public class PurgeBlobsActivity
//    {
//        private readonly ILogger _logger;

//        public PurgeBlobsActivity(ILoggerFactory loggerFactory)
//        {
//            _logger = loggerFactory.CreateLogger<PurgeBlobsActivity>();
//        }

//        [Function("PurgeDataSetFiles")]
//        public async Task Run(
//            [ActivityTrigger] Tuple<string, DateTimeOffset> input,
//            [ActivityTrigger] DateTimeOffset cutoff,
//            ILogger log)
//        {
//            log.LogInformation("Purging dataset files for agent {agentId} older than {cutoff}", input.Item1, input.Item2);
//            var blobServiceUrl = Environment.GetEnvironmentVariable("BlobServiceUrl");
//            var containerName = input.Item1.ToLower();
//            var managedIdentityClientId = Environment.GetEnvironmentVariable("ManagedIdentityClientId");
//            var blobStorageHelper = new BlobStorageHelper(blobServiceUrl, containerName, managedIdentityClientId);
//            await blobStorageHelper.DeleteBlobsNewerThanAsync(input.Item2);
//        }
//    }
//}