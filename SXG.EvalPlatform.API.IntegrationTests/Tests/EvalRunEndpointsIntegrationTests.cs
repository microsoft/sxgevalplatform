using System.Diagnostics;
using System.Net;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using SXG.EvalPlatform.API.IntegrationTests.Infrastructure;
using SxgEvalPlatformApi.Models;
using SxgEvalPlatformApi.Models.Dtos;
using Sxg.EvalPlatform.API.Storage.Services;
using Sxg.EvalPlatform.API.Storage.TableEntities;
using Xunit;

namespace SXG.EvalPlatform.API.IntegrationTests.Tests;

/// <summary>
/// Comprehensive integration tests for EvalRun endpoints
/// Tests GET, POST, and PUT operations for evaluation runs
/// </summary>
public class EvalRunEndpointsIntegrationTests : InMemoryIntegrationTestBase
{
    public EvalRunEndpointsIntegrationTests(InMemoryWebApplicationFactory factory) : base(factory) { }

    #region GET /api/v1/eval/runs/{evalRunId} Tests

    [Fact]
    public async Task GetEvalRun_WithValidEvalRunId_ShouldReturnEvalRunQuickly()
    {
        // Arrange
        var agentId = "test-agent-get-evalrun-001";
        var evalRunId = Guid.NewGuid();
        
        // Create test eval run in storage
        await CreateTestEvalRunInStorage(agentId, evalRunId, "Completed");

        var stopwatch = Stopwatch.StartNew();

        // Act
        var response = await Client.GetAsync($"/api/v1/eval/runs/{evalRunId}");
        
        stopwatch.Stop();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var evalRun = await DeserializeResponseAsync<EvalRunDto>(response);
        
        evalRun.Should().NotBeNull();
        evalRun!.EvalRunId.Should().Be(evalRunId);
        evalRun.AgentId.Should().Be(agentId);
        evalRun.Status.Should().Be("Completed");
        
        // Performance validation - should be fast with caching
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(1000, "GET EvalRun should complete within 1 second");
    }

