using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using SXG.EvalPlatform.API.IntegrationTests.Infrastructure;
using SXG.EvalPlatform.API.IntegrationTests.Helpers;
using SxgEvalPlatformApi.Models;
using SxgEvalPlatformApi.Models.Dtos;
using Sxg.EvalPlatform.API.Storage.Services;
using Xunit;

namespace SXG.EvalPlatform.API.IntegrationTests.Tests;

/// <summary>
/// Additional integration tests for EvalResult API edge cases and advanced scenarios
/// Tests performance, error handling, concurrent operations, and data validation
/// </summary>
[Collection("Integration Tests")]
public class EvalResultAdvancedIntegrationTests : InMemoryIntegrationTestBase
{
    private const string BaseUrl = "/api/v1/eval/results";

    public EvalResultAdvancedIntegrationTests(InMemoryWebApplicationFactory factory) : base(factory)
    {
    }

    #region Helper Methods (Local)

    /// <summary>
    /// Creates a test evaluation result data with sample metrics
    /// </summary>
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

    /// <summary>
    /// Creates a valid SaveEvaluationResultDto for testing
    /// </summary>
    private static SaveEvaluationResultDto CreateValidSaveEvaluationResultDto(Guid evalRunId, int recordCount = 3)
    {
        return new SaveEvaluationResultDto
        {
            EvalRunId = evalRunId,
            EvaluationRecords = CreateTestEvaluationRecords(recordCount)
        };
    }

    #endregion

    #region Data Validation and Edge Cases

    [Fact]
    public async Task SaveEvaluationResult_WithLargeDataset_ShouldHandleCorrectly()
    {
        // Arrange
        var agentId = "test-agent-large-001";
        var evalRunId = Guid.NewGuid();

        var evalRunTableService = Factory.Services.GetRequiredService<IEvalRunTableService>();

        // Create completed EvalRun
        await evalRunTableService.CreateEvalRunAsync(new Sxg.EvalPlatform.API.Storage.TableEntities.EvalRunTableEntity
        {
            PartitionKey = agentId,
            RowKey = evalRunId.ToString(),
            EvalRunId = evalRunId,
            AgentId = agentId,
            DataSetId = $"dataset-{agentId}",
            MetricsConfigurationId = $"metrics-{agentId}",
            Status = "Completed",
            LastUpdatedBy = "test",
            LastUpdatedOn = DateTime.UtcNow,
            StartedDatetime = DateTime.UtcNow.AddMinutes(-30),
            CompletedDatetime = DateTime.UtcNow.AddMinutes(-5),
            BlobFilePath = $"evalresults/{evalRunId}/",
            ContainerName = agentId.Replace(" ", "")
        });

        // Create large dataset (100 records)
        var saveRequest = CreateValidSaveEvaluationResultDto(evalRunId, 100);

        // Act
        var response = await Client.PostAsync(BaseUrl, CreateJsonContent(saveRequest));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<EvaluationResultSaveResponseDto>();
        result.Should().NotBeNull();
        result!.Success.Should().BeTrue();
        result.EvalRunId.Should().Be(evalRunId);

        // Verify we can retrieve the large dataset
        var getResponse = await Client.GetAsync($"{BaseUrl}/{evalRunId}");
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var getResult = await getResponse.Content.ReadFromJsonAsync<EvaluationResultResponseDto>();
        getResult.Should().NotBeNull();
        getResult!.EvaluationRecords.Should().NotBeNull();
        var records = getResult.EvaluationRecords.Value.EnumerateArray().ToList();
        records.Should().HaveCount(100);
    }

