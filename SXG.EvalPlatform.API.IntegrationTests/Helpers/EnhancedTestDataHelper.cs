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