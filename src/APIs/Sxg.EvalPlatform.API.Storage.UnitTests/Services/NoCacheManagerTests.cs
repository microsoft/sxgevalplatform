using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using FluentAssertions;
using Sxg.EvalPlatform.API.Storage.Services;

namespace Sxg.EvalPlatform.API.Storage.UnitTests.Services
{
    /// <summary>
    /// Unit tests for NoCacheManager - tests for disabled/no-op caching functionality
    /// </summary>
    [Trait("Category", TestCategories.Unit)]
    [Trait("Category", TestCategories.Cache)]
    [Trait("Category", TestCategories.Service)]
    public class NoCacheManagerTests
    {
        private readonly Mock<ILogger<NoCacheManager>> _mockLogger;
        private readonly NoCacheManager _cacheManager;

        public NoCacheManagerTests()
        {
            _mockLogger = new Mock<ILogger<NoCacheManager>>();
            _cacheManager = new NoCacheManager(_mockLogger.Object);
        }

        #region Constructor Tests

        [Fact]
        public void Constructor_WithValidLogger_InitializesSuccessfully()
        {
            // Arrange & Act
            var cacheManager = new NoCacheManager(_mockLogger.Object);

            // Assert
            cacheManager.Should().NotBeNull();
        }

        [Fact]
        public void Constructor_WithNullLogger_ThrowsArgumentNullException()
        {
            // Arrange, Act & Assert
            Assert.Throws<ArgumentNullException>(() => new NoCacheManager(null!));
        }

        #endregion

        #region GetAsync Tests

        [Fact]
        public async Task GetAsync_WithAnyKey_ReturnsNull()
        {
            // Arrange
            var key = "test-key";

            // Act
            var result = await _cacheManager.GetAsync<TestData>(key);

            // Assert
            result.Should().BeNull();
        }

        [Fact]
        public async Task GetAsync_WithDifferentKeys_AlwaysReturnsNull()
        {
            // Arrange
            var keys = new[] { "key1", "key2", "key3" };

            // Act & Assert
            foreach (var key in keys)
            {
                var result = await _cacheManager.GetAsync<TestData>(key);
                result.Should().BeNull();
            }
        }

        [Fact]
        public async Task GetAsync_WithCancellationToken_ReturnsNull()
        {
            // Arrange
            var key = "test-key";
            var cts = new CancellationTokenSource();

            // Act
            var result = await _cacheManager.GetAsync<TestData>(key, cts.Token);

            // Assert
            result.Should().BeNull();
        }

        #endregion

        #region SetAsync Tests

        [Fact]
        public async Task SetAsync_WithValidData_CompletesWithoutError()
        {
            // Arrange
            var key = "test-key";
            var value = new TestData { Id = "123", Name = "Test" };

            // Act
            var task = _cacheManager.SetAsync(key, value);
            await task;

            // Assert
            task.IsCompletedSuccessfully.Should().BeTrue();
        }

        [Fact]
        public async Task SetAsync_WithExpiration_CompletesWithoutError()
        {
            // Arrange
            var key = "test-key";
            var value = new TestData { Id = "123", Name = "Test" };
            var expiration = TimeSpan.FromMinutes(5);

            // Act
            var task = _cacheManager.SetAsync(key, value, expiration);
            await task;

            // Assert
            task.IsCompletedSuccessfully.Should().BeTrue();
        }

        [Fact]
        public async Task SetAsync_WithAbsoluteExpiration_CompletesWithoutError()
        {
            // Arrange
            var key = "test-key";
            var value = new TestData { Id = "123", Name = "Test" };
            var absoluteExpiration = DateTimeOffset.UtcNow.AddMinutes(5);

            // Act
            var task = _cacheManager.SetAsync(key, value, absoluteExpiration);
            await task;

            // Assert
            task.IsCompletedSuccessfully.Should().BeTrue();
        }

        [Fact]
        public async Task SetAsync_ThenGet_ReturnsNull()
        {
            // Arrange
            var key = "test-key";
            var value = new TestData { Id = "123", Name = "Test" };

            // Act
            await _cacheManager.SetAsync(key, value);
            var result = await _cacheManager.GetAsync<TestData>(key);

            // Assert
            result.Should().BeNull();
        }

        [Fact]
        public async Task SetAsync_WithCancellationToken_CompletesWithoutError()
        {
            // Arrange
            var key = "test-key";
            var value = new TestData { Id = "123", Name = "Test" };
            var cts = new CancellationTokenSource();

            // Act
            var task = _cacheManager.SetAsync(key, value, cancellationToken: cts.Token);
            await task;

            // Assert
            task.IsCompletedSuccessfully.Should().BeTrue();
        }

        #endregion

        #region RemoveAsync Tests

        [Fact]
        public async Task RemoveAsync_WithAnyKey_CompletesWithoutError()
        {
            // Arrange
            var key = "test-key";

            // Act
            var task = _cacheManager.RemoveAsync(key);
            await task;

            // Assert
            task.IsCompletedSuccessfully.Should().BeTrue();
        }

