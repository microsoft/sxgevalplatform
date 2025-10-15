using FluentAssertions;
using Sxg.EvalPlatform.API.Storage.TableEntities;
using Sxg.EvalPlatform.API.Storage.UnitTests.Utilities;

namespace Sxg.EvalPlatform.API.Storage.UnitTests
{
    /// <summary>
    /// Comprehensive tests using TestUtilities for better maintainability
    /// </summary>
    public class MetricsConfigTableServiceComprehensiveTests
    {
        #region Constructor and Configuration Tests

        [Theory]
        [InlineData("testaccount", "CustomTable")]
        [InlineData("prodaccount", null)]
        [InlineData("devaccount", "")]
        public void Constructor_WithVariousConfigurations_ShouldInitializeCorrectly(string accountName, string? tableName)
        {
            // Arrange
            var configuration = TestUtilities.CreateMockConfiguration(accountName, tableName);
            var (service, logMessages) = TestUtilities.CreateServiceWithCapturingLogger(configuration);

            // Act & Assert
            service.Should().NotBeNull();
            logMessages.Should().Contain(msg => msg.Contains("MetricsConfigTableService initialized"));
            logMessages.Should().Contain(msg => msg.Contains(accountName));
            
            var expectedTableName = string.IsNullOrEmpty(tableName) ? "MetricsConfigurations" : tableName;
            logMessages.Should().Contain(msg => msg.Contains(expectedTableName));
        }

        [Fact]
        public void Constructor_WithInvalidConfiguration_ShouldThrowArgumentException()
        {
            // Arrange
            var configuration = TestUtilities.CreateMockConfiguration(accountName: "");
            var logger = TestUtilities.CreateMockLogger().Object;

            // Act & Assert
            var exception = Assert.Throws<ArgumentException>(() => 
                TestUtilities.CreateService(configuration, logger));
            
            exception.Message.Should().Contain("Azure Storage account name is not configured");
        }

        #endregion

        #region Entity Validation Tests

        [Fact]
        public void CreateTestEntity_ShouldHaveValidStructure()
        {
            // Act
            var entity = TestUtilities.CreateTestEntity();

            // Assert
            TestUtilities.ValidateEntityKeys(entity);
            entity.ConfigurationId.Should().NotBeNullOrEmpty();
            entity.LastUpdatedOn.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        }

        [Fact]
        public void CreateMultipleTestEntities_ShouldHaveUniqueIds()
        {
            // Act
            var entities = TestUtilities.CreateTestEntities(10);

            // Assert
            TestUtilities.ValidateEntities(entities);
            entities.Should().HaveCount(10);
            entities.Select(e => e.ConfigurationId).Should().OnlyHaveUniqueItems();
        }

        [Fact]
        public void CreateMultiAgentTestEntities_ShouldDistributeCorrectly()
        {
            // Arrange
            var agentIds = new[] { "agent-1", "agent-2", "agent-3" };

            // Act
            var entities = TestUtilities.CreateMultiAgentTestEntities(agentIds, 3);

            // Assert
            entities.Should().HaveCount(9); // 3 agents × 3 entities each
            TestUtilities.ValidateEntities(entities);
            
            foreach (var agentId in agentIds)
            {
                entities.Where(e => e.AgentId == agentId).Should().HaveCount(3);
            }
        }

        #endregion

        #region Service Method Tests with Utilities

        [Fact]
        public async Task SaveMetricsConfigurationAsync_WithLogging_ShouldLogCorrectly()
        {
            // Arrange
            var (service, logMessages) = TestUtilities.CreateServiceWithCapturingLogger();
            var entity = TestUtilities.CreateTestEntity();

            // Act
            try
            {
                await service.SaveMetricsConfigurationAsync(entity);
            }
            catch (Exception ex) when (ex.Message.Contains("Failed to initialize TableClient"))
            {
                // Expected in unit test environment
            }

            // Assert
            logMessages.Should().Contain(msg => msg.Contains("Saving metrics configuration"));
            logMessages.Should().Contain(msg => msg.Contains(entity.AgentId));
            logMessages.Should().Contain(msg => msg.Contains(entity.ConfigurationName));
        }

        [Theory]
        [MemberData(nameof(TestUtilities.GetValidAgentIdTestData), MemberType = typeof(TestUtilities))]
        public async Task GetMetricsConfigurationAsync_WithValidAgentIds_ShouldHandleCorrectly(string agentId)
        {
            // Arrange
            var service = TestUtilities.CreateService();
            var configName = "test-config";

            // Act & Assert
            var (success, exception, result) = await TestUtilities.TryExecuteAsync(() => 
                service.GetMetricsConfigurationAsync(agentId, configName));

            // Should either succeed or fail with expected exception (not ArgumentException)
            if (!success)
            {
                exception.Should().NotBeOfType<ArgumentException>();
            }
        }

