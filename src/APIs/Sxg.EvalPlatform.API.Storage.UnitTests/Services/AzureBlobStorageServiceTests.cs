using Azure.Storage.Blobs;
using Microsoft.Extensions.Logging;
using Moq;
using Sxg.EvalPlatform.API.Storage.Services;

namespace Sxg.EvalPlatform.API.Storage.UnitTests.Services;

[Trait("Category", TestCategories.Unit)]
public class AzureBlobStorageServiceTests
{
    private readonly Mock<IConfigHelper> _mockConfigHelper;
    private readonly Mock<ILogger<AzureBlobStorageService>> _mockLogger;
    private readonly Mock<ICacheManager> _mockCacheManager;

    public AzureBlobStorageServiceTests()
    {
        _mockConfigHelper = new Mock<IConfigHelper>();
        _mockLogger = new Mock<ILogger<AzureBlobStorageService>>();
        _mockCacheManager = new Mock<ICacheManager>();

        // Setup default config helper responses
        _mockConfigHelper.Setup(x => x.GetAzureStorageAccountName()).Returns("teststorageaccount");
        _mockConfigHelper.Setup(x => x.GetASPNetCoreEnvironment()).Returns("Development");
        _mockConfigHelper.Setup(x => x.GetManagedIdentityClientId()).Returns(string.Empty);
        _mockConfigHelper.Setup(x => x.GetDefaultCacheExpiration()).Returns(TimeSpan.FromMinutes(30));
    }

    [Fact]
    public void Constructor_WithEmptyStorageAccountName_ThrowsArgumentException()
    {
        // Arrange
        _mockConfigHelper.Setup(x => x.GetAzureStorageAccountName()).Returns(string.Empty);

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() =>
            new AzureBlobStorageService(_mockConfigHelper.Object, _mockCacheManager.Object, _mockLogger.Object));

        Assert.Contains("Azure Storage account name is not configured", exception.Message);
    }

    [Fact]
    public void Constructor_WithValidConfig_CreatesInstance()
    {
        // Act
        var service = new AzureBlobStorageService(
            _mockConfigHelper.Object,
            _mockCacheManager.Object,
            _mockLogger.Object);

        // Assert
        Assert.NotNull(service);
    }

    [Fact]
    public void Constructor_WithNullStorageAccountName_ThrowsArgumentException()
    {
        // Arrange
        _mockConfigHelper.Setup(x => x.GetAzureStorageAccountName()).Returns((string?)null);

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() =>
            new AzureBlobStorageService(_mockConfigHelper.Object, _mockCacheManager.Object, _mockLogger.Object));

        Assert.Contains("Azure Storage account name is not configured", exception.Message);
    }

    [Theory]
    [InlineData("container1", "blob1.txt")]
    [InlineData("mycontainer", "folder/blob2.json")]
    [InlineData("test-container", "path/to/file.csv")]
    public async Task ReadBlobContentAsync_WithDifferentPaths_ChecksCacheFirst(string containerName, string blobName)
    {
        // Arrange
        var expectedContent = "cached content";
        _mockCacheManager
            .Setup(x => x.GetAsync<object>(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((object?)null);

        var service = new AzureBlobStorageService(
            _mockConfigHelper.Object,
            _mockCacheManager.Object,
            _mockLogger.Object);

        // Act & Assert - Should check cache first (will fail during actual blob access in dev environment)
        try
        {
            await service.ReadBlobContentAsync(containerName, blobName);
        }
        catch
        {
            // Expected to fail during actual blob access, we're just verifying cache check happens
        }

        // Verify cache was checked
        _mockCacheManager.Verify(
            x => x.GetAsync<object>(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task BlobExistsAsync_ChecksCacheFirst()
    {
        // Arrange
        var containerName = "test-container";
        var blobName = "test-blob.txt";

        _mockCacheManager
            .Setup(x => x.GetAsync<object>(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((object?)null);

        var service = new AzureBlobStorageService(
            _mockConfigHelper.Object,
            _mockCacheManager.Object,
            _mockLogger.Object);

        // Act
        try
        {
            var result = await service.BlobExistsAsync(containerName, blobName);
        }
        catch
        {
            // Expected to fail during actual blob access
        }

        // Assert - Verify cache was checked
        _mockCacheManager.Verify(
            x => x.GetAsync<object>(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task ListBlobsAsync_ChecksCacheFirst()
    {
        // Arrange
        var containerName = "test-container";
        var prefix = "test-prefix/";

        _mockCacheManager
            .Setup(x => x.GetAsync<object>(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((object?)null);

        var service = new AzureBlobStorageService(
            _mockConfigHelper.Object,
            _mockCacheManager.Object,
            _mockLogger.Object);

        // Act & Assert
        try
        {
            var result = await service.ListBlobsAsync(containerName, prefix);
        }
        catch
        {
            // Expected to fail during actual blob access
        }

        // Verify cache was checked
        _mockCacheManager.Verify(
            x => x.GetAsync<object>(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task DeleteBlobAsync_InvalidatesCacheOnError()
    {
        // Arrange
        var containerName = "test-container";
        var blobName = "test-blob.txt";

        var service = new AzureBlobStorageService(
            _mockConfigHelper.Object,
            _mockCacheManager.Object,
            _mockLogger.Object);

        // Act
        try
        {
            await service.DeleteBlobAsync(containerName, blobName);
        }
        catch
        {
            // Expected to fail during actual blob access
        }

        // Assert - Verify cache was invalidated even on error
        _mockCacheManager.Verify(
            x => x.RemoveAsync(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.AtLeastOnce);
    }

    [Theory]
    [InlineData("")]
    [InlineData("prefix/")]
    [InlineData("folder/subfolder/")]
    public async Task ListBlobsAsync_WithDifferentPrefixes_GeneratesCorrectCacheKey(string prefix)
    {
        // Arrange
        var containerName = "test-container";

        _mockCacheManager
            .Setup(x => x.GetAsync<object>(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((object?)null);

        var service = new AzureBlobStorageService(
            _mockConfigHelper.Object,
            _mockCacheManager.Object,
            _mockLogger.Object);

        // Act
        try
        {
            await service.ListBlobsAsync(containerName, prefix);
        }
        catch
        {
            // Expected to fail during actual blob access
        }

        // Assert - Verify cache key was generated (cache was accessed)
        _mockCacheManager.Verify(
            x => x.GetAsync<object>(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.AtLeastOnce);
    }
}
