using SxgEvalPlatformApi.Models.Dtos;
using SxgEvalPlatformApi.Models;
using FluentAssertions;
using System.Text.Json;

namespace SXG.EvalPlatform.API.IntegrationTests.Helpers;

/// <summary>
/// Enhanced helper class for creating comprehensive test data for integration tests
/// </summary>
public static class EnhancedTestDataHelper
{
    #region Configuration DTOs

    /// <summary>
    /// Creates a valid CreateConfigurationRequestDto for testing
    /// </summary>
    public static CreateConfigurationRequestDto CreateValidConfigurationDto(
        string agentId = "test-agent-001", 
        string? configurationName = null)
    {
        // Generate unique configuration name if not provided
        if (string.IsNullOrEmpty(configurationName))
        {
            configurationName = $"Test Configuration {DateTime.UtcNow:yyyyMMddHHmmssfff}";
        }
        
        return new CreateConfigurationRequestDto
        {
            AgentId = agentId,
            ConfigurationName = configurationName,
            EnvironmentName = "Production",
            Description = "Test configuration for integration tests",
            MetricsConfiguration = new List<SelectedMetricsConfigurationDto>
            {
                new SelectedMetricsConfigurationDto { MetricName = "BLEU", Threshold = 0.8 },
                new SelectedMetricsConfigurationDto { MetricName = "ROUGE", Threshold = 0.75 }
            }
        };
    }

    /// <summary>
    /// Creates a valid UpdateConfigurationRequestDto for testing
    /// </summary>
    public static UpdateConfigurationRequestDto CreateValidUpdateConfigurationDto()
    {
        return new UpdateConfigurationRequestDto
        {
            MetricsConfiguration = new List<SelectedMetricsConfigurationDto>
            {
                new SelectedMetricsConfigurationDto { MetricName = "BLEU", Threshold = 0.85 },
                new SelectedMetricsConfigurationDto { MetricName = "ROUGE", Threshold = 0.8 },
                new SelectedMetricsConfigurationDto { MetricName = "CoherenceScore", Threshold = 0.9 }
            }
        };
    }

    /// <summary>
    /// Creates an invalid CreateConfigurationRequestDto for negative testing
    /// </summary>
    public static CreateConfigurationRequestDto CreateInvalidConfigurationDto()
    {
        return new CreateConfigurationRequestDto
        {
            AgentId = "", // Invalid - empty AgentId
            ConfigurationName = "", // Invalid - empty ConfigurationName
            EnvironmentName = "Production",
            MetricsConfiguration = new List<SelectedMetricsConfigurationDto>()
        };
    }

    /// <summary>
    /// Creates a minimal invalid UpdateConfigurationRequestDto
    /// </summary>
    public static UpdateConfigurationRequestDto CreateInvalidUpdateConfigurationDto()
    {
        return new UpdateConfigurationRequestDto
        {
            MetricsConfiguration = new List<SelectedMetricsConfigurationDto>() // Empty list
        };
    }

    #endregion

    #region Sample Data Creation

    /// <summary>
    /// Creates a sample MetricsConfiguration for testing
    /// </summary>
    public static SxgEvalPlatformApi.Models.MetricsConfiguration CreateSampleMetricsConfiguration()
    {
        return new SxgEvalPlatformApi.Models.MetricsConfiguration
        {
            MetricName = "BLEU",
            Threshold = 0.8
        };
    }

    /// <summary>
    /// Creates a test EvalDataset for testing
    /// </summary>
    public static EvalDataset CreateTestDataset(
        string prompt = "Test prompt",
        string groundTruth = "Expected response")
    {
        return new EvalDataset
        {
            Prompt = prompt,
            GroundTruth = groundTruth,
            ActualResponse = "",
            ExpectedResponse = groundTruth
        };
    }

