using System.Net;
using FluentAssertions;
using SXG.EvalPlatform.API.IntegrationTests.Infrastructure;
using SXG.EvalPlatform.API.IntegrationTests.Helpers;
using SxgEvalPlatformApi.Models.Dtos;
using Xunit;

namespace SXG.EvalPlatform.API.IntegrationTests.Tests;

/// <summary>
/// Integration tests for authentication and authorization scenarios
/// </summary>
[Collection("Integration Tests")]
public class EvalConfigAuthenticationTests : InMemoryIntegrationTestBase
{
    private const string BaseUrl = "/api/v1/eval";

    public EvalConfigAuthenticationTests(InMemoryWebApplicationFactory factory) : base(factory)
    {
    }

    #region Authentication Tests

    [Fact]
    public async Task AccessEndpoints_WithoutAuthentication_ShouldAllowAccess()
    {
        // Note: Assuming API doesn't require authentication for testing
        // Modify this test based on actual authentication requirements
        
        // Act & Assert
        var endpoints = new[]
        {
            $"{BaseUrl}/configurations",
            $"{BaseUrl}/defaultconfiguration"
        };

        foreach (var endpoint in endpoints)
        {
            var response = await Client.GetAsync(endpoint);
            
            // Should not be 401 Unauthorized if no auth is required
            response.StatusCode.Should().NotBe(HttpStatusCode.Unauthorized);
        }
    }

    [Fact]
    public async Task CreateConfiguration_WithValidRequest_ShouldNotRequireSpecialPermissions()
    {
        // Arrange
        var createDto = EnhancedTestDataHelper.CreateValidConfigurationDto();
        var content = CreateJsonContent(createDto);

        // Act
        var response = await Client.PostAsync($"{BaseUrl}/configurations", content);

        // Assert - Should not fail due to authorization
        response.StatusCode.Should().NotBe(HttpStatusCode.Unauthorized);
        response.StatusCode.Should().NotBe(HttpStatusCode.Forbidden);
    }

    #endregion

    #region Cross-Agent Access Tests

    [Fact]
    public async Task Agent_ShouldOnlyAccessOwnConfigurations()
    {
        // Arrange - Create configurations for different agents
        var agent1Config = EnhancedTestDataHelper.CreateValidConfigurationDto("agent-001");
        var agent2Config = EnhancedTestDataHelper.CreateValidConfigurationDto("agent-002");

        var content1 = CreateJsonContent(agent1Config);
        var content2 = CreateJsonContent(agent2Config);

        // Create configurations
        var response1 = await Client.PostAsync($"{BaseUrl}/configurations", content1);
        var response2 = await Client.PostAsync($"{BaseUrl}/configurations", content2);

        response1.StatusCode.Should().Be(HttpStatusCode.Created);
        response2.StatusCode.Should().Be(HttpStatusCode.Created);

        // Act - Get configurations for each agent
        var agent1Configs = await Client.GetAsync($"{BaseUrl}/configurations?agentId=agent-001");
        var agent2Configs = await Client.GetAsync($"{BaseUrl}/configurations?agentId=agent-002");

        // Assert - Each agent should only see their own configurations
        agent1Configs.StatusCode.Should().Be(HttpStatusCode.OK);
        agent2Configs.StatusCode.Should().Be(HttpStatusCode.OK);

        // Verify content isolation (would need to parse response to verify)
        var agent1Content = await agent1Configs.Content.ReadAsStringAsync();
        var agent2Content = await agent2Configs.Content.ReadAsStringAsync();

        agent1Content.Should().NotContain("agent-002");
        agent2Content.Should().NotContain("agent-001");
    }

    #endregion

    #region Rate Limiting Tests

    [Fact]
    public async Task MakeMultipleRapidRequests_ShouldNotBeBLimited()
    {
        // Act - Make rapid successive requests
        var tasks = new List<Task<HttpResponseMessage>>();
        
        for (int i = 0; i < 50; i++)
        {
            tasks.Add(Client.GetAsync($"{BaseUrl}/configurations?agentId=test-agent"));
        }

        var responses = await Task.WhenAll(tasks);

        // Assert - Should not be rate limited (adjust based on actual rate limiting policy)
        responses.Should().AllSatisfy(r => 
        {
            r.StatusCode.Should().NotBe(HttpStatusCode.TooManyRequests);
        });
    }

    #endregion

    #region Data Validation Security

