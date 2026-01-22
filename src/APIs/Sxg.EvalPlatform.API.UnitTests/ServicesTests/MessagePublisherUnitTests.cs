using Azure;
using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using SxgEvalPlatformApi.Services;

namespace Sxg.EvalPlatform.API.UnitTests.ServicesTests
{
    /// <summary>
    /// Comprehensive unit tests for MessagePublisher.
    /// Tests Service Bus queue operations and message sending functionality.
    /// </summary>
    public class MessagePublisherUnitTests
    {
        private readonly Mock<ServiceBusClient> _mockClient;
        private readonly Mock<ServiceBusAdministrationClient> _mockAdminClient;
        private readonly Mock<ILogger<MessagePublisher>> _mockLogger;
        private readonly Mock<ServiceBusSender> _mockSender;

        public MessagePublisherUnitTests()
        {
            _mockClient = new Mock<ServiceBusClient>();
            _mockAdminClient = new Mock<ServiceBusAdministrationClient>();
            _mockLogger = new Mock<ILogger<MessagePublisher>>();
            _mockSender = new Mock<ServiceBusSender>();
        }

        #region Constructor Tests

        [Fact]
        public void Constructor_WithValidParameters_CreatesInstance()
        {
            // Arrange & Act
            var publisher = new MessagePublisher(
                _mockClient.Object,
                _mockAdminClient.Object,
                _mockLogger.Object);

            // Assert
            publisher.Should().NotBeNull();
            publisher.Should().BeAssignableTo<IMessagePublisher>();
        }

        [Fact]
        public void Constructor_LogsInitialization()
        {
            // Arrange
            _mockClient.Setup(x => x.FullyQualifiedNamespace).Returns("test-namespace.servicebus.windows.net");

            // Act
            var publisher = new MessagePublisher(
                _mockClient.Object,
                _mockAdminClient.Object,
                _mockLogger.Object);

            // Assert
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("MessagePublisher initialized")),
                    null,
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        #endregion

        #region SendMessageAsync - Queue Exists Tests

        [Fact]
        public async Task SendMessageAsync_WhenQueueExists_SendsMessage()
        {
            // Arrange
            var queueName = "test-queue";
            var message = "test message";

            _mockAdminClient.Setup(x => x.QueueExistsAsync(queueName, default))
                .ReturnsAsync(Response.FromValue(true, null!));
            
            _mockClient.Setup(x => x.CreateSender(queueName))
                .Returns(_mockSender.Object);
            
            _mockSender.Setup(x => x.SendMessageAsync(It.IsAny<ServiceBusMessage>(), default))
                .Returns(Task.CompletedTask);
            
            _mockSender.Setup(x => x.DisposeAsync())
                .Returns(ValueTask.CompletedTask);

            var publisher = new MessagePublisher(
                _mockClient.Object,
                _mockAdminClient.Object,
                _mockLogger.Object);

            // Act
            await publisher.SendMessageAsync(queueName, message);

            // Assert
            _mockSender.Verify(
                x => x.SendMessageAsync(It.Is<ServiceBusMessage>(m => m.Body.ToString() == message), default),
                Times.Once);
        }

