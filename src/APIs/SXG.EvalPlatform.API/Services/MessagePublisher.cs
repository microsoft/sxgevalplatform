using Azure.Messaging.ServiceBus;

namespace SxgEvalPlatformApi.Services
{
    public class MessagePublisher : IMessagePublisher
    {
        private readonly ServiceBusClient _client;

        public MessagePublisher(ServiceBusClient client)
        {
            _client = client;
        }

        public async Task SendMessageAsync(string queueName, string message)
        {
            var sender = _client.CreateSender(queueName);
            await sender.SendMessageAsync(new ServiceBusMessage(message));
        }
    }
}
