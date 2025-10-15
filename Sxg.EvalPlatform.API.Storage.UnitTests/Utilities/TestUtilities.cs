using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Sxg.EvalPlatform.API.Storage.TableEntities;
using Sxg.EvalPlatform.API.Storage.Services;

namespace Sxg.EvalPlatform.API.Storage.UnitTests.Utilities
{
    /// <summary>
    /// Test utilities for MetricsConfigTableService testing
    /// </summary>
    public static class TestUtilities
    {
        #region Configuration Builders

        /// <summary>
        /// Creates a mock configuration for testing
        /// </summary>
        public static IConfiguration CreateMockConfiguration(
            string accountName = "testaccount",
            string? tableName = null,
            string environment = "Test")
        {
            var configData = new Dictionary<string, string>
            {
                ["AzureStorage:AccountName"] = accountName,
                ["ASPNETCORE_ENVIRONMENT"] = environment
            };

            if (!string.IsNullOrEmpty(tableName))
            {
                configData["AzureStorage:MetricsConfigurationsTable"] = tableName;
            }

            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(configData!)
                .Build();

            return configuration;
        }

        /// <summary>
        /// Creates a mock configuration using Moq
        /// </summary>
        public static Mock<IConfiguration> CreateMockedConfiguration(
            string accountName = "testaccount",
            string? tableName = null,
            string environment = "Test")
        {
            var mockConfig = new Mock<IConfiguration>();
            mockConfig.Setup(c => c["AzureStorage:AccountName"]).Returns(accountName);
            mockConfig.Setup(c => c["AzureStorage:MetricsConfigurationsTable"]).Returns(tableName);
            mockConfig.Setup(c => c.GetValue<string>("ASPNETCORE_ENVIRONMENT")).Returns(environment);
            return mockConfig;
        }

        #endregion

        #region Logger Builders

        /// <summary>
        /// Creates a mock logger for testing
        /// </summary>
        public static Mock<ILogger<MetricsConfigTableService>> CreateMockLogger()
        {
            return new Mock<ILogger<MetricsConfigTableService>>();
        }

        /// <summary>
        /// Creates a mock logger that captures log messages
        /// </summary>
        public static (Mock<ILogger<MetricsConfigTableService>> Logger, List<string> CapturedMessages) CreateCapturingLogger()
        {
            var capturedMessages = new List<string>();
            var mockLogger = new Mock<ILogger<MetricsConfigTableService>>();

            mockLogger.Setup(x => x.Log(
                It.IsAny<LogLevel>(),
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()))
                .Callback<LogLevel, EventId, object, Exception?, Delegate>((level, eventId, state, exception, formatter) =>
                {
                    capturedMessages.Add(state.ToString() ?? string.Empty);
                });

            return (mockLogger, capturedMessages);
        }

        #endregion

        #region Service Builders

        /// <summary>
        /// Creates a MetricsConfigTableService for testing
        /// </summary>
        public static MetricsConfigTableService CreateService(
            IConfiguration? configuration = null,
            ILogger<MetricsConfigTableService>? logger = null)
        {
            var config = configuration ?? CreateMockConfiguration();
            var log = logger ?? CreateMockLogger().Object;
            return new MetricsConfigTableService(config, log);
        }

        /// <summary>
        /// Creates a service with capturing logger for testing
        /// </summary>
        public static (MetricsConfigTableService Service, List<string> LogMessages) CreateServiceWithCapturingLogger(
            IConfiguration? configuration = null)
        {
            var config = configuration ?? CreateMockConfiguration();
            var (logger, messages) = CreateCapturingLogger();
            var service = new MetricsConfigTableService(config, logger.Object);
            return (service, messages);
        }

        #endregion

        #region Entity Builders

        /// <summary>
        /// Creates a test entity with default values
        /// </summary>
        public static MetricsConfigurationTableEntity CreateTestEntity(
            string? agentId = null,
            string? configurationName = null,
            string? environmentName = null,
            string? description = null)
        {
            return new MetricsConfigurationTableEntity
            {
                AgentId = agentId ?? $"test-agent-{Guid.NewGuid():N}",
                ConfigurationName = configurationName ?? $"test-config-{DateTime.UtcNow:yyyyMMddHHmmss}",
                EnvironmentName = environmentName ?? "test",
                Description = description ?? "Test entity created by TestUtilities",
                LastUpdatedBy = "test-framework",
                LastUpdatedOn = DateTime.UtcNow
            };
        }

        /// <summary>
        /// Creates multiple test entities
        /// </summary>
        public static List<MetricsConfigurationTableEntity> CreateTestEntities(
            int count,
            string? agentId = null,
            string[]? environments = null)
        {
            var baseAgentId = agentId ?? $"test-agent-{Guid.NewGuid():N}";
            var envs = environments ?? new[] { "production", "staging", "development" };

            return Enumerable.Range(0, count)
                .Select(i => new MetricsConfigurationTableEntity
                {
                    AgentId = baseAgentId,
                    ConfigurationName = $"test-config-{i:D3}",
                    EnvironmentName = envs[i % envs.Length],
                    Description = $"Test entity {i} created by TestUtilities",
                    LastUpdatedBy = "test-framework",
                    LastUpdatedOn = DateTime.UtcNow.AddMinutes(-i)
                })
                .ToList();
        }