        [Fact]
        public async Task RemoveAsync_WithMultipleKeys_CompletesWithoutError()
        {
            // Arrange
            var keys = new[] { "key1", "key2", "key3" };

            // Act & Assert
            foreach (var key in keys)
            {
                var task = _cacheManager.RemoveAsync(key);
                await task;
                task.IsCompletedSuccessfully.Should().BeTrue();
            }
        }

        [Fact]
        public async Task RemoveAsync_WithCancellationToken_CompletesWithoutError()
        {
            // Arrange
            var key = "test-key";
            var cts = new CancellationTokenSource();

            // Act
            var task = _cacheManager.RemoveAsync(key, cts.Token);
            await task;

            // Assert
            task.IsCompletedSuccessfully.Should().BeTrue();
        }

        #endregion

        #region ExistsAsync Tests

        [Fact]
        public async Task ExistsAsync_WithAnyKey_ReturnsFalse()
        {
            // Arrange
            var key = "test-key";

            // Act
            var exists = await _cacheManager.ExistsAsync(key);

            // Assert
            exists.Should().BeFalse();
        }

        [Fact]
        public async Task ExistsAsync_AfterSet_StillReturnsFalse()
        {
            // Arrange
            var key = "test-key";
            var value = new TestData { Id = "123", Name = "Test" };

            // Act
            await _cacheManager.SetAsync(key, value);
            var exists = await _cacheManager.ExistsAsync(key);

            // Assert
            exists.Should().BeFalse();
        }

        [Fact]
        public async Task ExistsAsync_WithCancellationToken_ReturnsFalse()
        {
            // Arrange
            var key = "test-key";
            var cts = new CancellationTokenSource();

            // Act
            var exists = await _cacheManager.ExistsAsync(key, cts.Token);

            // Assert
            exists.Should().BeFalse();
        }

        #endregion

        #region GetOrCreateAsync Tests

        [Fact]
        public async Task GetOrCreateAsync_WithFactory_AlwaysExecutesFactory()
        {
            // Arrange
            var key = "test-key";
            var expectedValue = new TestData { Id = "123", Name = "Created Value" };
            var factoryExecuted = false;

            // Act
            var result = await _cacheManager.GetOrCreateAsync(
                key,
                () =>
                {
                    factoryExecuted = true;
                    return Task.FromResult(expectedValue);
                });

            // Assert
            factoryExecuted.Should().BeTrue();
            result.Should().NotBeNull();
            result.Id.Should().Be(expectedValue.Id);
        }

        [Fact]
        public async Task GetOrCreateAsync_CalledTwice_ExecutesFactoryBothTimes()
        {
            // Arrange
            var key = "test-key";
            var expectedValue = new TestData { Id = "123", Name = "Created Value" };
            var executionCount = 0;

            // Act
            var result1 = await _cacheManager.GetOrCreateAsync(
                key,
                () =>
                {
                    executionCount++;
                    return Task.FromResult(expectedValue);
                });

            var result2 = await _cacheManager.GetOrCreateAsync(
                key,
                () =>
                {
                    executionCount++;
                    return Task.FromResult(expectedValue);
                });

            // Assert
            executionCount.Should().Be(2);
            result1.Should().NotBeNull();
            result2.Should().NotBeNull();
        }

        [Fact]
        public async Task GetOrCreateAsync_WithNullFactory_ThrowsArgumentNullException()
        {
            // Arrange
            var key = "test-key";

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() =>
                _cacheManager.GetOrCreateAsync<TestData>(key, null!));
        }

        [Fact]
        public async Task GetOrCreateAsync_WithExpiration_ExecutesFactoryAndReturnsValue()
        {
            // Arrange
            var key = "test-key";
            var expectedValue = new TestData { Id = "123", Name = "Created Value" };
            var expiration = TimeSpan.FromMinutes(5);

            // Act
            var result = await _cacheManager.GetOrCreateAsync(
                key,
                () => Task.FromResult(expectedValue),
                expiration);

            // Assert
            result.Should().NotBeNull();
            result.Id.Should().Be(expectedValue.Id);
        }

        [Fact]
        public async Task GetOrCreateAsync_WithCancellationToken_ExecutesFactory()
        {
            // Arrange
            var key = "test-key";
            var expectedValue = new TestData { Id = "123", Name = "Created Value" };
            var cts = new CancellationTokenSource();

            // Act
            var result = await _cacheManager.GetOrCreateAsync(
                key,
                () => Task.FromResult(expectedValue),
                cancellationToken: cts.Token);

            // Assert
            result.Should().NotBeNull();
            result.Id.Should().Be(expectedValue.Id);
        }

        #endregion

        #region RefreshAsync Tests

        [Fact]
        public async Task RefreshAsync_WithAnyKey_CompletesWithoutError()
        {
            // Arrange
            var key = "test-key";

            // Act
            var task = _cacheManager.RefreshAsync(key);
            await task;

            // Assert
            task.IsCompletedSuccessfully.Should().BeTrue();
        }

        [Fact]
        public async Task RefreshAsync_WithCancellationToken_CompletesWithoutError()
        {
            // Arrange
            var key = "test-key";
            var cts = new CancellationTokenSource();

            // Act
            var task = _cacheManager.RefreshAsync(key, cts.Token);
            await task;

            // Assert
            task.IsCompletedSuccessfully.Should().BeTrue();
        }

