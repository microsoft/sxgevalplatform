using System.Net;
using FluentAssertions;
using SXG.EvalPlatform.API.IntegrationTests.Infrastructure;
using SxgEvalPlatformApi.Models.Dtos;
using Xunit;

namespace SXG.EvalPlatform.API.IntegrationTests.Tests;

/// <summary>
/// Integration tests for the Health API endpoint
/// Tests basic health check functionality, response format, and performance
/// </summary>
[Collection("Integration Tests")]
public class HealthEndpointIntegrationTests : InMemoryIntegrationTestBase
{
    private const string BaseUrl = "/api/v1/health";

    public HealthEndpointIntegrationTests(InMemoryWebApplicationFactory factory) : base(factory)
    {
    }

    #region GET /api/v1/health Tests

    [Fact]
    public async Task GetHealth_ShouldReturnHealthyStatus()
    {
        // Act
        var response = await Client.GetAsync(BaseUrl);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var healthStatus = await DeserializeResponseAsync<HealthStatusDto>(response);
        healthStatus.Should().NotBeNull();
        healthStatus!.Status.Should().Be("Healthy");
        healthStatus.Version.Should().NotBeNullOrEmpty();
        healthStatus.Environment.Should().NotBeNullOrEmpty();
        healthStatus.Timestamp.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));
    }

    [Fact]
    public async Task GetHealth_ShouldReturnCorrectContentType()
    {
        // Act
        var response = await Client.GetAsync(BaseUrl);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/json");
    }

    [Fact]
    public async Task GetHealth_ShouldContainRequiredFields()
    {
        // Act
        var response = await Client.GetAsync(BaseUrl);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var healthStatus = await DeserializeResponseAsync<HealthStatusDto>(response);
        healthStatus.Should().NotBeNull();
        
        // Verify all required fields are present and valid
        healthStatus!.Status.Should().NotBeNullOrEmpty("Status field is required");
        healthStatus.Version.Should().NotBeNullOrEmpty("Version field is required");
        healthStatus.Environment.Should().NotBeNullOrEmpty("Environment field is required");
        healthStatus.Timestamp.Should().NotBe(default(DateTime), "Timestamp field is required");
        
        // Verify timestamp is recent (within last 5 seconds)
        var timeDifference = Math.Abs((DateTime.UtcNow - healthStatus.Timestamp).TotalSeconds);
        timeDifference.Should().BeLessThan(5, "Timestamp should be very recent");
    }

    [Fact]
    public async Task GetHealth_ShouldHaveValidEnvironmentValue()
    {
        // Act
        var response = await Client.GetAsync(BaseUrl);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var healthStatus = await DeserializeResponseAsync<HealthStatusDto>(response);
        healthStatus.Should().NotBeNull();
        
        // Environment should be one of the expected values
        var validEnvironments = new[] { "Development", "Staging", "Production", "Test" };
        healthStatus!.Environment.Should().BeOneOf(validEnvironments, 
            "Environment should be a standard ASP.NET Core environment name");
    }

    [Fact]
    public async Task GetHealth_ShouldReturnConsistentResponse()
    {
        // Act - Call health endpoint multiple times
        var response1 = await Client.GetAsync(BaseUrl);
        var response2 = await Client.GetAsync(BaseUrl);
        var response3 = await Client.GetAsync(BaseUrl);

        // Assert - All responses should be successful
        response1.StatusCode.Should().Be(HttpStatusCode.OK);
        response2.StatusCode.Should().Be(HttpStatusCode.OK);
        response3.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var health1 = await DeserializeResponseAsync<HealthStatusDto>(response1);
        var health2 = await DeserializeResponseAsync<HealthStatusDto>(response2);
        var health3 = await DeserializeResponseAsync<HealthStatusDto>(response3);
        
        // Status, Version, and Environment should be consistent
        health1!.Status.Should().Be(health2!.Status).And.Be(health3!.Status);
        health1.Version.Should().Be(health2.Version).And.Be(health3.Version);
        health1.Environment.Should().Be(health2.Environment).And.Be(health3.Environment);
        
        // Timestamps should be different (or very close)
        health1.Timestamp.Should().BeBefore(health3.Timestamp.AddSeconds(1));
    }

    [Fact]
    public async Task GetHealth_ShouldPerformQuickly()
    {
        // Arrange
        var maxResponseTime = TimeSpan.FromMilliseconds(100);

        // Act
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var response = await Client.GetAsync(BaseUrl);
        stopwatch.Stop();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        stopwatch.Elapsed.Should().BeLessThan(maxResponseTime, 
            "Health endpoint should respond very quickly as it's used for monitoring");
        
        var healthStatus = await DeserializeResponseAsync<HealthStatusDto>(response);
        healthStatus.Should().NotBeNull();
        healthStatus!.Status.Should().Be("Healthy");
    }

    [Fact]
    public async Task GetHealth_WithMultipleConcurrentRequests_ShouldHandleCorrectly()
    {
        // Arrange
        var numberOfRequests = 10;
        var tasks = new List<Task<HttpResponseMessage>>();

        // Act - Make multiple concurrent requests
        for (int i = 0; i < numberOfRequests; i++)
        {
            tasks.Add(Client.GetAsync(BaseUrl));
        }

        var responses = await Task.WhenAll(tasks);

        // Assert - All requests should succeed
        foreach (var response in responses)
        {
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            
            var healthStatus = await DeserializeResponseAsync<HealthStatusDto>(response);
            healthStatus.Should().NotBeNull();
            healthStatus!.Status.Should().Be("Healthy");
        }
    }

    [Theory]
    [InlineData("GET")]
    public async Task GetHealth_ShouldOnlySupportGetMethod(string method)
    {
        // Act & Assert for GET (should work)
        if (method == "GET")
        {
            var response = await Client.GetAsync(BaseUrl);
            response.StatusCode.Should().Be(HttpStatusCode.OK);
        }
    }

    [Fact]
    public async Task GetHealth_WithUnsupportedMethods_ShouldReturnMethodNotAllowed()
    {
        // Act & Assert for unsupported methods
        var postResponse = await Client.PostAsync(BaseUrl, null);
        var putResponse = await Client.PutAsync(BaseUrl, null);
        var deleteResponse = await Client.DeleteAsync(BaseUrl);

        // Assert
        postResponse.StatusCode.Should().Be(HttpStatusCode.MethodNotAllowed);
        putResponse.StatusCode.Should().Be(HttpStatusCode.MethodNotAllowed);
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.MethodNotAllowed);
    }

    [Fact]
    public async Task GetHealth_ShouldNotRequireAuthentication()
    {
        // Note: Health endpoints typically should be accessible without authentication
        // for monitoring purposes
        
        // Act
        var response = await Client.GetAsync(BaseUrl);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK, 
            "Health endpoint should be accessible without authentication for monitoring");
    }

    #endregion

    #region Response Validation Tests

    [Fact]
    public async Task GetHealth_ResponseSchema_ShouldMatchExpectedStructure()
    {
        // Act
        var response = await Client.GetAsync(BaseUrl);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var content = await response.Content.ReadAsStringAsync();
        content.Should().NotBeNullOrEmpty();
        
        // Verify JSON structure contains expected properties
        content.Should().Contain("\"status\"", "Response should contain status field");
        content.Should().Contain("\"timestamp\"", "Response should contain timestamp field");
        content.Should().Contain("\"version\"", "Response should contain version field");
        content.Should().Contain("\"environment\"", "Response should contain environment field");
        
        // Verify it deserializes correctly
        var healthStatus = await DeserializeResponseAsync<HealthStatusDto>(response);
        healthStatus.Should().NotBeNull("Response should deserialize to HealthStatusDto");
    }

    [Fact]
    public async Task GetHealth_TimestampFormat_ShouldBeUtc()
    {
        // Act
        var response = await Client.GetAsync(BaseUrl);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var healthStatus = await DeserializeResponseAsync<HealthStatusDto>(response);
        healthStatus.Should().NotBeNull();
        
        // Timestamp should be in UTC and recent
        var utcNow = DateTime.UtcNow;
        healthStatus!.Timestamp.Kind.Should().Be(DateTimeKind.Utc, 
            "Health check timestamp should be in UTC");
        healthStatus.Timestamp.Should().BeBefore(utcNow.AddSeconds(1), 
            "Timestamp should be very recent");
        healthStatus.Timestamp.Should().BeAfter(utcNow.AddMinutes(-1), 
            "Timestamp should not be too old");
    }

    #endregion
}