using Microsoft.Extensions.Configuration;
using Xunit;
using Moq;
using FluentAssertions;
using Sxg.EvalPlatform.API.Storage;

namespace Sxg.EvalPlatform.API.Storage.UnitTests.ConfigurationTests
{
    /// <summary>
    /// Unit tests for ConfigHelper class covering all configuration retrieval methods
    /// </summary>
    [Trait("Category", TestCategories.Unit)]
    [Trait("Category", TestCategories.Configuration)]
    public class ConfigHelperTests
    {
        private readonly Dictionary<string, string?> _configurationData;

        public ConfigHelperTests()
        {
            _configurationData = new Dictionary<string, string?>
            {
                { "AzureStorage:AccountName", "teststorageaccount" },
                { "AzureStorage:PlatformConfigurationsContainer", "platformconfigs" },
                { "AzureStorage:DefaultMetricsConfiguration", "defaultmetrics.json" },
                { "AzureStorage:DefaultConfigurationBlob", "defaultconfig.json" },
                { "AzureStorage:MetricsConfigurationsTable", "MetricsConfigurations" },
                { "AzureStorage:DataSetsTable", "DataSets" },
                { "AzureStorage:DataSetFolderName", "datasets" },
                { "AzureStorage:DatasetsFolderName", "datasets" },
                { "AzureStorage:EvalResultsFolderName", "evalresults" },
                { "AzureStorage:MetricsConfigurationsFolderName", "metricsconfigs" },
                { "AzureStorage:DatasetEnrichmentRequestsQueueName", "dataset-enrichment-requests" },
                { "AzureStorage:EvalProcessingRequestsQueueName", "eval-processing-requests" },
                { "AzureStorage:EvalRunsTable", "EvalRuns" },
                { "Telemetry:AppInsightsConnectionString", "InstrumentationKey=test-key" },
                { "DataVerseAPI:DatasetEnrichmentRequestAPIEndPoint", "https://api.example.com/enrichment" },
                { "DataVerseAPI:Scope", "https://api.example.com/.default" },
                { "Cache:Provider", "Memory" },
                { "Cache:Redis:Endpoint", "redis.example.com:6380" },
                { "Cache:DefaultExpirationMinutes", "30" },
                { "ASPNETCORE_ENVIRONMENT", "Development" },
                { "FeatureFlags:EnablePublishingEvalResultsToDataPlatform", "true" },
                { "ManagedIdentity:ClientId", "test-client-id-123" }
            };
        }

        private IConfigHelper CreateConfigHelper(Dictionary<string, string?>? customConfig = null)
        {
            var configData = customConfig ?? _configurationData;
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(configData!)
                .Build();

            return new ConfigHelper(configuration);
        }

        #region Azure Storage Configuration Tests

        [Fact]
        public void GetAzureStorageAccountName_WithValidConfiguration_ReturnsAccountName()
        {
            // Arrange
            var configHelper = CreateConfigHelper();

            // Act
            var accountName = configHelper.GetAzureStorageAccountName();

            // Assert
            accountName.Should().Be("teststorageaccount");
        }

        [Fact]
        public void GetAzureStorageAccountName_WithMissingConfiguration_ThrowsInvalidOperationException()
        {
            // Arrange
            var config = new Dictionary<string, string?> { { "AzureStorage:AccountName", null } };
            var configHelper = CreateConfigHelper(config);

            // Act & Assert
            var exception = Assert.Throws<InvalidOperationException>(() => 
                configHelper.GetAzureStorageAccountName());
            exception.Message.Should().Contain("Azure Storage connection string is not configured");
        }

        [Fact]
        public void GetPlatformConfigurationsContainer_WithValidConfiguration_ReturnsContainerName()
        {
            // Arrange
            var configHelper = CreateConfigHelper();

            // Act
            var containerName = configHelper.GetPlatformConfigurationsContainer();

            // Assert
            containerName.Should().Be("platformconfigs");
        }

        [Fact]
        public void GetPlatformConfigurationsContainer_WithMissingConfiguration_ThrowsInvalidOperationException()
        {
            // Arrange
            var config = new Dictionary<string, string?> { { "AzureStorage:PlatformConfigurationsContainer", null } };
            var configHelper = CreateConfigHelper(config);

            // Act & Assert
            var exception = Assert.Throws<InvalidOperationException>(() => 
                configHelper.GetPlatformConfigurationsContainer());
            exception.Message.Should().Contain("Platform Configurations container name is not configured");
        }

