using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Sxg.EvalPlatform.API.Storage.TableEntities;
using Sxg.EvalPlatform.API.Storage.Helpers;
using Sxg.EvalPlatform.API.Storage.Services;

namespace Sxg.EvalPlatform.API.Storage.UnitTests
{
    /// <summary>
    /// Integration-style tests for MetricsConfigTableService
    /// These tests are designed to work with Azure Storage Emulator or actual Azure Table Storage
    /// Mark with [Fact(Skip = "Requires Azure Storage")] to skip in CI/CD
    /// </summary>
    public class MetricsConfigTableServiceIntegrationTests : IDisposable
    {
        private readonly MetricsConfigTableService _service;
        private readonly List<MetricsConfigurationTableEntity> _testEntities;
        private readonly string _testAgentId;

        public MetricsConfigTableServiceIntegrationTests()
        {
            // Setup for integration tests - uncomment when you have Azure Storage available
            var configuration = CreateIntegrationConfiguration();
            var logger = new Mock<ILogger<MetricsConfigTableService>>().Object;
            
            _service = new MetricsConfigTableService(configuration, logger);
            _testEntities = new List<MetricsConfigurationTableEntity>();
            _testAgentId = $"integration-test-agent-{Guid.NewGuid():N}";
        }

        public void Dispose()
        {
            // Cleanup test data
            CleanupTestData().Wait();
        }

        #region Full Integration Tests

        [Fact(Skip = "Requires Azure Storage Emulator or actual Azure Table Storage")]
        public async Task FullWorkflow_SaveRetrieveUpdateDelete_ShouldWorkEndToEnd()
        {
            // Arrange
            var entity = CreateTestEntity();
            
            try
            {
                // Act & Assert - Save
                var savedEntity = await _service.SaveMetricsConfigurationAsync(entity);
                savedEntity.Should().NotBeNull();
                savedEntity.ConfigurationId.Should().Be(entity.ConfigurationId);
                _testEntities.Add(savedEntity);

                // Act & Assert - Retrieve by ID
                var retrievedById = await _service.GetMetricsConfigurationByConfigurationIdAsync(entity.AgentId, entity.ConfigurationId);
                retrievedById.Should().NotBeNull();
                retrievedById!.ConfigurationName.Should().Be(entity.ConfigurationName);

                // Act & Assert - Retrieve by name and environment
                var retrievedByName = await _service.GetMetricsConfigurationAsync(
                    entity.AgentId, entity.ConfigurationName, entity.EnvironmentName);
                retrievedByName.Should().NotBeNull();
                retrievedByName!.ConfigurationId.Should().Be(entity.ConfigurationId);

                // Act & Assert - Update
                entity.Description = "Updated description";
                var updatedEntity = await _service.SaveMetricsConfigurationAsync(entity);
                updatedEntity.Description.Should().Be("Updated description");

                // Act & Assert - Verify exists
                var exists = await _service.MetricsConfigurationExistsAsync(entity.AgentId, entity.ConfigurationName);
                exists.Should().BeTrue();

                // Act & Assert - Delete
                var deleteResult = await _service.DeleteMetricsConfigurationByIdAsync(entity.AgentId, entity.ConfigurationId);
                deleteResult.Should().BeTrue();

                // Act & Assert - Verify deleted
                var existsAfterDelete = await _service.MetricsConfigurationExistsAsync(entity.AgentId, entity.ConfigurationName);
                existsAfterDelete.Should().BeFalse();

                _testEntities.Remove(savedEntity); // Don't try to cleanup in Dispose
            }
            catch (Exception ex)
            {
                // Log the exception for debugging
                throw new Exception($"Integration test failed: {ex.Message}", ex);
            }
        }

