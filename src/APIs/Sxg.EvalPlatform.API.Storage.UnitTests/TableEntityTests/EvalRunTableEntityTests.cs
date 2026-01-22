using Sxg.EvalPlatform.API.Storage.TableEntities;

namespace Sxg.EvalPlatform.API.Storage.UnitTests.TableEntityTests;

[Trait("Category", TestCategories.Unit)]
public class EvalRunTableEntityTests
{
    [Fact]
    public void EvalRunTableEntity_DefaultConstructor_CreatesInstance()
    {
        // Act
        var entity = new EvalRunTableEntity();

        // Assert
        Assert.NotNull(entity);
        // Constructor automatically creates a new GUID
        Assert.NotEqual(Guid.Empty, entity.EvalRunId);
        // RowKey should be set to the string representation of EvalRunId
        Assert.Equal(entity.EvalRunId.ToString(), entity.RowKey);
        // Status is automatically set to RequestSubmitted
        Assert.Equal("RequestSubmitted", entity.Status);
        // StartedDatetime is automatically set
        Assert.NotNull(entity.StartedDatetime);
    }

    [Fact]
    public void EvalRunTableEntity_SetProperties_StoresValues()
    {
        // Arrange
        var evalRunId = Guid.NewGuid();
        var agentId = "test-agent";
        var datasetId = "test-dataset";
        var startTime = DateTime.UtcNow;

        var entity = new EvalRunTableEntity();

        // Act
        entity.AgentId = agentId;
        entity.EvalRunId = evalRunId;
        entity.DataSetId = datasetId;
        entity.StartedDatetime = startTime;
        entity.Status = "InProgress";
        entity.Type = "MCS";
        entity.EnvironmentId = "test-env";

        // Assert
        Assert.Equal(agentId, entity.AgentId);
        Assert.Equal(evalRunId, entity.EvalRunId);
        Assert.Equal(datasetId, entity.DataSetId);
        Assert.Equal(startTime, entity.StartedDatetime);
        Assert.Equal("InProgress", entity.Status);
        Assert.Equal("MCS", entity.Type);
        Assert.Equal("test-env", entity.EnvironmentId);
    }

    [Fact]
    public void EvalRunTableEntity_PartitionKeyAndRowKey_SetCorrectly()
    {
        // Arrange
        var agentId = "test-agent";

        // Act
        var entity = new EvalRunTableEntity();
        var originalEvalRunId = entity.EvalRunId; // Capture the auto-generated ID
        entity.AgentId = agentId;

        // Assert
        Assert.Equal(agentId, entity.PartitionKey);
        // RowKey is set from the original EvalRunId created in constructor
        Assert.Equal(originalEvalRunId.ToString(), entity.RowKey);
        // EvalRunId should still be the original from constructor
        Assert.Equal(originalEvalRunId, entity.EvalRunId);
    }

    [Theory]
    [InlineData("Pending")]
    [InlineData("InProgress")]
    [InlineData("Completed")]
    [InlineData("Failed")]
    public void EvalRunTableEntity_WithDifferentStatuses_StoresCorrectly(string status)
    {
        // Arrange & Act
        var entity = new EvalRunTableEntity
        {
            Status = status
        };

        // Assert
        Assert.Equal(status, entity.Status);
    }

    [Fact]
    public void EvalRunTableEntity_WithTimestamps_StoresCorrectly()
    {
        // Arrange
        var startTime = DateTime.UtcNow.AddHours(-1);
        var completeTime = DateTime.UtcNow;
        var lastUpdated = DateTime.UtcNow;

        // Act
        var entity = new EvalRunTableEntity
        {
            StartedDatetime = startTime,
            CompletedDatetime = completeTime,
            LastUpdatedOn = lastUpdated
        };

        // Assert
        Assert.Equal(startTime, entity.StartedDatetime);
        Assert.Equal(completeTime, entity.CompletedDatetime);
        Assert.Equal(lastUpdated, entity.LastUpdatedOn);
    }

    [Fact]
    public void EvalRunTableEntity_WithMetricsConfigurationId_StoresCorrectly()
    {
        // Arrange
        var metricsConfigId = "config-123";

        // Act
        var entity = new EvalRunTableEntity
        {
            MetricsConfigurationId = metricsConfigId
        };

        // Assert
        Assert.Equal(metricsConfigId, entity.MetricsConfigurationId);
    }

    [Fact]
    public void EvalRunTableEntity_WithLastUpdatedBy_StoresCorrectly()
    {
        // Arrange
        var updatedBy = "user@example.com";

        // Act
        var entity = new EvalRunTableEntity
        {
            LastUpdatedBy = updatedBy
        };

        // Assert
        Assert.Equal(updatedBy, entity.LastUpdatedBy);
    }

    [Fact]
    public void EvalRunTableEntity_CompleteScenario_AllPropertiesSet()
    {
        // Arrange
        var agentId = "agent-001";
        var datasetId = "dataset-001";
        var metricsConfigId = "config-001";
        var environmentId = "production";
        var startTime = DateTime.UtcNow.AddHours(-2);
        var completeTime = DateTime.UtcNow;

        // Act
        var entity = new EvalRunTableEntity();
        var evalRunId = entity.EvalRunId; // Capture the auto-generated ID
        entity.AgentId = agentId;
        entity.DataSetId = datasetId;
        entity.MetricsConfigurationId = metricsConfigId;
        entity.EnvironmentId = environmentId;
        entity.Type = "MCS";
        entity.Status = "Completed";
        entity.StartedDatetime = startTime;
        entity.CompletedDatetime = completeTime;
        entity.LastUpdatedOn = completeTime;
        entity.LastUpdatedBy = "system";

        // Assert
        Assert.Equal(agentId, entity.AgentId);
        Assert.Equal(agentId, entity.PartitionKey);
        Assert.Equal(evalRunId, entity.EvalRunId);
        // RowKey is set in constructor from the auto-generated EvalRunId
        Assert.Equal(evalRunId.ToString(), entity.RowKey);
        Assert.Equal(datasetId, entity.DataSetId);
        Assert.Equal(metricsConfigId, entity.MetricsConfigurationId);
        Assert.Equal(environmentId, entity.EnvironmentId);
        Assert.Equal("MCS", entity.Type);
        Assert.Equal("Completed", entity.Status);
        Assert.Equal(startTime, entity.StartedDatetime);
        Assert.Equal(completeTime, entity.CompletedDatetime);
        Assert.Equal("system", entity.LastUpdatedBy);
    }

    [Theory]
    [InlineData("MCS")]
    [InlineData("RAG")]
    [InlineData("Agent")]
    public void EvalRunTableEntity_WithDifferentTypes_StoresCorrectly(string type)
    {
        // Arrange & Act
        var entity = new EvalRunTableEntity
        {
            Type = type
        };

        // Assert
        Assert.Equal(type, entity.Type);
    }
}
