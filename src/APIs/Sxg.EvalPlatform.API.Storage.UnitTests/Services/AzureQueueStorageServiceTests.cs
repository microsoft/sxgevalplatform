using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using FluentAssertions;
using Sxg.EvalPlatform.API.Storage.Services;

namespace Sxg.EvalPlatform.API.Storage.UnitTests.Services
{
    /// <summary>
    /// Unit tests for AzureQueueStorageService
    /// Note: These tests focus on parameter validation and error handling.
    /// Integration tests should cover actual Azure Queue Storage operations.
    /// </summary>
    [Trait("Category", TestCategories.Unit)]
    [Trait("Category", TestCategories.QueueStorage)]
    [Trait("Category", TestCategories.Service)]
    public class AzureQueueStorageServiceTests
    {
        private readonly Mock<ILogger<AzureQueueStorageService>> _mockLogger;
        private readonly Mock<IConfigHelper> _mockConfigHelper;

        public AzureQueueStorageServiceTests()
        {
            _mockLogger = new Mock<ILogger<AzureQueueStorageService>>();
            _mockConfigHelper = new Mock<IConfigHelper>();

            SetupDefaultMocks();
        }

        private void SetupDefaultMocks()
        {
            _mockConfigHelper
                .Setup(x => x.GetAzureStorageAccountName())
                .Returns(TestConstants.Storage.AccountName);

            _mockConfigHelper
                .Setup(x => x.GetASPNetCoreEnvironment())
                .Returns(TestConstants.Config.LocalEnvironment);

            _mockConfigHelper
                .Setup(x => x.GetManagedIdentityClientId())
                .Returns((string?)null);
        }

        #region Constructor Tests

        [Fact]
        public void Constructor_WithValidConfiguration_InitializesSuccessfully()
        {
            // Arrange & Act
            var service = new AzureQueueStorageService(_mockConfigHelper.Object, _mockLogger.Object);

            // Assert
            service.Should().NotBeNull();
        }

        [Fact]
        public void Constructor_WithNullConfigHelper_ThrowsException()
        {
            // Arrange, Act & Assert
            // Note: Throws NullReferenceException because configHelper is used before null check
            Assert.Throws<NullReferenceException>(() => 
                new AzureQueueStorageService(null!, _mockLogger.Object));
        }

        [Fact]
        public void Constructor_WithNullLogger_ThrowsArgumentNullException()
        {
            // Arrange & Act & Assert
            // Note: Logger is used in LogInformation before it can be validated, causing ArgumentNullException
            Assert.Throws<ArgumentNullException>(() =>
                new AzureQueueStorageService(_mockConfigHelper.Object, null!));
        }

        [Fact]
        public void Constructor_WithEmptyAccountName_ThrowsUriFormatException()
        {
            // Arrange
            _mockConfigHelper
                .Setup(x => x.GetAzureStorageAccountName())
                .Returns(string.Empty);

            // Act & Assert - Constructor throws UriFormatException when creating URI with empty hostname
            Assert.Throws<UriFormatException>(() => 
                new AzureQueueStorageService(_mockConfigHelper.Object, _mockLogger.Object));
        }

        #endregion

        #region SendMessageAsync Parameter Validation Tests

        [Fact]
        public async Task SendMessageAsync_WithNullQueueName_ThrowsArgumentException()
        {
            // Arrange
            var service = new AzureQueueStorageService(_mockConfigHelper.Object, _mockLogger.Object);

            // Act & Assert
            var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
                service.SendMessageAsync(null!, "test message"));
            
            exception.ParamName.Should().Be("queueName");
            exception.Message.Should().Contain("Queue name cannot be null or empty");
        }

        [Fact]
        public async Task SendMessageAsync_WithEmptyQueueName_ThrowsArgumentException()
        {
            // Arrange
            var service = new AzureQueueStorageService(_mockConfigHelper.Object, _mockLogger.Object);

            // Act & Assert
            var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
                service.SendMessageAsync(string.Empty, "test message"));
            