        #endregion

        #region ClearAsync Tests

        [Fact]
        public async Task ClearAsync_WithoutToken_CompletesWithoutError()
        {
            // Act
            var task = _cacheManager.ClearAsync();
            await task;

            // Assert
            task.IsCompletedSuccessfully.Should().BeTrue();
        }

        [Fact]
        public async Task ClearAsync_WithCancellationToken_CompletesWithoutError()
        {
            // Arrange
            var cts = new CancellationTokenSource();

            // Act
            var task = _cacheManager.ClearAsync(cts.Token);
            await task;

            // Assert
            task.IsCompletedSuccessfully.Should().BeTrue();
        }

        [Fact]
        public async Task ClearAsync_AfterMultipleSets_CompletesWithoutError()
        {
            // Arrange
            await _cacheManager.SetAsync("key1", new TestData { Id = "1", Name = "Test1" });
            await _cacheManager.SetAsync("key2", new TestData { Id = "2", Name = "Test2" });
            await _cacheManager.SetAsync("key3", new TestData { Id = "3", Name = "Test3" });

            // Act
            var task = _cacheManager.ClearAsync();
            await task;

            // Assert
            task.IsCompletedSuccessfully.Should().BeTrue();
        }

        #endregion

        #region GetStatisticsAsync Tests

        [Fact]
        public async Task GetStatisticsAsync_ReturnsDisabledStatistics()
        {
            // Act
            var statistics = await _cacheManager.GetStatisticsAsync();

            // Assert
            statistics.Should().NotBeNull();
            statistics.CacheType.Should().Be("NoCacheManager (Disabled)");
            statistics.ItemCount.Should().Be(0);
            statistics.HitCount.Should().Be(0);
            statistics.MissCount.Should().Be(0);
            statistics.HitRatio.Should().Be(0);
        }

        [Fact]
        public async Task GetStatisticsAsync_ContainsDisabledReason()
        {
            // Act
            var statistics = await _cacheManager.GetStatisticsAsync();

            // Assert
            statistics.AdditionalInfo.Should().ContainKey("Enabled");
            statistics.AdditionalInfo["Enabled"].Should().Be(false);
            statistics.AdditionalInfo.Should().ContainKey("Reason");
        }

        [Fact]
        public async Task GetStatisticsAsync_AfterOperations_StillReturnsZeroStatistics()
        {
            // Arrange
            await _cacheManager.SetAsync("key1", new TestData { Id = "1", Name = "Test" });
            await _cacheManager.GetAsync<TestData>("key1");
            await _cacheManager.GetAsync<TestData>("key2");
            await _cacheManager.RemoveAsync("key1");

            // Act
            var statistics = await _cacheManager.GetStatisticsAsync();

            // Assert
            statistics.ItemCount.Should().Be(0);
            statistics.HitCount.Should().Be(0);
            statistics.MissCount.Should().Be(0);
        }

        #endregion

        #region Integration/Workflow Tests

        [Fact]
        public async Task CompleteWorkflow_AllOperations_CompleteSuccessfully()
        {
            // Arrange
            var key = "test-key";
            var value = new TestData { Id = "123", Name = "Test" };

            // Act & Assert - Set
            await _cacheManager.SetAsync(key, value);

            // Act & Assert - Get (should be null)
            var getResult = await _cacheManager.GetAsync<TestData>(key);
            getResult.Should().BeNull();

            // Act & Assert - Exists (should be false)
            var exists = await _cacheManager.ExistsAsync(key);
            exists.Should().BeFalse();

            // Act & Assert - GetOrCreate (should execute factory)
            var getOrCreateResult = await _cacheManager.GetOrCreateAsync(
                key,
                () => Task.FromResult(value));
            getOrCreateResult.Should().NotBeNull();

            // Act & Assert - Refresh
            await _cacheManager.RefreshAsync(key);

            // Act & Assert - Remove
            await _cacheManager.RemoveAsync(key);

            // Act & Assert - Clear
            await _cacheManager.ClearAsync();

            // Act & Assert - Statistics
            var statistics = await _cacheManager.GetStatisticsAsync();
            statistics.ItemCount.Should().Be(0);
        }

        [Fact]
        public async Task MultipleOperations_Parallel_CompleteSuccessfully()
        {
            // Arrange
            var tasks = new List<Task>();

            // Act - Execute multiple operations in parallel
            for (int i = 0; i < 10; i++)
            {
                var key = $"key-{i}";
                var value = new TestData { Id = i.ToString(), Name = $"Test{i}" };

                tasks.Add(Task.Run(async () =>
                {
                    await _cacheManager.SetAsync(key, value);
                    await _cacheManager.GetAsync<TestData>(key);
                    await _cacheManager.ExistsAsync(key);
                    await _cacheManager.RemoveAsync(key);
                }));
            }

            await Task.WhenAll(tasks);

            // Assert - All operations completed without errors
            tasks.All(t => t.IsCompletedSuccessfully).Should().BeTrue();
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