    [Fact]
    public async Task CreateConfiguration_WithSQLInjectionAttempt_ShouldBeSanitized()
    {
        // Arrange - Try to inject SQL
        var maliciousDto = EnhancedTestDataHelper.CreateMaliciousConfigurationDto();
        
        var content = CreateJsonContent(maliciousDto);

        // Act
        var response = await Client.PostAsync($"{BaseUrl}/configurations", content);

        // Assert - Should handle malicious input gracefully
        // Either reject it (400) or sanitize it (201)
        response.StatusCode.Should().BeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.Created);
        
        // If created, verify it was sanitized properly
        if (response.StatusCode == HttpStatusCode.Created)
        {
            var location = response.Headers.Location?.ToString();
            var configId = ExtractIdFromLocation(location);
            
            var getResponse = await Client.GetAsync($"{BaseUrl}/configurations/{configId}");
            var responseContent = await getResponse.Content.ReadAsStringAsync();
            
            // Should not contain SQL injection attempts
            responseContent.Should().NotContain("DROP TABLE");
            responseContent.Should().NotContain("--;");
        }
    }

    [Fact]
    public async Task CreateConfiguration_WithXSSAttempt_ShouldBeSanitized()
    {
        // Arrange - Try to inject XSS  
        var maliciousDto = new CreateConfigurationRequestDto
        {
            AgentId = "test-agent",
            ConfigurationName = "<script>alert('xss')</script>",
            EnvironmentName = "Production", 
            Description = "<img src=x onerror=alert('xss')>",
            MetricsConfiguration = new List<SelectedMetricsConfigurationDto>
            {
                new SelectedMetricsConfigurationDto { MetricName = "BLEU", Threshold = 0.8 }
            }
        };
        
        var content = CreateJsonContent(maliciousDto);

        // Act
        var response = await Client.PostAsync($"{BaseUrl}/configurations", content);

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.Created);
        
        // If created, verify it was sanitized
        if (response.StatusCode == HttpStatusCode.Created)
        {
            var location = response.Headers.Location?.ToString();
            var configId = ExtractIdFromLocation(location);
            
            var getResponse = await Client.GetAsync($"{BaseUrl}/configurations/{configId}");
            var responseContent = await getResponse.Content.ReadAsStringAsync();
            
            // Should not contain script tags
            responseContent.Should().NotContain("<script>");
            responseContent.Should().NotContain("onerror=");
        }
    }

    #endregion

    #region Input Validation Security

    [Fact]
    public async Task CreateConfiguration_WithOversizedPayload_ShouldRejectRequest()
    {
        // Arrange - Create a very large payload
        var largeDto = EnhancedTestDataHelper.CreateValidConfigurationDto();
        largeDto.Description = new string('A', 1_000_000); // 1MB description
        
        var content = CreateJsonContent(largeDto);

        // Act
        var response = await Client.PostAsync($"{BaseUrl}/configurations", content);

        // Assert - Should reject oversized payloads
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.BadRequest, 
            HttpStatusCode.RequestEntityTooLarge,
            HttpStatusCode.RequestEntityTooLarge);
    }

    [Fact]
    public async Task GetConfiguration_WithMalformedGuid_ShouldReturnBadRequest()
    {
        // Arrange
        var malformedIds = new[]
        {
            "../../../etc/passwd",
            "'; DROP TABLE configurations; --",
            "<script>alert('xss')</script>",
            "../../../../windows/system32/config/sam",
            "null",
            "undefined"
        };

        // Act & Assert
        foreach (var malformedId in malformedIds)
        {
            var response = await Client.GetAsync($"{BaseUrl}/configurations/{malformedId}");
            
            // Should return 400 for malformed GUIDs
            response.StatusCode.Should().BeOneOf(
                HttpStatusCode.BadRequest, 
                HttpStatusCode.NotFound);
        }
    }

    #endregion

    #region Content Type Security

    [Fact]
    public async Task CreateConfiguration_WithInvalidContentType_ShouldReturnUnsupportedMediaType()
    {
        // Arrange
        var dto = EnhancedTestDataHelper.CreateValidConfigurationDto();
        var jsonContent = System.Text.Json.JsonSerializer.Serialize(dto);
        
        // Try with XML content type
        var content = new StringContent(jsonContent, System.Text.Encoding.UTF8, "application/xml");

        // Act
        var response = await Client.PostAsync($"{BaseUrl}/configurations", content);

        // Assert
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.UnsupportedMediaType,
            HttpStatusCode.BadRequest);
    }

    #endregion

    #region Helper Methods

    private string ExtractIdFromLocation(string? location)
    {
        if (string.IsNullOrEmpty(location))
            throw new ArgumentException("Location header is null or empty");

        var parts = location.Split('/');
        return parts[^1];
    }

    #endregion
}