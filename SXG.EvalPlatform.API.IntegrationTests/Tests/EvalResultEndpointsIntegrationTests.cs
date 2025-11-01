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
/// Comprehensive integration tests for regular EvalResult API endpoints
/// Ensures complete coverage of standard endpoints with proper validation
/// </summary>
[Collection("Integration Tests")]
public class EvalResultEndpointsIntegrationTests : InMemoryIntegrationTestBase
{
    private const string BaseUrl = "/api/v1/eval/results";

    public EvalResultEndpointsIntegrationTests(InMemoryWebApplicationFactory factory) : base(factory)
    {
    }

    #region Helper Methods

    private static JsonElement CreateTestEvaluationRecords(int recordCount = 3)
    {
        var testRecords = new List<object>();
        for (int i = 1; i <= recordCount; i++)
        {
            testRecords.Add(new
            {
                id = i,
                question = $"Test question {i}?",
                expectedAnswer = $"Expected answer {i}",
                actualAnswer = $"Actual answer {i}",
                metrics = new
                {
                    bleu = 0.85 + (i * 0.01),
                    rouge = 0.78 + (i * 0.02),
                    accuracy = 0.90 + (i * 0.01)
                },
                timestamp = DateTime.UtcNow.AddMinutes(-i).ToString("O")
            });
        }

        var json = JsonSerializer.Serialize(testRecords);
        return JsonSerializer.Deserialize<JsonElement>(json);
    }

    private static SaveEvaluationResultDto CreateValidSaveEvaluationResultDto(Guid evalRunId, int recordCount = 3)
    {
        return new SaveEvaluationResultDto
        {
            EvalRunId = evalRunId,
            EvaluationRecords = CreateTestEvaluationRecords(recordCount)
        };
    }

    private async Task<EvalRunTableEntity> CreateTestEvalRunInStorage(string agentId, Guid evalRunId, string status = "Completed")
    {
        var evalRunTableService = Factory.Services.GetRequiredService<IEvalRunTableService>();
        
        var entity = new EvalRunTableEntity
        {
            PartitionKey = agentId,
            RowKey = evalRunId.ToString(),
            EvalRunId = evalRunId,
            AgentId = agentId,
            DataSetId = $"dataset-{agentId}",
            MetricsConfigurationId = $"metrics-{agentId}",
            Status = status,
            LastUpdatedBy = "integration-test",
            LastUpdatedOn = DateTime.UtcNow,
            StartedDatetime = DateTime.UtcNow.AddMinutes(-30),
            CompletedDatetime = status == "Completed" || status == "Failed" ? DateTime.UtcNow.AddMinutes(-5) : null,
            BlobFilePath = $"evalresults/{evalRunId}/",
            ContainerName = agentId.Replace(" ", "").Replace("-", "")
        };

        return await evalRunTableService.CreateEvalRunAsync(entity);
    }

    private async Task CreateTestEvaluationResultsInStorage(string agentId, Guid evalRunId, int recordCount = 3)
    {
        var blobService = Factory.Services.GetRequiredService<IAzureBlobStorageService>();
        var testData = CreateTestEvaluationRecords(recordCount);
        var jsonContent = JsonSerializer.Serialize(testData, new JsonSerializerOptions { WriteIndented = true });
        var containerName = agentId.Replace(" ", "").Replace("-", "");
        var blobPath = $"evalresults/{evalRunId}/results.json";
        
        await blobService.WriteBlobContentAsync(containerName, blobPath, jsonContent);
    }

    #endregion

    #region GET /api/v1/eval/results/{evalRunId} Tests