    /// <summary>
    /// Creates a test EvalRunDto for testing
    /// </summary>
    public static EvalRunDto CreateTestEvalRun(
        string agentId = "test-agent-001",
        Guid? dataSetId = null,
        Guid? metricsConfigurationId = null)
    {
        return new EvalRunDto
        {
            EvalRunId = Guid.NewGuid(),
            AgentId = agentId,
            DataSetId = (dataSetId ?? Guid.NewGuid()).ToString(),
            MetricsConfigurationId = (metricsConfigurationId ?? Guid.NewGuid()).ToString(),
            Status = "Queued",
            LastUpdatedBy = "test-user",
            LastUpdatedOn = DateTime.UtcNow
        };
    }

    #endregion

    #region Bulk Data Generation

    /// <summary>
    /// Creates multiple configurations for performance testing
    /// </summary>
    public static List<CreateConfigurationRequestDto> CreateMultipleConfigurations(int count = 3, string agentId = "test-agent-001")
    {
        var configurations = new List<CreateConfigurationRequestDto>();
        
        for (int i = 0; i < count; i++)
        {
            configurations.Add(CreateValidConfigurationDto(
                agentId,
                $"Performance Test Config {i + 1}"
            ));
        }
        
        return configurations;
    }

    /// <summary>
    /// Creates configurations for different agents
    /// </summary>
    public static List<CreateConfigurationRequestDto> CreateMultiAgentConfigurations()
    {
        var agentIds = new[] { "agent-001", "agent-002", "agent-003" };
        var configurations = new List<CreateConfigurationRequestDto>();
        
        foreach (var agentId in agentIds)
        {
            configurations.Add(CreateValidConfigurationDto(
                agentId,
                $"Configuration for {agentId}"
            ));
        }
        
        return configurations;
    }

    #endregion

    #region Validation Helpers

    /// <summary>
    /// Validates that a configuration response matches the expected DTO
    /// </summary>
    public static void ValidateConfigurationResponse(object response, CreateConfigurationRequestDto expected)
    {
        response.Should().NotBeNull();
        
        // Convert to JsonElement for property checking
        var json = JsonSerializer.Serialize(response);
        var element = JsonSerializer.Deserialize<JsonElement>(json);
        
        element.GetProperty("agentId").GetString().Should().Be(expected.AgentId);
        element.GetProperty("configurationName").GetString().Should().Be(expected.ConfigurationName);
    }

    /// <summary>
    /// Creates test data for performance testing
    /// </summary>
    public static List<CreateConfigurationRequestDto> CreatePerformanceTestData(int count = 100)
    {
        var configurations = new List<CreateConfigurationRequestDto>();
        
        for (int i = 0; i < count; i++)
        {
            configurations.Add(CreateValidConfigurationDto(
                $"perf-agent-{i:D3}",
                $"Performance Configuration {i + 1}"
            ));
        }
        
        return configurations;
    }

    #endregion

    #region Security Test Data

    /// <summary>
    /// Creates malicious input data for security testing
    /// </summary>
    public static CreateConfigurationRequestDto CreateMaliciousConfigurationDto()
    {
        return new CreateConfigurationRequestDto
        {
            AgentId = "'; DROP TABLE Configurations; --",
            ConfigurationName = "<script>alert('xss')</script>",
            EnvironmentName = "Production",
            Description = "../../etc/passwd",
            MetricsConfiguration = new List<SelectedMetricsConfigurationDto>
            {
                new SelectedMetricsConfigurationDto { MetricName = "BLEU", Threshold = 0.8 }
            }
        };
    }

    /// <summary>
    /// Creates oversized input data for boundary testing
    /// </summary>
    public static CreateConfigurationRequestDto CreateOversizedConfigurationDto()
    {
        return new CreateConfigurationRequestDto
        {
            AgentId = "test-agent",
            ConfigurationName = new string('A', 200), // Exceeds 100 char limit
            EnvironmentName = "Production",
            Description = new string('B', 1000), // Exceeds 500 char limit
            MetricsConfiguration = new List<SelectedMetricsConfigurationDto>
            {
                new SelectedMetricsConfigurationDto { MetricName = "BLEU", Threshold = 0.8 }
            }
        };
    }

    #endregion

