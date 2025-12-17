using FluentAssertions;
using Moq;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Sxg.EvalPlatform.API.Storage.Entities;
using SxgEvalPlatformApi.Controllers;
using SxgEvalPlatformApi.RequestHandlers;
using SxgEvalPlatformApi.Services;
using Microsoft.AspNetCore.Mvc;

namespace Sxg.EvalPlatform.API.IntegrationTests
{
    /// <summary>
    /// Direct controller tests - bypassing HTTP layer to test business logic directly
    /// Note: These are really unit/component tests, not true integration tests
    /// </summary>
    public class EvalConfigControllerDirectTests
    {
        private readonly Mock<IMetricsConfigurationRequestHandler> _mockRequestHandler;
        private readonly Mock<IConfiguration> _mockConfiguration;
        private readonly Mock<ILogger<EvalConfigsController>> _mockLogger;
        private readonly Mock<ICallerIdentificationService> _mockCallerService;
        private readonly Mock<IOpenTelemetryService> _mockTelemetryService;
        private readonly EvalConfigsController _controller;

        public EvalConfigControllerDirectTests()
        {
            _mockRequestHandler = new Mock<IMetricsConfigurationRequestHandler>();
            _mockConfiguration = new Mock<IConfiguration>();
            _mockLogger = new Mock<ILogger<EvalConfigsController>>();
            _mockCallerService = new Mock<ICallerIdentificationService>();
            _mockTelemetryService = new Mock<IOpenTelemetryService>();

            _controller = new EvalConfigsController(
               _mockRequestHandler.Object,
          _mockConfiguration.Object,
            _mockLogger.Object,
                    _mockCallerService.Object,
                    _mockTelemetryService.Object
                );
        }

  //      [Fact]
  //      public async Task WhenGetDefaultMetricsConfigurationMethodIsInvoked_ShouldReturnValidConfiguration()
  //      {
  //          // Arrange
  //          var expectedConfig = new DefaultMetricsConfiguration
  //          {
  //              Version = "1.0",
  //              LastUpdated = DateTime.UtcNow.AddDays(-1),
  //              Categories = new List<Category>
  //     {
  //     new Category
  //        {
  //            CategoryName = "Accuracy",
  //    DisplayName = "Accuracy Metrics",
  //    Metrics = new List<Metric>
  //       {
  //         new Metric
  //     {
  //     MetricName = "coherence",
  //   DisplayName = "Coherence",
  //         Description = "Measures coherence of responses",
  //     DefaultThreshold = 0.7,
  //    Enabled = true,
  //IsMandatory = false,
  //        ScoreRange = new ScoreRange { Min = 0, Max = 1 }
  //  }
  //    }
  //      }
  //   }
  //          };

  //          _mockRequestHandler
  //       .Setup(x => x.GetDefaultMetricsConfigurationAsync())
  // .ReturnsAsync(expectedConfig);

  //          // Act
  //          var result = await _controller.GetDefaultMetricsConfiguration();

  //          // Assert
  //          var okResult = result.Should().BeOfType<DefaultMetricsConfiguration>().Subject;

  //          // Assert - Version
  //          okResult.Version.Should().NotBeNullOrWhiteSpace("version must be specified");
  //          okResult.Version.Should().MatchRegex(@"^\d+\.\d+(\.\d+)?$",
  //            "version should follow semantic versioning format");

  //          // Assert - LastUpdated
  //          okResult.LastUpdated.Should().NotBe(default(DateTime),
  //        "lastUpdated should have a valid date");
  //          okResult.LastUpdated.Should().BeBefore(DateTime.UtcNow.AddDays(1),
  //          "lastUpdated should not be in the future");

  //          // Assert - Categories
  //          okResult.Categories.Should().NotBeNull("categories collection must be initialized");
  //          okResult.Categories.Should().NotBeEmpty("at least one category must be defined");

  //          // Assert category structure
  //          foreach (var category in okResult.Categories)
  //          {
  //              category.CategoryName.Should().NotBeNullOrWhiteSpace("category name is required");
  //              category.DisplayName.Should().NotBeNullOrWhiteSpace("display name is required");
  //              category.Metrics.Should().NotBeEmpty("at least one metric must be defined");

