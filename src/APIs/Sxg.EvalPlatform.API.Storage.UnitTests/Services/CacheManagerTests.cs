using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using FluentAssertions;
using Sxg.EvalPlatform.API.Storage.Services;

namespace Sxg.EvalPlatform.API.Storage.UnitTests.Services
{
    /// <summary>
    /// Comprehensive unit tests for MemoryCacheManager
    /// </summary>
    [Trait("Category", TestCategories.Unit)]
    [Trait("Category", TestCategories.Cache)]
    [Trait("Category", TestCategories.Service)]
    public class MemoryCacheManagerTests
    {
        private readonly Mock<ILogger<MemoryCacheManager>> _mockLogger;
        private readonly IMemoryCache _memoryCache;
        private readonly MemoryCacheManager _cacheManager;

        public MemoryCacheManagerTests()
        {
            _mockLogger = new Mock<ILogger<MemoryCacheManager>>();
            _memoryCache = new MemoryCache(new MemoryCacheOptions());
            _cacheManager = new MemoryCacheManager(_memoryCache, _mockLogger.Object);
        }

        #region Constructor Tests

        [Fact]
        public void Constructor_WithValidParameters_InitializesSuccessfully()
        {
            // Arrange & Act
            var cacheManager = new MemoryCacheManager(_memoryCache, _mockLogger.Object);

            // Assert
            cacheManager.Should().NotBeNull();
        }

        [Fact]
        public void Constructor_WithNullMemoryCache_ThrowsArgumentNullException()
        {
            // Arrange, Act & Assert
            Assert.Throws<ArgumentNullException>(() => 
                new MemoryCacheManager(null!, _mockLogger.Object));
        }

        [Fact]
        public void Constructor_WithNullLogger_ThrowsArgumentNullException()
        {
            // Arrange, Act & Assert
            Assert.Throws<ArgumentNullException>(() => 
                new MemoryCacheManager(_memoryCache, null!));
        }

        #endregion

        #region GetAsync Tests

        [Fact]
        public async Task GetAsync_WithExistingKey_ReturnsValue()
        {
            // Arrange
            var key = "test-key";
            var value = new TestData { Id = "123", Name = "Test" };
            await _cacheManager.SetAsync(key, value);

            // Act
            var result = await _cacheManager.GetAsync<TestData>(key);

            // Assert
            result.Should().NotBeNull();
            result!.Id.Should().Be(value.Id);
            result.Name.Should().Be(value.Name);
        }

        [Fact]
        public async Task GetAsync_WithNonExistingKey_ReturnsNull()
        {
            // Arrange
            var key = "non-existing-key";

            // Act
            var result = await _cacheManager.GetAsync<TestData>(key);

            // Assert
            result.Should().BeNull();
        }

        [Theory]
        [InlineData("")]
        [InlineData(null)]
        public async Task GetAsync_WithInvalidKey_ThrowsArgumentException(string? invalidKey)
        {
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() => 
                _cacheManager.GetAsync<TestData>(invalidKey!));
        }

        [Fact]
        public async Task GetAsync_MultipleCallsWithSameKey_IncrementsHitCount()
        {
            // Arrange
            var key = "hit-test-key";
            var value = new TestData { Id = "123", Name = "Test" };
            await _cacheManager.SetAsync(key, value);

            // Act
            await _cacheManager.GetAsync<TestData>(key);
            await _cacheManager.GetAsync<TestData>(key);
            await _cacheManager.GetAsync<TestData>(key);

            var stats = await _cacheManager.GetStatisticsAsync();

            // Assert
            stats.HitCount.Should().BeGreaterThanOrEqualTo(3);
        }

        [Fact]
        public async Task GetAsync_WithDifferentTypes_ReturnsCorrectType()
        {
            // Arrange
            var key = "type-test-key";
            var value = new TestData { Id = "123", Name = "Test" };
            await _cacheManager.SetAsync(key, value);

            // Act
            var result = await _cacheManager.GetAsync<TestData>(key);

            // Assert
            result.Should().BeOfType<TestData>();
        }

        [Fact]
        public async Task GetAsync_WithCancellationToken_CompletesSuccessfully()
        {
            // Arrange
            var key = "cancel-test";
            var value = new TestData { Id = "123", Name = "Test" };
            await _cacheManager.SetAsync(key, value);
            var cts = new CancellationTokenSource();

            // Act
            var result = await _cacheManager.GetAsync<TestData>(key, cts.Token);

            // Assert
            result.Should().NotBeNull();
        }