    #region Concurrent Test Helpers

    /// <summary>
    /// Creates unique test data for concurrent testing scenarios
    /// </summary>
    public static CreateConfigurationRequestDto CreateConcurrentTestConfiguration(int threadId)
    {
        return new CreateConfigurationRequestDto
        {
            AgentId = $"concurrent-agent-{threadId}",
            ConfigurationName = $"Concurrent Test Config {threadId} - {DateTime.UtcNow:HHmmssfff}",
            EnvironmentName = "Testing",
            Description = $"Configuration created by thread {threadId}",
            MetricsConfiguration = new List<SelectedMetricsConfigurationDto>
            {
                new SelectedMetricsConfigurationDto { MetricName = "BLEU", Threshold = 0.8 + (threadId * 0.01) },
                new SelectedMetricsConfigurationDto { MetricName = "ROUGE", Threshold = 0.75 + (threadId * 0.01) }
            }
        };
    }

    #endregion
}

/// <summary>
/// Helper class for creating dataset test data
/// </summary>
public static class DatasetTestDataHelper
{
    #region Dataset DTOs

    /// <summary>
    /// Creates a valid SaveDatasetDto for testing
    /// </summary>
    public static SaveDatasetDto CreateValidSaveDatasetDto(
        string agentId = "test-agent-001",
        string? datasetName = null,
        string datasetType = DatasetTypes.Synthetic,
        int recordCount = 3)
    {
        // Generate unique dataset name if not provided
        if (string.IsNullOrEmpty(datasetName))
        {
            datasetName = $"Test Dataset {DateTime.UtcNow:yyyyMMddHHmmssfff}";
        }

        return new SaveDatasetDto
        {
            AgentId = agentId,
            DatasetType = datasetType,
            DatasetName = datasetName,
            DatasetRecords = CreateTestDatasetRecords(recordCount)
        };
    }

    /// <summary>
    /// Creates a valid UpdateDatasetDto for testing
    /// </summary>
    public static UpdateDatasetDto CreateValidUpdateDatasetDto(int recordCount = 5)
    {
        return new UpdateDatasetDto
        {
            DatasetRecords = CreateTestDatasetRecords(recordCount)
        };
    }

    /// <summary>
    /// Creates test dataset records
    /// </summary>
    public static List<EvalDataset> CreateTestDatasetRecords(int count = 3)
    {
        var records = new List<EvalDataset>();
        
        for (int i = 0; i < count; i++)
        {
            records.Add(new EvalDataset
            {
                Prompt = $"Test prompt {i + 1}: What is the capital of France?",
                GroundTruth = $"Ground truth {i + 1}: Paris",
                ActualResponse = $"Actual response {i + 1}: Paris is the capital",
                ExpectedResponse = $"Expected response {i + 1}: Paris"
            });
        }

        return records;
    }

    /// <summary>
    /// Creates a complex dataset for testing with varied content
    /// </summary>
    public static List<EvalDataset> CreateComplexDatasetRecords()
    {
        return new List<EvalDataset>
        {
            new EvalDataset
            {
                Prompt = "Translate 'Hello, how are you?' to French",
                GroundTruth = "Bonjour, comment allez-vous ?",
                ActualResponse = "Bonjour, comment ça va ?",
                ExpectedResponse = "Bonjour, comment allez-vous ?"
            },
            new EvalDataset
            {
                Prompt = "What is 2 + 2?",
                GroundTruth = "4",
                ActualResponse = "The answer is 4",
                ExpectedResponse = "4"
            },
            new EvalDataset
            {
                Prompt = "Explain quantum computing in one sentence",
                GroundTruth = "Quantum computing uses quantum mechanical phenomena to process information",
                ActualResponse = "Quantum computing leverages quantum physics to perform calculations",
                ExpectedResponse = "Quantum computing uses quantum mechanical phenomena to process information"
            },
            new EvalDataset
            {
                Prompt = "What are the primary colors?",
                GroundTruth = "Red, blue, and yellow",
                ActualResponse = "The primary colors are red, blue, and yellow",
                ExpectedResponse = "Red, blue, and yellow"
            }
        };
    }