        [Fact(Skip = "Requires Azure Storage Emulator or actual Azure Table Storage")]
        public async Task MultipleEntities_SaveAndRetrieveAll_ShouldWorkCorrectly()
        {
            // Arrange
            var entities = CreateMultipleTestEntities(5);

            try
            {
                // Act - Save all entities
                var savedEntities = new List<MetricsConfigurationTableEntity>();
                foreach (var entity in entities)
                {
                    var saved = await _service.SaveMetricsConfigurationAsync(entity);
                    savedEntities.Add(saved);
                    _testEntities.Add(saved);
                }

                // Act - Retrieve all by agent ID
                var allEntities = await _service.GetAllMetricsConfigurationsByAgentIdAsync(_testAgentId);

                // Assert
                allEntities.Should().HaveCount(5);
                allEntities.Select(e => e.ConfigurationId).Should().BeEquivalentTo(
                    savedEntities.Select(e => e.ConfigurationId));
            }
            catch (Exception ex)
            {
                throw new Exception($"Multiple entities test failed: {ex.Message}", ex);
            }
        }

        [Fact(Skip = "Requires Azure Storage Emulator or actual Azure Table Storage")]
        public async Task EnvironmentFiltering_ShouldReturnCorrectEntities()
        {
            // Arrange
            var prodEntities = CreateTestEntitiesForEnvironment("production", 3);
            var stagingEntities = CreateTestEntitiesForEnvironment("staging", 2);
            var allEntities = prodEntities.Concat(stagingEntities).ToList();

            try
            {
                // Act - Save all entities
                foreach (var entity in allEntities)
                {
                    await _service.SaveMetricsConfigurationAsync(entity);
                    _testEntities.Add(entity);
                }

                // Act - Retrieve production entities
                var prodResults = await _service.GetAllMetricsConfigurations(
                    _testAgentId, "production");

                // Act - Retrieve staging entities
                var stagingResults = await _service.GetAllMetricsConfigurations(
                    _testAgentId, "staging");

                // Assert
                prodResults.Should().HaveCount(3);
                stagingResults.Should().HaveCount(2);
                prodResults.Should().AllSatisfy(e => e.EnvironmentName.Should().Be("production"));
                stagingResults.Should().AllSatisfy(e => e.EnvironmentName.Should().Be("staging"));
            }
            catch (Exception ex)
            {
                throw new Exception($"Environment filtering test failed: {ex.Message}", ex);
            }
        }

        [Fact(Skip = "Requires Azure Storage Emulator or actual Azure Table Storage")]
        public async Task ConcurrentOperations_ShouldHandleCorrectly()
        {
            // Arrange
            var entities = CreateMultipleTestEntities(10);

            try
            {
                // Act - Save entities concurrently
                var saveTasks = entities.Select(async entity =>
                {
                    var saved = await _service.SaveMetricsConfigurationAsync(entity);
                    _testEntities.Add(saved);
                    return saved;
                });

                var savedEntities = await Task.WhenAll(saveTasks);

                // Act - Read entities concurrently
                var readTasks = savedEntities.Select(entity =>
                    _service.GetMetricsConfigurationByConfigurationIdAsync(entity.AgentId, entity.ConfigurationId));

                var readResults = await Task.WhenAll(readTasks);

                // Assert
                readResults.Should().AllSatisfy(result => result.Should().NotBeNull());
                readResults.Length.Should().Be(10);
            }
            catch (Exception ex)
            {
                throw new Exception($"Concurrent operations test failed: {ex.Message}", ex);
            }
        }

        #endregion

        #region Error Handling Integration Tests

        [Fact(Skip = "Requires Azure Storage Emulator or actual Azure Table Storage")]
        public async Task GetNonExistentEntity_ShouldReturnNull()
        {
            // Arrange
            var nonExistentId = Guid.NewGuid().ToString();

            // Act
            var result = await _service.GetMetricsConfigurationByConfigurationIdAsync(_testAgentId, nonExistentId);

            // Assert
            result.Should().BeNull();
        }

        [Fact(Skip = "Requires Azure Storage Emulator or actual Azure Table Storage")]
        public async Task DeleteNonExistentEntity_ShouldReturnFalse()
        {
            // Arrange
            var nonExistentId = Guid.NewGuid().ToString();

            // Act
            var result = await _service.DeleteMetricsConfigurationByIdAsync(_testAgentId, nonExistentId);

            // Assert
            result.Should().BeFalse();
        }

