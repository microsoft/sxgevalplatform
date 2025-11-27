//using Microsoft.Azure.WebJobs;
//using Microsoft.Azure.WebJobs.Extensions.DurableTask;
//using Microsoft.Extensions.Logging;
//namespace EvalPurgeJob
//{
//    public class PurgeBlobsActivity
//    {
//        private readonly ILogger _logger;

//        public PurgeBlobsActivity(ILoggerFactory loggerFactory)
//        {
//            _logger = loggerFactory.CreateLogger<PurgeBlobsActivity>();
//        }

//        [FunctionName("DeleteBlobsActivity")]
//        public async Task Run(
//            [ActivityTrigger] object input,
//            ILogger log)
//        {
//            //var container = BlobStorageService.GetContainerClient();
//            //var names = new List<string>();

//            //await foreach (var blob in container.GetBlobsAsync())
//            //    names.Add(blob.Name);

//            //return names;
//        }
//    }
//}