    [Fact]
    public async Task SaveEvaluationResult_WithComplexNestedJson_ShouldHandleCorrectly()
    {
        // Arrange
        var agentId = "test-agent-complex-001";
        var evalRunId = Guid.NewGuid();

        var evalRunTableService = Factory.Services.GetRequiredService<IEvalRunTableService>();

        // Create completed EvalRun
        await evalRunTableService.CreateEvalRunAsync(new Sxg.EvalPlatform.API.Storage.TableEntities.EvalRunTableEntity
        {
            PartitionKey = agentId,
            RowKey = evalRunId.ToString(),
            EvalRunId = evalRunId,
            AgentId = agentId,
            DataSetId = $"dataset-{agentId}",
            MetricsConfigurationId = $"metrics-{agentId}",
            Status = "Completed",
            LastUpdatedBy = "test",
            LastUpdatedOn = DateTime.UtcNow,
            StartedDatetime = DateTime.UtcNow.AddMinutes(-30),
            CompletedDatetime = DateTime.UtcNow.AddMinutes(-5),
            BlobFilePath = $"evalresults/{evalRunId}/",
            ContainerName = agentId.Replace(" ", "")
        });

        // Create complex nested JSON structure
        var complexData = new List<object>
        {
            new
            {
                id = 1,
                question = "Complex question with nested data",
                response = new
                {
                    text = "Complex response",
                    confidence = 0.95,
                    sources = new[]
                    {
                        new { url = "https://example.com/1", title = "Source 1", relevance = 0.9 },
                        new { url = "https://example.com/2", title = "Source 2", relevance = 0.8 }
                    }
                },
                evaluation = new
                {
                    metrics = new
                    {
                        primary = new { bleu = 0.85, rouge = 0.78 },
                        secondary = new { coherence = 0.92, fluency = 0.88 }
                    }
                }
            }
        };

        var json = JsonSerializer.Serialize(complexData);
        var complexRecords = JsonSerializer.Deserialize<JsonElement>(json);

        var saveRequest = new SaveEvaluationResultDto
        {
            EvalRunId = evalRunId,
            EvaluationRecords = complexRecords
        };

        // Act
        var response = await Client.PostAsync(BaseUrl, CreateJsonContent(saveRequest));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Verify data integrity by retrieving
        var getResponse = await Client.GetAsync($"{BaseUrl}/{evalRunId}");
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var getResult = await getResponse.Content.ReadFromJsonAsync<EvaluationResultResponseDto>();
        
        getResult.Should().NotBeNull();
        getResult!.EvaluationRecords.Should().NotBeNull();
        var retrievedRecords = getResult.EvaluationRecords.Value.EnumerateArray().ToList();
        retrievedRecords.Should().HaveCount(1);
        
        // Verify complex structure is preserved
        var record = retrievedRecords[0];
        record.TryGetProperty("evaluation", out var evaluation).Should().BeTrue();
        evaluation.TryGetProperty("metrics", out var metrics).Should().BeTrue();
        metrics.TryGetProperty("primary", out var primary).Should().BeTrue();
        primary.TryGetProperty("bleu", out _).Should().BeTrue();
    }