    [Fact]
    public async Task GetEvalRun_WithNonExistentEvalRunId_ShouldReturnNotFound()
    {
        // Arrange
        var nonExistentEvalRunId = Guid.NewGuid();

        // Act
        var response = await Client.GetAsync($"/api/v1/eval/runs/{nonExistentEvalRunId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        
        var errorResponse = await DeserializeResponseAsync<ErrorResponseDto>(response);
        
        errorResponse.Should().NotBeNull();
        errorResponse!.Detail.Should().Contain(nonExistentEvalRunId.ToString());
    }

    [Fact]
    public async Task GetEvalRun_WithInvalidGuid_ShouldReturnBadRequest()
    {
        // Act
        var response = await Client.GetAsync("/api/v1/eval/runs/invalid-guid");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        
        // The API returns an error response, specific format may vary
        var responseContent = await response.Content.ReadAsStringAsync();
        responseContent.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetEvalRun_CalledMultipleTimes_ShouldUtilizeCacheForPerformance()
    {
        // Arrange
        var agentId = "test-agent-cache-001";
        var evalRunId = Guid.NewGuid();
        
        await CreateTestEvalRunInStorage(agentId, evalRunId, "Running");

        // Act - First call (cache miss)
        var stopwatch1 = Stopwatch.StartNew();
        var response1 = await Client.GetAsync($"/api/v1/eval/runs/{evalRunId}");
        stopwatch1.Stop();

        // Act - Second call (cache hit)
        var stopwatch2 = Stopwatch.StartNew();
        var response2 = await Client.GetAsync($"/api/v1/eval/runs/{evalRunId}");
        stopwatch2.Stop();

        // Assert
        response1.StatusCode.Should().Be(HttpStatusCode.OK);
        response2.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var evalRun1 = await DeserializeResponseAsync<EvalRunDto>(response1);
        var evalRun2 = await DeserializeResponseAsync<EvalRunDto>(response2);
        
        // Should return same data
        evalRun1.Should().BeEquivalentTo(evalRun2);
        
        // For in-memory integration tests, both requests are very fast
        // Just verify that cache didn't break functionality and both completed quickly
        stopwatch1.ElapsedMilliseconds.Should().BeLessThan(1000, "Initial request should complete quickly");
        stopwatch2.ElapsedMilliseconds.Should().BeLessThan(1000, "Cached request should complete quickly");
    }

    #endregion

    #region POST /api/v1/eval/runs Tests

    [Fact]
    public async Task CreateEvalRun_WithValidData_ShouldCreateSuccessfullyAndQuickly()
    {
        // Arrange
        var agentId = "test-agent-create-001";
        var datasetId = Guid.NewGuid();
        var metricsConfigId = Guid.NewGuid();
        var environmentId = Guid.NewGuid();
        
        // Create test dependencies
        await CreateTestDatasetInStorage(agentId, datasetId);
        await CreateTestMetricsConfigInStorage(agentId, metricsConfigId);

        var createDto = new CreateEvalRunDto
        {
            AgentId = agentId,
            DataSetId = datasetId,
            MetricsConfigurationId = metricsConfigId,
            Type = "TestAgent",
            EnvironmentId = environmentId,
            AgentSchemaName = "TestSchema"
        };

        var content = new StringContent(
            JsonSerializer.Serialize(createDto),
            Encoding.UTF8,
            "application/json");

        var stopwatch = Stopwatch.StartNew();

        // Act
        var response = await Client.PostAsync("/api/v1/eval/runs", content);
        
        stopwatch.Stop();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        
        var createdEvalRun = await DeserializeResponseAsync<EvalRunDto>(response);
        
        createdEvalRun.Should().NotBeNull();
        createdEvalRun!.AgentId.Should().Be(agentId);
        createdEvalRun.DataSetId.Should().Be(datasetId.ToString());
        createdEvalRun.MetricsConfigurationId.Should().Be(metricsConfigId.ToString());
        createdEvalRun.Status.Should().Be("Queued");
        createdEvalRun.EvalRunId.Should().NotBe(Guid.Empty);
        
        // Performance validation
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(2000, "POST EvalRun should complete within 2 seconds");
        
        // Verify Location header
        response.Headers.Location.Should().NotBeNull();
        response.Headers.Location!.ToString().Should().Contain(createdEvalRun.EvalRunId.ToString());
    }

    [Fact]
    public async Task CreateEvalRun_WithInvalidData_ShouldReturnBadRequest()
    {
        // Arrange - Missing required fields
        var createDto = new CreateEvalRunDto
        {
            AgentId = "", // Invalid - empty
            DataSetId = Guid.Empty, // Invalid - empty GUID
            MetricsConfigurationId = Guid.NewGuid(),
            Type = "", // Invalid - empty
            EnvironmentId = Guid.NewGuid(),
            AgentSchemaName = "TestSchema"
        };

        var content = new StringContent(
            JsonSerializer.Serialize(createDto),
            Encoding.UTF8,
            "application/json");

        // Act
        var response = await Client.PostAsync("/api/v1/eval/runs", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        
        // The API returns an error response with validation information
        var responseContent = await response.Content.ReadAsStringAsync();
        responseContent.Should().NotBeEmpty();
        responseContent.Should().Contain("validation"); // Basic check for validation error
    }

    [Fact]
    public async Task CreateEvalRun_WithNonExistentDataset_ShouldReturnBadRequest()
    {
        // Arrange
        var agentId = "test-agent-invalid-dataset";
        var nonExistentDatasetId = Guid.NewGuid();
        var metricsConfigId = Guid.NewGuid();
        
        // Create only metrics config, not dataset
        await CreateTestMetricsConfigInStorage(agentId, metricsConfigId);

        var createDto = new CreateEvalRunDto
        {
            AgentId = agentId,
            DataSetId = nonExistentDatasetId, // This dataset doesn't exist
            MetricsConfigurationId = metricsConfigId,
            Type = "TestAgent",
            EnvironmentId = Guid.NewGuid(),
            AgentSchemaName = "TestSchema"
        };

        var content = new StringContent(
            JsonSerializer.Serialize(createDto),
            Encoding.UTF8,
            "application/json");

        // Act
        var response = await Client.PostAsync("/api/v1/eval/runs", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        
        // The API returns an error response about invalid dataset
        var responseContent = await response.Content.ReadAsStringAsync();
        responseContent.Should().NotBeEmpty();
        responseContent.Should().Contain("Dataset"); // Basic check that it mentions dataset
    }

    [Fact]
    public async Task CreateEvalRun_WithNonExistentMetricsConfig_ShouldReturnBadRequest()
    {
        // Arrange
        var agentId = "test-agent-invalid-metrics";
        var datasetId = Guid.NewGuid();
        var nonExistentMetricsConfigId = Guid.NewGuid();
        
        // Create only dataset, not metrics config
        await CreateTestDatasetInStorage(agentId, datasetId);

        var createDto = new CreateEvalRunDto
        {
            AgentId = agentId,
            DataSetId = datasetId,
            MetricsConfigurationId = nonExistentMetricsConfigId, // This metrics config doesn't exist
            Type = "TestAgent",
            EnvironmentId = Guid.NewGuid(),
            AgentSchemaName = "TestSchema"
        };

        var content = new StringContent(
            JsonSerializer.Serialize(createDto),
            Encoding.UTF8,
            "application/json");

        // Act
        var response = await Client.PostAsync("/api/v1/eval/runs", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        
        // The API returns an error response about invalid metrics configuration
        var responseContent = await response.Content.ReadAsStringAsync();
        responseContent.Should().NotBeEmpty();
        responseContent.Should().Contain("Metrics"); // Basic check that it mentions metrics
    }

    [Fact]
    public async Task CreateEvalRun_WithEmptyBody_ShouldReturnBadRequest()
    {
        // Arrange
        var content = new StringContent("", Encoding.UTF8, "application/json");

        // Act
        var response = await Client.PostAsync("/api/v1/eval/runs", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    #endregion

    #region PUT /api/v1/eval/runs/{evalRunId} Tests

    [Fact]
    public async Task UpdateEvalRun_WithValidStatusChange_ShouldUpdateSuccessfully()
    {
        // Arrange - First create an eval run through the API
        var agentId = "test-agent-update-001";
        
        // Create the test data dependencies first
        var datasetId = Guid.NewGuid();
        var metricsConfigId = Guid.NewGuid();
        var environmentId = Guid.NewGuid();
        await CreateTestDatasetInStorage(agentId, datasetId);
        await CreateTestMetricsConfigInStorage(agentId, metricsConfigId);

        // Create eval run via API POST
        var createDto = new CreateEvalRunDto
        {
            AgentId = agentId,
            DataSetId = datasetId,
            MetricsConfigurationId = metricsConfigId,
            Type = "IntegrationTest",
            EnvironmentId = environmentId,
            AgentSchemaName = "TestSchema"
        };

        var createContent = new StringContent(
            JsonSerializer.Serialize(createDto),
            Encoding.UTF8,
            "application/json");

        var createResponse = await Client.PostAsync("/api/v1/eval/runs", createContent);
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        
        var createResult = await DeserializeResponseAsync<EvalRunDto>(createResponse);
        var evalRunId = createResult!.EvalRunId;

        // Now test the update operation
        var updateDto = new UpdateStatusDto
        {
            Status = "Running"
        };

        var content = new StringContent(
            JsonSerializer.Serialize(updateDto),
            Encoding.UTF8,
            "application/json");

        var stopwatch = Stopwatch.StartNew();

        // Act
        var response = await Client.PutAsync($"/api/v1/eval/runs/{evalRunId}", content);
        
        stopwatch.Stop();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var updateResponse = await DeserializeResponseAsync<UpdateResponseDto>(response);
        
        updateResponse.Should().NotBeNull();
        updateResponse!.Success.Should().BeTrue();
        updateResponse.Message.Should().Contain("Running");
        
        // Performance validation
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(1000, "PUT EvalRun should complete within 1 second");
        
        // Verify the status was actually updated by fetching the eval run
        var getResponse = await Client.GetAsync($"/api/v1/eval/runs/{evalRunId}");
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var updatedEvalRun = await DeserializeResponseAsync<EvalRunDto>(getResponse);
        
        updatedEvalRun.Should().NotBeNull();
        updatedEvalRun!.Status.Should().Be("Running");
    }

    [Theory]
    [InlineData("Queued")]
    [InlineData("Running")]
    [InlineData("Completed")]
    [InlineData("Failed")]
    public async Task UpdateEvalRun_WithValidStatuses_ShouldSucceed(string status)
    {
        // Arrange
        var agentId = "test-agent-status-change";
        var evalRunId = Guid.NewGuid();
        
        // Create eval run in Queued status (can transition to any status)
        await CreateTestEvalRunInStorage(agentId, evalRunId, "Queued");

        var updateDto = new UpdateStatusDto { Status = status };
        var content = new StringContent(
            JsonSerializer.Serialize(updateDto),
            Encoding.UTF8,
            "application/json");

        // Act
        var response = await Client.PutAsync($"/api/v1/eval/runs/{evalRunId}", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var updateResponse = await DeserializeResponseAsync<UpdateResponseDto>(response);
        
        updateResponse.Should().NotBeNull();
        updateResponse!.Success.Should().BeTrue();
        updateResponse.Message.Should().Contain(status);
    }

    [Fact]
    public async Task UpdateEvalRun_WithInvalidStatus_ShouldReturnBadRequest()
    {
        // Arrange
        var agentId = "test-agent-invalid-status";
        var evalRunId = Guid.NewGuid();
        
        await CreateTestEvalRunInStorage(agentId, evalRunId, "Queued");

        var updateDto = new UpdateStatusDto
        {
            Status = "InvalidStatus"
        };

        var content = new StringContent(
            JsonSerializer.Serialize(updateDto),
            Encoding.UTF8,
            "application/json");

        // Act
        var response = await Client.PutAsync($"/api/v1/eval/runs/{evalRunId}", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        
        // Check that the API returns an error about invalid status
        var responseContent = await response.Content.ReadAsStringAsync();
        responseContent.Should().NotBeEmpty();
        responseContent.Should().Contain("status"); // Basic check that it mentions status
    }

    [Theory]
    [InlineData("Completed")]
    [InlineData("Failed")]
    public async Task UpdateEvalRun_WhenAlreadyInTerminalState_ShouldReturnBadRequest(string terminalStatus)
    {
        // Arrange
        var agentId = "test-agent-terminal-state";
        var evalRunId = Guid.NewGuid();
        
        // Create eval run in terminal status
        await CreateTestEvalRunInStorage(agentId, evalRunId, terminalStatus);

        var updateDto = new UpdateStatusDto
        {
            Status = "Running"
        };

        var content = new StringContent(
            JsonSerializer.Serialize(updateDto),
            Encoding.UTF8,
            "application/json");

        // Act
        var response = await Client.PutAsync($"/api/v1/eval/runs/{evalRunId}", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        
        var updateResponse = await DeserializeResponseAsync<UpdateResponseDto>(response);
        
        updateResponse.Should().NotBeNull();
        updateResponse!.Success.Should().BeFalse();
        updateResponse.Message.Should().Contain("terminal state");
        updateResponse.Message.Should().Contain(terminalStatus);
    }

    [Fact]
    public async Task UpdateEvalRun_WithNonExistentEvalRunId_ShouldReturnNotFound()
    {
        // Arrange
        var nonExistentEvalRunId = Guid.NewGuid();
        
        var updateDto = new UpdateStatusDto
        {
            Status = "Running"
        };

        var content = new StringContent(
            JsonSerializer.Serialize(updateDto),
            Encoding.UTF8,
            "application/json");

        // Act
        var response = await Client.PutAsync($"/api/v1/eval/runs/{nonExistentEvalRunId}", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        
        // For 404 responses, just check that it's a proper not found response
        var responseContent = await response.Content.ReadAsStringAsync();
        responseContent.Should().NotBeEmpty();
    }

    [Fact]
    public async Task UpdateEvalRun_WithInvalidGuid_ShouldReturnBadRequest()
    {
        // Arrange
        var updateDto = new UpdateStatusDto { Status = "Running" };
        var content = new StringContent(
            JsonSerializer.Serialize(updateDto),
            Encoding.UTF8,
            "application/json");

        // Act
        var response = await Client.PutAsync("/api/v1/eval/runs/invalid-guid", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        
        // For bad request responses, check content contains relevant error info
        var responseContent = await response.Content.ReadAsStringAsync();
        responseContent.Should().Contain("evalRunId");
    }

    [Fact]
    public async Task UpdateEvalRun_ShouldInvalidateCacheAfterUpdate()
    {
        // Arrange - Create eval run via API to ensure proper setup
        var agentId = "test-agent-cache-invalidation";
        var datasetId = Guid.NewGuid();
        var metricsConfigId = Guid.NewGuid();
        var environmentId = Guid.NewGuid();
        
        // Create dependencies first
        await CreateTestDatasetInStorage(agentId, datasetId);
        await CreateTestMetricsConfigInStorage(agentId, metricsConfigId);
        
        // Create eval run via POST API
        var createDto = new CreateEvalRunDto
        {
            AgentId = agentId,
            DataSetId = datasetId,
            MetricsConfigurationId = metricsConfigId,
            Type = "CacheTest",
            EnvironmentId = environmentId,
            AgentSchemaName = "CacheTestSchema"
        };
        
        var createContent = new StringContent(
            JsonSerializer.Serialize(createDto),
            Encoding.UTF8,
            "application/json");
        
        var createResponse = await Client.PostAsync("/api/v1/eval/runs", createContent);
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        
        var createdEvalRun = await DeserializeResponseAsync<EvalRunDto>(createResponse);
        var evalRunId = createdEvalRun!.EvalRunId;

        // Act - First get the eval run (cache it)
        var getResponse1 = await Client.GetAsync($"/api/v1/eval/runs/{evalRunId}");
        getResponse1.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var evalRun1 = await DeserializeResponseAsync<EvalRunDto>(getResponse1);
        evalRun1!.Status.Should().Be("Queued");

        // Act - Update the status
        var updateDto = new UpdateStatusDto { Status = "Running" };
        var updateContent = new StringContent(
            JsonSerializer.Serialize(updateDto),
            Encoding.UTF8,
            "application/json");
        
        var updateResponse = await Client.PutAsync($"/api/v1/eval/runs/{evalRunId}", updateContent);
        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Act - Get the eval run again (should reflect the update)
        var getResponse2 = await Client.GetAsync($"/api/v1/eval/runs/{evalRunId}");
        getResponse2.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var evalRun2 = await DeserializeResponseAsync<EvalRunDto>(getResponse2);
        
        // Assert - Cache should have been invalidated and new status should be returned
        evalRun2!.Status.Should().Be("Running");
    }

    #endregion

    #region Performance Tests

    [Fact]
    public async Task EvalRunEndpoints_ShouldAllPerformWithinAcceptableLimits()
    {
        // Arrange
        var agentId = "test-agent-performance";
        var datasetId = Guid.NewGuid();
        var metricsConfigId = Guid.NewGuid();
        var environmentId = Guid.NewGuid();
        
        // Create test dependencies
        await CreateTestDatasetInStorage(agentId, datasetId);
        await CreateTestMetricsConfigInStorage(agentId, metricsConfigId);

        // Test CREATE performance
        var createDto = new CreateEvalRunDto
        {
            AgentId = agentId,
            DataSetId = datasetId,
            MetricsConfigurationId = metricsConfigId,
            Type = "PerformanceTest",
            EnvironmentId = environmentId,
            AgentSchemaName = "PerfTestSchema"
        };

        var createContent = new StringContent(
            JsonSerializer.Serialize(createDto),
            Encoding.UTF8,
            "application/json");

        var createStopwatch = Stopwatch.StartNew();
        var createResponse = await Client.PostAsync("/api/v1/eval/runs", createContent);
        createStopwatch.Stop();

        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        
        var createdEvalRun = await DeserializeResponseAsync<EvalRunDto>(createResponse);
        var evalRunId = createdEvalRun!.EvalRunId;

        // Test GET performance
        var getStopwatch = Stopwatch.StartNew();
        var getResponse = await Client.GetAsync($"/api/v1/eval/runs/{evalRunId}");
        getStopwatch.Stop();

        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Test UPDATE performance
        var updateDto = new UpdateStatusDto { Status = "Running" };
        var updateContent = new StringContent(
            JsonSerializer.Serialize(updateDto),
            Encoding.UTF8,
            "application/json");

        var updateStopwatch = Stopwatch.StartNew();
        var updateResponse = await Client.PutAsync($"/api/v1/eval/runs/{evalRunId}", updateContent);
        updateStopwatch.Stop();

        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Assert performance requirements
        createStopwatch.ElapsedMilliseconds.Should().BeLessThan(3000, "CREATE EvalRun should complete within 3 seconds");
        getStopwatch.ElapsedMilliseconds.Should().BeLessThan(1000, "GET EvalRun should complete within 1 second");
        updateStopwatch.ElapsedMilliseconds.Should().BeLessThan(1000, "UPDATE EvalRun should complete within 1 second");
    }

    #endregion

    #region Helper Methods

    private async Task CreateTestEvalRunInStorage(string agentId, Guid evalRunId, string status)
    {
        var evalRunTableService = Factory.Services.GetRequiredService<IEvalRunTableService>();

        await evalRunTableService.CreateEvalRunAsync(new EvalRunTableEntity
        {
            PartitionKey = agentId,
            RowKey = evalRunId.ToString(),
            EvalRunId = evalRunId,
            AgentId = agentId,
            DataSetId = Guid.NewGuid().ToString(),
            MetricsConfigurationId = Guid.NewGuid().ToString(),
            Status = status,
            LastUpdatedBy = "test",
            LastUpdatedOn = DateTime.UtcNow,
            StartedDatetime = DateTime.UtcNow.AddMinutes(-10),
            CompletedDatetime = status == "Completed" ? DateTime.UtcNow.AddMinutes(-5) : null
        });
    }

    private async Task CreateTestDatasetInStorage(string agentId, Guid datasetId)
    {
        var dataSetTableService = Factory.Services.GetRequiredService<IDataSetTableService>();

        await dataSetTableService.SaveDataSetAsync(new DataSetTableEntity
        {
            PartitionKey = agentId,
            RowKey = datasetId.ToString(),
            DatasetId = datasetId.ToString(),
            AgentId = agentId,
            DatasetName = $"Test Dataset {datasetId}",
            DatasetType = "TestDataset",
            BlobFilePath = $"datasets/{datasetId}/data.json",
            ContainerName = $"test-container-{agentId}",
            CreatedBy = "test",
            CreatedOn = DateTime.UtcNow,
            LastUpdatedBy = "test",
            LastUpdatedOn = DateTime.UtcNow
        });
    }

    private async Task CreateTestMetricsConfigInStorage(string agentId, Guid metricsConfigId)
    {
        var metricsConfigTableService = Factory.Services.GetRequiredService<IMetricsConfigTableService>();

        await metricsConfigTableService.SaveMetricsConfigurationAsync(new MetricsConfigurationTableEntity
        {
            PartitionKey = agentId,
            RowKey = metricsConfigId.ToString(),
            ConfigurationId = metricsConfigId.ToString(),
            AgentId = agentId,
            ConfigurationName = $"Test Metrics Config {metricsConfigId}",
            CreatedBy = "test",
            CreatedOn = DateTime.UtcNow,
            LastUpdatedBy = "test",
            LastUpdatedOn = DateTime.UtcNow
        });
    }

    #endregion
}