    /// <summary>
    /// Creates datasets for performance testing
    /// </summary>
    public static SaveDatasetDto CreatePerformanceTestDataset(string agentId, int index)
    {
        return new SaveDatasetDto
        {
            AgentId = agentId,
            DatasetType = index % 2 == 0 ? DatasetTypes.Synthetic : DatasetTypes.Golden,
            DatasetName = $"Performance Dataset {index + 1}",
            DatasetRecords = CreateTestDatasetRecords(5 + index) // Varying sizes
        };
    }

    /// <summary>
    /// Creates datasets for agent isolation testing
    /// </summary>
    public static SaveDatasetDto CreateAgentIsolationDataset(string agentId)
    {
        return new SaveDatasetDto
        {
            AgentId = agentId,
            DatasetType = DatasetTypes.Synthetic,
            DatasetName = $"Dataset for {agentId}",
            DatasetRecords = CreateTestDatasetRecords(2)
        };
    }

    /// <summary>
    /// Creates a dataset with special characters and edge cases
    /// </summary>
    public static SaveDatasetDto CreateEdgeCaseDataset(string agentId)
    {
        return new SaveDatasetDto
        {
            AgentId = agentId,
            DatasetType = DatasetTypes.Golden,
            DatasetName = "Dataset with Unicode: 你好 🌟 Émojis & Special-Chars!",
            DatasetRecords = new List<EvalDataset>
            {
                new EvalDataset
                {
                    Prompt = "Handle special characters: !@#$%^&*()",
                    GroundTruth = "Special characters: !@#$%^&*()",
                    ActualResponse = "Response with special chars: !@#$%^&*()",
                    ExpectedResponse = "Special characters: !@#$%^&*()"
                },
                new EvalDataset
                {
                    Prompt = "Unicode test: 你好世界 🌍",
                    GroundTruth = "Hello world in Chinese: 你好世界",
                    ActualResponse = "Chinese greeting: 你好世界",
                    ExpectedResponse = "Hello world in Chinese: 你好世界"
                }
            }
        };
    }

    #endregion

    #region Validation Helpers

