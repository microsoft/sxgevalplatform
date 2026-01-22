using Xunit;
using FluentAssertions;
using Sxg.EvalPlatform.API.Storage.Helpers;
using Sxg.EvalPlatform.API.Storage.TableEntities;

namespace Sxg.EvalPlatform.API.Storage.UnitTests.HelperTests
{
    /// <summary>
    /// Unit tests for MetricsConfigurationEntityHelper
    /// </summary>
    [Trait("Category", TestCategories.Unit)]
    [Trait("Category", TestCategories.Helper)]
    public class MetricsConfigurationEntityHelperTests
    {
        #region CreateEntity Tests

        [Fact]
        public void CreateEntity_WithoutConfigurationId_CreatesEntityWithGeneratedId()
        {
            // Arrange
            var agentId = TestConstants.Agents.DefaultAgentId;
            var configurationName = TestConstants.MetricsConfigs.ConfigName;
            var environmentName = TestConstants.Config.DevelopmentEnvironment;

            // Act
            var entity = MetricsConfigurationEntityHelper.CreateEntity(
                agentId, configurationName, environmentName);

            // Assert
            entity.Should().NotBeNull();
            entity.AgentId.Should().Be(agentId);
            entity.PartitionKey.Should().Be(agentId);
            entity.ConfigurationName.Should().Be(configurationName);
            entity.EnvironmentName.Should().Be(environmentName);
            entity.ConfigurationId.Should().NotBeNullOrEmpty();
            entity.RowKey.Should().Be(entity.ConfigurationId);
            Guid.TryParse(entity.ConfigurationId, out _).Should().BeTrue();
        }

        [Fact]
        public void CreateEntity_WithConfigurationId_CreatesEntityWithSpecificId()
        {
            // Arrange
            var agentId = TestConstants.Agents.DefaultAgentId;
            var configurationName = TestConstants.MetricsConfigs.ConfigName;
            var environmentName = TestConstants.Config.DevelopmentEnvironment;
            var configurationId = Guid.NewGuid().ToString();

            // Act
            var entity = MetricsConfigurationEntityHelper.CreateEntity(
                agentId, configurationName, environmentName, configurationId);

            // Assert
            entity.Should().NotBeNull();
            entity.AgentId.Should().Be(agentId);
            entity.ConfigurationId.Should().Be(configurationId);
            entity.PartitionKey.Should().Be(agentId);
            entity.RowKey.Should().Be(configurationId);
            entity.ConfigurationName.Should().Be(configurationName);
            entity.EnvironmentName.Should().Be(environmentName);
        }

        [Fact]
        public void CreateEntity_SetsLastUpdatedOnToCurrentTime()
        {
            // Arrange
            var agentId = TestConstants.Agents.DefaultAgentId;
            var configurationName = TestConstants.MetricsConfigs.ConfigName;
            var environmentName = TestConstants.Config.DevelopmentEnvironment;
            var beforeCreation = DateTime.UtcNow;

            // Act
            var entity = MetricsConfigurationEntityHelper.CreateEntity(
                agentId, configurationName, environmentName);
            var afterCreation = DateTime.UtcNow;

            // Assert
            entity.LastUpdatedOn.Should().NotBeNull();
            entity.LastUpdatedOn!.Value.Should().BeOnOrAfter(beforeCreation);
            entity.LastUpdatedOn.Value.Should().BeOnOrBefore(afterCreation);
        }

        [Theory]
        [InlineData("Development")]
        [InlineData("Staging")]
        [InlineData("Production")]
        [InlineData("Test")]
        public void CreateEntity_WithDifferentEnvironments_CreatesEntityCorrectly(string environmentName)
        {
            // Arrange
            var agentId = TestConstants.Agents.DefaultAgentId;
            var configurationName = "TestConfig";

            // Act
            var entity = MetricsConfigurationEntityHelper.CreateEntity(
                agentId, configurationName, environmentName);

            // Assert
            entity.EnvironmentName.Should().Be(environmentName);
        }

        #endregion

        #region GetPartitionKey Tests

        [Fact]
        public void GetPartitionKey_WithValidAgentId_ReturnsAgentId()
        {
            // Arrange
            var agentId = TestConstants.Agents.DefaultAgentId;

            // Act
            var partitionKey = MetricsConfigurationEntityHelper.GetPartitionKey(agentId);

            // Assert
            partitionKey.Should().Be(agentId);
        }