        [Fact]
        public async Task SendMessageAsync_WhenQueueExists_LogsSuccess()
        {
            // Arrange
            var queueName = "existing-queue";
            var message = "test message";

            _mockAdminClient.Setup(x => x.QueueExistsAsync(queueName, default))
                .ReturnsAsync(Response.FromValue(true, null!));
            
            _mockClient.Setup(x => x.CreateSender(queueName))
                .Returns(_mockSender.Object);
            
            _mockSender.Setup(x => x.SendMessageAsync(It.IsAny<ServiceBusMessage>(), default))
                .Returns(Task.CompletedTask);
            
            _mockSender.Setup(x => x.DisposeAsync())
                .Returns(ValueTask.CompletedTask);

            var publisher = new MessagePublisher(
                _mockClient.Object,
                _mockAdminClient.Object,
                _mockLogger.Object);

            // Act
            await publisher.SendMessageAsync(queueName, message);

            // Assert
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Successfully sent message")),
                    null,
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task SendMessageAsync_WhenQueueExists_DisposeSender()
        {
            // Arrange
            var queueName = "test-queue";
            var message = "test message";

            _mockAdminClient.Setup(x => x.QueueExistsAsync(queueName, default))
                .ReturnsAsync(Response.FromValue(true, null!));
            
            _mockClient.Setup(x => x.CreateSender(queueName))
                .Returns(_mockSender.Object);
            
            _mockSender.Setup(x => x.SendMessageAsync(It.IsAny<ServiceBusMessage>(), default))
                .Returns(Task.CompletedTask);
            
            _mockSender.Setup(x => x.DisposeAsync())
                .Returns(ValueTask.CompletedTask);

            var publisher = new MessagePublisher(
                _mockClient.Object,
                _mockAdminClient.Object,
                _mockLogger.Object);

            // Act
            await publisher.SendMessageAsync(queueName, message);

            // Assert
            _mockSender.Verify(x => x.DisposeAsync(), Times.Once);
        }

        #endregion

        #region SendMessageAsync - Queue Does Not Exist Tests

        [Fact]
        public async Task SendMessageAsync_WhenQueueDoesNotExist_CreatesQueue()
        {
            // Arrange
            var queueName = "new-queue";
            var message = "test message";

            _mockAdminClient.Setup(x => x.QueueExistsAsync(queueName, default))
                .ReturnsAsync(Response.FromValue(false, null!));
            
            _mockAdminClient.Setup(x => x.CreateQueueAsync(queueName, default))
                .ReturnsAsync(Mock.Of<Response<QueueProperties>>());
            
            _mockClient.Setup(x => x.CreateSender(queueName))
                .Returns(_mockSender.Object);
            
            _mockSender.Setup(x => x.SendMessageAsync(It.IsAny<ServiceBusMessage>(), default))
                .Returns(Task.CompletedTask);
            
            _mockSender.Setup(x => x.DisposeAsync())
                .Returns(ValueTask.CompletedTask);

            var publisher = new MessagePublisher(
                _mockClient.Object,
                _mockAdminClient.Object,
                _mockLogger.Object);

            // Act
            await publisher.SendMessageAsync(queueName, message);

            // Assert
            _mockAdminClient.Verify(x => x.CreateQueueAsync(queueName, default), Times.Once);
        }

        [Fact]
        public async Task SendMessageAsync_WhenQueueDoesNotExist_LogsQueueCreation()
        {
            // Arrange
            var queueName = "new-queue";
            var message = "test message";

            _mockAdminClient.Setup(x => x.QueueExistsAsync(queueName, default))
                .ReturnsAsync(Response.FromValue(false, null!));
            
            _mockAdminClient.Setup(x => x.CreateQueueAsync(queueName, default))
                .ReturnsAsync(Mock.Of<Response<QueueProperties>>());
            
            _mockClient.Setup(x => x.CreateSender(queueName))
                .Returns(_mockSender.Object);
            
            _mockSender.Setup(x => x.SendMessageAsync(It.IsAny<ServiceBusMessage>(), default))
                .Returns(Task.CompletedTask);
            
            _mockSender.Setup(x => x.DisposeAsync())
                .Returns(ValueTask.CompletedTask);

            var publisher = new MessagePublisher(
                _mockClient.Object,
                _mockAdminClient.Object,
                _mockLogger.Object);

            // Act
            await publisher.SendMessageAsync(queueName, message);

            // Assert
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("does not exist. Creating queue")),
                    null,
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
            
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Successfully created queue")),
                    null,
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task SendMessageAsync_AfterCreatingQueue_SendsMessage()
        {
            // Arrange
            var queueName = "new-queue";
            var message = "test message";

            _mockAdminClient.Setup(x => x.QueueExistsAsync(queueName, default))
                .ReturnsAsync(Response.FromValue(false, null!));
            
            _mockAdminClient.Setup(x => x.CreateQueueAsync(queueName, default))
                .ReturnsAsync(Mock.Of<Response<QueueProperties>>());
            
            _mockClient.Setup(x => x.CreateSender(queueName))
                .Returns(_mockSender.Object);
            
            _mockSender.Setup(x => x.SendMessageAsync(It.IsAny<ServiceBusMessage>(), default))
                .Returns(Task.CompletedTask);
            
            _mockSender.Setup(x => x.DisposeAsync())
                .Returns(ValueTask.CompletedTask);

            var publisher = new MessagePublisher(
                _mockClient.Object,
                _mockAdminClient.Object,
                _mockLogger.Object);

            // Act
            await publisher.SendMessageAsync(queueName, message);

            // Assert
            _mockSender.Verify(
                x => x.SendMessageAsync(It.Is<ServiceBusMessage>(m => m.Body.ToString() == message), default),
                Times.Once);
        }

        #endregion

        #region SendMessageAsync - Error Handling Tests

        [Fact]
        public async Task SendMessageAsync_WhenSendFails_LogsErrorAndThrows()
        {
            // Arrange
            var queueName = "test-queue";
            var message = "test message";
            var expectedException = new ServiceBusException("Send failed", ServiceBusFailureReason.ServiceCommunicationProblem);

            _mockAdminClient.Setup(x => x.QueueExistsAsync(queueName, default))
                .ReturnsAsync(Response.FromValue(true, null!));
            
            _mockClient.Setup(x => x.CreateSender(queueName))
                .Returns(_mockSender.Object);
            
            _mockSender.Setup(x => x.SendMessageAsync(It.IsAny<ServiceBusMessage>(), default))
                .ThrowsAsync(expectedException);
            
            _mockSender.Setup(x => x.DisposeAsync())
                .Returns(ValueTask.CompletedTask);

            var publisher = new MessagePublisher(
                _mockClient.Object,
                _mockAdminClient.Object,
                _mockLogger.Object);

            // Act & Assert
            var exception = await Assert.ThrowsAsync<ServiceBusException>(
                () => publisher.SendMessageAsync(queueName, message));

            exception.Should().Be(expectedException);
            
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Failed to send message")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task SendMessageAsync_WhenQueueCreationFails_LogsErrorAndThrows()
        {
            // Arrange
            var queueName = "test-queue";
            var message = "test message";
            var expectedException = new ServiceBusException("Queue creation failed", ServiceBusFailureReason.ServiceCommunicationProblem);

            _mockAdminClient.Setup(x => x.QueueExistsAsync(queueName, default))
                .ReturnsAsync(Response.FromValue(false, null!));
            
            _mockAdminClient.Setup(x => x.CreateQueueAsync(queueName, default))
                .ThrowsAsync(expectedException);

            var publisher = new MessagePublisher(
                _mockClient.Object,
                _mockAdminClient.Object,
                _mockLogger.Object);

            // Act & Assert
            var exception = await Assert.ThrowsAsync<ServiceBusException>(
                () => publisher.SendMessageAsync(queueName, message));

            exception.Should().Be(expectedException);
            
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Failed to send message")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task SendMessageAsync_EvenWhenSendFails_DisposeSender()
        {
            // Arrange
            var queueName = "test-queue";
            var message = "test message";

            _mockAdminClient.Setup(x => x.QueueExistsAsync(queueName, default))
                .ReturnsAsync(Response.FromValue(true, null!));
            
            _mockClient.Setup(x => x.CreateSender(queueName))
                .Returns(_mockSender.Object);
            
            _mockSender.Setup(x => x.SendMessageAsync(It.IsAny<ServiceBusMessage>(), default))
                .ThrowsAsync(new ServiceBusException("Send failed", ServiceBusFailureReason.ServiceCommunicationProblem));
            
            _mockSender.Setup(x => x.DisposeAsync())
                .Returns(ValueTask.CompletedTask);

            var publisher = new MessagePublisher(
                _mockClient.Object,
                _mockAdminClient.Object,
                _mockLogger.Object);

            // Act & Assert
            await Assert.ThrowsAsync<ServiceBusException>(
                () => publisher.SendMessageAsync(queueName, message));

            _mockSender.Verify(x => x.DisposeAsync(), Times.Once);
        }

        #endregion

        #region SendMessageAsync - Various Messages Tests

        [Theory]
        [InlineData("Simple message")]
        [InlineData("")]
        [InlineData("{\"json\": \"message\"}")]
        [InlineData("Message with special characters: ���!@#$%")]
        public async Task SendMessageAsync_WithVariousMessages_SendsSuccessfully(string message)
        {
            // Arrange
            var queueName = "test-queue";

            _mockAdminClient.Setup(x => x.QueueExistsAsync(queueName, default))
                .ReturnsAsync(Response.FromValue(true, null!));
            
            _mockClient.Setup(x => x.CreateSender(queueName))
                .Returns(_mockSender.Object);
            
            _mockSender.Setup(x => x.SendMessageAsync(It.IsAny<ServiceBusMessage>(), default))
                .Returns(Task.CompletedTask);
            
            _mockSender.Setup(x => x.DisposeAsync())
                .Returns(ValueTask.CompletedTask);

            var publisher = new MessagePublisher(
                _mockClient.Object,
                _mockAdminClient.Object,
                _mockLogger.Object);

            // Act
            await publisher.SendMessageAsync(queueName, message);

            // Assert
            _mockSender.Verify(
                x => x.SendMessageAsync(It.IsAny<ServiceBusMessage>(), default),
                Times.Once);
        }

        [Theory]
        [InlineData("queue-1")]
        [InlineData("queue-2")]
        [InlineData("my-eval-queue")]
        public async Task SendMessageAsync_WithDifferentQueues_SendsToCorrectQueue(string queueName)
        {
            // Arrange
            var message = "test message";

            _mockAdminClient.Setup(x => x.QueueExistsAsync(queueName, default))
                .ReturnsAsync(Response.FromValue(true, null!));
            
            _mockClient.Setup(x => x.CreateSender(queueName))
                .Returns(_mockSender.Object);
            
            _mockSender.Setup(x => x.SendMessageAsync(It.IsAny<ServiceBusMessage>(), default))
                .Returns(Task.CompletedTask);
            
            _mockSender.Setup(x => x.DisposeAsync())
                .Returns(ValueTask.CompletedTask);

            var publisher = new MessagePublisher(
                _mockClient.Object,
                _mockAdminClient.Object,
                _mockLogger.Object);

            // Act
            await publisher.SendMessageAsync(queueName, message);

            // Assert
            _mockClient.Verify(x => x.CreateSender(queueName), Times.Once);
        }

        #endregion

        #region Integration Tests

        [Fact]
        public async Task SendMessageAsync_MultipleCalls_AllSucceed()
        {
            // Arrange
            var queueName = "test-queue";
            var messages = new[] { "Message1", "Message2", "Message3" };

            _mockAdminClient.Setup(x => x.QueueExistsAsync(queueName, default))
                .ReturnsAsync(Response.FromValue(true, null!));
            
            _mockClient.Setup(x => x.CreateSender(queueName))
                .Returns(_mockSender.Object);
            
            _mockSender.Setup(x => x.SendMessageAsync(It.IsAny<ServiceBusMessage>(), default))
                .Returns(Task.CompletedTask);
            
            _mockSender.Setup(x => x.DisposeAsync())
                .Returns(ValueTask.CompletedTask);

            var publisher = new MessagePublisher(
                _mockClient.Object,
                _mockAdminClient.Object,
                _mockLogger.Object);

            // Act
            foreach (var message in messages)
            {
                await publisher.SendMessageAsync(queueName, message);
            }

            // Assert
            _mockSender.Verify(
                x => x.SendMessageAsync(It.IsAny<ServiceBusMessage>(), default),
                Times.Exactly(messages.Length));
        }

        #endregion
    }
}