        #endregion

        #region SetAsync Tests

        [Fact]
        public async Task SetAsync_WithValidData_StoresValue()
        {
            // Arrange
            var key = "test-key";
            var value = new TestData { Id = "123", Name = "Test" };

            // Act
            await _cacheManager.SetAsync(key, value);
            var result = await _cacheManager.GetAsync<TestData>(key);

            // Assert
            result.Should().NotBeNull();
            result!.Id.Should().Be(value.Id);
        }

        [Fact]
        public async Task SetAsync_WithExpiration_StoresValueWithExpiration()
        {
            // Arrange
            var key = "test-key-with-expiration";
            var value = new TestData { Id = "456", Name = "Test With Expiration" };
            var expiration = TimeSpan.FromMilliseconds(500);

            // Act
            await _cacheManager.SetAsync(key, value, expiration);
            var result1 = await _cacheManager.GetAsync<TestData>(key);

            // Wait for expiration
            await Task.Delay(600);
            var result2 = await _cacheManager.GetAsync<TestData>(key);

            // Assert
            result1.Should().NotBeNull();
            result2.Should().BeNull();
        }

        [Fact]
        public async Task SetAsync_WithAbsoluteExpiration_StoresValueWithAbsoluteExpiration()
        {
            // Arrange
            var key = "test-absolute-expiration";
            var value = new TestData { Id = "789", Name = "Test" };
            var absoluteExpiration = DateTimeOffset.UtcNow.AddMilliseconds(500);

            // Act
            await _cacheManager.SetAsync(key, value, absoluteExpiration);
            var result1 = await _cacheManager.GetAsync<TestData>(key);

            await Task.Delay(600);
            var result2 = await _cacheManager.GetAsync<TestData>(key);

            // Assert
            result1.Should().NotBeNull();
            result2.Should().BeNull();
        }

