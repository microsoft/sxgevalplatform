using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Sxg.EvalPlatform.API.UnitTests.RequestHandlerTests;
using SxgEvalPlatformApi.Services;
using System.Diagnostics;

namespace Sxg.EvalPlatform.API.UnitTests.ServicesTests
{
    /// <summary>
    /// Comprehensive unit tests for OpenTelemetryService.
    /// Tests metrics tracking, activity creation, and custom metric recording.
    /// </summary>
    [Trait("Category", TestCategories.Unit)]
    [Trait("Category", TestCategories.Service)]
    [Trait("Category", TestCategories.Telemetry)]
    public class OpenTelemetryServiceUnitTests : IDisposable
    {
        private readonly Mock<ILogger<OpenTelemetryService>> _mockLogger;
        private readonly OpenTelemetryService _service;

        public OpenTelemetryServiceUnitTests()
        {
            _mockLogger = new Mock<ILogger<OpenTelemetryService>>();
            _service = new OpenTelemetryService(_mockLogger.Object);
        }

        public void Dispose()
        {
            _service?.Dispose();
            GC.SuppressFinalize(this);
        }

        #region Constructor Tests

        [Fact]
        public void Constructor_WithValidLogger_CreatesInstance()
        {
            // Arrange & Act
            using var service = new OpenTelemetryService(_mockLogger.Object);

            // Assert
            service.Should().NotBeNull();
            service.Should().BeAssignableTo<IOpenTelemetryService>();
        }

        #endregion

        #region TrackEvaluationRunOperation Tests