        [Fact]
        public void GetDefaultMetricsConfiguration_WithValidConfiguration_ReturnsConfigName()
        {
            // Arrange
            var configHelper = CreateConfigHelper();

            // Act
            var configName = configHelper.GetDefaultMetricsConfiguration();

            // Assert
            configName.Should().Be("defaultmetrics.json");
        }

        [Fact]
        public void GetDefaultConfigurationBlob_WithValidConfiguration_ReturnsBlobName()
        {
            // Arrange
            var configHelper = CreateConfigHelper();

            // Act
            var blobName = configHelper.GetDefaultConfigurationBlob();

            // Assert
            blobName.Should().Be("defaultconfig.json");
        }

        [Fact]
        public void GetMetricsConfigurationsTable_WithValidConfiguration_ReturnsTableName()
        {
            // Arrange
            var configHelper = CreateConfigHelper();

            // Act
            var tableName = configHelper.GetMetricsConfigurationsTable();

            // Assert
            tableName.Should().Be("MetricsConfigurations");
        }

        [Fact]
        public void GetDataSetsTable_WithValidConfiguration_ReturnsTableName()
        {
            // Arrange
            var configHelper = CreateConfigHelper();

            // Act
            var tableName = configHelper.GetDataSetsTable();

            // Assert
            tableName.Should().Be("DataSets");
        }

        [Fact]
        public void GetDataSetFolderName_WithValidConfiguration_ReturnsFolderName()
        {
            // Arrange
            var configHelper = CreateConfigHelper();

            // Act
            var folderName = configHelper.GetDataSetFolderName();

            // Assert
            folderName.Should().Be("datasets");
        }

        [Fact]
        public void GetDatasetsFolderName_WithValidConfiguration_ReturnsFolderName()
        {
            // Arrange
            var configHelper = CreateConfigHelper();

            // Act
            var folderName = configHelper.GetDatasetsFolderName();

            // Assert
            folderName.Should().Be("datasets");
        }

        [Fact]
        public void GetDatasetsFolderName_WithMissingConfiguration_ReturnsDefaultValue()
        {
            // Arrange
            var config = new Dictionary<string, string?> { { "AzureStorage:DatasetsFolderName", null } };
            var configHelper = CreateConfigHelper(config);

            // Act
            var folderName = configHelper.GetDatasetsFolderName();

            // Assert
            folderName.Should().Be("datasets");
        }

        [Fact]
        public void EvalResultsFolderName_WithValidConfiguration_ReturnsFolderName()
        {
            // Arrange
            var configHelper = CreateConfigHelper();

            // Act
            var folderName = configHelper.EvalResultsFolderName();

            // Assert
            folderName.Should().Be("evalresults");
        }

        [Fact]
        public void MetricsConfigurationsFolderName_WithValidConfiguration_ReturnsFolderName()
        {
            // Arrange
            var configHelper = CreateConfigHelper();

            // Act
            var folderName = configHelper.MetricsConfigurationsFolderName();

            // Assert
            folderName.Should().Be("metricsconfigs");
        }

        [Fact]
        public void GetMetricsConfigurationsFolderName_WithValidConfiguration_ReturnsFolderName()
        {
            // Arrange
            var configHelper = CreateConfigHelper();

            // Act
            var folderName = configHelper.GetMetricsConfigurationsFolderName();

            // Assert
            folderName.Should().Be("metricsconfigs");
        }

        #endregion

        #region Queue Configuration Tests

        [Fact]
        public void GetDatasetEnrichmentRequestsQueueName_WithValidConfiguration_ReturnsQueueName()
        {
            // Arrange
            var configHelper = CreateConfigHelper();

            // Act
            var queueName = configHelper.GetDatasetEnrichmentRequestsQueueName();

            // Assert
            queueName.Should().Be("dataset-enrichment-requests");
        }

        [Fact]
        public void GetDatasetEnrichmentRequestsQueueName_WithMissingConfiguration_ThrowsInvalidOperationException()
        {
            // Arrange
            var config = new Dictionary<string, string?> { { "AzureStorage:DatasetEnrichmentRequestsQueueName", null } };
            var configHelper = CreateConfigHelper(config);

            // Act & Assert
            var exception = Assert.Throws<InvalidOperationException>(() => 
                configHelper.GetDatasetEnrichmentRequestsQueueName());
            exception.Message.Should().Contain("Dataset Enrichment Requests queue name is not configured");
        }

