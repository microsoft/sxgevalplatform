using Microsoft.Extensions.Logging;
using Moq;
using Sxg.EvalPlatform.API.Storage.Services;
using Sxg.EvalPlatform.API.Storage.TableEntities;
using Azure;

namespace Sxg.EvalPlatform.API.Storage.UnitTests.Services;

[Trait("Category", TestCategories.Unit)]
public class DataSetTableServiceTests
{
    private readonly Mock<IConfigHelper> _mockConfigHelper;
    private readonly Mock<ILogger<DataSetTableService>> _mockLogger;
    private readonly Mock<ICacheManager> _mockCacheManager;

    public DataSetTableServiceTests()
    {
        _mockConfigHelper = new Mock<IConfigHelper>();
        _mockLogger = new Mock<ILogger<DataSetTableService>>();
        _mockCacheManager = new Mock<ICacheManager>();

        // Setup default config helper responses
        _mockConfigHelper.Setup(x => x.GetAzureStorageAccountName()).Returns("teststorageaccount");
        _mockConfigHelper.Setup(x => x.GetDataSetsTable()).Returns("DataSets");
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
            new DataSetTableService(_mockConfigHelper.Object, _mockLogger.Object, _mockCacheManager.Object));

        Assert.Contains("Azure Storage account name is not configured", exception.Message);
    }

    [Fact]
    public void Constructor_WithValidConfig_CreatesInstance()
    {
        // Act
        var service = new DataSetTableService(
            _mockConfigHelper.Object,
            _mockLogger.Object,
            _mockCacheManager.Object);

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
            new DataSetTableService(_mockConfigHelper.Object, _mockLogger.Object, _mockCacheManager.Object));

        Assert.Contains("Azure Storage account name is not configured", exception.Message);
    }

    [Fact]
    public async Task GetDataSetAsync_WithCachedData_ReturnsCachedValue()
    {
        // Arrange
        var agentId = "test-agent";
        var datasetId = "test-dataset-id";
        var expectedEntity = new DataSetTableEntity
        {
            AgentId = agentId,
            DatasetId = datasetId,
            DatasetName = "TestDataset",
            DatasetType = "Training"
        };

        _mockCacheManager
            .Setup(x => x.GetAsync<DataSetTableEntity>(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedEntity);

        var service = new DataSetTableService(
            _mockConfigHelper.Object,
            _mockLogger.Object,
            _mockCacheManager.Object);

        // Act
        var result = await service.GetDataSetAsync(agentId, datasetId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(agentId, result.AgentId);
        Assert.Equal(datasetId, result.DatasetId);

        // Verify cache was checked
        _mockCacheManager.Verify(
            x => x.GetAsync<DataSetTableEntity>(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetDataSetByIdAsync_WithCachedData_ReturnsCachedValue()
    {
        // Arrange
        var datasetId = "test-dataset-id";
        var expectedEntity = new DataSetTableEntity
        {
            AgentId = "test-agent",
            DatasetId = datasetId,
            DatasetName = "TestDataset",
            DatasetType = "Training"
        };

        _mockCacheManager
            .Setup(x => x.GetAsync<DataSetTableEntity>(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedEntity);

        var service = new DataSetTableService(
            _mockConfigHelper.Object,
            _mockLogger.Object,
            _mockCacheManager.Object);

        // Act
        var result = await service.GetDataSetByIdAsync(datasetId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(datasetId, result.DatasetId);

        // Verify cache was checked
        _mockCacheManager.Verify(
            x => x.GetAsync<DataSetTableEntity>(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetAllDataSetsByAgentIdAsync_WithCachedData_ReturnsCachedList()
    {
        // Arrange
        var agentId = "test-agent";
        var expectedList = new List<DataSetTableEntity>
        {
            new DataSetTableEntity
            {
                AgentId = agentId,
                DatasetId = "dataset-1",
                DatasetName = "Dataset1",
                DatasetType = "Training"
            },
            new DataSetTableEntity
            {
                AgentId = agentId,
                DatasetId = "dataset-2",
                DatasetName = "Dataset2",
                DatasetType = "Test"
            }
        };

        _mockCacheManager
            .Setup(x => x.GetAsync<List<DataSetTableEntity>>(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedList);

        var service = new DataSetTableService(
            _mockConfigHelper.Object,
            _mockLogger.Object,
            _mockCacheManager.Object);

        // Act
        var result = await service.GetAllDataSetsByAgentIdAsync(agentId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.Count);
        Assert.All(result, item => Assert.Equal(agentId, item.AgentId));

        // Verify cache was checked
        _mockCacheManager.Verify(
            x => x.GetAsync<List<DataSetTableEntity>>(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetAllDataSetsByAgentIdAndTypeAsync_WithCachedData_ReturnsCachedList()
    {
        // Arrange
        var agentId = "test-agent";
        var datasetType = "Training";
        var expectedList = new List<DataSetTableEntity>
        {
            new DataSetTableEntity
            {
                AgentId = agentId,
                DatasetId = "dataset-1",
                DatasetName = "Dataset1",
                DatasetType = datasetType
            }
        };

        _mockCacheManager
            .Setup(x => x.GetAsync<List<DataSetTableEntity>>(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedList);

        var service = new DataSetTableService(
            _mockConfigHelper.Object,
            _mockLogger.Object,
            _mockCacheManager.Object);

        // Act
        var result = await service.GetAllDataSetsByAgentIdAndTypeAsync(agentId, datasetType);

        // Assert
        Assert.NotNull(result);
        Assert.Single(result);
        Assert.Equal(datasetType, result[0].DatasetType);
    }

    [Fact]
    public async Task GetDataSetsByDatasetNameAsync_WithCachedData_ReturnsCachedList()
    {
        // Arrange
        var agentId = "test-agent";
        var datasetName = "TestDataset";
        var expectedList = new List<DataSetTableEntity>
        {
            new DataSetTableEntity
            {
                AgentId = agentId,
                DatasetId = "dataset-1",
                DatasetName = datasetName,
                DatasetType = "Training"
            }
        };

        _mockCacheManager
            .Setup(x => x.GetAsync<List<DataSetTableEntity>>(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedList);

        var service = new DataSetTableService(
            _mockConfigHelper.Object,
            _mockLogger.Object,
            _mockCacheManager.Object);

        // Act
        var result = await service.GetDataSetsByDatasetNameAsync(agentId, datasetName);

        // Assert
        Assert.NotNull(result);
        Assert.Single(result);
        Assert.Equal(datasetName, result[0].DatasetName);
    }

    [Fact]
    public async Task DataSetExistsAsync_WithExistingDataset_ReturnsTrue()
    {
        // Arrange
        var agentId = "test-agent";
        var datasetId = "test-dataset-id";
        var expectedEntity = new DataSetTableEntity
        {
            AgentId = agentId,
            DatasetId = datasetId
        };

        _mockCacheManager
            .Setup(x => x.GetAsync<DataSetTableEntity>(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedEntity);

        var service = new DataSetTableService(
            _mockConfigHelper.Object,
            _mockLogger.Object,
            _mockCacheManager.Object);

        // Act
        var result = await service.DataSetExistsAsync(agentId, datasetId);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task GetDataSetByDatasetNameAndTypeAsync_WithCachedData_ReturnsCachedValue()
    {
        // Arrange
        var agentId = "test-agent";
        var datasetName = "TestDataset";
        var datasetType = "Training";
        var expectedEntity = new DataSetTableEntity
        {
            AgentId = agentId,
            DatasetId = "dataset-1",
            DatasetName = datasetName,
            DatasetType = datasetType
        };

        _mockCacheManager
            .Setup(x => x.GetAsync<DataSetTableEntity>(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedEntity);

        var service = new DataSetTableService(
            _mockConfigHelper.Object,
            _mockLogger.Object,
            _mockCacheManager.Object);

        // Act
        var result = await service.GetDataSetByDatasetNameAndTypeAsync(agentId, datasetName, datasetType);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(datasetName, result.DatasetName);
        Assert.Equal(datasetType, result.DatasetType);
    }

    #region Cache Miss and Error Handling Tests

    [Fact]
    public async Task GetDataSetAsync_WhenCacheThrowsException_PropagatesException()
    {
        // Arrange
        var agentId = "test-agent";
        var datasetId = "test-dataset-id";

        _mockCacheManager
            .Setup(x => x.GetAsync<DataSetTableEntity>(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Cache error"));

        var service = new DataSetTableService(
            _mockConfigHelper.Object,
            _mockLogger.Object,
            _mockCacheManager.Object);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await service.GetDataSetAsync(agentId, datasetId));
    }

    #endregion

    #region Cache Key Tests

    [Fact]
    public async Task GetDataSetAsync_UsesCorrectedCacheKey()
    {
        // Arrange
        var agentId = "test-agent";
        var datasetId = "test-dataset-id";
        var expectedCacheKey = $"DATASET:{agentId}:{datasetId}";
        string? actualCacheKey = null;

        var expectedEntity = new DataSetTableEntity
        {
            AgentId = agentId,
            DatasetId = datasetId
        };

        _mockCacheManager
            .Setup(x => x.GetAsync<DataSetTableEntity>(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, CancellationToken>((key, _) => actualCacheKey = key)
            .ReturnsAsync(expectedEntity);

        var service = new DataSetTableService(
            _mockConfigHelper.Object,
            _mockLogger.Object,
            _mockCacheManager.Object);

        // Act
        await service.GetDataSetAsync(agentId, datasetId);

        // Assert
        Assert.Equal(expectedCacheKey, actualCacheKey);
    }

    [Fact]
    public async Task GetDataSetByIdAsync_UsesCorrectCacheKey()
    {
        // Arrange
        var datasetId = "test-dataset-id";
        var expectedCacheKey = $"DATASET_ID:{datasetId}";
        string? actualCacheKey = null;

        var expectedEntity = new DataSetTableEntity
        {
            DatasetId = datasetId
        };

        _mockCacheManager
            .Setup(x => x.GetAsync<DataSetTableEntity>(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, CancellationToken>((key, _) => actualCacheKey = key)
            .ReturnsAsync(expectedEntity);

        var service = new DataSetTableService(
            _mockConfigHelper.Object,
            _mockLogger.Object,
            _mockCacheManager.Object);

        // Act
        await service.GetDataSetByIdAsync(datasetId);

        // Assert
        Assert.Equal(expectedCacheKey, actualCacheKey);
    }

    [Fact]
    public async Task GetAllDataSetsByAgentIdAsync_UsesCorrectCacheKey()
    {
        // Arrange
        var agentId = "test-agent";
        var expectedCacheKey = $"DATASETS_AGENT:{agentId}";
        string? actualCacheKey = null;

        _mockCacheManager
            .Setup(x => x.GetAsync<List<DataSetTableEntity>>(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, CancellationToken>((key, _) => actualCacheKey = key)
            .ReturnsAsync(new List<DataSetTableEntity>());

        var service = new DataSetTableService(
            _mockConfigHelper.Object,
            _mockLogger.Object,
            _mockCacheManager.Object);

        // Act
        await service.GetAllDataSetsByAgentIdAsync(agentId);

        // Assert
        Assert.Equal(expectedCacheKey, actualCacheKey);
    }

    [Fact]
    public async Task GetAllDataSetsByAgentIdAndTypeAsync_UsesCorrectCacheKey()
    {
        // Arrange
        var agentId = "test-agent";
        var datasetType = "Training";
        var expectedCacheKey = $"DATASETS_AGENT_TYPE:{agentId}:{datasetType}";
        string? actualCacheKey = null;

        _mockCacheManager
            .Setup(x => x.GetAsync<List<DataSetTableEntity>>(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, CancellationToken>((key, _) => actualCacheKey = key)
            .ReturnsAsync(new List<DataSetTableEntity>());

        var service = new DataSetTableService(
            _mockConfigHelper.Object,
            _mockLogger.Object,
            _mockCacheManager.Object);

        // Act
        await service.GetAllDataSetsByAgentIdAndTypeAsync(agentId, datasetType);

        // Assert
        Assert.Equal(expectedCacheKey, actualCacheKey);
    }

    [Fact]
    public async Task GetDataSetsByDatasetNameAsync_UsesCorrectCacheKey()
    {
        // Arrange
        var agentId = "test-agent";
        var datasetName = "TestDataset";
        var expectedCacheKey = $"DATASETS_NAME:{agentId}:{datasetName}";
        string? actualCacheKey = null;

        _mockCacheManager
            .Setup(x => x.GetAsync<List<DataSetTableEntity>>(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, CancellationToken>((key, _) => actualCacheKey = key)
            .ReturnsAsync(new List<DataSetTableEntity>());

        var service = new DataSetTableService(
            _mockConfigHelper.Object,
            _mockLogger.Object,
            _mockCacheManager.Object);

        // Act
        await service.GetDataSetsByDatasetNameAsync(agentId, datasetName);

        // Assert
        Assert.Equal(expectedCacheKey, actualCacheKey);
    }

    [Fact]
    public async Task GetDataSetByDatasetNameAndTypeAsync_UsesCorrectCacheKey()
    {
        // Arrange
        var agentId = "test-agent";
        var datasetName = "TestDataset";
        var datasetType = "Training";
        var expectedCacheKey = $"DATASET_NAME_TYPE:{agentId}:{datasetName}:{datasetType}";
        string? actualCacheKey = null;

        _mockCacheManager
            .Setup(x => x.GetAsync<DataSetTableEntity>(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, CancellationToken>((key, _) => actualCacheKey = key)
            .ReturnsAsync(new DataSetTableEntity());

        var service = new DataSetTableService(
            _mockConfigHelper.Object,
            _mockLogger.Object,
            _mockCacheManager.Object);

        // Act
        await service.GetDataSetByDatasetNameAndTypeAsync(agentId, datasetName, datasetType);

        // Assert
        Assert.Equal(expectedCacheKey, actualCacheKey);
    }

    #endregion

    #region Write Operations Tests (Limited without TableClient mock)

    [Fact]
    public async Task SaveDataSetAsync_AttemptsTableClientAccess()
    {
        // Arrange
        var entity = new DataSetTableEntity
        {
            AgentId = "test-agent",
            DatasetId = "test-dataset-id",
            DatasetName = "TestDataset",
            DatasetType = "Training"
        };

        _mockCacheManager
            .Setup(x => x.RemoveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var service = new DataSetTableService(
            _mockConfigHelper.Object,
            _mockLogger.Object,
            _mockCacheManager.Object);

        // Act & Assert - Will throw when trying to access TableClient
        await Assert.ThrowsAnyAsync<Exception>(async () =>
            await service.SaveDataSetAsync(entity));

        // Note: Cache invalidation happens after TableClient access,
        // so we can't verify it in this test without mocking TableClient
    }

    [Fact]
    public async Task DeleteDataSetAsync_WithCacheMiss_AttemptsTableAccess()
    {
        // Arrange
        var agentId = "test-agent";
        var datasetId = "test-dataset-id";

        _mockCacheManager
            .Setup(x => x.GetAsync<DataSetTableEntity>(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((DataSetTableEntity?)null);

        _mockCacheManager
            .Setup(x => x.RemoveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var service = new DataSetTableService(
            _mockConfigHelper.Object,
            _mockLogger.Object,
            _mockCacheManager.Object);

        // Act & Assert
        await Assert.ThrowsAnyAsync<Exception>(async () =>
            await service.DeleteDataSetAsync(agentId, datasetId));

        // Verify GetDataSetAsync was called to fetch entity before deletion
        _mockCacheManager.Verify(
            x => x.GetAsync<DataSetTableEntity>(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task DeleteAllDataSetsByAgentIdAsync_WithEmptyList_ReturnsZero()
    {
        // Arrange
        var agentId = "test-agent";

        _mockCacheManager
            .Setup(x => x.GetAsync<List<DataSetTableEntity>>(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<DataSetTableEntity>());

        _mockCacheManager
            .Setup(x => x.RemoveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var service = new DataSetTableService(
            _mockConfigHelper.Object,
            _mockLogger.Object,
            _mockCacheManager.Object);

        // Act
        var result = await service.DeleteAllDataSetsByAgentIdAsync(agentId);

        // Assert
        Assert.Equal(0, result);
        _mockCacheManager.Verify(
            x => x.RemoveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.AtLeastOnce); // Cache invalidation should still occur
    }

    [Fact]
    public async Task DeleteAllDataSetsByAgentIdAsync_InvalidatesAgentCache()
    {
        // Arrange
        var agentId = "test-agent";
        var expectedAgentCacheKey = $"DATASETS_AGENT:{agentId}";
        var removedCacheKeys = new List<string>();

        _mockCacheManager
            .Setup(x => x.GetAsync<List<DataSetTableEntity>>(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<DataSetTableEntity>());

        _mockCacheManager
            .Setup(x => x.RemoveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, CancellationToken>((key, _) => removedCacheKeys.Add(key))
            .Returns(Task.CompletedTask);

        var service = new DataSetTableService(
            _mockConfigHelper.Object,
            _mockLogger.Object,
            _mockCacheManager.Object);

        // Act
        await service.DeleteAllDataSetsByAgentIdAsync(agentId);

        // Assert
        Assert.Contains(expectedAgentCacheKey, removedCacheKeys);
        _mockCacheManager.Verify(
            x => x.RemoveAsync(expectedAgentCacheKey, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task DeleteAllDataSetsByAgentIdAsync_WithCacheInvalidationError_ContinuesExecution()
    {
        // Arrange
        var agentId = "test-agent";

        _mockCacheManager
            .Setup(x => x.GetAsync<List<DataSetTableEntity>>(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<DataSetTableEntity>());

        _mockCacheManager
            .Setup(x => x.RemoveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Cache service unavailable"));

        var service = new DataSetTableService(
            _mockConfigHelper.Object,
            _mockLogger.Object,
            _mockCacheManager.Object);

        // Act - Should not throw, cache invalidation errors are logged but don't break execution
        var result = await service.DeleteAllDataSetsByAgentIdAsync(agentId);

        // Assert
        Assert.Equal(0, result);
        // Verify warning was logged
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Failed to invalidate")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task UpdateDataSetMetadataAsync_WithCachedEntity_AttemptsTableAccess()
    {
        // Arrange
        var agentId = "test-agent";
        var datasetId = "test-dataset-id";
        var existingEntity = new DataSetTableEntity
        {
            AgentId = agentId,
            DatasetId = datasetId,
            DatasetName = "OldName"
        };

        _mockCacheManager
            .Setup(x => x.GetAsync<DataSetTableEntity>(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingEntity);

        _mockCacheManager
            .Setup(x => x.RemoveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var service = new DataSetTableService(
            _mockConfigHelper.Object,
            _mockLogger.Object,
            _mockCacheManager.Object);

        // Act & Assert
        await Assert.ThrowsAnyAsync<Exception>(async () =>
            await service.UpdateDataSetMetadataAsync(agentId, datasetId, entity =>
            {
                entity.DatasetName = "NewName";
            }));

        // Verify GetDataSetAsync was called
        _mockCacheManager.Verify(
            x => x.GetAsync<DataSetTableEntity>(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task UpdateDataSetMetadataAsync_WithNonExistingEntity_AttemptsTableAccess()
    {
        // Arrange
        var agentId = "test-agent";
        var datasetId = "non-existing-dataset";

        _mockCacheManager
            .Setup(x => x.GetAsync<DataSetTableEntity>(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((DataSetTableEntity?)null);

        var service = new DataSetTableService(
            _mockConfigHelper.Object,
            _mockLogger.Object,
            _mockCacheManager.Object);

        // Act & Assert - Will throw trying to access TableClient for GetDataSetAsync
        await Assert.ThrowsAnyAsync<Exception>(async () =>
            await service.UpdateDataSetMetadataAsync(agentId, datasetId, entity =>
            {
                entity.DatasetName = "NewName";
            }));

        // Verify GetDataSetAsync was attempted
        _mockCacheManager.Verify(
            x => x.GetAsync<DataSetTableEntity>(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    #endregion

    #region Cache Invalidation Tests

    [Theory]
    [InlineData(null, "DataSets")]
    [InlineData("", "DataSets")]
    public void Constructor_WithInvalidStorageAccountName_ThrowsArgumentException(string? accountName, string tableName)
    {
        // Arrange
        _mockConfigHelper.Setup(x => x.GetAzureStorageAccountName()).Returns(accountName);
        _mockConfigHelper.Setup(x => x.GetDataSetsTable()).Returns(tableName);

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() =>
            new DataSetTableService(_mockConfigHelper.Object, _mockLogger.Object, _mockCacheManager.Object));

        Assert.Contains("Azure Storage account name is not configured", exception.Message);
    }

    [Fact]
    public void Constructor_WithValidConfig_DoesNotInitializeTableClientImmediately()
    {
        // Act
        var service = new DataSetTableService(
            _mockConfigHelper.Object,
            _mockLogger.Object,
            _mockCacheManager.Object);

        // Assert - No exception should be thrown during construction
        // TableClient is lazy-initialized
        Assert.NotNull(service);
    }

    #endregion

    #region InvalidateDataSetCacheAsync Tests

    [Fact]
    public async Task InvalidateAgentCacheAsync_InvalidatesAgentCache()
    {
        // Arrange
        var agentId = "test-agent";
        var removedCacheKeys = new List<string>();

        // Setup empty list to avoid TableClient access
        _mockCacheManager
            .Setup(x => x.GetAsync<List<DataSetTableEntity>>(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<DataSetTableEntity>());

        _mockCacheManager
            .Setup(x => x.RemoveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, CancellationToken>((key, _) => removedCacheKeys.Add(key))
            .Returns(Task.CompletedTask);

        var service = new DataSetTableService(
            _mockConfigHelper.Object,
            _mockLogger.Object,
            _mockCacheManager.Object);

        // Act - DeleteAllDataSetsByAgentIdAsync calls InvalidateAgentCacheAsync
        await service.DeleteAllDataSetsByAgentIdAsync(agentId);

        // Assert - Verify agent cache was invalidated
        var expectedAgentCacheKey = $"DATASETS_AGENT:{agentId}";
        Assert.Contains(expectedAgentCacheKey, removedCacheKeys);
        Assert.Single(removedCacheKeys); // Only agent cache should be invalidated with empty list
    }

    [Fact]
    public async Task InvalidateCacheAsync_WhenCacheManagerThrows_LogsWarningAndContinues()
    {
        // Arrange
        var agentId = "test-agent";

        // Setup an empty list to avoid TableClient access
        _mockCacheManager
            .Setup(x => x.GetAsync<List<DataSetTableEntity>>(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<DataSetTableEntity>());

        _mockCacheManager
            .Setup(x => x.RemoveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Cache service unavailable"));

        var service = new DataSetTableService(
            _mockConfigHelper.Object,
            _mockLogger.Object,
            _mockCacheManager.Object);

        // Act - Should not throw despite cache manager throwing
        var result = await service.DeleteAllDataSetsByAgentIdAsync(agentId);

        // Assert
        Assert.Equal(0, result); // No datasets were actually deleted from storage
        
        // Verify warning was logged for cache invalidation failure
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Failed to invalidate")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    #endregion

    #region Cache Verification Tests

    [Fact]
    public async Task GetDataSetAsync_WithCacheHit_DoesNotAccessStorage()
    {
        // Arrange
        var agentId = "test-agent";
        var datasetId = "test-dataset-id";
        var cachedEntity = new DataSetTableEntity
        {
            AgentId = agentId,
            DatasetId = datasetId
        };

        _mockCacheManager
            .Setup(x => x.GetAsync<DataSetTableEntity>(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(cachedEntity);

        var service = new DataSetTableService(
            _mockConfigHelper.Object,
            _mockLogger.Object,
            _mockCacheManager.Object);

        // Act
        var result = await service.GetDataSetAsync(agentId, datasetId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(datasetId, result.DatasetId);
        // Verify cache was checked
        _mockCacheManager.Verify(
            x => x.GetAsync<DataSetTableEntity>(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once);
        // Verify cache set was NOT called (because we got a cache hit)
        _mockCacheManager.Verify(
            x => x.SetAsync(It.IsAny<string>(), It.IsAny<DataSetTableEntity>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task GetAllDataSetsByAgentIdAsync_WithEmptyCache_ReturnsEmptyList()
    {
        // Arrange
        var agentId = "test-agent";
        var emptyList = new List<DataSetTableEntity>();

        _mockCacheManager
            .Setup(x => x.GetAsync<List<DataSetTableEntity>>(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(emptyList);

        var service = new DataSetTableService(
            _mockConfigHelper.Object,
            _mockLogger.Object,
            _mockCacheManager.Object);

        // Act
        var result = await service.GetAllDataSetsByAgentIdAsync(agentId);

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    #endregion
}
