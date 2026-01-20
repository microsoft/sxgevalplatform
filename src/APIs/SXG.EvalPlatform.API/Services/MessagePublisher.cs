using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;
using Microsoft.Extensions.Logging;

namespace SxgEvalPlatformApi.Services
{
    public class MessagePublisher : IMessagePublisher
    {
        private readonly ServiceBusClient _client;
        private readonly ServiceBusAdministrationClient _adminClient;
        private readonly ILogger<MessagePublisher> _logger;

        public MessagePublisher(
            ServiceBusClient client, 
            ServiceBusAdministrationClient adminClient,
            ILogger<MessagePublisher> logger)
        {
            _client = client;
            _adminClient = adminClient;
            _logger = logger;
            
            _logger.LogInformation("MessagePublisher initialized with Service Bus namespace: {Namespace}", 
                _client.FullyQualifiedNamespace);
        }

        public async Task SendMessageAsync(string queueName, string message)
        {
            try
            {
                // Ensure queue exists before sending
                if (!await _adminClient.QueueExistsAsync(queueName))
                {
                    _logger.LogInformation("Queue {QueueName} does not exist. Creating queue...", queueName);
                    await _adminClient.CreateQueueAsync(queueName);
                    _logger.LogInformation("Successfully created queue: {QueueName}", queueName);
                }

                var sender = _client.CreateSender(queueName);
                try
                {
                    await sender.SendMessageAsync(new ServiceBusMessage(message));
                    _logger.LogInformation("Successfully sent message to queue: {QueueName}", queueName);
                }
                finally
                {
                    await sender.DisposeAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send message to queue: {QueueName}", queueName);
                throw;
            }
        }
    }
}
