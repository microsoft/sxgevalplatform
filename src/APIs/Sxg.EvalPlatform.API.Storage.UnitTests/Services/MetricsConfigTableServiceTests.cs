using Microsoft.Extensions.Logging;
using Moq;
using Sxg.EvalPlatform.API.Storage.Services;
using Sxg.EvalPlatform.API.Storage.TableEntities;

namespace Sxg.EvalPlatform.API.Storage.UnitTests.Services;

[Trait("Category", TestCategories.Unit)]
public class MetricsConfigTableServiceTests
{
    private readonly Mock<IConfigHelper> _mockConfigHelper;
    private readonly Mock<ILogger<MetricsConfigTableService>> _mockLogger;
    private readonly Mock<ICacheManager> _mockCacheManager;

    public MetricsConfigTableServiceTests()
    {
        _mockConfigHelper = new Mock<IConfigHelper>();
        _mockLogger = new Mock<ILogger<MetricsConfigTableService>>();
        _mockCacheManager = new Mock<ICacheManager>();

        // Setup default config helper responses
        _mockConfigHelper.Setup(x => x.GetAzureStorageAccountName()).Returns("teststorageaccount");
        _mockConfigHelper.Setup(x => x.GetMetricsConfigurationsTable()).Returns("MetricsConfig");
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
            new MetricsConfigTableService(_mockConfigHelper.Object, _mockLogger.Object, _mockCacheManager.Object));

        Assert.Contains("Azure Storage account name is not configured", exception.Message);
    }

    [Fact]
    public void Constructor_WithValidConfig_CreatesInstance()
    {
        // Act
        var service = new MetricsConfigTableService(
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
            new MetricsConfigTableService(_mockConfigHelper.Object, _mockLogger.Object, _mockCacheManager.Object));

        Assert.Contains("Azure Storage account name is not configured", exception.Message);
    }

    [Fact]
    public async Task GetMetricsConfigurationByConfigurationIdAsync_WithCachedData_ReturnsCachedValue()
    {
        // Arrange
        var configId = Guid.NewGuid().ToString();
        var expectedEntity = new MetricsConfigurationTableEntity
        {
            AgentId = "test-agent",
            ConfigurationId = configId,
            ConfigurationName = "TestConfig",
            EnvironmentName = "Dev"
        };

        _mockCacheManager
            .Setup(x => x.GetAsync<MetricsConfigurationTableEntity>(
                It.IsAny<string>(), 
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedEntity);

        var service = new MetricsConfigTableService(
            _mockConfigHelper.Object,
            _mockLogger.Object,
            _mockCacheManager.Object);

        // Act
        var result = await service.GetMetricsConfigurationByConfigurationIdAsync(configId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(configId, result.ConfigurationId);
        Assert.Equal("test-agent", result.AgentId);
        
        // Verify cache was checked
        _mockCacheManager.Verify(
            x => x.GetAsync<MetricsConfigurationTableEntity>(
                It.IsAny<string>(), 
                It.IsAny<CancellationToken>()), 
            Times.Once);
    }

    [Fact]
    public async Task GetAllMetricsConfigurations_WithCachedData_ReturnsCachedList()
    {
        // Arrange
        var agentId = "test-agent";
        var environment = "Dev";
        var expectedList = new List<MetricsConfigurationTableEntity>
        {
            new MetricsConfigurationTableEntity
            {
                AgentId = agentId,
                ConfigurationId = Guid.NewGuid().ToString(),
                ConfigurationName = "Config1",
                EnvironmentName = environment
            },
            new MetricsConfigurationTableEntity
            {
                AgentId = agentId,
                ConfigurationId = Guid.NewGuid().ToString(),
                ConfigurationName = "Config2",
                EnvironmentName = environment
            }
        };

        _mockCacheManager
            .Setup(x => x.GetAsync<List<MetricsConfigurationTableEntity>>(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedList);

        var service = new MetricsConfigTableService(
            _mockConfigHelper.Object,
            _mockLogger.Object,
            _mockCacheManager.Object);

        // Act
        var result = await service.GetAllMetricsConfigurations(agentId, environment);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.Count);
        Assert.All(result, item => Assert.Equal(agentId, item.AgentId));

        // Verify cache was checked
        _mockCacheManager.Verify(
            x => x.GetAsync<List<MetricsConfigurationTableEntity>>(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetAllMetricsConfigurations_WithConfigNameAndEnvironment_ChecksCacheCorrectly()
    {
        // Arrange
        var agentId = "test-agent";
        var configName = "TestConfig";
        var environment = "Dev";
        var expectedList = new List<MetricsConfigurationTableEntity>
        {
            new MetricsConfigurationTableEntity
            {
                AgentId = agentId,
                ConfigurationId = Guid.NewGuid().ToString(),
                ConfigurationName = configName,
                EnvironmentName = environment
            }
        };

        _mockCacheManager
            .Setup(x => x.GetAsync<List<MetricsConfigurationTableEntity>>(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedList);

        var service = new MetricsConfigTableService(
            _mockConfigHelper.Object,
            _mockLogger.Object,
            _mockCacheManager.Object);

        // Act
        var result = await service.GetAllMetricsConfigurations(agentId, configName, environment);

        // Assert
        Assert.NotNull(result);
        Assert.Single(result);
        Assert.Equal(configName, result[0].ConfigurationName);
        Assert.Equal(environment, result[0].EnvironmentName);
    }
}