        [Fact]
        public void GetEvalProcessingRequestsQueueName_WithValidConfiguration_ReturnsQueueName()
        {
            // Arrange
            var configHelper = CreateConfigHelper();

            // Act
            var queueName = configHelper.GetEvalProcessingRequestsQueueName();

            // Assert
            queueName.Should().Be("eval-processing-requests");
        }

        #endregion

        #region Telemetry Configuration Tests

        [Fact]
        public void AppInsightsConnectionString_WithValidConfiguration_ReturnsConnectionString()
        {
            // Arrange
            var configHelper = CreateConfigHelper();

            // Act
            var connectionString = configHelper.AppInsightsConnectionString();

            // Assert
            connectionString.Should().Be("InstrumentationKey=test-key");
        }

        [Fact]
        public void AppInsightsConnectionString_WithMissingConfiguration_ThrowsInvalidOperationException()
        {
            // Arrange
            var config = new Dictionary<string, string?> { { "Telemetry:AppInsightsConnectionString", null } };
            var configHelper = CreateConfigHelper(config);

            // Act & Assert
            var exception = Assert.Throws<InvalidOperationException>(() => 
                configHelper.AppInsightsConnectionString());
            exception.Message.Should().Contain("Application Insights connection string is not configured");
        }

        #endregion

        #region DataVerse API Configuration Tests

        [Fact]
        public void GetDatasetEnrichmentRequestAPIEndPoint_WithValidConfiguration_ReturnsEndpoint()
        {
            // Arrange
            var configHelper = CreateConfigHelper();

            // Act
            var endpoint = configHelper.GetDatasetEnrichmentRequestAPIEndPoint();

            // Assert
            endpoint.Should().Be("https://api.example.com/enrichment");
        }

        [Fact]
        public void GetDataVerseAPIScope_WithValidConfiguration_ReturnsScope()
        {
            // Arrange
            var configHelper = CreateConfigHelper();

            // Act
            var scope = configHelper.GetDataVerseAPIScope();

            // Assert
            scope.Should().Be("https://api.example.com/.default");
        }

        #endregion

        #region Cache Configuration Tests

        [Fact]
        public void GetCacheProvider_WithValidConfiguration_ReturnsProvider()
        {
            // Arrange
            var configHelper = CreateConfigHelper();

            // Act
            var provider = configHelper.GetCacheProvider();

            // Assert
            provider.Should().Be("Memory");
        }

        [Fact]
        public void GetCacheProvider_WithMissingConfiguration_ReturnsDefaultMemory()
        {
            // Arrange
            var config = new Dictionary<string, string?> { { "Cache:Provider", null } };
            var configHelper = CreateConfigHelper(config);

            // Act
            var provider = configHelper.GetCacheProvider();

            // Assert
            provider.Should().Be("Memory");
        }

        [Fact]
        public void GetRedisCacheEndpoint_WithValidConfiguration_ReturnsEndpoint()
        {
            // Arrange
            var configHelper = CreateConfigHelper();

            // Act
            var endpoint = configHelper.GetRedisCacheEndpoint();

            // Assert
            endpoint.Should().Be("redis.example.com:6380");
        }

        [Fact]
        public void GetRedisCacheEndpoint_WithMissingConfiguration_ReturnsNull()
        {
            // Arrange
            var config = new Dictionary<string, string?> { { "Cache:Redis:Endpoint", null } };
            var configHelper = CreateConfigHelper(config);

            // Act
            var endpoint = configHelper.GetRedisCacheEndpoint();

            // Assert
            endpoint.Should().BeNull();
        }

        [Fact]
        public void GetDefaultCacheExpiration_WithValidConfiguration_ReturnsTimeSpan()
        {
            // Arrange
            var configHelper = CreateConfigHelper();

            // Act
            var expiration = configHelper.GetDefaultCacheExpiration();

            // Assert
            expiration.Should().Be(TimeSpan.FromMinutes(30));
        }