        /// <summary>
        /// Creates entities with specific agent IDs for multi-agent testing
        /// </summary>
        public static List<MetricsConfigurationTableEntity> CreateMultiAgentTestEntities(
            string[] agentIds,
            int entitiesPerAgent = 2)
        {
            var entities = new List<MetricsConfigurationTableEntity>();

            foreach (var agentId in agentIds)
            {
                for (int i = 0; i < entitiesPerAgent; i++)
                {
                    entities.Add(new MetricsConfigurationTableEntity
                    {
                        AgentId = agentId,
                        ConfigurationName = $"config-{i}",
                        EnvironmentName = i % 2 == 0 ? "production" : "staging",
                        Description = $"Entity {i} for agent {agentId}",
                        LastUpdatedBy = "test-framework",
                        LastUpdatedOn = DateTime.UtcNow.AddMinutes(-i)
                    });
                }
            }

            return entities;
        }

        #endregion

        #region Validation Helpers

        /// <summary>
        /// Validates that an entity has proper key settings
        /// </summary>
        public static void ValidateEntityKeys(MetricsConfigurationTableEntity entity)
        {
            if (entity == null) throw new ArgumentNullException(nameof(entity));

            // Validate PartitionKey
            if (string.IsNullOrEmpty(entity.PartitionKey) || entity.PartitionKey != entity.AgentId)
                throw new InvalidOperationException("PartitionKey should equal AgentId");

            // Validate RowKey
            if (string.IsNullOrEmpty(entity.RowKey) || entity.RowKey != entity.ConfigurationId)
                throw new InvalidOperationException("RowKey should equal ConfigurationId");

            // Validate ConfigurationId is a valid GUID
            if (!Guid.TryParse(entity.ConfigurationId, out _))
                throw new InvalidOperationException("ConfigurationId should be a valid GUID");
        }

        /// <summary>
        /// Validates a list of entities
        /// </summary>
        public static void ValidateEntities(IEnumerable<MetricsConfigurationTableEntity> entities)
        {
            if (entities == null) throw new ArgumentNullException(nameof(entities));

            foreach (var entity in entities)
            {
                ValidateEntityKeys(entity);
            }

            // Check for unique ConfigurationIds
            var configIds = entities.Select(e => e.ConfigurationId).ToList();
            if (configIds.Count != configIds.Distinct().Count())
                throw new InvalidOperationException("All entities should have unique ConfigurationIds");
        }

        #endregion

        #region Async Test Helpers

        /// <summary>
        /// Executes an async operation and captures any exceptions
        /// </summary>
        public static async Task<(bool Success, Exception? Exception, T? Result)> TryExecuteAsync<T>(Func<Task<T>> operation)
        {
            try
            {
                var result = await operation();
                return (true, null, result);
            }
            catch (Exception ex)
            {
                return (false, ex, default(T));
            }
        }

        /// <summary>
        /// Executes an async operation without return value and captures any exceptions
        /// </summary>
        public static async Task<(bool Success, Exception? Exception)> TryExecuteAsync(Func<Task> operation)
        {
            try
            {
                await operation();
                return (true, null);
            }
            catch (Exception ex)
            {
                return (false, ex);
            }
        }

        /// <summary>
        /// Runs multiple async operations concurrently and returns results
        /// </summary>
        public static async Task<List<(bool Success, Exception? Exception, T? Result)>> ExecuteConcurrentlyAsync<T>(
            IEnumerable<Func<Task<T>>> operations)
        {
            var tasks = operations.Select(TryExecuteAsync).ToList();
            return (await Task.WhenAll(tasks)).ToList();
        }

        #endregion

        #region Test Data Generators

        /// <summary>
        /// Generates test data for parameterized tests
        /// </summary>
        public static IEnumerable<object[]> GetInvalidStringTestData()
        {
            yield return new object[] { "", "Empty string" };
            yield return new object[] { "   ", "Whitespace only" };
            yield return new object[] { null!, "Null string" };
        }

        /// <summary>
        /// Generates valid agent ID test data
        /// </summary>
        public static IEnumerable<object[]> GetValidAgentIdTestData()
        {
            yield return new object[] { "simple-agent" };
            yield return new object[] { "agent-with-numbers-123" };
            yield return new object[] { "agent_with_underscores" };
            yield return new object[] { "agent.with.dots" };
            yield return new object[] { Guid.NewGuid().ToString() };
        }

        /// <summary>
        /// Generates environment test data
        /// </summary>
        public static IEnumerable<object[]> GetEnvironmentTestData()
        {
            yield return new object[] { "production" };
            yield return new object[] { "staging" };
            yield return new object[] { "development" };
            yield return new object[] { "test" };
            yield return new object[] { "integration" };
        }

        #endregion

        #region Performance Helpers

        /// <summary>
        /// Measures execution time of an operation
        /// </summary>
        public static async Task<(T Result, TimeSpan Duration)> MeasureAsync<T>(Func<Task<T>> operation)
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            var result = await operation();
            stopwatch.Stop();
            return (result, stopwatch.Elapsed);
        }

        /// <summary>
        /// Measures execution time of an operation without return value
        /// </summary>
        public static async Task<TimeSpan> MeasureAsync(Func<Task> operation)
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            await operation();
            stopwatch.Stop();
            return stopwatch.Elapsed;
        }

        #endregion
    }
}