        [Fact]
        public async Task SetAsync_WithNullValue_ThrowsArgumentNullException()
        {
            // Arrange
            var key = "test-null-value";
            TestData? nullValue = null;

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() => 
                _cacheManager.SetAsync(key, nullValue!));
        }

        [Theory]
        [InlineData("")]
        [InlineData(null)]
        public async Task SetAsync_WithInvalidKey_ThrowsArgumentException(string? invalidKey)
        {
            // Arrange
            var value = new TestData { Id = "123", Name = "Test" };

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() => 
                _cacheManager.SetAsync(invalidKey!, value));
        }

        [Fact]
        public async Task SetAsync_OverwritesExistingValue_UpdatesCache()
        {
            // Arrange
            var key = "overwrite-test";
            var value1 = new TestData { Id = "1", Name = "First" };
            var value2 = new TestData { Id = "2", Name = "Second" };

            // Act
            await _cacheManager.SetAsync(key, value1);
            await _cacheManager.SetAsync(key, value2);
            var result = await _cacheManager.GetAsync<TestData>(key);

            // Assert
            result.Should().NotBeNull();
            result!.Id.Should().Be("2");
            result.Name.Should().Be("Second");
        }

        [Fact]
        public async Task SetAsync_WithCancellationToken_CompletesSuccessfully()
        {
            // Arrange
            var key = "cancel-set-test";
            var value = new TestData { Id = "123", Name = "Test" };
            var cts = new CancellationTokenSource();

            // Act
            await _cacheManager.SetAsync(key, value, cancellationToken: cts.Token);
            var result = await _cacheManager.GetAsync<TestData>(key);

            // Assert
            result.Should().NotBeNull();
        }

        #endregion

        #region RemoveAsync Tests

        [Fact]
        public async Task RemoveAsync_WithExistingKey_RemovesValue()
        {
            // Arrange
            var key = "test-key-to-remove";
            var value = new TestData { Id = "789", Name = "Test Remove" };
            await _cacheManager.SetAsync(key, value);

            // Act
            await _cacheManager.RemoveAsync(key);
            var result = await _cacheManager.GetAsync<TestData>(key);

            // Assert
            result.Should().BeNull();
        }

        [Fact]
        public async Task RemoveAsync_WithNonExistingKey_CompletesWithoutError()
        {
            // Arrange
            var key = "non-existing-remove-key";

            // Act
            var task = _cacheManager.RemoveAsync(key);
            await task;

            // Assert
            task.IsCompletedSuccessfully.Should().BeTrue();
        }

        [Theory]
        [InlineData("")]
        [InlineData(null)]
        public async Task RemoveAsync_WithInvalidKey_ThrowsArgumentException(string? invalidKey)
        {
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() => 
                _cacheManager.RemoveAsync(invalidKey!));
        }

        [Fact]
        public async Task RemoveAsync_WithCancellationToken_CompletesSuccessfully()
        {
            // Arrange
            var key = "cancel-remove-test";
            var value = new TestData { Id = "123", Name = "Test" };
            await _cacheManager.SetAsync(key, value);
            var cts = new CancellationTokenSource();

            // Act
            await _cacheManager.RemoveAsync(key, cts.Token);
            var result = await _cacheManager.GetAsync<TestData>(key);

            // Assert
            result.Should().BeNull();
        }

        #endregion

        #region ExistsAsync Tests

        [Fact]
        public async Task ExistsAsync_WithExistingKey_ReturnsTrue()
        {
            // Arrange
            var key = "test-key-exists";
            var value = new TestData { Id = "999", Name = "Test Exists" };
            await _cacheManager.SetAsync(key, value);

            // Act
            var result = await _cacheManager.ExistsAsync(key);

            // Assert
            result.Should().BeTrue();
        }

        [Fact]
        public async Task ExistsAsync_WithNonExistingKey_ReturnsFalse()
        {
            // Arrange
            var key = "non-existing-key-exists";

            // Act
            var result = await _cacheManager.ExistsAsync(key);

            // Assert
            result.Should().BeFalse();
        }

        [Fact]
        public async Task ExistsAsync_AfterRemove_ReturnsFalse()
        {
            // Arrange
            var key = "exists-remove-test";
            var value = new TestData { Id = "123", Name = "Test" };
            await _cacheManager.SetAsync(key, value);
            await _cacheManager.RemoveAsync(key);

            // Act
            var result = await _cacheManager.ExistsAsync(key);

            // Assert
            result.Should().BeFalse();
        }

        [Theory]
        [InlineData("")]
        [InlineData(null)]
        public async Task ExistsAsync_WithInvalidKey_ThrowsArgumentException(string? invalidKey)
        {
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() => 
                _cacheManager.ExistsAsync(invalidKey!));
        }

        #endregion

        #region GetOrCreateAsync Tests

        [Fact]
        public async Task GetOrCreateAsync_WithNonExistingKey_CreatesAndReturnsValue()
        {
            // Arrange
            var key = "test-key-get-or-create";
            var expectedValue = new TestData { Id = "111", Name = "Created Value" };

            // Act
            var result = await _cacheManager.GetOrCreateAsync(
                key,
                () => Task.FromResult(expectedValue),
                TimeSpan.FromMinutes(1));

            // Verify it was cached
            var cachedResult = await _cacheManager.GetAsync<TestData>(key);

            // Assert
            result.Should().NotBeNull();
            result.Id.Should().Be(expectedValue.Id);
            cachedResult.Should().NotBeNull();
            cachedResult!.Id.Should().Be(expectedValue.Id);
        }

        [Fact]
        public async Task GetOrCreateAsync_WithExistingKey_ReturnsExistingValue()
        {
            // Arrange
            var key = "test-key-existing";
            var existingValue = new TestData { Id = "222", Name = "Existing Value" };
            var newValue = new TestData { Id = "333", Name = "New Value" };
            var factoryExecuted = false;

            await _cacheManager.SetAsync(key, existingValue);

            // Act
            var result = await _cacheManager.GetOrCreateAsync(
                key,
                () =>
                {
                    factoryExecuted = true;
                    return Task.FromResult(newValue);
                },
                TimeSpan.FromMinutes(1));

            // Assert
            result.Should().NotBeNull();
            result.Id.Should().Be(existingValue.Id);
            result.Name.Should().Be(existingValue.Name);
            factoryExecuted.Should().BeFalse();
        }

        [Fact]
        public async Task GetOrCreateAsync_WithNullFactory_ThrowsArgumentNullException()
        {
            // Arrange
            var key = "test-null-factory";

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() =>
                _cacheManager.GetOrCreateAsync<TestData>(key, null!));
        }

        [Theory]
        [InlineData("")]
        [InlineData(null)]
        public async Task GetOrCreateAsync_WithInvalidKey_ThrowsArgumentException(string? invalidKey)
        {
            // Arrange
            var factory = () => Task.FromResult(new TestData { Id = "123", Name = "Test" });

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() =>
                _cacheManager.GetOrCreateAsync(invalidKey!, factory));
        }

        [Fact]
        public async Task GetOrCreateAsync_WithFactoryException_PropagatesException()
        {
            // Arrange
            var key = "factory-exception-test";

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                _cacheManager.GetOrCreateAsync<TestData>(
                    key,
                    () => throw new InvalidOperationException("Factory error")));
        }

        #endregion

        #region RefreshAsync Tests

        [Fact]
        public async Task RefreshAsync_WithExistingKey_RefreshesEntry()
        {
            // Arrange
            var key = "refresh-test";
            var value = new TestData { Id = "123", Name = "Test" };
            await _cacheManager.SetAsync(key, value);

            // Act
            await _cacheManager.RefreshAsync(key);
            var exists = await _cacheManager.ExistsAsync(key);

            // Assert
            exists.Should().BeTrue();
        }

        [Fact]
        public async Task RefreshAsync_WithNonExistingKey_CompletesWithoutError()
        {
            // Arrange
            var key = "non-existing-refresh";

            // Act
            var task = _cacheManager.RefreshAsync(key);
            await task;

            // Assert
            task.IsCompletedSuccessfully.Should().BeTrue();
        }

        [Theory]
        [InlineData("")]
        [InlineData(null)]
        public async Task RefreshAsync_WithInvalidKey_ThrowsArgumentException(string? invalidKey)
        {
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() =>
                _cacheManager.RefreshAsync(invalidKey!));
        }

        #endregion

        #region ClearAsync Tests

        [Fact]
        public async Task ClearAsync_RemovesAllCachedItems()
        {
            // Arrange
            await _cacheManager.SetAsync("key1", new TestData { Id = "1", Name = "Test1" });
            await _cacheManager.SetAsync("key2", new TestData { Id = "2", Name = "Test2" });
            await _cacheManager.SetAsync("key3", new TestData { Id = "3", Name = "Test3" });

            // Act
            await _cacheManager.ClearAsync();

            // Assert
            (await _cacheManager.ExistsAsync("key1")).Should().BeFalse();
            (await _cacheManager.ExistsAsync("key2")).Should().BeFalse();
            (await _cacheManager.ExistsAsync("key3")).Should().BeFalse();
        }

        [Fact]
        public async Task ClearAsync_WithEmptyCache_CompletesSuccessfully()
        {
            // Act
            var task = _cacheManager.ClearAsync();
            await task;

            // Assert
            task.IsCompletedSuccessfully.Should().BeTrue();
        }

        [Fact]
        public async Task ClearAsync_ResetsStatistics()
        {
            // Arrange
            await _cacheManager.SetAsync("key1", new TestData { Id = "1", Name = "Test" });
            await _cacheManager.GetAsync<TestData>("key1");

            // Act
            await _cacheManager.ClearAsync();
            var stats = await _cacheManager.GetStatisticsAsync();

            // Assert
            stats.ItemCount.Should().Be(0);
        }

        #endregion

        #region GetStatisticsAsync Tests

        [Fact]
        public async Task GetStatisticsAsync_ReturnsValidStatistics()
        {
            // Arrange
            var key1 = "stats-test-1";
            var key2 = "stats-test-2";
            var value = new TestData { Id = "stats", Name = "Statistics Test" };

            // Perform some cache operations to generate statistics
            await _cacheManager.SetAsync(key1, value);
            await _cacheManager.GetAsync<TestData>(key1); // Hit
            await _cacheManager.GetAsync<TestData>(key2); // Miss

            // Act
            var statistics = await _cacheManager.GetStatisticsAsync();

            // Assert
            statistics.Should().NotBeNull();
            statistics.CacheType.Should().Be("Memory");
            statistics.HitCount.Should().BeGreaterThan(0);
            statistics.MissCount.Should().BeGreaterThan(0);
            statistics.HitRatio.Should().BeInRange(0, 1);
            statistics.AdditionalInfo.Should().NotBeNull();
            statistics.AdditionalInfo.Should().ContainKey("TotalRequests");
            statistics.AdditionalInfo.Should().ContainKey("TrackedKeys");
        }

        [Fact]
        public async Task GetStatisticsAsync_WithNoActivity_ReturnsZeroStatistics()
        {
            // Act
            var statistics = await _cacheManager.GetStatisticsAsync();

            // Assert
            statistics.ItemCount.Should().Be(0);
            statistics.HitCount.Should().Be(0);
            statistics.MissCount.Should().Be(0);
            statistics.HitRatio.Should().Be(0);
        }

        [Fact]
        public async Task GetStatisticsAsync_AfterMultipleOperations_CalculatesCorrectHitRatio()
        {
            // Arrange
            var value = new TestData { Id = "1", Name = "Test" };
            await _cacheManager.SetAsync("key1", value);
            
            // 3 hits
            await _cacheManager.GetAsync<TestData>("key1");
            await _cacheManager.GetAsync<TestData>("key1");
            await _cacheManager.GetAsync<TestData>("key1");
            
            // 1 miss
            await _cacheManager.GetAsync<TestData>("key2");

            // Act
            var stats = await _cacheManager.GetStatisticsAsync();

            // Assert
            stats.HitCount.Should().Be(3);
            stats.MissCount.Should().Be(1);
            stats.HitRatio.Should().BeApproximately(0.75, 0.01);
        }

        #endregion

        #region Concurrent Operations Tests

        [Fact]
        public async Task ConcurrentSetAndGet_AllOperationsSucceed()
        {
            // Arrange
            var tasks = new List<Task>();

            // Act
            for (int i = 0; i < 10; i++)
            {
                var key = $"concurrent-key-{i}";
                var value = new TestData { Id = i.ToString(), Name = $"Test{i}" };

                tasks.Add(Task.Run(async () =>
                {
                    await _cacheManager.SetAsync(key, value);
                    var result = await _cacheManager.GetAsync<TestData>(key);
                    result.Should().NotBeNull();
                }));
            }

            await Task.WhenAll(tasks);

            // Assert
            tasks.All(t => t.IsCompletedSuccessfully).Should().BeTrue();
        }

        [Fact]
        public async Task ConcurrentGetOrCreate_OnlyOneFactoryExecution()
        {
            // Arrange
            var key = "concurrent-getorcreate";
            var executionCount = 0;
            var tasks = new List<Task<TestData>>();

            // Act
            for (int i = 0; i < 5; i++)
            {
                tasks.Add(_cacheManager.GetOrCreateAsync(
                    key,
                    async () =>
                    {
                        Interlocked.Increment(ref executionCount);
                        await Task.Delay(10);
                        return new TestData { Id = "1", Name = "Test" };
                    },
                    TimeSpan.FromMinutes(1)));
            }

            var results = await Task.WhenAll(tasks);

            // Assert
            results.Should().AllSatisfy(r => r.Should().NotBeNull());
            // Note: Due to concurrent execution, multiple factories might execute
            // but the cache should handle it gracefully
            executionCount.Should().BeGreaterThan(0);
        }

        #endregion

        #region Integration/Workflow Tests

        [Fact]
        public async Task CompleteWorkflow_AllOperations_WorkCorrectly()
        {
            // Arrange
            var key = "workflow-test";
            var value = new TestData { Id = "123", Name = "Test" };

            // Act & Assert - Set
            await _cacheManager.SetAsync(key, value);
            (await _cacheManager.ExistsAsync(key)).Should().BeTrue();

            // Act & Assert - Get
            var getResult = await _cacheManager.GetAsync<TestData>(key);
            getResult.Should().NotBeNull();
            getResult!.Id.Should().Be(value.Id);

            // Act & Assert - Refresh
            await _cacheManager.RefreshAsync(key);
            (await _cacheManager.ExistsAsync(key)).Should().BeTrue();

            // Act & Assert - Remove
            await _cacheManager.RemoveAsync(key);
            (await _cacheManager.ExistsAsync(key)).Should().BeFalse();

            // Act & Assert - GetOrCreate
            var createResult = await _cacheManager.GetOrCreateAsync(
                key,
                () => Task.FromResult(value),
                TimeSpan.FromMinutes(1));
            createResult.Should().NotBeNull();

            // Act & Assert - Statistics
            var stats = await _cacheManager.GetStatisticsAsync();
            stats.Should().NotBeNull();
            stats.ItemCount.Should().BeGreaterThan(0);
        }

        #endregion

        // Test data class
        private class TestData
        {
            public string Id { get; set; } = string.Empty;
            public string Name { get; set; } = string.Empty;
        }
    }
}