        [Theory]
        [InlineData("agent-123")]
        [InlineData("my-agent")]
        [InlineData("test_agent")]
        public void GetPartitionKey_WithVariousAgentIds_ReturnsCorrectKey(string agentId)
        {
            // Act
            var partitionKey = MetricsConfigurationEntityHelper.GetPartitionKey(agentId);

            // Assert
            partitionKey.Should().Be(agentId);
        }

        #endregion

        #region GetRowKey Tests

        [Fact]
        public void GetRowKey_WithValidConfigurationId_ReturnsConfigurationId()
        {
            // Arrange
            var configurationId = Guid.NewGuid().ToString();

            // Act
            var rowKey = MetricsConfigurationEntityHelper.GetRowKey(configurationId);

            // Assert
            rowKey.Should().Be(configurationId);
        }

        [Fact]
        public void GetRowKey_ReturnsInputUnmodified()
        {
            // Arrange
            var configurationId = "custom-config-id";

            // Act
            var rowKey = MetricsConfigurationEntityHelper.GetRowKey(configurationId);

            // Assert
            rowKey.Should().Be(configurationId);
        }

        #endregion

        #region GenerateNewConfigurationId Tests

        [Fact]
        public void GenerateNewConfigurationId_ReturnsValidGuid()
        {
            // Act
            var configurationId = MetricsConfigurationEntityHelper.GenerateNewConfigurationId();

            // Assert
            Guid.TryParse(configurationId, out _).Should().BeTrue();
        }

        [Fact]
        public void GenerateNewConfigurationId_CalledMultipleTimes_ReturnsDifferentGuids()
        {
            // Act
            var id1 = MetricsConfigurationEntityHelper.GenerateNewConfigurationId();
            var id2 = MetricsConfigurationEntityHelper.GenerateNewConfigurationId();
            var id3 = MetricsConfigurationEntityHelper.GenerateNewConfigurationId();

            // Assert
            id1.Should().NotBe(id2);
            id2.Should().NotBe(id3);
            id1.Should().NotBe(id3);
        }

        [Fact]
        public void GenerateNewConfigurationId_ReturnsNonEmptyString()
        {
            // Act
            var configurationId = MetricsConfigurationEntityHelper.GenerateNewConfigurationId();

            // Assert
            configurationId.Should().NotBeNullOrEmpty();
        }

        #endregion

        #region ValidateKeys Tests

        [Fact]
        public void ValidateKeys_WithValidEntity_ReturnsTrue()
        {
            // Arrange
            var entity = MetricsConfigurationEntityHelper.CreateEntity(
                TestConstants.Agents.DefaultAgentId,
                "TestConfig",
                "Development",
                Guid.NewGuid().ToString());

            // Act
            var isValid = MetricsConfigurationEntityHelper.ValidateKeys(entity);

            // Assert
            isValid.Should().BeTrue();
        }

        [Fact]
        public void ValidateKeys_WithMissingPartitionKey_ReturnsFalse()
        {
            // Arrange
            var configurationId = Guid.NewGuid().ToString();
            var entity = new MetricsConfigurationTableEntity
            {
                AgentId = TestConstants.Agents.DefaultAgentId, // This sets PartitionKey automatically
                ConfigurationId = configurationId
            };
            // Manually set PartitionKey to null after it was auto-set
            entity.PartitionKey = null;

            // Act
            var isValid = MetricsConfigurationEntityHelper.ValidateKeys(entity);

            // Assert
            isValid.Should().BeFalse();
        }

        [Fact]
        public void ValidateKeys_WithEmptyRowKey_ReturnsFalse()
        {
            // Arrange
            var configId = Guid.NewGuid().ToString();
            var entity = new MetricsConfigurationTableEntity
            {
                AgentId = TestConstants.Agents.DefaultAgentId,
                ConfigurationId = configId
            };
            // Manually override RowKey to empty string after it was auto-set
            entity.RowKey = string.Empty;

            // Act
            var isValid = MetricsConfigurationEntityHelper.ValidateKeys(entity);

            // Assert
            isValid.Should().BeFalse();
        }

        [Fact]
        public void ValidateKeys_WithMismatchedPartitionKeyAndAgentId_ReturnsFalse()
        {
            // Arrange
            var configurationId = Guid.NewGuid().ToString();
            var entity = new MetricsConfigurationTableEntity
            {
                AgentId = TestConstants.Agents.DefaultAgentId, // This sets PartitionKey automatically
                ConfigurationId = configurationId
            };
            // Manually override PartitionKey after it was auto-set to create mismatch
            entity.PartitionKey = "different-agent";

            // Act
            var isValid = MetricsConfigurationEntityHelper.ValidateKeys(entity);

            // Assert
            isValid.Should().BeFalse();
        }