  //              foreach (var metric in category.Metrics)
  //              {
  //                  metric.MetricName.Should().NotBeNullOrWhiteSpace("metric name is required");
  //                  metric.DisplayName.Should().NotBeNullOrWhiteSpace("display name is required");
  //                  metric.Description.Should().NotBeNullOrWhiteSpace("description is required");
  //                  metric.DefaultThreshold.Should().BeGreaterThanOrEqualTo(0,
  //                 "default threshold should be non-negative");
  //                  metric.ScoreRange.Min.Should().BeLessThanOrEqualTo(metric.ScoreRange.Max,
  //            "score range min should be <= max");
  //                  metric.DefaultThreshold.Should().BeInRange(metric.ScoreRange.Min, metric.ScoreRange.Max,
  //               "default threshold should be within score range");
  //              }
  //          }

  //          // Verify the mock was called
  //          _mockRequestHandler.Verify(x => x.GetDefaultMetricsConfigurationAsync(), Times.Once);
  //      }

        [Fact]
        public async Task WhenGetConfigurationsByMetricsConfigurationId_ShouldReturnValidConfiguration()
        {
            // Arrange
            var configurationId = Guid.NewGuid();
            var expectedMetrics = new List<SelectedMetricsConfiguration>
{
      new SelectedMetricsConfiguration { MetricName = "coherence", Threshold = 0.75 },
       new SelectedMetricsConfiguration { MetricName = "groundedness", Threshold = 0.80 }
       };

            _mockRequestHandler
       .Setup(x => x.GetMetricsConfigurationByConfigurationIdAsync(configurationId.ToString()))
     .ReturnsAsync(expectedMetrics);

            // Act
            var result = await _controller.GetConfigurationsByMetricsConfigurationId(configurationId);

            // Assert
            var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
            var metrics = okResult.Value.Should().BeAssignableTo<IList<SelectedMetricsConfiguration>>().Subject;

            // Assert - Configuration Structure
            metrics.Should().NotBeEmpty("the configuration should contain at least one metric");
            metrics.Count.Should().Be(2, "we expect 2 metrics");

            // Assert - Each Selected Metric
            foreach (var metric in metrics)
            {
                metric.MetricName.Should().NotBeNullOrWhiteSpace("metric name is required");
                metric.Threshold.Should().BeGreaterThanOrEqualTo(0,
                       $"threshold should be non-negative for metric: {metric.MetricName}");
                metric.Threshold.Should().BeLessThanOrEqualTo(1.0,
                         $"threshold should not exceed 1.0 for metric: {metric.MetricName}");
            }

            // Assert - Specific Metrics
            var coherenceMetric = metrics.FirstOrDefault(m => m.MetricName == "coherence");
            coherenceMetric.Should().NotBeNull("coherence metric should be present");
            coherenceMetric!.Threshold.Should().Be(0.75, "coherence threshold should match");

            var groundednessMetric = metrics.FirstOrDefault(m => m.MetricName == "groundedness");
            groundednessMetric.Should().NotBeNull("groundedness metric should be present");
            groundednessMetric!.Threshold.Should().Be(0.80, "groundedness threshold should match");

            // Assert - Unique Metric Names
            var metricNames = metrics.Select(m => m.MetricName).ToList();
            metricNames.Should().OnlyHaveUniqueItems("metric names must be unique in a configuration");

            // Verify the mock was called
            _mockRequestHandler.Verify(
                   x => x.GetMetricsConfigurationByConfigurationIdAsync(configurationId.ToString()),
                   Times.Once);
        }

        [Fact]
        public async Task WhenGetConfigurationsByInvalidId_ShouldReturnNotFound()
        {
            // Arrange
            var invalidConfigurationId = Guid.NewGuid();

            _mockRequestHandler
          .Setup(x => x.GetMetricsConfigurationByConfigurationIdAsync(invalidConfigurationId.ToString()))
            .ReturnsAsync((IList<SelectedMetricsConfiguration>?)null);

            // Act
            var result = await _controller.GetConfigurationsByMetricsConfigurationId(invalidConfigurationId);

            // Assert
            result.Result.Should().BeOfType<NotFoundObjectResult>(
             "requesting a non-existent configuration should return NotFound");

            // Verify the mock was called
            _mockRequestHandler.Verify(
                        x => x.GetMetricsConfigurationByConfigurationIdAsync(invalidConfigurationId.ToString()),
          Times.Once);
        }
    }
}