        #endregion

        #region Performance Tests

        [Fact(Skip = "Requires Azure Storage Emulator or actual Azure Table Storage")]
        public async Task LargeDataSet_ShouldPerformReasonably()
        {
            // Arrange
            var largeEntitySet = CreateMultipleTestEntities(50);
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            try
            {
                // Act - Save large dataset
                foreach (var entity in largeEntitySet)
                {
                    await _service.SaveMetricsConfigurationAsync(entity);
                    _testEntities.Add(entity);
                }

                stopwatch.Stop();

                // Assert - Should complete within reasonable time (adjust as needed)
                stopwatch.ElapsedMilliseconds.Should().BeLessThan(30000); // 30 seconds

                // Act - Retrieve all
                stopwatch.Restart();
                var allEntities = await _service.GetAllMetricsConfigurationsByAgentIdAsync(_testAgentId);
                stopwatch.Stop();

                // Assert
                allEntities.Should().HaveCount(50);
                stopwatch.ElapsedMilliseconds.Should().BeLessThan(5000); // 5 seconds
            }
            catch (Exception ex)
            {
                throw new Exception($"Performance test failed: {ex.Message}", ex);
            }
        }

        #endregion

        #region Helper Methods

        private IConfiguration CreateIntegrationConfiguration()
        {
            // For integration tests, you would typically use:
            // 1. Azure Storage Emulator: "UseDevelopmentStorage=true"
            // 2. Actual Azure Storage: Your connection string
            
            var configData = new Dictionary<string, string>
            {
                ["AzureStorage:AccountName"] = "devstoreaccount1", // Storage Emulator
                ["AzureStorage:MetricsConfigurationsTable"] = "IntegrationTestTable",
                ["ASPNETCORE_ENVIRONMENT"] = "Test"
            };

            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(configData!)
                .Build();

            return configuration;
        }

        private MetricsConfigurationTableEntity CreateTestEntity()
        {
            return new MetricsConfigurationTableEntity
            {
                AgentId = _testAgentId,
                ConfigurationName = $"integration-test-config-{Guid.NewGuid():N}",
                EnvironmentName = "integration-test",
                Description = "Entity created for integration testing",
                LastUpdatedBy = "integration-test-framework",
                LastUpdatedOn = DateTime.UtcNow
            };
        }

        private List<MetricsConfigurationTableEntity> CreateMultipleTestEntities(int count)
        {
            return Enumerable.Range(0, count)
                .Select(i => new MetricsConfigurationTableEntity
                {
                    AgentId = _testAgentId,
                    ConfigurationName = $"integration-config-{i:D3}",
                    EnvironmentName = i % 2 == 0 ? "production" : "staging",
                    Description = $"Integration test entity {i}",
                    LastUpdatedBy = "integration-test-framework",
                    LastUpdatedOn = DateTime.UtcNow.AddMinutes(-i)
                })
                .ToList();
        }

        private List<MetricsConfigurationTableEntity> CreateTestEntitiesForEnvironment(string environment, int count)
        {
            return Enumerable.Range(0, count)
                .Select(i => new MetricsConfigurationTableEntity
                {
                    AgentId = _testAgentId,
                    ConfigurationName = $"{environment}-config-{i:D3}",
                    EnvironmentName = environment,
                    Description = $"Test entity for {environment} environment",
                    LastUpdatedBy = "integration-test-framework",
                    LastUpdatedOn = DateTime.UtcNow.AddMinutes(-i)
                })
                .ToList();
        }

        private async Task CleanupTestData()
        {
            // Clean up any test entities that were created
            try
            {
                foreach (var entity in _testEntities.ToList())
                {
                    await _service.DeleteMetricsConfigurationByIdAsync(entity.AgentId, entity.ConfigurationId);
                }
            }
            catch (Exception ex)
            {
                // Log cleanup errors but don't fail the test
                System.Diagnostics.Debug.WriteLine($"Error during cleanup: {ex.Message}");
            }
        }

        #endregion
    }
}