        [Fact]
        public void ValidateKeys_WithMismatchedRowKeyAndConfigurationId_ReturnsFalse()
        {
            // Arrange
            var configId = Guid.NewGuid().ToString();
            var entity = new MetricsConfigurationTableEntity
            {
                AgentId = TestConstants.Agents.DefaultAgentId,
                ConfigurationId = configId  // This sets RowKey automatically
            };
            // Manually override RowKey after it was auto-set to create mismatch
            entity.RowKey = "different-key";

            // Act
            var isValid = MetricsConfigurationEntityHelper.ValidateKeys(entity);

            // Assert
            isValid.Should().BeFalse();
        }

        [Fact]
        public void ValidateKeys_WithInvalidGuidInConfigurationId_ReturnsFalse()
        {
            // Arrange
            var entity = new MetricsConfigurationTableEntity
            {
                PartitionKey = TestConstants.Agents.DefaultAgentId,
                RowKey = "not-a-guid",
                ConfigurationId = "not-a-guid",
                AgentId = TestConstants.Agents.DefaultAgentId
            };

            // Act
            var isValid = MetricsConfigurationEntityHelper.ValidateKeys(entity);

            // Assert
            isValid.Should().BeFalse();
        }

        #endregion

        #region IsValidGuid Tests

        [Fact]
        public void IsValidGuid_WithValidGuid_ReturnsTrue()
        {
            // Arrange
            var validGuid = Guid.NewGuid().ToString();

            // Act
            var isValid = MetricsConfigurationEntityHelper.IsValidGuid(validGuid);

            // Assert
            isValid.Should().BeTrue();
        }

        [Theory]
        [InlineData("not-a-guid")]
        [InlineData("12345")]
        [InlineData("")]
        [InlineData("abc-def-ghi")]
        public void IsValidGuid_WithInvalidGuid_ReturnsFalse(string invalidGuid)
        {
            // Act
            var isValid = MetricsConfigurationEntityHelper.IsValidGuid(invalidGuid);

            // Assert
            isValid.Should().BeFalse();
        }

        [Fact]
        public void IsValidGuid_WithGuidInDifferentFormats_ReturnsTrue()
        {
            // Arrange
            var guid = Guid.NewGuid();
            var formats = new[]
            {
                guid.ToString("D"), // 00000000-0000-0000-0000-000000000000
                guid.ToString("N"), // 00000000000000000000000000000000
                guid.ToString("B"), // {00000000-0000-0000-0000-000000000000}
                guid.ToString("P")  // (00000000-0000-0000-0000-000000000000)
            };

            // Act & Assert
            foreach (var format in formats)
            {
                MetricsConfigurationEntityHelper.IsValidGuid(format).Should().BeTrue();
            }
        }

        #endregion

        #region BuildFilterString Tests

        [Fact]
        public void BuildFilterString_WithOnlyAgentId_ReturnsBasicFilter()
        {
            // Arrange
            var agentId = TestConstants.Agents.DefaultAgentId;

            // Act
            var filter = MetricsConfigurationEntityHelper.BuildFilterString(agentId);

            // Assert
            filter.Should().Be($"PartitionKey eq '{agentId}'");
        }

        [Fact]
        public void BuildFilterString_WithAgentIdAndConfigurationName_ReturnsFilterWithName()
        {
            // Arrange
            var agentId = TestConstants.Agents.DefaultAgentId;
            var configurationName = TestConstants.MetricsConfigs.ConfigName;

            // Act
            var filter = MetricsConfigurationEntityHelper.BuildFilterString(agentId, configurationName);

            // Assert
            filter.Should().Be($"PartitionKey eq '{agentId}' and ConfigurationName eq '{configurationName}'");
        }

        [Fact]
        public void BuildFilterString_WithAllParameters_ReturnsCompleteFilter()
        {
            // Arrange
            var agentId = TestConstants.Agents.DefaultAgentId;
            var configurationName = TestConstants.MetricsConfigs.ConfigName;
            var environmentName = TestConstants.Config.DevelopmentEnvironment;

            // Act
            var filter = MetricsConfigurationEntityHelper.BuildFilterString(
                agentId, configurationName, environmentName);

            // Assert
            filter.Should().Be(
                $"PartitionKey eq '{agentId}' and ConfigurationName eq '{configurationName}' and EnvironmentName eq '{environmentName}'");
        }

