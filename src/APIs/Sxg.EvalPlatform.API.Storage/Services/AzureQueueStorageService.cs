using Azure.Core;
using Azure.Storage.Queues;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SXG.EvalPlatform.Common;


namespace Sxg.EvalPlatform.API.Storage.Services
{
    /// <summary>
    /// Service for Azure Queue Storage operations
    /// </summary>
    public class AzureQueueStorageService : IAzureQueueStorageService
    {
        private readonly QueueServiceClient _queueServiceClient;
        private readonly ILogger<AzureQueueStorageService> _logger;

        public AzureQueueStorageService(IConfigHelper configHelper, IConfiguration configuration, ILogger<AzureQueueStorageService> logger)
        {
            _logger = logger;

            var accountName = configHelper.GetAzureStorageAccountName();

            var queueUri = $"https://{accountName}.queue.core.windows.net";

            var environment = configuration.GetValue<string>("ASPNETCORE_ENVIRONMENT") ?? "Production";
            TokenCredential credential = CommonUtils.GetTokenCredential(environment);

            // Use managed identity for authentication
            _queueServiceClient = new QueueServiceClient(new Uri(queueUri), credential);
            _logger.LogInformation("Azure Queue Storage service initialized with managed identity for account: {AccountName}", accountName);
        }

        /// <inheritdoc />
        public async Task<bool> SendMessageAsync(string queueName, string messageContent)
        {
            try
            {
                _logger.LogInformation("Sending message to queue: {QueueName}", queueName);

                if (string.IsNullOrEmpty(queueName))
                {
                    throw new ArgumentException("Queue name cannot be null or empty", nameof(queueName));
                }
                
                if (string.IsNullOrEmpty(messageContent))
                {
                    throw new ArgumentException("Message content cannot be null or empty", nameof(messageContent));
                }

                var queueClient = _queueServiceClient.GetQueueClient(queueName.ToLower());

                // Create queue if it doesn't exist
                await queueClient.CreateIfNotExistsAsync();

                // Send message to queue
                await queueClient.SendMessageAsync(messageContent);

                _logger.LogInformation("Successfully sent message to queue: {QueueName}", queueName);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send message to queue: {QueueName}", queueName);
                throw;
            }
        }
    }
}
