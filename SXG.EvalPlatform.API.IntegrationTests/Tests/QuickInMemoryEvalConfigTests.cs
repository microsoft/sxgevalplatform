using System.Collections.Generic;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using FluentAssertions;
using SXG.EvalPlatform.API.IntegrationTests.Infrastructure;
using SXG.EvalPlatform.API.IntegrationTests.Helpers;
using SxgEvalPlatformApi.Models.Dtos;
using Xunit;

namespace SXG.EvalPlatform.API.IntegrationTests.Tests;

/// <summary>
/// Quick test with in-memory implementations to verify the test works
/// </summary>
[Collection("Integration Tests")]
public class QuickInMemoryEvalConfigTests : InMemoryIntegrationTestBase
{
    private const string BaseUrl = "/api/v1/eval";
    private const string TestAgentId = "test-agent-001";

    public QuickInMemoryEvalConfigTests(InMemoryWebApplicationFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task CreateConfiguration_WithValidData_ShouldReturnCreated()
    {
        // Arrange
        var createDto = new CreateConfigurationRequestDto
        {
            AgentId = TestAgentId,
            ConfigurationName = "test-config",
            EnvironmentName = "Production",
            Description = "Test configuration for integration tests",
            MetricsConfiguration = new List<SelectedMetricsConfigurationDto>
            {
                new SelectedMetricsConfigurationDto { MetricName = "BLEU", Threshold = 0.8 },
                new SelectedMetricsConfigurationDto { MetricName = "ROUGE", Threshold = 0.75 }
            }
        };

        var jsonContent = JsonContent.Create(createDto);

        // Act
        var response = await Client.PostAsync($"{BaseUrl}/configurations", jsonContent);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        
        var responseContent = await response.Content.ReadAsStringAsync();
        responseContent.Should().NotBeNullOrWhiteSpace();
    }
}