            exception.ParamName.Should().Be("queueName");
        }

        [Fact]
        public async Task SendMessageAsync_WithNullMessageContent_ThrowsArgumentException()
        {
            // Arrange
            var service = new AzureQueueStorageService(_mockConfigHelper.Object, _mockLogger.Object);

            // Act & Assert
            var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
                service.SendMessageAsync(TestConstants.Storage.QueueName, null!));
            
            exception.ParamName.Should().Be("messageContent");
            exception.Message.Should().Contain("Message content cannot be null or empty");
        }

        [Fact]
        public async Task SendMessageAsync_WithEmptyMessageContent_ThrowsArgumentException()
        {
            // Arrange
            var service = new AzureQueueStorageService(_mockConfigHelper.Object, _mockLogger.Object);

            // Act & Assert
            var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
                service.SendMessageAsync(TestConstants.Storage.QueueName, string.Empty));
            
            exception.ParamName.Should().Be("messageContent");
        }

        #endregion

        #region Queue Name Normalization Tests

        // Note: These tests would require integration testing with actual Azure Queue Storage
        // to verify queue name normalization behavior. The service should handle:
        // - Converting queue names to lowercase
        // - Creating queue if it doesn't exist
        // These behaviors are tested in integration tests

        #endregion

        #region Environment Configuration Tests

        [Theory]
        [InlineData("Local")]
        [InlineData("Development")]
        [InlineData("Staging")]
        [InlineData("Production")]
        public void Constructor_WithDifferentEnvironments_InitializesSuccessfully(string environment)
        {
            // Arrange
            _mockConfigHelper
                .Setup(x => x.GetASPNetCoreEnvironment())
                .Returns(environment);

            // Act
            var service = new AzureQueueStorageService(_mockConfigHelper.Object, _mockLogger.Object);

            // Assert
            service.Should().NotBeNull();
        }

        [Fact]
        public void Constructor_WithManagedIdentityClientId_InitializesSuccessfully()
        {
            // Arrange
            _mockConfigHelper
                .Setup(x => x.GetManagedIdentityClientId())
                .Returns("test-client-id");

            // Act
            var service = new AzureQueueStorageService(_mockConfigHelper.Object, _mockLogger.Object);

            // Assert
            service.Should().NotBeNull();
        }

        #endregion

        #region Error Handling Tests

        [Fact]
        public async Task SendMessageAsync_WithBothParametersNull_ThrowsArgumentException()
        {
            // Arrange
            var service = new AzureQueueStorageService(_mockConfigHelper.Object, _mockLogger.Object);

            // Act & Assert
            var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
                service.SendMessageAsync(null!, null!));
            
            // Either parameter validation should fail
            exception.Should().NotBeNull();
        }

        #endregion

        #region Logging Tests

        [Fact]
        public void Constructor_WhenCalled_LogsInitialization()
        {
            // Arrange & Act
            var service = new AzureQueueStorageService(_mockConfigHelper.Object, _mockLogger.Object);

            // Assert
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("initialized")),
                    It.IsAny<Exception>(),
                    It.Is<Func<It.IsAnyType, Exception?, string>>((v, t) => true)),
                Times.Once);
        }

        #endregion

        #region Integration Scenarios Documentation

        // The following scenarios should be covered in integration tests:
        // 1. SendMessageAsync with valid parameters successfully sends message
        // 2. SendMessageAsync creates queue if it doesn't exist
        // 3. SendMessageAsync handles Azure Storage exceptions appropriately
        // 4. SendMessageAsync correctly uses managed identity for authentication
        // 5. Queue name is converted to lowercase
        // 6. Large messages are handled correctly
        // 7. Special characters in messages are preserved
        // 8. Concurrent message sends are handled correctly

        #endregion
    }
}