    /// <summary>
    /// Validates that a dataset response matches expected values
    /// </summary>
    public static void ValidateDatasetSaveResponse(DatasetSaveResponseDto response, string expectedStatus = "created")
    {
        response.Should().NotBeNull();
        response.DatasetId.Should().NotBeEmpty();
        response.Status.Should().Be(expectedStatus);
        response.Message.Should().NotBeEmpty();
        response.CreatedOn.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));
    }

    /// <summary>
    /// Validates that dataset metadata matches expected values
    /// </summary>
    public static void ValidateDatasetMetadata(DatasetMetadataDto metadata, string expectedAgentId, string expectedDatasetName, string expectedDatasetType, int expectedRecordCount)
    {
        metadata.Should().NotBeNull();
        metadata.DatasetId.Should().NotBeEmpty();
        metadata.AgentId.Should().Be(expectedAgentId);
        metadata.DatasetName.Should().Be(expectedDatasetName);
        metadata.DatasetType.Should().Be(expectedDatasetType);
        metadata.RecordCount.Should().Be(expectedRecordCount);
        metadata.CreatedOn.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));
    }

    /// <summary>
    /// Validates that dataset content matches expected records
    /// </summary>
    public static void ValidateDatasetContent(List<EvalDataset> actualContent, List<EvalDataset> expectedContent)
    {
        actualContent.Should().NotBeNull();
        actualContent.Should().HaveCount(expectedContent.Count);
        
        for (int i = 0; i < expectedContent.Count; i++)
        {
            actualContent[i].Prompt.Should().Be(expectedContent[i].Prompt);
            actualContent[i].GroundTruth.Should().Be(expectedContent[i].GroundTruth);
            actualContent[i].ActualResponse.Should().Be(expectedContent[i].ActualResponse);
            actualContent[i].ExpectedResponse.Should().Be(expectedContent[i].ExpectedResponse);
        }
    }

    #endregion

    #region EvalResult DTOs

    /// <summary>
    /// Creates a valid SaveEvaluationResultDto for testing
    /// </summary>
    public static SaveEvaluationResultDto CreateValidSaveEvaluationResultDto(
        Guid evalRunId, 
        int recordCount = 3)
    {
        return new SaveEvaluationResultDto
        {
            EvalRunId = evalRunId,
            EvaluationRecords = CreateTestEvaluationRecords(recordCount)
        };
    }

    /// <summary>
    /// Creates test evaluation result data with sample metrics
    /// </summary>
    public static JsonElement CreateTestEvaluationRecords(int recordCount = 3)
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
                    bleu = Math.Round(0.85 + (i * 0.01), 3),
                    rouge = Math.Round(0.78 + (i * 0.02), 3),
                    accuracy = Math.Round(0.90 + (i * 0.01), 3),
                    precision = Math.Round(0.88 + (i * 0.015), 3),
                    recall = Math.Round(0.82 + (i * 0.02), 3),
                    f1Score = Math.Round(0.85 + (i * 0.01), 3)
                },
                metadata = new
                {
                    evaluationTime = DateTime.UtcNow.AddMinutes(-i).ToString("O"),
                    promptTokens = 150 + (i * 10),
                    responseTokens = 200 + (i * 15),
                    evaluationDuration = $"00:00:{30 + i:D2}"
                },
                timestamp = DateTime.UtcNow.AddMinutes(-i).ToString("O")
            });
        }

        var json = JsonSerializer.Serialize(testRecords);
        return JsonSerializer.Deserialize<JsonElement>(json);
    }

    /// <summary>
    /// Creates a test EvalRun entity for integration testing
    /// </summary>
    public static EvalRunDto CreateTestEvalRunDto(
        string agentId = "test-agent-001",
        string status = "Completed",
        string? dataSetId = null,
        string? metricsConfigId = null,
        DateTime? startedDateTime = null,
        DateTime? completedDateTime = null)
    {
        var startTime = startedDateTime ?? DateTime.UtcNow.AddMinutes(-30);
        var completedTime = (status == "Completed" || status == "Failed") 
            ? (completedDateTime ?? DateTime.UtcNow.AddMinutes(-5))
            : (DateTime?)null;

        return new EvalRunDto
        {
            EvalRunId = Guid.NewGuid(),
            AgentId = agentId,
            DataSetId = dataSetId ?? $"dataset-{agentId}",
            MetricsConfigurationId = metricsConfigId ?? $"metrics-{agentId}",
            Status = status,
            LastUpdatedBy = "integration-test",
            LastUpdatedOn = DateTime.UtcNow,
            StartedDatetime = startTime,
            CompletedDatetime = completedTime
        };
    }

    /// <summary>
    /// Creates an EvalRunTableEntity for storage testing
    /// </summary>
    public static Sxg.EvalPlatform.API.Storage.TableEntities.EvalRunTableEntity CreateTestEvalRunTableEntity(
        string agentId = "test-agent-001",
        string status = "Completed",
        Guid? evalRunId = null,
        string? dataSetId = null,
        string? metricsConfigId = null,
        DateTime? startedDateTime = null,
        DateTime? completedDateTime = null)
    {
        var runId = evalRunId ?? Guid.NewGuid();
        var startTime = startedDateTime ?? DateTime.UtcNow.AddMinutes(-30);
        var completedTime = (status == "Completed" || status == "Failed") 
            ? (completedDateTime ?? DateTime.UtcNow.AddMinutes(-5))
            : (DateTime?)null;

        return new Sxg.EvalPlatform.API.Storage.TableEntities.EvalRunTableEntity
        {
            PartitionKey = agentId,
            RowKey = runId.ToString(),
            EvalRunId = runId,
            AgentId = agentId,
            DataSetId = dataSetId ?? $"dataset-{agentId}",
            MetricsConfigurationId = metricsConfigId ?? $"metrics-{agentId}",
            Status = status,
            LastUpdatedBy = "integration-test",
            LastUpdatedOn = DateTime.UtcNow,
            StartedDatetime = startTime,
            CompletedDatetime = completedTime,
            BlobFilePath = $"evalresults/{runId}/",
            ContainerName = agentId.Replace(" ", "")
        };
    }

    #endregion

    #region EvalResult Validation Methods

    /// <summary>
    /// Validates SaveEvaluationResultDto response
    /// </summary>
    public static void ValidateEvaluationResultSaveResponse(EvaluationResultSaveResponseDto response, Guid expectedEvalRunId)
    {
        response.Should().NotBeNull();
        response.Success.Should().BeTrue();
        response.EvalRunId.Should().Be(expectedEvalRunId);
        response.Message.Should().NotBeEmpty();
        response.Message.Should().Contain("successfully");
    }

    /// <summary>
    /// Validates EvaluationResultResponseDto response
    /// </summary>
    public static void ValidateEvaluationResultResponse(EvaluationResultResponseDto response, Guid expectedEvalRunId)
    {
        response.Should().NotBeNull();
        response.Success.Should().BeTrue();
        response.EvalRunId.Should().Be(expectedEvalRunId);
        response.EvaluationRecords.Should().NotBeNull();
        response.Message.Should().NotBeEmpty();
        response.Message.Should().Contain("successfully");
    }

    /// <summary>
    /// Validates that evaluation records contain expected structure and data
    /// </summary>
    public static void ValidateEvaluationRecords(JsonElement? evaluationRecords, int expectedRecordCount)
    {
        evaluationRecords.Should().NotBeNull();
        evaluationRecords!.Value.ValueKind.Should().Be(JsonValueKind.Array);
        
        var records = evaluationRecords.Value.EnumerateArray().ToList();
        records.Should().HaveCount(expectedRecordCount);

        foreach (var record in records)
        {
            // Validate required fields exist
            record.TryGetProperty("id", out _).Should().BeTrue();
            record.TryGetProperty("question", out _).Should().BeTrue();
            record.TryGetProperty("actualAnswer", out _).Should().BeTrue();
            record.TryGetProperty("metrics", out var metrics).Should().BeTrue();
            record.TryGetProperty("timestamp", out _).Should().BeTrue();

            // Validate metrics structure
            metrics.TryGetProperty("bleu", out _).Should().BeTrue();
            metrics.TryGetProperty("rouge", out _).Should().BeTrue();
            metrics.TryGetProperty("accuracy", out _).Should().BeTrue();
        }
    }

    /// <summary>
    /// Validates EvalRunDto list response
    /// </summary>
    public static void ValidateEvalRunList(List<EvalRunDto> evalRuns, string expectedAgentId, int expectedCount)
    {
        evalRuns.Should().NotBeNull();
        evalRuns.Should().HaveCount(expectedCount);
        evalRuns.Should().OnlyContain(r => r.AgentId == expectedAgentId);
        
        foreach (var run in evalRuns)
        {
            run.EvalRunId.Should().NotBeEmpty();
            run.AgentId.Should().Be(expectedAgentId);
            run.DataSetId.Should().NotBeEmpty();
            run.MetricsConfigurationId.Should().NotBeEmpty();
            run.Status.Should().NotBeEmpty();
            run.LastUpdatedOn.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromHours(1));
            run.StartedDatetime.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromHours(1));
        }
    }

    /// <summary>
    /// Validates EvaluationResultResponseDto list from date range query
    /// </summary>
    public static void ValidateEvaluationResultList(List<EvaluationResultResponseDto> results, int expectedCount)
    {
        results.Should().NotBeNull();
        results.Should().HaveCount(expectedCount);
        results.Should().OnlyContain(r => r.Success);
        
        foreach (var result in results)
        {
            result.EvalRunId.Should().NotBeEmpty();
            result.EvaluationRecords.Should().NotBeNull();
            result.Message.Should().NotBeEmpty();
        }
    }

    #endregion
}