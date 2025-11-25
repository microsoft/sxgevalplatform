namespace SxgEvalPlatformApi.Services
{
    public interface IMessagePublisher
    {
        /// <summary>
        /// Sends a message to the specified Service Bus queue.
        /// </summary>
        /// <param name="queueName">The name of the queue to send the message to.</param>
        /// <param name="message">The message content.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task SendMessageAsync(string queueName, string message);
    }
}