        [Fact]
        public void BuildFilterString_WithNullConfigurationName_IgnoresName()
        {
            // Arrange
            var agentId = TestConstants.Agents.DefaultAgentId;
            var environmentName = TestConstants.Config.DevelopmentEnvironment;

            // Act
            var filter = MetricsConfigurationEntityHelper.BuildFilterString(
                agentId, null, environmentName);

            // Assert
            filter.Should().Be($"PartitionKey eq '{agentId}' and EnvironmentName eq '{environmentName}'");
        }

        [Fact]
        public void BuildFilterString_WithEmptyConfigurationName_IgnoresName()
        {
            // Arrange
            var agentId = TestConstants.Agents.DefaultAgentId;
            var environmentName = TestConstants.Config.DevelopmentEnvironment;

            // Act
            var filter = MetricsConfigurationEntityHelper.BuildFilterString(
                agentId, "", environmentName);

            // Assert
            filter.Should().Be($"PartitionKey eq '{agentId}' and EnvironmentName eq '{environmentName}'");
        }

        [Fact]
        public void BuildFilterString_WithNullEnvironmentName_IgnoresEnvironment()
        {
            // Arrange
            var agentId = TestConstants.Agents.DefaultAgentId;
            var configurationName = TestConstants.MetricsConfigs.ConfigName;

            // Act
            var filter = MetricsConfigurationEntityHelper.BuildFilterString(
                agentId, configurationName, null);

            // Assert
            filter.Should().Be($"PartitionKey eq '{agentId}' and ConfigurationName eq '{configurationName}'");
        }

        [Theory]
        [InlineData("Development", "MyConfig")]
        [InlineData("Production", "DefaultConfig")]
        [InlineData("Staging", "TestConfig")]
        public void BuildFilterString_WithVariousInputs_BuildsCorrectFilter(
            string environmentName, string configurationName)
        {
            // Arrange
            var agentId = "test-agent";

            // Act
            var filter = MetricsConfigurationEntityHelper.BuildFilterString(
                agentId, configurationName, environmentName);

            // Assert
            filter.Should().Contain($"PartitionKey eq '{agentId}'");
            filter.Should().Contain($"ConfigurationName eq '{configurationName}'");
            filter.Should().Contain($"EnvironmentName eq '{environmentName}'");
        }

        #endregion

        #region Integration Tests

        [Fact]
        public void CreateAndValidate_WithGeneratedId_ProducesValidEntity()
        {
            // Arrange
            var agentId = TestConstants.Agents.DefaultAgentId;
            var configurationName = "IntegrationTestConfig";
            var environmentName = "Integration";

            // Act
            var entity = MetricsConfigurationEntityHelper.CreateEntity(
                agentId, configurationName, environmentName);
            var isValid = MetricsConfigurationEntityHelper.ValidateKeys(entity);

            // Assert
            isValid.Should().BeTrue();
            entity.PartitionKey.Should().Be(agentId);
            entity.RowKey.Should().Be(entity.ConfigurationId);
        }

        [Fact]
        public void CreateAndValidate_WithSpecificId_ProducesValidEntity()
        {
            // Arrange
            var agentId = TestConstants.Agents.DefaultAgentId;
            var configurationName = "IntegrationTestConfig";
            var environmentName = "Integration";
            var configurationId = Guid.NewGuid().ToString();

            // Act
            var entity = MetricsConfigurationEntityHelper.CreateEntity(
                agentId, configurationName, environmentName, configurationId);
            var isValid = MetricsConfigurationEntityHelper.ValidateKeys(entity);

            // Assert
            isValid.Should().BeTrue();
            entity.ConfigurationId.Should().Be(configurationId);
            entity.PartitionKey.Should().Be(agentId);
            entity.RowKey.Should().Be(configurationId);
        }

        [Fact]
        public void KeysFromHelperMethods_MatchEntityKeys()
        {
            // Arrange
            var agentId = "test-agent-123";
            var configurationId = Guid.NewGuid().ToString();

            // Act
            var partitionKey = MetricsConfigurationEntityHelper.GetPartitionKey(agentId);
            var rowKey = MetricsConfigurationEntityHelper.GetRowKey(configurationId);

            var entity = MetricsConfigurationEntityHelper.CreateEntity(
                agentId, "TestConfig", "Test", configurationId);

            // Assert
            entity.PartitionKey.Should().Be(partitionKey);
            entity.RowKey.Should().Be(rowKey);
        }

        #endregion
    }
}
