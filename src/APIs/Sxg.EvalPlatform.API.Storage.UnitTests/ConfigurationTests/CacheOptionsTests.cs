using Sxg.EvalPlatform.API.Storage.Configuration;

namespace Sxg.EvalPlatform.API.Storage.UnitTests.ConfigurationTests;

[Trait("Category", TestCategories.Unit)]
public class CacheOptionsTests
{
    [Fact]
    public void CacheOptions_DefaultConstructor_SetsDefaultValues()
    {
        // Act
        var options = new CacheOptions();

        // Assert
        Assert.Equal("None", options.Provider);
        Assert.Equal(30, options.DefaultExpirationMinutes);
        Assert.NotNull(options.Redis);
        Assert.NotNull(options.Memory);
    }

    [Theory]
    [InlineData("None")]
    [InlineData("Memory")]
    [InlineData("Redis")]
    public void CacheOptions_SetProvider_StoresValue(string provider)
    {
        // Arrange & Act
        var options = new CacheOptions
        {
            Provider = provider
        };

        // Assert
        Assert.Equal(provider, options.Provider);
    }

    [Theory]
    [InlineData(5)]
    [InlineData(30)]
    [InlineData(60)]
    [InlineData(120)]
    public void CacheOptions_SetDefaultExpiration_StoresValue(int minutes)
    {
        // Arrange & Act
        var options = new CacheOptions
        {
            DefaultExpirationMinutes = minutes
        };

        // Assert
        Assert.Equal(minutes, options.DefaultExpirationMinutes);
    }

    [Fact]
    public void CacheOptions_WithRedisOptions_ConfiguresCorrectly()
    {
        // Arrange & Act
        var options = new CacheOptions
        {
            Provider = "Redis",
            Redis = new RedisOptions
            {
                Endpoint = "mycache.redis.cache.windows.net:6380",
                InstanceName = "mycache",
                UseManagedIdentity = true
            }
        };

        // Assert
        Assert.Equal("Redis", options.Provider);
        Assert.NotNull(options.Redis);
        Assert.Equal("mycache.redis.cache.windows.net:6380", options.Redis.Endpoint);
        Assert.Equal("mycache", options.Redis.InstanceName);
        Assert.True(options.Redis.UseManagedIdentity);
    }

    [Fact]
    public void CacheOptions_WithMemoryOptions_ConfiguresCorrectly()
    {
        // Arrange & Act
        var options = new CacheOptions
        {
            Provider = "Memory",
            Memory = new MemoryCacheOptions
            {
                SizeLimitMB = 1000,
                CompactionPercentage = 0.3,
                ExpirationScanFrequencySeconds = 120
            }
        };

        // Assert
        Assert.Equal("Memory", options.Provider);
        Assert.NotNull(options.Memory);
        Assert.Equal(1000, options.Memory.SizeLimitMB);
        Assert.Equal(0.3, options.Memory.CompactionPercentage);
        Assert.Equal(120, options.Memory.ExpirationScanFrequencySeconds);
    }
}

[Trait("Category", TestCategories.Unit)]
public class RedisOptionsTests
{
    [Fact]
    public void RedisOptions_DefaultConstructor_SetsDefaultValues()
    {
        // Act
        var options = new RedisOptions();

        // Assert
        Assert.Null(options.Endpoint);
        Assert.Null(options.InstanceName);
        Assert.True(options.UseManagedIdentity);
        Assert.Equal(30, options.ConnectTimeoutSeconds);
        Assert.Equal(30, options.CommandTimeoutSeconds);
        Assert.True(options.UseSsl);
        Assert.NotNull(options.Retry);
    }

    [Fact]
    public void RedisOptions_SetEndpoint_StoresValue()
    {
        // Arrange & Act
        var options = new RedisOptions
        {
            Endpoint = "mycache.redis.cache.windows.net:6380"
        };

        // Assert
        Assert.Equal("mycache.redis.cache.windows.net:6380", options.Endpoint);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void RedisOptions_SetUseManagedIdentity_StoresValue(bool useManagedIdentity)
    {
        // Arrange & Act
        var options = new RedisOptions
        {
            UseManagedIdentity = useManagedIdentity
        };

        // Assert
        Assert.Equal(useManagedIdentity, options.UseManagedIdentity);
    }

    [Theory]
    [InlineData(10)]
    [InlineData(30)]
    [InlineData(60)]
    public void RedisOptions_SetTimeouts_StoresValues(int timeoutSeconds)
    {
        // Arrange & Act
        var options = new RedisOptions
        {
            ConnectTimeoutSeconds = timeoutSeconds,
            CommandTimeoutSeconds = timeoutSeconds
        };

        // Assert
        Assert.Equal(timeoutSeconds, options.ConnectTimeoutSeconds);
        Assert.Equal(timeoutSeconds, options.CommandTimeoutSeconds);
    }

    [Fact]
    public void RedisOptions_WithRetryOptions_ConfiguresCorrectly()
    {
        // Arrange & Act
        var options = new RedisOptions
        {
            Retry = new RedisRetryOptions
            {
                Enabled = true,
                MaxRetryAttempts = 5,
                BaseDelayMs = 500,
                MaxDelayMs = 10000
            }
        };

        // Assert
        Assert.NotNull(options.Retry);
        Assert.True(options.Retry.Enabled);
        Assert.Equal(5, options.Retry.MaxRetryAttempts);
        Assert.Equal(500, options.Retry.BaseDelayMs);
        Assert.Equal(10000, options.Retry.MaxDelayMs);
    }
}

[Trait("Category", TestCategories.Unit)]
public class MemoryCacheOptionsTests
{
    [Fact]
    public void MemoryCacheOptions_DefaultConstructor_SetsDefaultValues()
    {
        // Act
        var options = new MemoryCacheOptions();

        // Assert
        Assert.Equal(500, options.SizeLimitMB);
        Assert.Equal(0.25, options.CompactionPercentage);
        Assert.Equal(60, options.ExpirationScanFrequencySeconds);
    }