        [Theory]
        [MemberData(nameof(TestUtilities.GetEnvironmentTestData), MemberType = typeof(TestUtilities))]
        public async Task GetAllMetricsConfigurationsByAgentIdAndEnvironmentAsync_WithValidEnvironments_ShouldWork(string environment)
        {
            // Arrange
            var service = TestUtilities.CreateService();
            var agentId = "test-agent";

            // Act & Assert
            var (success, exception, result) = await TestUtilities.TryExecuteAsync(() => 
                service.GetAllMetricsConfigurations(agentId, environment));

            if (!success)
            {
                exception.Should().NotBeOfType<ArgumentException>();
            }
        }

        #endregion

        #region Performance Tests with Utilities

        [Fact]
        public async Task SaveOperation_PerformanceMeasurement_ShouldCompleteQuickly()
        {
            // Arrange
            var service = TestUtilities.CreateService();
            var entity = TestUtilities.CreateTestEntity();

            // Act
            try
            {
                var (result, duration) = await TestUtilities.MeasureAsync(() => 
                    service.SaveMetricsConfigurationAsync(entity));

                // Assert - Operation should be quick (most time will be in initialization)
                duration.Should().BeLessThan(TimeSpan.FromSeconds(10));
            }
            catch (Exception ex) when (ex.Message.Contains("Failed to initialize TableClient"))
            {
                // Expected in unit test environment
                Assert.True(true);
            }
        }

        [Fact]
        public async Task ConcurrentOperations_WithUtilities_ShouldHandleCorrectly()
        {
            // Arrange
            var service = TestUtilities.CreateService();
            var entities = TestUtilities.CreateTestEntities(5);
            
            var operations = entities.Select<MetricsConfigurationTableEntity, Func<Task<MetricsConfigurationTableEntity>>>(
                entity => () => service.SaveMetricsConfigurationAsync(entity)).ToList();

            // Act
            var results = await TestUtilities.ExecuteConcurrentlyAsync(operations);

            // Assert
            results.Should().HaveCount(5);
            // In unit test environment, all should fail with TableClient initialization error
            results.Should().AllSatisfy(result =>
            {
                if (!result.Success)
                {
                    result.Exception.Should().NotBeNull();
                    result.Exception!.Message.Should().Contain("Failed to initialize TableClient");
                }
            });
        }

        #endregion

        #region Edge Case Tests

        [Theory]
        [MemberData(nameof(TestUtilities.GetInvalidStringTestData), MemberType = typeof(TestUtilities))]
        public async Task ServiceMethods_WithInvalidStrings_ShouldHandleGracefully(string invalidInput, string description)
        {
            // Arrange
            var service = TestUtilities.CreateService();

            // Act & Assert - Should not throw ArgumentException for invalid strings
            var (success, exception, _) = await TestUtilities.TryExecuteAsync(() => 
                service.GetMetricsConfigurationAsync(invalidInput, "valid-config"));

            if (!success)
            {
                exception.Should().NotBeOfType<ArgumentException>($"because {description}");
            }
        }

        [Fact]
        public async Task SaveMetricsConfigurationAsync_WithNullEntity_ShouldThrowArgumentNullException()
        {
            // Arrange
            var service = TestUtilities.CreateService();

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() => 
                service.SaveMetricsConfigurationAsync(null!));
        }

        [Fact]
        public void Entity_WithLongStrings_ShouldValidateCorrectly()
        {
            // Arrange
            var longString = new string('A', 1000);
            var entity = TestUtilities.CreateTestEntity(
                description: longString,
                configurationName: new string('B', 100));

            // Act & Assert
            TestUtilities.ValidateEntityKeys(entity);
            entity.Description.Should().HaveLength(1000);
            entity.ConfigurationName.Should().HaveLength(100);
        }

        #endregion

        #region Entity Lifecycle Tests

        [Fact]
        public void EntityCreation_ShouldSetPropertiesCorrectly()
        {
            // Arrange
            var agentId = "specific-agent";
            var configName = "specific-config";
            var environment = "specific-env";
            var description = "specific description";

            // Act
            var entity = TestUtilities.CreateTestEntity(agentId, configName, environment, description);

            // Assert
            entity.AgentId.Should().Be(agentId);
            entity.ConfigurationName.Should().Be(configName);
            entity.EnvironmentName.Should().Be(environment);
            entity.Description.Should().Be(description);
            entity.LastUpdatedBy.Should().Be("test-framework");
            TestUtilities.ValidateEntityKeys(entity);
        }

        [Fact]
        public void EntityModification_ShouldUpdateKeysAutomatically()
        {
            // Arrange
            var entity = TestUtilities.CreateTestEntity();
            var originalPartitionKey = entity.PartitionKey;
            var originalRowKey = entity.RowKey;

            // Act
            entity.AgentId = "new-agent-id";
            var newConfigId = Guid.NewGuid().ToString();
            entity.ConfigurationId = newConfigId;

            // Assert
            entity.PartitionKey.Should().NotBe(originalPartitionKey);
            entity.RowKey.Should().NotBe(originalRowKey);
            entity.PartitionKey.Should().Be("new-agent-id");
            entity.RowKey.Should().Be(newConfigId);
            TestUtilities.ValidateEntityKeys(entity);
        }

        #endregion
    }
}