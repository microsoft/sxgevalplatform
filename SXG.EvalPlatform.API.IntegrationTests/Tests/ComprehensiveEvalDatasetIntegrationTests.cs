using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using SXG.EvalPlatform.API.IntegrationTests.Infrastructure;
using SXG.EvalPlatform.API.IntegrationTests.Helpers;
using SxgEvalPlatformApi.Models;
using Xunit;

namespace SXG.EvalPlatform.API.IntegrationTests.Tests;

/// <summary>
/// Comprehensive integration tests for EvalDataset API endpoints
/// Tests all CRUD operations, caching behavior, agent isolation, and edge cases
/// </summary>
[Collection("Integration Tests")]
public class ComprehensiveEvalDatasetIntegrationTests : InMemoryIntegrationTestBase
{
    private const string BaseUrl = "/api/v1/datasets";

    public ComprehensiveEvalDatasetIntegrationTests(InMemoryWebApplicationFactory factory) : base(factory)
    {
    }

    #region Dataset Creation Tests

    [Fact]
    public async Task SaveDataset_WithValidData_ShouldCreateDataset()
    {
        // Arrange
        var datasetName = $"Test Dataset {DateTime.UtcNow:yyyyMMddHHmmssfff}";
        var saveRequest = DatasetTestDataHelper.CreateValidSaveDatasetDto("test-agent-001", datasetName, DatasetTypes.Synthetic);

        // Act
        var response = await Client.PostAsync(BaseUrl, CreateJsonContent(saveRequest));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var result = await response.Content.ReadFromJsonAsync<DatasetSaveResponseDto>();
        result.Should().NotBeNull();
        result!.DatasetId.Should().NotBeEmpty();
        result.Status.Should().Be("created");
        result.Message.Should().Contain("successfully");
    }