    [Fact]
    public async Task SaveEvaluationResult_WithSpecialCharacters_ShouldHandleCorrectly()
    {
        // Arrange
        var agentId = "test-agent-special-001";
        var evalRunId = Guid.NewGuid();

        var evalRunTableService = Factory.Services.GetRequiredService<IEvalRunTableService>();

        // Create completed EvalRun
        await evalRunTableService.CreateEvalRunAsync(new Sxg.EvalPlatform.API.Storage.TableEntities.EvalRunTableEntity
        {
            PartitionKey = agentId,
            RowKey = evalRunId.ToString(),
            EvalRunId = evalRunId,
            AgentId = agentId,
            DataSetId = $"dataset-{agentId}",
            MetricsConfigurationId = $"metrics-{agentId}",
            Status = "Completed",
            LastUpdatedBy = "test",
            LastUpdatedOn = DateTime.UtcNow,
            StartedDatetime = DateTime.UtcNow.AddMinutes(-30),
            CompletedDatetime = DateTime.UtcNow.AddMinutes(-5),
            BlobFilePath = $"evalresults/{evalRunId}/",
            ContainerName = agentId.Replace(" ", "")
        });

        // Create data with special characters
        var specialCharData = new List<object>
        {
            new
            {
                id = 1,
                question = "Question with émojis 🤖 and spëcial châractërs: ñ, é, ü, ß, 中文, العربية, 🌟",
                expectedAnswer = "Expected with quotes \"and\" apostrophes' and newlines\nand tabs\t",
                actualAnswer = "Actual with backslashes \\ and forward slashes / and pipes |",
                metrics = new
                {
                    score = 0.85,
                    notes = "Contains unicode: ∑, ∆, π, ∞, ≠, ≤, ≥"
                },
                json_content = "{ \"nested\": \"json with \\\"escaped\\\" quotes\" }",
                html_content = "<div class=\"test\">HTML content with &lt;tags&gt;</div>"
            }
        };

        var json = JsonSerializer.Serialize(specialCharData);
        var specialRecords = JsonSerializer.Deserialize<JsonElement>(json);

        var saveRequest = new SaveEvaluationResultDto
        {
            EvalRunId = evalRunId,
            EvaluationRecords = specialRecords
        };

        // Act
        var response = await Client.PostAsync(BaseUrl, CreateJsonContent(saveRequest));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Verify data integrity
        var getResponse = await Client.GetAsync($"{BaseUrl}/{evalRunId}");
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var getResult = await getResponse.Content.ReadFromJsonAsync<EvaluationResultResponseDto>();
        
        getResult.Should().NotBeNull();
        var retrievedRecords = getResult!.EvaluationRecords!.Value.EnumerateArray().ToList();
        retrievedRecords.Should().HaveCount(1);
        
        var record = retrievedRecords[0];
        record.TryGetProperty("question", out var questionProp).Should().BeTrue();
        questionProp.GetString().Should().Contain("🤖");
        questionProp.GetString().Should().Contain("中文");
    }

    #endregion

    #region Performance and Concurrency Tests

    [Fact]
    public async Task GetEvaluationResult_ConcurrentRequests_ShouldHandleCorrectly()
    {
        // Arrange
        var agentId = "test-agent-concurrent-001";
        var evalRunId = Guid.NewGuid();

        var evalRunTableService = Factory.Services.GetRequiredService<IEvalRunTableService>();
        var blobService = Factory.Services.GetRequiredService<IAzureBlobStorageService>();

        // Create completed EvalRun
        await evalRunTableService.CreateEvalRunAsync(new Sxg.EvalPlatform.API.Storage.TableEntities.EvalRunTableEntity
        {
            PartitionKey = agentId,
            RowKey = evalRunId.ToString(),
            EvalRunId = evalRunId,
            AgentId = agentId,
            DataSetId = $"dataset-{agentId}",
            MetricsConfigurationId = $"metrics-{agentId}",
            Status = "Completed",
            LastUpdatedBy = "test",
            LastUpdatedOn = DateTime.UtcNow,
            StartedDatetime = DateTime.UtcNow.AddMinutes(-30),
            CompletedDatetime = DateTime.UtcNow.AddMinutes(-5),
            BlobFilePath = $"evalresults/{evalRunId}/",
            ContainerName = agentId.Replace(" ", "")
        });

        // Create result data in blob storage
        var testData = CreateTestEvaluationRecords(5);
        var jsonContent = JsonSerializer.Serialize(testData, new JsonSerializerOptions { WriteIndented = true });
        var containerName = agentId.Replace(" ", "");
        var blobPath = $"evalresults/{evalRunId}/results.json";
        
        await blobService.WriteBlobContentAsync(containerName, blobPath, jsonContent);

        // Act - Make 10 concurrent requests
        var tasks = new List<Task<HttpResponseMessage>>();
        for (int i = 0; i < 10; i++)
        {
            tasks.Add(Client.GetAsync($"{BaseUrl}/{evalRunId}"));
        }

        var responses = await Task.WhenAll(tasks);

        // Assert
        responses.Should().HaveCount(10);
        responses.Should().OnlyContain(r => r.StatusCode == HttpStatusCode.OK);

        // Verify all responses contain the same data
        var results = new List<EvaluationResultResponseDto>();
        foreach (var response in responses)
        {
            var result = await response.Content.ReadFromJsonAsync<EvaluationResultResponseDto>();
            results.Add(result!);
        }

        results.Should().OnlyContain(r => r.Success);
        results.Should().OnlyContain(r => r.EvalRunId == evalRunId);
        
        // All should have the same evaluation records
        var firstRecords = results[0].EvaluationRecords!.Value.GetRawText();
        results.Should().OnlyContain(r => r.EvaluationRecords!.Value.GetRawText() == firstRecords);
    }

