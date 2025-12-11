using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Sxg.EvalPlatform.API.Storage.Entities;
using System.Net;

namespace Sxg.EvalPlatform.API.IntegrationTests
{
    /// <summary>
    /// Integration tests for EvalConfigsController endpoints
    /// These tests verify that the API endpoints return expected data and maintain data integrity
    /// </summary>
    public class EvalConfigControllerIntegrationTests : IntegrationTestBase
    {
        public EvalConfigControllerIntegrationTests(WebApplicationFactory<Program> factory)
                : base(factory)
        {
        }

        [Fact]
        public async Task WhenGetDefaultMetricsConfigurationMethodIsInvokedItShouldReturnTheDefaultMetricsConfigurations()
        {
            // Arrange
            var endpoint = "/api/v1/eval/configurations/defaultconfiguration";

            // Act
            var response = await Client.GetAsync(endpoint);
            var result = await response.Content.ReadFromJsonAsync<DefaultMetricsConfiguration>();

            // Assert - Response Status
            response.StatusCode.Should().Be(HttpStatusCode.OK, "the endpoint should return 200 OK");
            result.Should().NotBeNull("the response should contain a valid DefaultMetricsConfiguration object");

            // Assert - Version
            result!.Version.Should().NotBeNullOrWhiteSpace("version must be specified");
            result.Version.Should().MatchRegex(@"^\d+\.\d+(\.\d+)?$",
      "version should follow semantic versioning format (e.g., 1.0 or 1.0.0)");

            // Assert - LastUpdated
            result.LastUpdated.Should().NotBe(default(DateTime),
     "lastUpdated should have a valid date");
            result.LastUpdated.Should().BeBefore(DateTime.UtcNow.AddDays(1),
             "lastUpdated should not be in the future");

            // Assert - Categories
            result.Categories.Should().NotBeNull("categories collection must be initialized");
            result.Categories.Should().NotBeEmpty("at least one category must be defined");

            // Assert each category structure
            foreach (var category in result.Categories)
            {
                // Category level assertions
                category.CategoryName.Should().NotBeNullOrWhiteSpace(
                             $"category name is required for category: {category.CategoryName}");
                category.DisplayName.Should().NotBeNullOrWhiteSpace(
                 $"display name is required for category: {category.CategoryName}");
                category.Metrics.Should().NotBeNull(
             $"metrics collection must be initialized for category: {category.CategoryName}");
                category.Metrics.Should().NotBeEmpty(
           $"at least one metric must be defined for category: {category.CategoryName}");

                // Metric level assertions
                foreach (var metric in category.Metrics)
                {
                    metric.MetricName.Should().NotBeNullOrWhiteSpace(
                    $"metric name is required in category: {category.CategoryName}");
                    metric.DisplayName.Should().NotBeNullOrWhiteSpace(
                              $"display name is required for metric: {metric.MetricName}");
                    metric.Description.Should().NotBeNullOrWhiteSpace(
                      $"description is required for metric: {metric.MetricName}");

                    // DefaultThreshold validations
                    metric.DefaultThreshold.Should().BeGreaterThanOrEqualTo(0,
                             $"default threshold should be non-negative for metric: {metric.MetricName}");

                    // ScoreRange validations
                    metric.ScoreRange.Should().NotBeNull(
                 $"score range must be defined for metric: {metric.MetricName}");
                    metric.ScoreRange.Min.Should().BeLessThanOrEqualTo(metric.ScoreRange.Max,
          $"score range min should be less than or equal to max for metric: {metric.MetricName}");
                    metric.DefaultThreshold.Should().BeInRange(metric.ScoreRange.Min, metric.ScoreRange.Max,
                          $"default threshold should be within score range for metric: {metric.MetricName}");

                    // Boolean field validations - just verify they have valid boolean values
                    (metric.Enabled == true || metric.Enabled == false).Should().BeTrue(
                       $"enabled should be a boolean for metric: {metric.MetricName}");
                    (metric.IsMandatory == true || metric.IsMandatory == false).Should().BeTrue(
              $"isMandatory should be a boolean for metric: {metric.MetricName}");
                }
            }

            // Additional structural assertions
            var categoryNames = result.Categories.Select(c => c.CategoryName).ToList();
            categoryNames.Should().OnlyHaveUniqueItems("category names must be unique");

            foreach (var category in result.Categories)
            {
                var metricNames = category.Metrics.Select(m => m.MetricName).ToList();
                metricNames.Should().OnlyHaveUniqueItems(
          $"metric names must be unique within category: {category.CategoryName}");
            }
        }

