using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Moq;
using Sxg.EvalPlatform.API.Storage.Services;
using Xunit;

namespace Sxg.EvalPlatform.API.Storage.UnitTests.Services
{
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
            Assert.NotNull(result);
            Assert.Equal(value.Id, result.Id);
            Assert.Equal(value.Name, result.Name);
        }

        [Fact]
        public async Task GetAsync_WithNonExistingKey_ReturnsNull()
        {
            // Arrange
            var key = "non-existing-key";

            // Act
            var result = await _cacheManager.GetAsync<TestData>(key);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public async Task SetAsync_WithExpiration_StoresValueWithExpiration()
        {
            // Arrange
            var key = "test-key-with-expiration";
            var value = new TestData { Id = "456", Name = "Test With Expiration" };
            var expiration = TimeSpan.FromSeconds(1);

            // Act
            await _cacheManager.SetAsync(key, value, expiration);
            var result1 = await _cacheManager.GetAsync<TestData>(key);

            // Wait for expiration
            await Task.Delay(1100);
            var result2 = await _cacheManager.GetAsync<TestData>(key);

            // Assert
            Assert.NotNull(result1);
            Assert.Null(result2);
        }

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
            Assert.Null(result);
        }

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
            Assert.True(result);
        }

        [Fact]
        public async Task ExistsAsync_WithNonExistingKey_ReturnsFalse()
        {
            // Arrange
            var key = "non-existing-key-exists";

            // Act
            var result = await _cacheManager.ExistsAsync(key);

            // Assert
            Assert.False(result);
        }

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
         TimeSpan.FromMinutes(1)
            );

            // Verify it was cached
            var cachedResult = await _cacheManager.GetAsync<TestData>(key);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(expectedValue.Id, result.Id);
            Assert.NotNull(cachedResult);
            Assert.Equal(expectedValue.Id, cachedResult.Id);
        }

        [Fact]
        public async Task GetOrCreateAsync_WithExistingKey_ReturnsExistingValue()
        {
            // Arrange
            var key = "test-key-existing";
            var existingValue = new TestData { Id = "222", Name = "Existing Value" };
            var newValue = new TestData { Id = "333", Name = "New Value" };

            await _cacheManager.SetAsync(key, existingValue);

            // Act
            var result = await _cacheManager.GetOrCreateAsync(
       key,
                () => Task.FromResult(newValue), // This should not be called
              TimeSpan.FromMinutes(1)
            );

            // Assert
            Assert.NotNull(result);
            Assert.Equal(existingValue.Id, result.Id);
            Assert.Equal(existingValue.Name, result.Name);
        }

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
            Assert.NotNull(statistics);
            Assert.Equal("Memory", statistics.CacheType);
            Assert.True(statistics.HitCount > 0);
            Assert.True(statistics.MissCount > 0);
            Assert.True(statistics.HitRatio >= 0 && statistics.HitRatio <= 1);
            Assert.NotNull(statistics.AdditionalInfo);
        }

        [Theory]
        [InlineData("")]
        [InlineData(null)]
        public async Task GetAsync_WithInvalidKey_ThrowsArgumentException(string invalidKey)
        {
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() => _cacheManager.GetAsync<TestData>(invalidKey));
        }

        [Fact]
        public async Task SetAsync_WithNullValue_ThrowsArgumentNullException()
        {
            // Arrange
            var key = "test-null-value";
            TestData nullValue = null;

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() => _cacheManager.SetAsync(key, nullValue));
        }

        private class TestData
        {
            public string Id { get; set; } = string.Empty;
            public string Name { get; set; } = string.Empty;
        }
    }
}