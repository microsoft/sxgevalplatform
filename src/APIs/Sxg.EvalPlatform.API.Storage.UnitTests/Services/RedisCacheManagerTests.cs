using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Moq;
using Sxg.EvalPlatform.API.Storage.Services;
using StackExchange.Redis;
using System.Text;
using System.Text.Json;

namespace Sxg.EvalPlatform.API.Storage.UnitTests.Services;

[Trait("Category", TestCategories.Unit)]
public class RedisCacheManagerTests
{
    private readonly Mock<IDistributedCache> _mockDistributedCache;
    private readonly Mock<ILogger<RedisCacheManager>> _mockLogger;
    private readonly Mock<IConnectionMultiplexer> _mockConnectionMultiplexer;

    public RedisCacheManagerTests()
    {
        _mockDistributedCache = new Mock<IDistributedCache>();
        _mockLogger = new Mock<ILogger<RedisCacheManager>>();
        _mockConnectionMultiplexer = new Mock<IConnectionMultiplexer>();
    }

    [Fact]
    public void Constructor_WithNullDistributedCache_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new RedisCacheManager(null!, _mockLogger.Object));
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new RedisCacheManager(_mockDistributedCache.Object, null!));
    }

    [Fact]
    public void Constructor_WithValidParameters_CreatesInstance()
    {
        // Act
        var manager = new RedisCacheManager(
            _mockDistributedCache.Object,
            _mockLogger.Object);

        // Assert
        Assert.NotNull(manager);
    }

    [Fact]
    public void Constructor_WithConnectionMultiplexer_CreatesInstance()
    {
        // Act
        var manager = new RedisCacheManager(
            _mockDistributedCache.Object,
            _mockLogger.Object,
            _mockConnectionMultiplexer.Object);

        // Assert
        Assert.NotNull(manager);
    }

    [Fact]
    public async Task GetAsync_WithNullKey_ThrowsArgumentException()
    {
        // Arrange
        var manager = new RedisCacheManager(
            _mockDistributedCache.Object,
            _mockLogger.Object);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            manager.GetAsync<string>(null!));
    }

    [Fact]
    public async Task GetAsync_WithEmptyKey_ThrowsArgumentException()
    {
        // Arrange
        var manager = new RedisCacheManager(
            _mockDistributedCache.Object,
            _mockLogger.Object);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            manager.GetAsync<string>(string.Empty));
    }

    [Fact]
    public async Task GetAsync_WithCacheMiss_ReturnsNull()
    {
        // Arrange
        var key = "test-key";
        _mockDistributedCache
            .Setup(x => x.GetAsync(key, It.IsAny<CancellationToken>()))
            .ReturnsAsync((byte[]?)null);

        var manager = new RedisCacheManager(
            _mockDistributedCache.Object,
            _mockLogger.Object);

        // Act
        var result = await manager.GetAsync<string>(key);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetAsync_WithCacheHit_ReturnsDeserializedValue()
    {
        // Arrange
        var key = "test-key";
        var expectedValue = new TestClass { Name = "Test", Value = 123 };
        var json = JsonSerializer.Serialize(expectedValue);
        var bytes = Encoding.UTF8.GetBytes(json);

        _mockDistributedCache
            .Setup(x => x.GetAsync(key, It.IsAny<CancellationToken>()))
            .ReturnsAsync(bytes);

        var manager = new RedisCacheManager(
            _mockDistributedCache.Object,
            _mockLogger.Object);

        // Act
        var result = await manager.GetAsync<TestClass>(key);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(expectedValue.Name, result.Name);
        Assert.Equal(expectedValue.Value, result.Value);
    }

    [Fact]
    public async Task SetAsync_WithNullKey_ThrowsArgumentException()
    {
        // Arrange
        var manager = new RedisCacheManager(
            _mockDistributedCache.Object,
            _mockLogger.Object);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            manager.SetAsync<string>(null!, "value"));
    }

    [Fact]
    public async Task SetAsync_WithNullValue_ThrowsArgumentNullException()
    {
        // Arrange
        var manager = new RedisCacheManager(
            _mockDistributedCache.Object,
            _mockLogger.Object);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            manager.SetAsync<string>("key", null!));
    }

    [Fact]
    public async Task SetAsync_WithValidParameters_CallsDistributedCache()
    {
        // Arrange
        var key = "test-key";
        var value = new TestClass { Name = "Test", Value = 123 };
        var expiration = TimeSpan.FromMinutes(30);

        _mockDistributedCache
            .Setup(x => x.SetAsync(
                key,
                It.IsAny<byte[]>(),
                It.IsAny<DistributedCacheEntryOptions>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var manager = new RedisCacheManager(
            _mockDistributedCache.Object,
            _mockLogger.Object);

        // Act
        await manager.SetAsync(key, value, expiration);

        // Assert
        _mockDistributedCache.Verify(
            x => x.SetAsync(
                key,
                It.IsAny<byte[]>(),
                It.Is<DistributedCacheEntryOptions>(o =>
                    o.AbsoluteExpirationRelativeToNow == expiration),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task SetAsync_WithAbsoluteExpiration_CallsDistributedCache()
    {
        // Arrange
        var key = "test-key";
        var value = new TestClass { Name = "Test", Value = 123 };
        var absoluteExpiration = DateTimeOffset.UtcNow.AddHours(1);

        _mockDistributedCache
            .Setup(x => x.SetAsync(
                key,
                It.IsAny<byte[]>(),
                It.IsAny<DistributedCacheEntryOptions>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var manager = new RedisCacheManager(
            _mockDistributedCache.Object,
            _mockLogger.Object);

        // Act
        await manager.SetAsync(key, value, absoluteExpiration);

        // Assert
        _mockDistributedCache.Verify(
            x => x.SetAsync(
                key,
                It.IsAny<byte[]>(),
                It.Is<DistributedCacheEntryOptions>(o =>
                    o.AbsoluteExpiration == absoluteExpiration),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task RemoveAsync_WithNullKey_ThrowsArgumentException()
    {
        // Arrange
        var manager = new RedisCacheManager(
            _mockDistributedCache.Object,
            _mockLogger.Object);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            manager.RemoveAsync(null!));
    }

    [Fact]
    public async Task RemoveAsync_WithValidKey_CallsDistributedCache()
    {
        // Arrange
        var key = "test-key";

        _mockDistributedCache
            .Setup(x => x.RemoveAsync(key, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var manager = new RedisCacheManager(
            _mockDistributedCache.Object,
            _mockLogger.Object);

        // Act
        await manager.RemoveAsync(key);

        // Assert
        _mockDistributedCache.Verify(
            x => x.RemoveAsync(key, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ExistsAsync_WithNullKey_ThrowsArgumentException()
    {
        // Arrange
        var manager = new RedisCacheManager(
            _mockDistributedCache.Object,
            _mockLogger.Object);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            manager.ExistsAsync(null!));
    }

    [Fact]
    public async Task ExistsAsync_WithExistingKey_ReturnsTrue()
    {
        // Arrange
        var key = "test-key";
        var bytes = Encoding.UTF8.GetBytes("test-value");

        _mockDistributedCache
            .Setup(x => x.GetAsync(key, It.IsAny<CancellationToken>()))
            .ReturnsAsync(bytes);

        var manager = new RedisCacheManager(
            _mockDistributedCache.Object,
            _mockLogger.Object);

        // Act
        var result = await manager.ExistsAsync(key);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task ExistsAsync_WithNonExistingKey_ReturnsFalse()
    {
        // Arrange
        var key = "test-key";

        _mockDistributedCache
            .Setup(x => x.GetAsync(key, It.IsAny<CancellationToken>()))
            .ReturnsAsync((byte[]?)null);

        var manager = new RedisCacheManager(
            _mockDistributedCache.Object,
            _mockLogger.Object);

        // Act
        var result = await manager.ExistsAsync(key);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task GetOrCreateAsync_WithNullKey_ThrowsArgumentException()
    {
        // Arrange
        var manager = new RedisCacheManager(
            _mockDistributedCache.Object,
            _mockLogger.Object);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            manager.GetOrCreateAsync<string>(null!, () => Task.FromResult("value")));
    }

    [Fact]
    public async Task GetOrCreateAsync_WithNullFactory_ThrowsArgumentNullException()
    {
        // Arrange
        var manager = new RedisCacheManager(
            _mockDistributedCache.Object,
            _mockLogger.Object);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            manager.GetOrCreateAsync<string>("key", null!));
    }

    [Fact]
    public async Task GetOrCreateAsync_WithCacheHit_ReturnsExistingValue()
    {
        // Arrange
        var key = "test-key";
        var cachedValue = new TestClass { Name = "Cached", Value = 999 };
        var json = JsonSerializer.Serialize(cachedValue);
        var bytes = Encoding.UTF8.GetBytes(json);
        var factoryCalled = false;

        _mockDistributedCache
            .Setup(x => x.GetAsync(key, It.IsAny<CancellationToken>()))
            .ReturnsAsync(bytes);

        var manager = new RedisCacheManager(
            _mockDistributedCache.Object,
            _mockLogger.Object);

        // Act
        var result = await manager.GetOrCreateAsync(key, () =>
        {
            factoryCalled = true;
            return Task.FromResult(new TestClass { Name = "New", Value = 1 });
        });

        // Assert
        Assert.NotNull(result);
        Assert.Equal(cachedValue.Name, result.Name);
        Assert.Equal(cachedValue.Value, result.Value);
        Assert.False(factoryCalled);
    }

    [Fact]
    public async Task GetOrCreateAsync_WithCacheMiss_CallsFactoryAndCachesValue()
    {
        // Arrange
        var key = "test-key";
        var newValue = new TestClass { Name = "New", Value = 123 };
        var factoryCalled = false;

        _mockDistributedCache
            .Setup(x => x.GetAsync(key, It.IsAny<CancellationToken>()))
            .ReturnsAsync((byte[]?)null);

        _mockDistributedCache
            .Setup(x => x.SetAsync(
                key,
                It.IsAny<byte[]>(),
                It.IsAny<DistributedCacheEntryOptions>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var manager = new RedisCacheManager(
            _mockDistributedCache.Object,
            _mockLogger.Object);

        // Act
        var result = await manager.GetOrCreateAsync(key, () =>
        {
            factoryCalled = true;
            return Task.FromResult(newValue);
        });

        // Assert
        Assert.NotNull(result);
        Assert.Equal(newValue.Name, result.Name);
        Assert.Equal(newValue.Value, result.Value);
        Assert.True(factoryCalled);

        _mockDistributedCache.Verify(
            x => x.SetAsync(
                key,
                It.IsAny<byte[]>(),
                It.IsAny<DistributedCacheEntryOptions>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task RefreshAsync_WithNullKey_ThrowsArgumentException()
    {
        // Arrange
        var manager = new RedisCacheManager(
            _mockDistributedCache.Object,
            _mockLogger.Object);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            manager.RefreshAsync(null!));
    }

    [Fact]
    public async Task RefreshAsync_WithValidKey_CallsDistributedCache()
    {
        // Arrange
        var key = "test-key";

        _mockDistributedCache
            .Setup(x => x.RefreshAsync(key, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var manager = new RedisCacheManager(
            _mockDistributedCache.Object,
            _mockLogger.Object);

        // Act
        await manager.RefreshAsync(key);

        // Assert
        _mockDistributedCache.Verify(
            x => x.RefreshAsync(key, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetStatisticsAsync_ReturnsValidStatistics()
    {
        // Arrange
        var manager = new RedisCacheManager(
            _mockDistributedCache.Object,
            _mockLogger.Object);

        // Act
        var stats = await manager.GetStatisticsAsync();

        // Assert
        Assert.NotNull(stats);
        Assert.Equal("Redis", stats.CacheType);
        Assert.Equal(0, stats.HitCount);
        Assert.Equal(0, stats.MissCount);
        Assert.Equal(0, stats.HitRatio);
    }

    [Fact]
    public async Task GetAsync_WithTimeout_ReturnsNullGracefully()
    {
        // Arrange
        var key = "test-key";

        _mockDistributedCache
            .Setup(x => x.GetAsync(key, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TimeoutException("Redis timeout"));

        var manager = new RedisCacheManager(
            _mockDistributedCache.Object,
            _mockLogger.Object);

        // Act
        var result = await manager.GetAsync<string>(key);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task SetAsync_WithException_LogsButDoesNotThrow()
    {
        // Arrange
        var key = "test-key";
        var value = new TestClass { Name = "Test", Value = 123 };

        _mockDistributedCache
            .Setup(x => x.SetAsync(
                key,
                It.IsAny<byte[]>(),
                It.IsAny<DistributedCacheEntryOptions>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Cache error"));

        var manager = new RedisCacheManager(
            _mockDistributedCache.Object,
            _mockLogger.Object);

        // Act & Assert - Should not throw
        await manager.SetAsync(key, value);
    }

    // Helper class for testing
    private class TestClass
    {
        public string Name { get; set; } = string.Empty;
        public int Value { get; set; }
    }
}
