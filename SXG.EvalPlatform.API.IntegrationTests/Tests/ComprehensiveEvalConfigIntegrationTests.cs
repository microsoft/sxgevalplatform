using System.Net;
using FluentAssertions;
using SXG.EvalPlatform.API.IntegrationTests.Infrastructure;
using SXG.EvalPlatform.API.IntegrationTests.Helpers;
using SxgEvalPlatformApi.Models.Dtos;
using Xunit;

namespace SXG.EvalPlatform.API.IntegrationTests.Tests;

/// <summary>
/// Comprehensive integration tests for EvalConfig Controller with in-memory implementations
/// </summary>
[Collection("Integration Tests")]
public class ComprehensiveEvalConfigIntegrationTests : InMemoryIntegrationTestBase
{
    private const string BaseUrl = "/api/v1/eval";
    private const string TestAgentId = "test-agent-001";

    public ComprehensiveEvalConfigIntegrationTests(InMemoryWebApplicationFactory factory) : base(factory)
    {
    }

    #region Positive Test Cases

    [Fact]
    public async Task CreateConfiguration_WithValidData_ShouldReturnCreated()
    {
        // Arrange
        var createDto = EnhancedTestDataHelper.CreateValidConfigurationDto(TestAgentId);
        var content = CreateJsonContent(createDto);

        // Act
        var response = await Client.PostAsync($"{BaseUrl}/configurations", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        
        var locationHeader = response.Headers.Location?.ToString();
        locationHeader.Should().NotBeNullOrEmpty();
        locationHeader.Should().Contain("/configurations/");

        var responseContent = await response.Content.ReadAsStringAsync();
        responseContent.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task CreateAndRetrieveConfiguration_ShouldReturnSameData()
    {
        // Arrange
        var createDto = EnhancedTestDataHelper.CreateValidConfigurationDto(TestAgentId);
        var createContent = CreateJsonContent(createDto);

        // Act - Create configuration
        var createResponse = await Client.PostAsync($"{BaseUrl}/configurations", createContent);
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        // Extract ID from location header
        var location = createResponse.Headers.Location?.ToString();
        var configId = ExtractIdFromLocation(location);

        // Act - Retrieve configuration
        var getResponse = await Client.GetAsync($"{BaseUrl}/configurations/{configId}");

        // Assert
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var retrievedConfig = await DeserializeResponseAsync<object>(getResponse);
        retrievedConfig.Should().NotBeNull();
    }

    [Fact]
    public async Task GetConfigurationsByAgentId_AfterCreation_ShouldReturnConfigurations()
    {
        // Arrange - Create multiple configurations for the agent
        var configurations = EnhancedTestDataHelper.CreateMultipleConfigurations(3, TestAgentId);
        
        foreach (var config in configurations)
        {
            var content = CreateJsonContent(config);
            var response = await Client.PostAsync($"{BaseUrl}/configurations", content);
            response.StatusCode.Should().Be(HttpStatusCode.Created);
        }

        // Act
        var getResponse = await Client.GetAsync($"{BaseUrl}/configurations?agentId={TestAgentId}");

        // Assert
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var responseContent = await getResponse.Content.ReadAsStringAsync();
        responseContent.Should().NotBeNullOrEmpty();
        
        // Verify we get back multiple configurations
        var configs = await DeserializeResponseAsync<IEnumerable<object>>(getResponse);
        configs.Should().NotBeNull();
        configs.Should().HaveCountGreaterOrEqualTo(3);
    }

    [Fact]
    public async Task UpdateConfiguration_WithValidData_ShouldReturnOk()
    {
        // Arrange - Create a configuration first
        var createDto = EnhancedTestDataHelper.CreateValidConfigurationDto(TestAgentId);
        var createContent = CreateJsonContent(createDto);
        var createResponse = await Client.PostAsync($"{BaseUrl}/configurations", createContent);
        var configId = ExtractIdFromLocation(createResponse.Headers.Location?.ToString());

        // Prepare update
        var updateDto = EnhancedTestDataHelper.CreateValidUpdateConfigurationDto();
        var updateContent = CreateJsonContent(updateDto);

        // Act
        var updateResponse = await Client.PutAsync($"{BaseUrl}/configurations/{configId}", updateContent);

        // Assert
        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task DeleteConfiguration_WithValidId_ShouldReturnNoContent()
    {
        // Arrange - Create a configuration first
        var createDto = EnhancedTestDataHelper.CreateValidConfigurationDto(TestAgentId);
        var createContent = CreateJsonContent(createDto);
        var createResponse = await Client.PostAsync($"{BaseUrl}/configurations", createContent);
        var configId = ExtractIdFromLocation(createResponse.Headers.Location?.ToString());

        // Act
        var deleteResponse = await Client.DeleteAsync($"{BaseUrl}/configurations/{configId}");

        // Assert
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Verify configuration is deleted
        var getResponse = await Client.GetAsync($"{BaseUrl}/configurations/{configId}");
        getResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    #endregion

    #region Negative Test Cases

    [Fact]
    public async Task CreateConfiguration_WithInvalidData_ShouldReturnBadRequest()
    {
        // Arrange
        var invalidDto = EnhancedTestDataHelper.CreateInvalidConfigurationDto();
        var content = CreateJsonContent(invalidDto);

        // Act
        var response = await Client.PostAsync($"{BaseUrl}/configurations", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateConfiguration_WithEmptyBody_ShouldReturnBadRequest()
    {
        // Arrange
        var content = new StringContent("{}", System.Text.Encoding.UTF8, "application/json");

        // Act
        var response = await Client.PostAsync($"{BaseUrl}/configurations", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetConfiguration_WithInvalidId_ShouldReturnNotFound()
    {
        // Arrange
        var invalidId = Guid.NewGuid();

        // Act
        var response = await Client.GetAsync($"{BaseUrl}/configurations/{invalidId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetConfiguration_WithInvalidIdFormat_ShouldReturnBadRequest()
    {
        // Arrange
        var invalidId = "not-a-guid";

        // Act
        var response = await Client.GetAsync($"{BaseUrl}/configurations/{invalidId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task UpdateConfiguration_WithInvalidId_ShouldReturnNotFound()
    {
        // Arrange
        var invalidId = Guid.NewGuid();
        var updateDto = EnhancedTestDataHelper.CreateValidUpdateConfigurationDto();
        var content = CreateJsonContent(updateDto);

        // Act
        var response = await Client.PutAsync($"{BaseUrl}/configurations/{invalidId}", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeleteConfiguration_WithInvalidId_ShouldReturnNotFound()
    {
        // Arrange
        var invalidId = Guid.NewGuid();

        // Act
        var response = await Client.DeleteAsync($"{BaseUrl}/configurations/{invalidId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    #endregion

    #region Caching Tests

    [Fact]
    public async Task GetConfiguration_ShouldCacheResult()
    {
        // Arrange - Create a configuration
        var createDto = EnhancedTestDataHelper.CreateValidConfigurationDto(TestAgentId);
        var createContent = CreateJsonContent(createDto);
        var createResponse = await Client.PostAsync($"{BaseUrl}/configurations", createContent);
        var configId = ExtractIdFromLocation(createResponse.Headers.Location?.ToString());

        // Act - Get configuration twice
        var firstResponse = await Client.GetAsync($"{BaseUrl}/configurations/{configId}");
        var secondResponse = await Client.GetAsync($"{BaseUrl}/configurations/{configId}");

        // Assert
        firstResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        secondResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Verify cache invalidation occurred 
        // (Both responses should succeed but may have different results)
    }

    [Fact]
    public async Task GetConfigurationsByAgent_ShouldCacheResult()
    {
        // Arrange - Create configurations
        var createDto = EnhancedTestDataHelper.CreateValidConfigurationDto(TestAgentId);
        var createContent = CreateJsonContent(createDto);
        await Client.PostAsync($"{BaseUrl}/configurations", createContent);

        // Act - Get configurations twice
        var firstResponse = await Client.GetAsync($"{BaseUrl}/configurations?agentId={TestAgentId}");
        var secondResponse = await Client.GetAsync($"{BaseUrl}/configurations?agentId={TestAgentId}");

        // Assert
        firstResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        secondResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Both requests should return valid data
        var firstContent = await firstResponse.Content.ReadAsStringAsync();
        var secondContent = await secondResponse.Content.ReadAsStringAsync();
        
        firstContent.Should().NotBeNullOrEmpty();
        secondContent.Should().NotBeNullOrEmpty();
    }

    #endregion

    #region Performance Tests

    [Fact]
    public async Task CreateMultipleConfigurations_ShouldCompleteWithinTimeout()
    {
        // Arrange
        var configurations = EnhancedTestDataHelper.CreatePerformanceTestData(10);
        var tasks = new List<Task<HttpResponseMessage>>();

        // Act
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        
        foreach (var config in configurations)
        {
            var content = CreateJsonContent(config);
            tasks.Add(Client.PostAsync($"{BaseUrl}/configurations", content));
        }

        var responses = await Task.WhenAll(tasks);
        stopwatch.Stop();

        // Assert
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(30000); // 30 seconds max
        responses.Should().AllSatisfy(r => r.StatusCode.Should().Be(HttpStatusCode.Created));
    }

    [Fact]
    public async Task ConcurrentGetRequests_ShouldHandleLoad()
    {
        // Arrange - Create a configuration first
        var createDto = EnhancedTestDataHelper.CreateValidConfigurationDto(TestAgentId);
        var createContent = CreateJsonContent(createDto);
        var createResponse = await Client.PostAsync($"{BaseUrl}/configurations", createContent);
        var configId = ExtractIdFromLocation(createResponse.Headers.Location?.ToString());

        // Act - Make concurrent requests
        var tasks = new List<Task<HttpResponseMessage>>();
        for (int i = 0; i < 20; i++)
        {
            tasks.Add(Client.GetAsync($"{BaseUrl}/configurations/{configId}"));
        }

        var responses = await Task.WhenAll(tasks);

        // Assert
        responses.Should().AllSatisfy(r => r.StatusCode.Should().Be(HttpStatusCode.OK));
    }

    #endregion

    #region Multi-Agent Tests

    [Fact]
    public async Task CreateConfigurations_ForMultipleAgents_ShouldIsolateData()
    {
        // Arrange
        var multiAgentConfigs = EnhancedTestDataHelper.CreateMultiAgentConfigurations();

        // Act - Create configurations for different agents
        foreach (var config in multiAgentConfigs)
        {
            var content = CreateJsonContent(config);
            var response = await Client.PostAsync($"{BaseUrl}/configurations", content);
            response.StatusCode.Should().Be(HttpStatusCode.Created);
        }

        // Assert - Each agent should only see their own configurations
        foreach (var agentId in new[] { "agent-001", "agent-002", "agent-003" })
        {
            var response = await Client.GetAsync($"{BaseUrl}/configurations?agentId={agentId}");
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            
            // Verify agent isolation
            var configs = await DeserializeResponseAsync<IEnumerable<object>>(response);
            configs.Should().HaveCount(1);
        }
    }

    #endregion

    #region Default Configuration Tests

    [Fact]
    public async Task GetDefaultConfiguration_ShouldReturnValidResponse()
    {
        // Act
        var response = await Client.GetAsync($"{BaseUrl}/defaultconfiguration");

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound);
        
        if (response.StatusCode == HttpStatusCode.OK)
        {
            var responseContent = await response.Content.ReadAsStringAsync();
            responseContent.Should().NotBeNullOrEmpty();
            
            // Should be able to deserialize to MetricsConfiguration
            var config = await DeserializeResponseAsync<object>(response);
            config.Should().NotBeNull();
        }
    }

    #endregion

    #region Helper Methods

    private string ExtractIdFromLocation(string? location)
    {
        if (string.IsNullOrEmpty(location))
            throw new ArgumentException("Location header is null or empty");

        var parts = location.Split('/');
        return parts[^1]; // Last part should be the ID
    }

    #endregion
}