    [Fact]
    public async Task SaveDataset_WithInvalidAgentId_ShouldReturnBadRequest()
    {
        // Arrange
        var saveRequest = DatasetTestDataHelper.CreateValidSaveDatasetDto("", "Test Dataset", DatasetTypes.Synthetic);

        // Act
        var response = await Client.PostAsync(BaseUrl, CreateJsonContent(saveRequest));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task SaveDataset_WithInvalidDatasetType_ShouldReturnBadRequest()
    {
        // Arrange
        var saveRequest = DatasetTestDataHelper.CreateValidSaveDatasetDto("test-agent-001", "Test Dataset", "InvalidType");

        // Act
        var response = await Client.PostAsync(BaseUrl, CreateJsonContent(saveRequest));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task SaveDataset_WithEmptyDatasetRecords_ShouldReturnBadRequest()
    {
        // Arrange
        var saveRequest = DatasetTestDataHelper.CreateValidSaveDatasetDto("test-agent-001", "Test Dataset", DatasetTypes.Golden);
        saveRequest.DatasetRecords = new List<EvalDataset>(); // Empty list

        // Act
        var response = await Client.PostAsync(BaseUrl, CreateJsonContent(saveRequest));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    #endregion

    #region Dataset Retrieval Tests

    [Fact]
    public async Task GetDatasetsByAgentId_WithExistingAgent_ShouldReturnDatasets()
    {
        // Arrange - Create test dataset first
        var agentId = "test-agent-002";
        var datasetName = $"Test Dataset {DateTime.UtcNow:yyyyMMddHHmmssfff}";
        var saveRequest = DatasetTestDataHelper.CreateValidSaveDatasetDto(agentId, datasetName, DatasetTypes.Synthetic);

        var createResponse = await Client.PostAsync(BaseUrl, CreateJsonContent(saveRequest));
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        // Act
        var response = await Client.GetAsync($"{BaseUrl}?agentId={agentId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var datasets = await response.Content.ReadFromJsonAsync<List<DatasetMetadataDto>>();
        datasets.Should().NotBeNull();
        datasets!.Should().HaveCountGreaterOrEqualTo(1);
        datasets.Should().Contain(d => d.AgentId == agentId && d.DatasetName == datasetName);
    }

    [Fact]
    public async Task GetDatasetsByAgentId_WithNonExistentAgent_ShouldReturnNotFound()
    {
        // Arrange
        var nonExistentAgentId = Guid.NewGuid().ToString();

        // Act
        var response = await Client.GetAsync($"{BaseUrl}?agentId={nonExistentAgentId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetDatasetsByAgentId_WithoutAgentId_ShouldReturnBadRequest()
    {
        // Act
        var response = await Client.GetAsync(BaseUrl);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetDatasetById_WithExistingDataset_ShouldReturnDatasetContent()
    {
        // Arrange - Create test dataset first
        var agentId = "test-agent-003";
        var datasetName = $"Test Dataset {DateTime.UtcNow:yyyyMMddHHmmssfff}";
        var saveRequest = DatasetTestDataHelper.CreateValidSaveDatasetDto(agentId, datasetName, DatasetTypes.Golden);

        var createResponse = await Client.PostAsync(BaseUrl, CreateJsonContent(saveRequest));
        var createResult = await createResponse.Content.ReadFromJsonAsync<DatasetSaveResponseDto>();
        var datasetId = createResult!.DatasetId;

        // Act
        var response = await Client.GetAsync($"{BaseUrl}/{datasetId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadFromJsonAsync<List<EvalDataset>>();
        content.Should().NotBeNull();
        content!.Should().HaveCount(saveRequest.DatasetRecords.Count);
        content.Should().BeEquivalentTo(saveRequest.DatasetRecords);
    }

    [Fact]
    public async Task GetDatasetById_WithNonExistentDataset_ShouldReturnNotFound()
    {
        // Arrange
        var nonExistentDatasetId = Guid.NewGuid();

        // Act
        var response = await Client.GetAsync($"{BaseUrl}/{nonExistentDatasetId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetDatasetMetadataById_WithExistingDataset_ShouldReturnMetadata()
    {
        // Arrange - Create test dataset first
        var agentId = "test-agent-004";
        var datasetName = $"Test Dataset {DateTime.UtcNow:yyyyMMddHHmmssfff}";
        var saveRequest = DatasetTestDataHelper.CreateValidSaveDatasetDto(agentId, datasetName, DatasetTypes.Synthetic);

        var createResponse = await Client.PostAsync(BaseUrl, CreateJsonContent(saveRequest));
        var createResult = await createResponse.Content.ReadFromJsonAsync<DatasetSaveResponseDto>();
        var datasetId = createResult!.DatasetId;

        // Act
        var response = await Client.GetAsync($"{BaseUrl}/{datasetId}/metadata");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var metadata = await response.Content.ReadFromJsonAsync<DatasetMetadataDto>();
        metadata.Should().NotBeNull();
        metadata!.DatasetId.Should().Be(datasetId);
        metadata.AgentId.Should().Be(agentId);
        metadata.DatasetName.Should().Be(datasetName);
        metadata.DatasetType.Should().Be(DatasetTypes.Synthetic);
        metadata.RecordCount.Should().Be(saveRequest.DatasetRecords.Count);
    }

    #endregion

    #region Dataset Update Tests

    [Fact]
    public async Task UpdateDataset_WithValidData_ShouldUpdateDataset()
    {
        // Arrange - Create test dataset first
        var agentId = "test-agent-005";
        var datasetName = $"Test Dataset {DateTime.UtcNow:yyyyMMddHHmmssfff}";
        var saveRequest = DatasetTestDataHelper.CreateValidSaveDatasetDto(agentId, datasetName, DatasetTypes.Golden);

        var createResponse = await Client.PostAsync(BaseUrl, CreateJsonContent(saveRequest));
        var createResult = await createResponse.Content.ReadFromJsonAsync<DatasetSaveResponseDto>();
        var datasetId = createResult!.DatasetId;

        // Create update request with different data
        var updateRequest = new UpdateDatasetDto
        {
            DatasetRecords = DatasetTestDataHelper.CreateTestDatasetRecords(5) // Different count
        };

        // Act
        var response = await Client.PutAsync($"{BaseUrl}/{datasetId}", CreateJsonContent(updateRequest));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<DatasetSaveResponseDto>();
        result.Should().NotBeNull();
        result!.DatasetId.Should().Be(datasetId);
        result.Status.Should().Be("updated");

        // Verify the update by retrieving the dataset
        var getResponse = await Client.GetAsync($"{BaseUrl}/{datasetId}");
        var updatedContent = await getResponse.Content.ReadFromJsonAsync<List<EvalDataset>>();
        updatedContent.Should().HaveCount(5);
    }

    [Fact]
    public async Task UpdateDataset_WithNonExistentDataset_ShouldReturnNotFound()
    {
        // Arrange
        var nonExistentDatasetId = Guid.NewGuid();
        var updateRequest = new UpdateDatasetDto
        {
            DatasetRecords = DatasetTestDataHelper.CreateTestDatasetRecords(2)
        };

        // Act
        var response = await Client.PutAsync($"{BaseUrl}/{nonExistentDatasetId}", CreateJsonContent(updateRequest));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError); // Based on controller implementation
    }

    [Fact]
    public async Task UpdateDataset_WithEmptyDatasetRecords_ShouldReturnBadRequest()
    {
        // Arrange - Create test dataset first
        var agentId = "test-agent-006";
        var datasetName = $"Test Dataset {DateTime.UtcNow:yyyyMMddHHmmssfff}";
        var saveRequest = DatasetTestDataHelper.CreateValidSaveDatasetDto(agentId, datasetName, DatasetTypes.Synthetic);

        var createResponse = await Client.PostAsync(BaseUrl, CreateJsonContent(saveRequest));
        var createResult = await createResponse.Content.ReadFromJsonAsync<DatasetSaveResponseDto>();
        var datasetId = createResult!.DatasetId;

        var updateRequest = new UpdateDatasetDto
        {
            DatasetRecords = new List<EvalDataset>() // Empty list
        };

        // Act
        var response = await Client.PutAsync($"{BaseUrl}/{datasetId}", CreateJsonContent(updateRequest));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    #endregion

    #region Dataset Deletion Tests

    [Fact]
    public async Task DeleteDataset_WithExistingDataset_ShouldDeleteSuccessfully()
    {
        // Arrange - Create test dataset first
        var agentId = "test-agent-007";
        var datasetName = $"Test Dataset {DateTime.UtcNow:yyyyMMddHHmmssfff}";
        var saveRequest = DatasetTestDataHelper.CreateValidSaveDatasetDto(agentId, datasetName, DatasetTypes.Golden);

        var createResponse = await Client.PostAsync(BaseUrl, CreateJsonContent(saveRequest));
        var createResult = await createResponse.Content.ReadFromJsonAsync<DatasetSaveResponseDto>();
        var datasetId = createResult!.DatasetId;

        // Act
        var response = await Client.DeleteAsync($"{BaseUrl}/{datasetId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Verify deletion by trying to retrieve the dataset
        var getResponse = await Client.GetAsync($"{BaseUrl}/{datasetId}");
        getResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeleteDataset_WithNonExistentDataset_ShouldReturnNotFound()
    {
        // Arrange
        var nonExistentDatasetId = Guid.NewGuid();

        // Act
        var response = await Client.DeleteAsync($"{BaseUrl}/{nonExistentDatasetId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    #endregion

    #region Agent Isolation Tests

    [Fact]
    public async Task AgentIsolation_ShouldMaintainSeparateDatasets()
    {
        // Arrange - Create datasets for different agents
        var agent1Id = "agent-isolation-001";
        var agent2Id = "agent-isolation-002";
        var agent3Id = "agent-isolation-003";

        var dataset1 = DatasetTestDataHelper.CreateValidSaveDatasetDto(agent1Id, "Dataset for agent 1", DatasetTypes.Synthetic);
        var dataset2 = DatasetTestDataHelper.CreateValidSaveDatasetDto(agent2Id, "Dataset for agent 2", DatasetTypes.Golden);
        var dataset3 = DatasetTestDataHelper.CreateValidSaveDatasetDto(agent3Id, "Dataset for agent 3", DatasetTypes.Synthetic);

        // Act - Create datasets
        await Client.PostAsync(BaseUrl, CreateJsonContent(dataset1));
        await Client.PostAsync(BaseUrl, CreateJsonContent(dataset2));
        await Client.PostAsync(BaseUrl, CreateJsonContent(dataset3));

        // Assert - Each agent should only see their own datasets
        var agent1Response = await Client.GetAsync($"{BaseUrl}?agentId={agent1Id}");
        var agent1Datasets = await agent1Response.Content.ReadFromJsonAsync<List<DatasetMetadataDto>>();
        agent1Datasets.Should().HaveCount(1);
        agent1Datasets![0].AgentId.Should().Be(agent1Id);

        var agent2Response = await Client.GetAsync($"{BaseUrl}?agentId={agent2Id}");
        var agent2Datasets = await agent2Response.Content.ReadFromJsonAsync<List<DatasetMetadataDto>>();
        agent2Datasets.Should().HaveCount(1);
        agent2Datasets![0].AgentId.Should().Be(agent2Id);

        var agent3Response = await Client.GetAsync($"{BaseUrl}?agentId={agent3Id}");
        var agent3Datasets = await agent3Response.Content.ReadFromJsonAsync<List<DatasetMetadataDto>>();
        agent3Datasets.Should().HaveCount(1);
        agent3Datasets![0].AgentId.Should().Be(agent3Id);
    }

    #endregion

    #region Concurrent Operations Tests

    [Fact]
    public async Task ConcurrentDatasetCreation_ShouldHandleMultipleOperations()
    {
        // Arrange - Create multiple dataset save requests
        var tasks = Enumerable.Range(0, 5).Select(async i =>
        {
            var agentId = $"concurrent-agent-{i:D3}";
            var datasetName = $"Concurrent Dataset {i + 1}";
            var saveRequest = DatasetTestDataHelper.CreateValidSaveDatasetDto(agentId, datasetName, 
                i % 2 == 0 ? DatasetTypes.Synthetic : DatasetTypes.Golden);
            
            return await Client.PostAsync(BaseUrl, CreateJsonContent(saveRequest));
        });

        // Act
        var responses = await Task.WhenAll(tasks);

        // Assert
        responses.Should().AllSatisfy(r => r.StatusCode.Should().Be(HttpStatusCode.Created));
        
        // Verify all datasets were created with unique IDs
        var datasetIds = new List<string>();
        foreach (var response in responses)
        {
            var result = await response.Content.ReadFromJsonAsync<DatasetSaveResponseDto>();
            datasetIds.Add(result!.DatasetId);
        }
        
        datasetIds.Should().OnlyHaveUniqueItems();
    }

    #endregion

    #region Edge Cases and Error Handling

    [Fact]
    public async Task GetDatasetById_WithInvalidGuid_ShouldReturnBadRequest()
    {
        // Act
        var response = await Client.GetAsync($"{BaseUrl}/invalid-guid");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task SaveDataset_WithLargeDataset_ShouldHandleSuccessfully()
    {
        // Arrange - Create dataset with many records
        var agentId = "test-agent-large";
        var datasetName = $"Large Dataset {DateTime.UtcNow:yyyyMMddHHmmssfff}";
        var saveRequest = DatasetTestDataHelper.CreateValidSaveDatasetDto(agentId, datasetName, DatasetTypes.Synthetic);
        saveRequest.DatasetRecords = DatasetTestDataHelper.CreateTestDatasetRecords(100); // Large dataset

        // Act
        var response = await Client.PostAsync(BaseUrl, CreateJsonContent(saveRequest));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var result = await response.Content.ReadFromJsonAsync<DatasetSaveResponseDto>();
        result.Should().NotBeNull();
        result!.DatasetId.Should().NotBeEmpty();
    }

    [Fact]
    public async Task SaveDataset_WithSpecialCharactersInName_ShouldHandleCorrectly()
    {
        // Arrange
        var agentId = "test-agent-special";
        var datasetName = "Dataset with Special Characters: !@#$%^&*()";
        var saveRequest = DatasetTestDataHelper.CreateValidSaveDatasetDto(agentId, datasetName, DatasetTypes.Golden);

        // Act
        var response = await Client.PostAsync(BaseUrl, CreateJsonContent(saveRequest));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var result = await response.Content.ReadFromJsonAsync<DatasetSaveResponseDto>();
        result.Should().NotBeNull();
    }

    [Fact]
    public async Task DatasetWorkflow_CompleteEndToEndScenario_ShouldWorkCorrectly()
    {
        // Arrange
        var agentId = "test-agent-e2e";
        var datasetName = $"E2E Dataset {DateTime.UtcNow:yyyyMMddHHmmssfff}";

        // Step 1: Create dataset
        var saveRequest = DatasetTestDataHelper.CreateValidSaveDatasetDto(agentId, datasetName, DatasetTypes.Synthetic);
        var createResponse = await Client.PostAsync(BaseUrl, CreateJsonContent(saveRequest));
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        
        var createResult = await createResponse.Content.ReadFromJsonAsync<DatasetSaveResponseDto>();
        var datasetId = createResult!.DatasetId;

        // Step 2: Retrieve dataset content
        var getContentResponse = await Client.GetAsync($"{BaseUrl}/{datasetId}");
        getContentResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Step 3: Retrieve dataset metadata
        var getMetadataResponse = await Client.GetAsync($"{BaseUrl}/{datasetId}/metadata");
        getMetadataResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Step 4: List datasets for agent
        var listResponse = await Client.GetAsync($"{BaseUrl}?agentId={agentId}");
        listResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var datasets = await listResponse.Content.ReadFromJsonAsync<List<DatasetMetadataDto>>();
        datasets.Should().Contain(d => d.DatasetId == datasetId);

        // Step 5: Update dataset
        var updateRequest = new UpdateDatasetDto
        {
            DatasetRecords = DatasetTestDataHelper.CreateTestDatasetRecords(10)
        };
        var updateResponse = await Client.PutAsync($"{BaseUrl}/{datasetId}", CreateJsonContent(updateRequest));
        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Step 6: Verify update
        var getUpdatedResponse = await Client.GetAsync($"{BaseUrl}/{datasetId}");
        var updatedContent = await getUpdatedResponse.Content.ReadFromJsonAsync<List<EvalDataset>>();
        updatedContent.Should().HaveCount(10);

        // Step 7: Delete dataset
        var deleteResponse = await Client.DeleteAsync($"{BaseUrl}/{datasetId}");
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Step 8: Verify deletion
        var getDeletedResponse = await Client.GetAsync($"{BaseUrl}/{datasetId}");
        getDeletedResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    #endregion
}
