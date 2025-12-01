using Azure.Core;
using Azure.Storage.Queues;
using Sxg.EvalPlatform.API.Storage;
using SXG.EvalPlatform.Common;
using SxgEvalPlatformApi.Models;
using System.Text.Json;

namespace Sxg.EvalPlatform.API.IntegrationTests
{
    public class EvalRunDebugger
    {
        [Fact] 
        public async Task ReTriggerEvalRunAsync()
        {
            await SendEvalProcessingRequestAsync(new Guid("f16e0d4d-74d3-49b6-afcb-86bea32995b8"), "d47b89bc-024d-42e2-9b84-7db4fc225e64"); 
        }

        private async Task SendEvalProcessingRequestAsync(Guid evalRunId, string metricsConfigurationId)
        {
            var processingRequest = new EvalProcessingRequest
            {
                EvalRunId = evalRunId,
                MetricsConfigurationId = metricsConfigurationId,
                RequestedAt = DateTime.UtcNow,
                Priority = "Normal",
            };

            var messageContent = JsonSerializer.Serialize(processingRequest, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            var queueName = "eval-processing-requests";
            var accountName = "sxgagentevaldev";
                        
            var queueUri = $"https://{accountName}.queue.core.windows.net";

            
            TokenCredential credential = CommonUtils.GetTokenCredential("Local");

            // Use managed identity for authentication
            var queueServiceClient = new QueueServiceClient(new Uri(queueUri), credential);
            //var success = await queueServiceClient.SendMessageAsync(queueName, messageContent);

            var queueClient = queueServiceClient.GetQueueClient(queueName.ToLower());

            // Create queue if it doesn't exist
            await queueClient.CreateIfNotExistsAsync();

            // Send message to queue
            await queueClient.SendMessageAsync(messageContent);
        }
    }
}