        [Fact]
        public void GetDefaultCacheExpiration_WithMissingConfiguration_ReturnsDefaultValue()
        {
            // Arrange
            var config = new Dictionary<string, string?> { { "Cache:DefaultExpirationMinutes", null } };
            var configHelper = CreateConfigHelper(config);

            // Act
            var expiration = configHelper.GetDefaultCacheExpiration();

            // Assert
            expiration.Should().Be(TimeSpan.FromMinutes(30));
        }

        [Fact]
        public void IsDistributedCacheEnabled_WithRedisProvider_ReturnsTrue()
        {
            // Arrange
            var config = new Dictionary<string, string?> { { "Cache:Provider", "Redis" } };
            var configHelper = CreateConfigHelper(config);

            // Act
            var isEnabled = configHelper.IsDistributedCacheEnabled();

            // Assert
            isEnabled.Should().BeTrue();
        }

        [Fact]
        public void IsDistributedCacheEnabled_WithMemoryProvider_ReturnsFalse()
        {
            // Arrange
            var config = new Dictionary<string, string?> { { "Cache:Provider", "Memory" } };
            var configHelper = CreateConfigHelper(config);

            // Act
            var isEnabled = configHelper.IsDistributedCacheEnabled();

            // Assert
            isEnabled.Should().BeFalse();
        }

        [Theory]
        [InlineData("Memory", true)]
        [InlineData("Redis", true)]
        [InlineData("None", false)]
        [InlineData("Disabled", false)]
        [InlineData("", false)]
        public void IsCachingEnabled_WithVariousProviders_ReturnsExpectedResult(string provider, bool expected)
        {
            // Arrange
            var config = new Dictionary<string, string?> { { "Cache:Provider", provider } };
            var configHelper = CreateConfigHelper(config);

            // Act
            var isEnabled = configHelper.IsCachingEnabled();

            // Assert
            isEnabled.Should().Be(expected);
        }

        #endregion

        #region Environment Configuration Tests

        [Fact]
        public void GetASPNetCoreEnvironment_WithValidConfiguration_ReturnsEnvironment()
        {
            // Arrange
            var configHelper = CreateConfigHelper();

            // Act
            var environment = configHelper.GetASPNetCoreEnvironment();

            // Assert
            environment.Should().Be("Development");
        }

        [Fact]
        public void GetASPNetCoreEnvironment_WithMissingConfiguration_ReturnsProduction()
        {
            // Arrange
            var config = new Dictionary<string, string?> { { "ASPNETCORE_ENVIRONMENT", null } };
            var configHelper = CreateConfigHelper(config);

            // Act
            var environment = configHelper.GetASPNetCoreEnvironment();

            // Assert
            environment.Should().Be("Production");
        }

        #endregion

        #region Table Configuration Tests

        [Fact]
        public void GetEvalRunTableName_WithValidConfiguration_ReturnsTableName()
        {
            // Arrange
            var configHelper = CreateConfigHelper();

            // Act
            var tableName = configHelper.GetEvalRunTableName();

            // Assert
            tableName.Should().Be("EvalRuns");
        }

        [Fact]
        public void GetEvalRunTableName_WithMissingConfiguration_ThrowsInvalidOperationException()
        {
            // Arrange
            var config = new Dictionary<string, string?> { { "AzureStorage:EvalRunsTable", null } };
            var configHelper = CreateConfigHelper(config);

            // Act & Assert
            var exception = Assert.Throws<InvalidOperationException>(() => 
                configHelper.GetEvalRunTableName());
            exception.Message.Should().Contain("Eval Runs table name is not configured");
        }

        #endregion

        #region Feature Flags Tests

        [Fact]
        public void GetEnablePublishingEvalResultsToDataPlatform_WithTrueValue_ReturnsTrue()
        {
            // Arrange
            var configHelper = CreateConfigHelper();

            // Act
            var isEnabled = configHelper.GetEnablePublishingEvalResultsToDataPlatform();

            // Assert
            isEnabled.Should().BeTrue();
        }

        [Fact]
        public void GetEnablePublishingEvalResultsToDataPlatform_WithMissingConfiguration_ReturnsDefaultTrue()
        {
            // Arrange
            var config = new Dictionary<string, string?> 
            { 
                { "FeatureFlags:EnablePublishingEvalResultsToDataPlatform", null } 
            };
            var configHelper = CreateConfigHelper(config);

            // Act
            var isEnabled = configHelper.GetEnablePublishingEvalResultsToDataPlatform();

            // Assert
            isEnabled.Should().BeTrue();
        }