    #endregion

    #region Status Validation Tests

    [Theory]
    [InlineData("Created")]
    [InlineData("Running")]
    [InlineData("Paused")]
    public async Task SaveEvaluationResult_WithNonTerminalStatus_ShouldReturnBadRequest(string status)
    {
        // Arrange
        var agentId = $"test-agent-status-{status.ToLower()}-001";
        var evalRunId = Guid.NewGuid();

        var evalRunTableService = Factory.Services.GetRequiredService<IEvalRunTableService>();

        // Create EvalRun with non-terminal status
        await evalRunTableService.CreateEvalRunAsync(new Sxg.EvalPlatform.API.Storage.TableEntities.EvalRunTableEntity
        {
            PartitionKey = agentId,
            RowKey = evalRunId.ToString(),
            EvalRunId = evalRunId,
            AgentId = agentId,
            DataSetId = $"dataset-{agentId}",
            MetricsConfigurationId = $"metrics-{agentId}",
            Status = status,
            LastUpdatedBy = "test",
            LastUpdatedOn = DateTime.UtcNow,
            StartedDatetime = DateTime.UtcNow.AddMinutes(-30),
            CompletedDatetime = null,
            BlobFilePath = $"evalresults/{evalRunId}/",
            ContainerName = agentId.Replace(" ", "")
        });

        var saveRequest = CreateValidSaveEvaluationResultDto(evalRunId);

        // Act
        var response = await Client.PostAsync(BaseUrl, CreateJsonContent(saveRequest));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Theory]
    [InlineData("Completed")]
    [InlineData("Failed")]
    public async Task SaveEvaluationResult_WithTerminalStatus_ShouldSucceed(string status)
    {
        // Arrange
        var agentId = $"test-agent-terminal-{status.ToLower()}-001";
        var evalRunId = Guid.NewGuid();

        var evalRunTableService = Factory.Services.GetRequiredService<IEvalRunTableService>();

        // Create EvalRun with terminal status
        await evalRunTableService.CreateEvalRunAsync(new Sxg.EvalPlatform.API.Storage.TableEntities.EvalRunTableEntity
        {
            PartitionKey = agentId,
            RowKey = evalRunId.ToString(),
            EvalRunId = evalRunId,
            AgentId = agentId,
            DataSetId = $"dataset-{agentId}",
            MetricsConfigurationId = $"metrics-{agentId}",
            Status = status,
            LastUpdatedBy = "test",
            LastUpdatedOn = DateTime.UtcNow,
            StartedDatetime = DateTime.UtcNow.AddMinutes(-30),
            CompletedDatetime = DateTime.UtcNow.AddMinutes(-5),
            BlobFilePath = $"evalresults/{evalRunId}/",
            ContainerName = agentId.Replace(" ", "")
        });

        var saveRequest = CreateValidSaveEvaluationResultDto(evalRunId);

        // Act
        var response = await Client.PostAsync(BaseUrl, CreateJsonContent(saveRequest));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<EvaluationResultSaveResponseDto>();
        result.Should().NotBeNull();
        result!.Success.Should().BeTrue();
        result.EvalRunId.Should().Be(evalRunId);
    }

    #endregion
}