    [Theory]
    [InlineData(100)]
    [InlineData(500)]
    [InlineData(1000)]
    [InlineData(0)]
    public void MemoryCacheOptions_SetSizeLimit_StoresValue(int sizeLimitMB)
    {
        // Arrange & Act
        var options = new MemoryCacheOptions
        {
            SizeLimitMB = sizeLimitMB
        };

        // Assert
        Assert.Equal(sizeLimitMB, options.SizeLimitMB);
    }

    [Theory]
    [InlineData(0.1)]
    [InlineData(0.25)]
    [InlineData(0.5)]
    [InlineData(1.0)]
    public void MemoryCacheOptions_SetCompactionPercentage_StoresValue(double compactionPercentage)
    {
        // Arrange & Act
        var options = new MemoryCacheOptions
        {
            CompactionPercentage = compactionPercentage
        };

        // Assert
        Assert.Equal(compactionPercentage, options.CompactionPercentage);
    }

    [Theory]
    [InlineData(30)]
    [InlineData(60)]
    [InlineData(120)]
    public void MemoryCacheOptions_SetScanFrequency_StoresValue(int seconds)
    {
        // Arrange & Act
        var options = new MemoryCacheOptions
        {
            ExpirationScanFrequencySeconds = seconds
        };

        // Assert
        Assert.Equal(seconds, options.ExpirationScanFrequencySeconds);
    }
}

[Trait("Category", TestCategories.Unit)]
public class RedisRetryOptionsTests
{
    [Fact]
    public void RedisRetryOptions_DefaultConstructor_SetsDefaultValues()
    {
        // Act
        var options = new RedisRetryOptions();

        // Assert
        Assert.True(options.Enabled);
        Assert.Equal(3, options.MaxRetryAttempts);
        Assert.Equal(1000, options.BaseDelayMs);
        Assert.Equal(5000, options.MaxDelayMs);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void RedisRetryOptions_SetEnabled_StoresValue(bool enabled)
    {
        // Arrange & Act
        var options = new RedisRetryOptions
        {
            Enabled = enabled
        };

        // Assert
        Assert.Equal(enabled, options.Enabled);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(5)]
    [InlineData(10)]
    public void RedisRetryOptions_SetMaxRetryAttempts_StoresValue(int maxRetryAttempts)
    {
        // Arrange & Act
        var options = new RedisRetryOptions
        {
            MaxRetryAttempts = maxRetryAttempts
        };

        // Assert
        Assert.Equal(maxRetryAttempts, options.MaxRetryAttempts);
    }

    [Theory]
    [InlineData(500, 5000)]
    [InlineData(1000, 10000)]
    [InlineData(2000, 20000)]
    public void RedisRetryOptions_SetDelays_StoresValues(int baseDelayMs, int maxDelayMs)
    {
        // Arrange & Act
        var options = new RedisRetryOptions
        {
            BaseDelayMs = baseDelayMs,
            MaxDelayMs = maxDelayMs
        };

        // Assert
        Assert.Equal(baseDelayMs, options.BaseDelayMs);
        Assert.Equal(maxDelayMs, options.MaxDelayMs);
    }

    [Fact]
    public void RedisRetryOptions_CompleteConfiguration_AllPropertiesSet()
    {
        // Arrange & Act
        var options = new RedisRetryOptions
        {
            Enabled = false,
            MaxRetryAttempts = 7,
            BaseDelayMs = 2000,
            MaxDelayMs = 30000
        };

        // Assert
        Assert.False(options.Enabled);
        Assert.Equal(7, options.MaxRetryAttempts);
        Assert.Equal(2000, options.BaseDelayMs);
        Assert.Equal(30000, options.MaxDelayMs);
    }
}