        [Fact]
        public void GetEnablePublishingEvalResultsToDataPlatform_WithFalseValue_ReturnsFalse()
        {
            // Arrange
            var config = new Dictionary<string, string?> 
            { 
                { "FeatureFlags:EnablePublishingEvalResultsToDataPlatform", "false" } 
            };
            var configHelper = CreateConfigHelper(config);

            // Act
            var isEnabled = configHelper.GetEnablePublishingEvalResultsToDataPlatform();

            // Assert
            isEnabled.Should().BeFalse();
        }

        #endregion

        #region Managed Identity Configuration Tests

        [Fact]
        public void GetManagedIdentityClientId_WithValidConfiguration_ReturnsClientId()
        {
            // Arrange
            var configHelper = CreateConfigHelper();

            // Act
            var clientId = configHelper.GetManagedIdentityClientId();

            // Assert
            clientId.Should().Be("test-client-id-123");
        }

        [Fact]
        public void GetManagedIdentityClientId_WithMissingConfiguration_ReturnsNull()
        {
            // Arrange
            var config = new Dictionary<string, string?> { { "ManagedIdentity:ClientId", null } };
            var configHelper = CreateConfigHelper(config);

            // Act
            var clientId = configHelper.GetManagedIdentityClientId();

            // Assert
            clientId.Should().BeNull();
        }

        #endregion

        #region Generic Configuration Section Tests

        [Fact]
        public void GetConfigurationSection_WithValidSection_ReturnsBindedObject()
        {
            // Arrange
            var config = new Dictionary<string, string?>
            {
                { "TestSection:Property1", "Value1" },
                { "TestSection:Property2", "123" }
            };
            var configHelper = CreateConfigHelper(config);

            // Act
            var section = configHelper.GetConfigurationSection<TestConfigSection>("TestSection");

            // Assert
            section.Should().NotBeNull();
            section.Property1.Should().Be("Value1");
            section.Property2.Should().Be(123);
        }

        [Fact]
        public void GetConfigurationSection_WithMissingSection_ReturnsDefaultObject()
        {
            // Arrange
            var config = new Dictionary<string, string?>();
            var configHelper = CreateConfigHelper(config);

            // Act
            var section = configHelper.GetConfigurationSection<TestConfigSection>("NonExistentSection");

            // Assert
            section.Should().NotBeNull();
            section.Property1.Should().BeNull();
            section.Property2.Should().Be(0);
        }

        // Test configuration class for generic section testing
        private class TestConfigSection
        {
            public string? Property1 { get; set; }
            public int Property2 { get; set; }
        }

        #endregion

        #region Edge Cases and Error Scenarios

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        public void GetAzureStorageAccountName_WithEmptyOrWhitespace_ThrowsInvalidOperationException(string value)
        {
            // Arrange
            var config = new Dictionary<string, string?> { { "AzureStorage:AccountName", value } };
            var configHelper = CreateConfigHelper(config);

            // Act & Assert
            var exception = Assert.Throws<InvalidOperationException>(() => 
                configHelper.GetAzureStorageAccountName());
            exception.Message.Should().Contain("Azure Storage connection string is not configured");
        }

        [Fact]
        public void GetDefaultCacheExpiration_WithInvalidNumber_ReturnsDefaultValue()
        {
            // Arrange
            var config = new Dictionary<string, string?> { { "Cache:DefaultExpirationMinutes", "invalid" } };
            var configHelper = CreateConfigHelper(config);

            // Act
            var expiration = configHelper.GetDefaultCacheExpiration();

            // Assert
            expiration.Should().Be(TimeSpan.FromMinutes(30));
        }

        [Theory]
        [InlineData("redis")]
        [InlineData("REDIS")]
        [InlineData("ReDiS")]
        public void IsDistributedCacheEnabled_WithCaseInsensitiveRedis_ReturnsTrue(string provider)
        {
            // Arrange
            var config = new Dictionary<string, string?> { { "Cache:Provider", provider } };
            var configHelper = CreateConfigHelper(config);

            // Act
            var isEnabled = configHelper.IsDistributedCacheEnabled();

            // Assert
            isEnabled.Should().BeTrue();
        }

        #endregion
    }
}