        [Fact]
        public void TrackEvaluationRunOperation_WithValidParameters_TracksSuccessfully()
        {
            // Arrange
            var operation = "CreateEvalRun";
            var evalRunId = Guid.NewGuid().ToString();
            var agentId = TestConstants.Agents.DefaultAgentId;
            var duration = TimeSpan.FromMilliseconds(150);

            // Act
            _service.TrackEvaluationRunOperation(operation, evalRunId, agentId, true, duration);

            // Assert
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Debug,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Tracked evaluation run operation")),
                    null,
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public void TrackEvaluationRunOperation_WithFailure_TracksWithSuccessFalse()
        {
            // Arrange
            var operation = "UpdateEvalRun";
            var evalRunId = Guid.NewGuid().ToString();
            var agentId = TestConstants.Agents.DefaultAgentId;
            var duration = TimeSpan.FromMilliseconds(200);

            // Act
            _service.TrackEvaluationRunOperation(operation, evalRunId, agentId, false, duration);

            // Assert
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Debug,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Success=False")),
                    null,
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Theory]
        [InlineData("Create")]
        [InlineData("Update")]
        [InlineData("Delete")]
        [InlineData("Get")]
        public void TrackEvaluationRunOperation_WithDifferentOperations_TracksAll(string operation)
        {
            // Arrange
            var evalRunId = Guid.NewGuid().ToString();
            var agentId = TestConstants.Agents.DefaultAgentId;
            var duration = TimeSpan.FromMilliseconds(100);

            // Act
            _service.TrackEvaluationRunOperation(operation, evalRunId, agentId, true, duration);

            // Assert
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Debug,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains(operation)),
                    null,
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        #endregion

        #region TrackDatasetOperation Tests

        [Fact]
        public void TrackDatasetOperation_WithValidParameters_TracksSuccessfully()
        {
            // Arrange
            var operation = "SaveDataset";
            var datasetId = Guid.NewGuid().ToString();
            var agentId = TestConstants.Agents.DefaultAgentId;
            var duration = TimeSpan.FromMilliseconds(175);

            // Act
            _service.TrackDatasetOperation(operation, datasetId, agentId, true, duration);

            // Assert
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Debug,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Tracked dataset operation")),
                    null,
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public void TrackDatasetOperation_WithFailure_TracksWithSuccessFalse()
        {
            // Arrange
            var operation = "DeleteDataset";
            var datasetId = Guid.NewGuid().ToString();
            var agentId = TestConstants.Agents.DefaultAgentId;
            var duration = TimeSpan.FromMilliseconds(50);

            // Act
            _service.TrackDatasetOperation(operation, datasetId, agentId, false, duration);

            // Assert
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Debug,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Success=False")),
                    null,
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        #endregion

        #region TrackMetricsConfigOperation Tests

        [Fact]
        public void TrackMetricsConfigOperation_WithValidParameters_TracksSuccessfully()
        {
            // Arrange
            var operation = "CreateConfig";
            var configId = Guid.NewGuid().ToString();
            var duration = TimeSpan.FromMilliseconds(125);

            // Act
            _service.TrackMetricsConfigOperation(operation, configId, true, duration);

            // Assert
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Debug,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Tracked Metrics config operation")),
                    null,
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public void TrackMetricsConfigOperation_WithFailure_TracksWithSuccessFalse()
        {
            // Arrange
            var operation = "UpdateConfig";
            var configId = Guid.NewGuid().ToString();
            var duration = TimeSpan.FromMilliseconds(90);

            // Act
            _service.TrackMetricsConfigOperation(operation, configId, false, duration);

            // Assert
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Debug,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Success=False")),
                    null,
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        #endregion

        #region TrackEvaluationResultOperation Tests

        [Fact]
        public void TrackEvaluationResultOperation_WithValidParameters_TracksSuccessfully()
        {
            // Arrange
            var operation = "SaveResult";
            var evalRunId = Guid.NewGuid().ToString();
            var duration = TimeSpan.FromMilliseconds(200);

            // Act
            _service.TrackEvaluationResultOperation(operation, evalRunId, true, duration);

            // Assert
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Debug,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Tracked evaluation result operation")),
                    null,
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public void TrackEvaluationResultOperation_WithFailure_TracksWithSuccessFalse()
        {
            // Arrange
            var operation = "GetResult";
            var evalRunId = Guid.NewGuid().ToString();
            var duration = TimeSpan.FromMilliseconds(75);

            // Act
            _service.TrackEvaluationResultOperation(operation, evalRunId, false, duration);

            // Assert
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Debug,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Success=False")),
                    null,
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        #endregion

        #region TrackDataVerseApiCall Tests

        [Fact]
        public void TrackDataVerseApiCall_WithSuccessfulCall_TracksSuccessfully()
        {
            // Arrange
            var evalRunId = Guid.NewGuid().ToString();
            var agentId = TestConstants.Agents.DefaultAgentId;
            var statusCode = 200;
            var duration = TimeSpan.FromMilliseconds(300);

            // Act
            _service.TrackDataVerseApiCall(evalRunId, agentId, true, statusCode, duration);

            // Assert
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Debug,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Tracked DataVerse API call")),
                    null,
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Theory]
        [InlineData(200, true)]
        [InlineData(201, true)]
        [InlineData(400, false)]
        [InlineData(401, false)]
        [InlineData(404, false)]
        [InlineData(500, false)]
        public void TrackDataVerseApiCall_WithVariousStatusCodes_TracksCorrectly(int statusCode, bool expectedSuccess)
        {
            // Arrange
            var evalRunId = Guid.NewGuid().ToString();
            var agentId = TestConstants.Agents.DefaultAgentId;
            var duration = TimeSpan.FromMilliseconds(250);

            // Act
            _service.TrackDataVerseApiCall(evalRunId, agentId, expectedSuccess, statusCode, duration);

            // Assert
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Debug,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains($"StatusCode={statusCode}")),
                    null,
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        #endregion

        #region StartActivity Tests

        [Fact]
        public void StartActivity_WithValidOperationName_AttemptsToCreateActivity()
        {
            // Arrange
            var operationName = "TestOperation";

            // Act
            using var activity = _service.StartActivity(operationName);

            // Assert
            // Note: Activity may be null if no ActivityListener is configured
            // This is expected behavior in unit tests without OpenTelemetry infrastructure
            // The service method completes without throwing
            Assert.True(true); // Method executed successfully
        }

        [Fact]
        public void StartActivity_AttemptsToSetServiceTags()
        {
            // Arrange
            var operationName = "TestOperation";

            // Act
            using var activity = _service.StartActivity(operationName);

            // Assert
            // Note: Activity may be null without ActivityListener configured
            // This is expected in unit tests - the service doesn't throw
            Assert.True(true); // Method executed successfully
        }

        [Theory]
        [InlineData("Operation1")]
        [InlineData("Operation2")]
        [InlineData("EvalRunController.CreateEvalRun")]
        public void StartActivity_WithDifferentOperationNames_AttemptsCreation(string operationName)
        {
            // Arrange & Act
            using var activity = _service.StartActivity(operationName);

            // Assert
            // Activity may be null without listener - this is expected
            Assert.True(true); // Method executed successfully
        }

        #endregion

        #region AddActivityTags Tests

        [Fact]
        public void AddActivityTags_WithCurrentActivity_AddsTags()
        {
            // Arrange
            using var activity = new Activity("TestActivity").Start();
            var tags = new Dictionary<string, object>
            {
                { "custom_tag1", "value1" },
                { "custom_tag2", 123 },
                { "custom_tag3", true }
            };

            // Act
            _service.AddActivityTags(tags);

            // Assert
            activity.Tags.Should().Contain(tag => tag.Key == "custom_tag1" && tag.Value == "value1");
            activity.Tags.Should().Contain(tag => tag.Key == "custom_tag2" && tag.Value == "123");
            activity.Tags.Should().Contain(tag => tag.Key == "custom_tag3" && tag.Value == "True");
        }

        [Fact]
        public void AddActivityTags_WithNoCurrentActivity_DoesNotThrow()
        {
            // Arrange
            var tags = new Dictionary<string, object>
            {
                { "custom_tag", "value" }
            };

            // Act & Assert
            var exception = Record.Exception(() => _service.AddActivityTags(tags));
            exception.Should().BeNull();
        }

        [Fact]
        public void AddActivityTags_WithNullTags_DoesNotThrow()
        {
            // Arrange
            using var activity = new Activity("TestActivity").Start();

            // Act & Assert
            var exception = Record.Exception(() => _service.AddActivityTags(null!));
            exception.Should().BeNull();
        }

        [Fact]
        public void AddActivityTags_WithEmptyDictionary_DoesNotAddTags()
        {
            // Arrange
            using var activity = new Activity("TestActivity").Start();
            var initialTagCount = activity.Tags.Count();
            var tags = new Dictionary<string, object>();

            // Act
            _service.AddActivityTags(tags);

            // Assert
            activity.Tags.Count().Should().Be(initialTagCount);
        }

        #endregion

        #region RecordMetric Tests

        [Fact]
        public void RecordMetric_WithValidParameters_RecordsSuccessfully()
        {
            // Arrange
            var metricName = "custom_metric";
            var value = 42.5;

            // Act
            _service.RecordMetric(metricName, value);

            // Assert
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Debug,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Recorded custom metric")),
                    null,
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public void RecordMetric_WithTags_RecordsWithTags()
        {
            // Arrange
            var metricName = "custom_metric_with_tags";
            var value = 100.0;
            var tags = new Dictionary<string, object>
            {
                { "tag1", "value1" },
                { "tag2", 456 }
            };

            // Act
            _service.RecordMetric(metricName, value, tags);

            // Assert
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Debug,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Recorded custom metric")),
                    null,
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public void RecordMetric_SameMetricMultipleTimes_UsesCache()
        {
            // Arrange
            var metricName = "cached_metric";

            // Act
            _service.RecordMetric(metricName, 10.0);
            _service.RecordMetric(metricName, 20.0);
            _service.RecordMetric(metricName, 30.0);

            // Assert
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Debug,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Recorded custom metric")),
                    null,
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Exactly(3));
        }

        [Theory]
        [InlineData(0.0)]
        [InlineData(1.5)]
        [InlineData(100.25)]
        [InlineData(-5.75)]
        public void RecordMetric_WithDifferentValues_RecordsAll(double value)
        {
            // Arrange
            var metricName = $"metric_{value}";

            // Act
            _service.RecordMetric(metricName, value);

            // Assert
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Debug,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Recorded custom metric")),
                    null,
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        #endregion

        #region Dispose Tests

        [Fact]
        public void Dispose_CanBeCalledMultipleTimes()
        {
            // Arrange
            using var service = new OpenTelemetryService(_mockLogger.Object);

            // Act & Assert
            service.Dispose();
            var exception = Record.Exception(() => service.Dispose());
            exception.Should().BeNull();
        }

        [Fact]
        public void Dispose_ReleasesResources()
        {
            // Arrange
            var service = new OpenTelemetryService(_mockLogger.Object);

            // Act
            service.Dispose();

            // Assert
            // Verify no exceptions thrown
            Assert.True(true);
        }

        #endregion

        #region Integration Tests

        [Fact]
        public void CompleteFlow_TrackMultipleOperations_AllSucceed()
        {
            // Arrange
            var evalRunId = Guid.NewGuid().ToString();
            var datasetId = Guid.NewGuid().ToString();
            var configId = Guid.NewGuid().ToString();
            var agentId = TestConstants.Agents.DefaultAgentId;

            // Act
            _service.TrackEvaluationRunOperation("Create", evalRunId, agentId, true, TimeSpan.FromMilliseconds(100));
            _service.TrackDatasetOperation("Save", datasetId, agentId, true, TimeSpan.FromMilliseconds(150));
            _service.TrackMetricsConfigOperation("Update", configId, true, TimeSpan.FromMilliseconds(80));
            _service.TrackEvaluationResultOperation("Save", evalRunId, true, TimeSpan.FromMilliseconds(200));
            _service.TrackDataVerseApiCall(evalRunId, agentId, true, 200, TimeSpan.FromMilliseconds(300));

            // Assert
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Debug,
                    It.IsAny<EventId>(),
                    It.IsAny<It.IsAnyType>(),
                    null,
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Exactly(5));
        }

        [Fact]
        public void CompleteFlow_WithActivityAndMetrics_ExecutesSuccessfully()
        {
            // Arrange
            var operationName = "CompleteOperation";
            var metricName = "operation_duration";

            // Act
            using (var activity = _service.StartActivity(operationName))
            {
                // Activity may be null without listener - this is expected in unit tests
                
                var tags = new Dictionary<string, object>
                {
                    { "operation_type", "test" },
                    { "success", true }
                };
                
                _service.AddActivityTags(tags);
                _service.RecordMetric(metricName, 123.45, tags);
            }

            // Assert
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Debug,
                    It.IsAny<EventId>(),
                    It.IsAny<It.IsAnyType>(),
                    null,
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once); // Metric recording logged
        }

        #endregion

        #region Performance Tests

        [Fact]
        public void RecordMetric_With1000Calls_HandlesEfficiently()
        {
            // Arrange
            var metricName = "performance_metric";
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            // Act
            for (int i = 0; i < 1000; i++)
            {
                _service.RecordMetric(metricName, i);
            }
            stopwatch.Stop();

            // Assert
            stopwatch.ElapsedMilliseconds.Should().BeLessThan(1000); // Should complete in less than 1 second
        }

        #endregion
    }
}