    [Fact]
    public async Task GetEvaluationResult_WithValidEvalRunId_ShouldReturnResult()
    {
        // Arrange
        var agentId = "test-agent-get-001";
        var evalRunId = Guid.NewGuid();

        await CreateTestEvalRunInStorage(agentId, evalRunId, "Completed");
        await CreateTestEvaluationResultsInStorage(agentId, evalRunId, 3);

        // Act
        var response = await Client.GetAsync($"{BaseUrl}/{evalRunId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await DeserializeResponseAsync<EvaluationResultResponseDto>(response);
        result.Should().NotBeNull();
        result!.Success.Should().BeTrue();
        result.EvalRunId.Should().Be(evalRunId);
        result.EvaluationRecords.Should().NotBeNull();
        result.Message.Should().Contain("successfully");
    }

    [Fact]
    public async Task GetEvaluationResult_WithNonExistentEvalRunId_ShouldReturnNotFound()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();

        // Act
        var response = await Client.GetAsync($"{BaseUrl}/{nonExistentId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetEvaluationResult_WithInvalidGuid_ShouldReturnBadRequest()
    {
        // Arrange
        var invalidGuid = "not-a-guid";

        // Act
        var response = await Client.GetAsync($"{BaseUrl}/{invalidGuid}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    #endregion

    #region POST /api/v1/eval/results Tests

    [Fact]
    public async Task SaveEvaluationResult_WithValidData_ShouldSaveSuccessfully()
    {
        // Arrange
        var agentId = "test-agent-save-001";
        var evalRunId = Guid.NewGuid();

        await CreateTestEvalRunInStorage(agentId, evalRunId, "Completed");

        var saveRequest = CreateValidSaveEvaluationResultDto(evalRunId, 5);

        // Act
        var response = await Client.PostAsync(BaseUrl, CreateJsonContent(saveRequest));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await DeserializeResponseAsync<EvaluationResultSaveResponseDto>(response);
        result.Should().NotBeNull();
        result!.Success.Should().BeTrue();
        result.EvalRunId.Should().Be(evalRunId);
        result.Message.Should().Contain("successfully");
    }

    [Fact]
    public async Task SaveEvaluationResult_WithNonExistentEvalRunId_ShouldReturnBadRequest()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();
        var saveRequest = CreateValidSaveEvaluationResultDto(nonExistentId);

        // Act
        var response = await Client.PostAsync(BaseUrl, CreateJsonContent(saveRequest));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    #endregion

    #region GET /api/v1/eval/results/agent/{agentId} Tests

    [Fact]
    public async Task GetEvalRunsByAgent_WithValidAgentId_ShouldReturnEvalRuns()
    {
        // Arrange
        var agentId = "test-agent-get-runs-001";
        var evalRunId1 = Guid.NewGuid();
        var evalRunId2 = Guid.NewGuid();

        await CreateTestEvalRunInStorage(agentId, evalRunId1, "Completed");
        await CreateTestEvalRunInStorage(agentId, evalRunId2, "Running");

        // Act
        var response = await Client.GetAsync($"{BaseUrl}/agent/{agentId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var evalRuns = await DeserializeResponseAsync<List<EvalRunDto>>(response);
        evalRuns.Should().NotBeNull();
        evalRuns!.Should().HaveCount(2);
        evalRuns.Should().OnlyContain(r => r.AgentId == agentId);
    }

    [Fact]
    public async Task GetEvalRunsByAgent_WithNonExistentAgent_ShouldReturnEmptyList()
    {
        // Arrange
        var nonExistentAgentId = "non-existent-agent";

        // Act
        var response = await Client.GetAsync($"{BaseUrl}/agent/{nonExistentAgentId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var evalRuns = await DeserializeResponseAsync<List<EvalRunDto>>(response);
        evalRuns.Should().NotBeNull();
        evalRuns!.Should().BeEmpty();
    }

    #endregion

    #region GET /api/v1/eval/results/agent/{agentId}/daterange Tests

    [Fact]
    public async Task GetEvaluationResultsByDateRange_WithValidDateRange_ShouldReturnResults()
    {
        // Arrange
        var agentId = "test-agent-daterange-001";
        // Set date range to include the StartedDatetime which is set to -30 minutes
        // EndDateTime cannot be in the future, so use current time
        var startDate = DateTime.UtcNow.AddMinutes(-35);
        var endDate = DateTime.UtcNow.AddMinutes(-1); // 1 minute ago to avoid future validation
        
        var evalRunId1 = Guid.NewGuid();
        var evalRunId2 = Guid.NewGuid();

        await CreateTestEvalRunInStorage(agentId, evalRunId1, "Completed");
        await CreateTestEvalRunInStorage(agentId, evalRunId2, "Failed");
        
        await CreateTestEvaluationResultsInStorage(agentId, evalRunId1, 2);
        await CreateTestEvaluationResultsInStorage(agentId, evalRunId2, 2);

        // Act
        var response = await Client.GetAsync($"{BaseUrl}/agent/{agentId}/daterange?startDateTime={startDate:O}&endDateTime={endDate:O}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var results = await DeserializeResponseAsync<List<EvaluationResultResponseDto>>(response);
        results.Should().NotBeNull();
        results!.Should().HaveCount(2);
        results.Should().OnlyContain(r => r.Success);
    }

    [Fact]
    public async Task GetEvaluationResultsByDateRange_WithInvalidDateRange_ShouldReturnBadRequest()
    {
        // Arrange
        var agentId = "test-agent-invalid-range-001";
        var startDate = DateTime.UtcNow.AddHours(-1);
        var endDate = DateTime.UtcNow.AddHours(-2); // End before start

        // Act
        var response = await Client.GetAsync($"{BaseUrl}/agent/{agentId}/daterange?startDateTime={startDate:O}&endDateTime={endDate:O}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    #endregion

    #region End-to-End Workflow Test

    [Fact]
    public async Task EvalResults_CompleteEvaluationLifecycle_ShouldWorkCorrectly()
    {
        // Arrange - Complete evaluation workflow
        var agentId = "test-agent-e2e-workflow";
        var evalRunId = Guid.NewGuid();

        // Step 1: Create a completed eval run (simulating evaluation completion)
        await CreateTestEvalRunInStorage(agentId, evalRunId, "Completed");

        // Calculate date range after creating the EvalRun to ensure proper timing
        var startDate = DateTime.UtcNow.AddMinutes(-35);
        var endDate = DateTime.UtcNow.AddMinutes(-1);

        // Step 2: Verify no results exist initially
        var initialGetResponse = await Client.GetAsync($"{BaseUrl}/{evalRunId}");
        initialGetResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);

        // Step 3: Save evaluation results through regular endpoint
        var saveRequest = CreateValidSaveEvaluationResultDto(evalRunId, 4);
        var saveResponse = await Client.PostAsync(BaseUrl, CreateJsonContent(saveRequest));
        saveResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var saveResult = await DeserializeResponseAsync<EvaluationResultSaveResponseDto>(saveResponse);
        saveResult.Should().NotBeNull();
        saveResult!.Success.Should().BeTrue();
        saveResult.EvalRunId.Should().Be(evalRunId);

        // Step 4: Retrieve saved results through regular endpoint
        // Add small delay to ensure cache consistency
        await Task.Delay(100);
        var getResponse = await Client.GetAsync($"{BaseUrl}/{evalRunId}");
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var getResult = await DeserializeResponseAsync<EvaluationResultResponseDto>(getResponse);
        getResult.Should().NotBeNull();
        getResult!.Success.Should().BeTrue();
        getResult.EvalRunId.Should().Be(evalRunId);
        getResult.EvaluationRecords.Should().NotBeNull();

        // Step 5: Verify agent's eval runs list includes this run
        var agentRunsResponse = await Client.GetAsync($"{BaseUrl}/agent/{agentId}");
        agentRunsResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var agentRuns = await DeserializeResponseAsync<List<EvalRunDto>>(agentRunsResponse);
        agentRuns.Should().NotBeNull();
        agentRuns!.Should().Contain(r => r.EvalRunId == evalRunId);

        // Step 7: Verify end-to-end workflow is complete
        // All main functionality has been tested - the regular optimized endpoints handle everything needed

        // Note: Date range testing is covered separately in dedicated tests due to timing complexity
    }

    #endregion
}