        [Fact]
        public async Task WhenGetConfigurationsByMetricsConfigurationIdIsInvokedItShouldReturnTheMetricsConfiguration()
        {
            // Arrange
            // First, we need to create a test configuration to retrieve
            var testAgentId = "test-agent-integration";
            var testConfigurationName = "IntegrationTestConfig";
            var testEnvironmentName = "test";

            var createRequest = new
            {
                agentId = testAgentId,
                configurationName = testConfigurationName,
                environmentName = testEnvironmentName,
                description = "Integration test configuration",
                metricsConfiguration = new[]
                 {
                    new { metricName = "coherence", threshold = 0.75 },
     new { metricName = "groundedness", threshold = 0.80 }
    }
            };

            // Create a configuration first
            var createResponse = await PostAsync("/api/v1/eval/configurations", createRequest);
            var acceptableStatuses = new[] { HttpStatusCode.Created, HttpStatusCode.OK };
            acceptableStatuses.Should().Contain(createResponse.StatusCode,
             "configuration should be created or already exist");

            var createResult = await createResponse.Content.ReadFromJsonAsync<ConfigurationResponse>();
            createResult.Should().NotBeNull("create response should contain data");
            var configurationId = createResult!.ConfigurationId;
            configurationId.Should().NotBeNullOrWhiteSpace("configuration ID should be returned");

            // Act - Retrieve the configuration
            var endpoint = $"/api/v1/eval/configurations/{configurationId}";
            var response = await Client.GetAsync(endpoint);
            var result = await response.Content.ReadFromJsonAsync<List<SelectedMetricsConfiguration>>();

            // Assert - Response Status
            response.StatusCode.Should().Be(HttpStatusCode.OK,
                 "the endpoint should return 200 OK for valid configuration ID");
            result.Should().NotBeNull("the response should contain a valid metrics configuration list");

            // Assert - Configuration Structure
            result.Should().NotBeEmpty("the configuration should contain at least one metric");
            result!.Count.Should().Be(2, "we created a configuration with 2 metrics");

            // Assert - Each Selected Metric
            foreach (var metric in result)
            {
                metric.MetricName.Should().NotBeNullOrWhiteSpace("metric name is required");
                metric.Threshold.Should().BeGreaterThanOrEqualTo(0,
               $"threshold should be non-negative for metric: {metric.MetricName}");
                metric.Threshold.Should().BeLessThanOrEqualTo(1.0,
                      $"threshold should not exceed 1.0 for metric: {metric.MetricName}");
            }

            // Assert - Specific Metrics
            var coherenceMetric = result.FirstOrDefault(m => m.MetricName == "coherence");
            coherenceMetric.Should().NotBeNull("coherence metric should be present");
            coherenceMetric!.Threshold.Should().Be(0.75, "coherence threshold should match the created value");

            var groundednessMetric = result.FirstOrDefault(m => m.MetricName == "groundedness");
            groundednessMetric.Should().NotBeNull("groundedness metric should be present");
            groundednessMetric!.Threshold.Should().Be(0.80, "groundedness threshold should match the created value");

            // Assert - Unique Metric Names
            var metricNames = result.Select(m => m.MetricName).ToList();
            metricNames.Should().OnlyHaveUniqueItems("metric names must be unique in a configuration");

            // Cleanup - Delete the test configuration
            var deleteResponse = await DeleteAsync($"/api/v1/eval/configurations/{configurationId}");
            deleteResponse.StatusCode.Should().Be(HttpStatusCode.OK,
       "test configuration should be cleaned up successfully");
        }

        [Fact]
        public async Task WhenGetConfigurationsByInvalidIdIsInvokedItShouldReturnNotFound()
        {
            // Arrange
            var invalidConfigurationId = Guid.NewGuid(); // Random GUID that doesn't exist
            var endpoint = $"/api/v1/eval/configurations/{invalidConfigurationId}";

            // Act
            var response = await Client.GetAsync(endpoint);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.NotFound,
    "requesting a non-existent configuration should return 404");
        }

        // Helper class for deserialization
        private class ConfigurationResponse
        {
            public string ConfigurationId { get; set; } = string.Empty;
            public string Status { get; set; } = string.Empty;
            public string Message { get; set; } = string.Empty;
        }
    }
}