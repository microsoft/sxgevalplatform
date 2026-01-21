using Xunit;
using FluentAssertions;
using Sxg.EvalPlatform.API.Storage.Helpers;
using Sxg.EvalPlatform.API.Storage.TableEntities;
using Azure;

namespace Sxg.EvalPlatform.API.Storage.UnitTests.HelperTests
{
    /// <summary>
    /// Unit tests for DataSetTableEntityHelper
    /// </summary>
    [Trait("Category", TestCategories.Unit)]
    [Trait("Category", TestCategories.Helper)]
    public class DataSetTableEntityHelperTests
    {
        #region CreateEntity Tests

        [Fact]
        public void CreateEntity_WithoutDatasetId_CreatesEntityWithGeneratedId()
        {
            // Arrange
            var agentId = TestConstants.Agents.DefaultAgentId;
            var blobFilePath = "datasets/test.json";
            var containerName = TestConstants.Storage.ContainerName;
            var datasetType = TestConstants.Datasets.DatasetType;
            var datasetName = TestConstants.Datasets.DatasetName;
            var lastUpdatedBy = "test-user";

            // Act
            var entity = DataSetTableEntityHelper.CreateEntity(
                agentId, blobFilePath, containerName, datasetType, datasetName, lastUpdatedBy);

            // Assert
            entity.Should().NotBeNull();
            entity.AgentId.Should().Be(agentId);
            entity.PartitionKey.Should().Be(agentId);
            entity.BlobFilePath.Should().Be(blobFilePath);
            entity.ContainerName.Should().Be(containerName);
            entity.DatasetType.Should().Be(datasetType);
            entity.DatasetName.Should().Be(datasetName);
            entity.LastUpdatedBy.Should().Be(lastUpdatedBy);
            entity.DatasetId.Should().NotBeNullOrEmpty();
            entity.RowKey.Should().Be(entity.DatasetId);
            Guid.TryParse(entity.DatasetId, out _).Should().BeTrue();
        }

        [Fact]
        public void CreateEntity_WithDatasetId_CreatesEntityWithSpecificId()
        {
            // Arrange
            var agentId = TestConstants.Agents.DefaultAgentId;
            var datasetId = Guid.NewGuid().ToString();
            var blobFilePath = "datasets/test.json";
            var containerName = TestConstants.Storage.ContainerName;
            var datasetType = TestConstants.Datasets.DatasetType;
            var datasetName = TestConstants.Datasets.DatasetName;
            var lastUpdatedBy = "test-user";

            // Act
            var entity = DataSetTableEntityHelper.CreateEntity(
                agentId, datasetId, blobFilePath, containerName, datasetType, datasetName, lastUpdatedBy);

            // Assert
            entity.Should().NotBeNull();
            entity.AgentId.Should().Be(agentId);
            entity.DatasetId.Should().Be(datasetId);
            entity.PartitionKey.Should().Be(agentId);
            entity.RowKey.Should().Be(datasetId);
            entity.BlobFilePath.Should().Be(blobFilePath);
            entity.ContainerName.Should().Be(containerName);
            entity.DatasetType.Should().Be(datasetType);
            entity.DatasetName.Should().Be(datasetName);
            entity.LastUpdatedBy.Should().Be(lastUpdatedBy);
        }

        [Fact]
        public void CreateEntity_WithDefaultLastUpdatedBy_UsesSystem()
        {
            // Arrange
            var agentId = TestConstants.Agents.DefaultAgentId;
            var blobFilePath = "datasets/test.json";
            var containerName = TestConstants.Storage.ContainerName;
            var datasetType = TestConstants.Datasets.DatasetType;
            var datasetName = TestConstants.Datasets.DatasetName;

            // Act
            var entity = DataSetTableEntityHelper.CreateEntity(
                agentId, blobFilePath, containerName, datasetType, datasetName);

            // Assert
            entity.LastUpdatedBy.Should().Be("system");
        }

        [Fact]
        public void CreateEntity_SetsLastUpdatedOnToCurrentTime()
        {
            // Arrange
            var agentId = TestConstants.Agents.DefaultAgentId;
            var blobFilePath = "datasets/test.json";
            var containerName = TestConstants.Storage.ContainerName;
            var datasetType = TestConstants.Datasets.DatasetType;
            var datasetName = TestConstants.Datasets.DatasetName;
            var beforeCreation = DateTime.UtcNow;

            // Act
            var entity = DataSetTableEntityHelper.CreateEntity(
                agentId, blobFilePath, containerName, datasetType, datasetName);
            var afterCreation = DateTime.UtcNow;

            // Assert
            entity.LastUpdatedOn.Should().BeOnOrAfter(beforeCreation);
            entity.LastUpdatedOn.Should().BeOnOrBefore(afterCreation);
        }

        #endregion

        #region GetPartitionKey Tests

        [Fact]
        public void GetPartitionKey_WithValidAgentId_ReturnsAgentId()
        {
            // Arrange
            var agentId = TestConstants.Agents.DefaultAgentId;

            // Act
            var partitionKey = DataSetTableEntityHelper.GetPartitionKey(agentId);

            // Assert
            partitionKey.Should().Be(agentId);
        }

        [Theory]
        [InlineData("agent-123")]
        [InlineData("my-agent")]
        [InlineData("test_agent")]
        public void GetPartitionKey_WithVariousAgentIds_ReturnsCorrectKey(string agentId)
        {
            // Act
            var partitionKey = DataSetTableEntityHelper.GetPartitionKey(agentId);

            // Assert
            partitionKey.Should().Be(agentId);
        }

        #endregion

        #region GetRowKey Tests

        [Fact]
        public void GetRowKey_WithValidDatasetId_ReturnsDatasetId()
        {
            // Arrange
            var datasetId = Guid.NewGuid().ToString();

            // Act
            var rowKey = DataSetTableEntityHelper.GetRowKey(datasetId);

            // Assert
            rowKey.Should().Be(datasetId);
        }

        [Fact]
        public void GetRowKey_ReturnsInputUnmodified()
        {
            // Arrange
            var datasetId = "custom-dataset-id";

            // Act
            var rowKey = DataSetTableEntityHelper.GetRowKey(datasetId);

            // Assert
            rowKey.Should().Be(datasetId);
        }

        #endregion

        #region GenerateNewDatasetId Tests

        [Fact]
        public void GenerateNewDatasetId_ReturnsValidGuid()
        {
            // Act
            var datasetId = DataSetTableEntityHelper.GenerateNewDatasetId();

            // Assert
            Guid.TryParse(datasetId, out _).Should().BeTrue();
        }

        [Fact]
        public void GenerateNewDatasetId_CalledMultipleTimes_ReturnsDifferentGuids()
        {
            // Act
            var id1 = DataSetTableEntityHelper.GenerateNewDatasetId();
            var id2 = DataSetTableEntityHelper.GenerateNewDatasetId();
            var id3 = DataSetTableEntityHelper.GenerateNewDatasetId();

            // Assert
            id1.Should().NotBe(id2);
            id2.Should().NotBe(id3);
            id1.Should().NotBe(id3);
        }

        [Fact]
        public void GenerateNewDatasetId_ReturnsNonEmptyString()
        {
            // Act
            var datasetId = DataSetTableEntityHelper.GenerateNewDatasetId();

            // Assert
            datasetId.Should().NotBeNullOrEmpty();
        }

        #endregion

        #region ValidateKeys Tests

        [Fact]
        public void ValidateKeys_WithValidEntity_ReturnsTrue()
        {
            // Arrange
            var entity = DataSetTableEntityHelper.CreateEntity(
                TestConstants.Agents.DefaultAgentId,
                Guid.NewGuid().ToString(),
                "test.json",
                "testcontainer",
                "TestType",
                "Test Dataset");

            // Act
            var isValid = DataSetTableEntityHelper.ValidateKeys(entity);

            // Assert
            isValid.Should().BeTrue();
        }

        [Fact]
        public void ValidateKeys_WithMissingPartitionKey_ReturnsFalse()
        {
            // Arrange
            var datasetId = Guid.NewGuid().ToString();
            var entity = new DataSetTableEntity
            {
                AgentId = TestConstants.Agents.DefaultAgentId, // This sets PartitionKey automatically
                DatasetId = datasetId
            };
            // Manually set PartitionKey to null after it was auto-set
            entity.PartitionKey = null;

            // Act
            var isValid = DataSetTableEntityHelper.ValidateKeys(entity);

            // Assert
            isValid.Should().BeFalse();
        }

        [Fact]
        public void ValidateKeys_WithMismatchedRowKeyAndDatasetId_ReturnsFalse()
        {
            // Arrange
            var datasetId = Guid.NewGuid().ToString();
            var entity = new DataSetTableEntity
            {
                AgentId = TestConstants.Agents.DefaultAgentId,
                DatasetId = datasetId
            };
            // Manually override RowKey to a different value after it was auto-set
            entity.RowKey = "different-row-key";

            // Act
            var isValid = DataSetTableEntityHelper.ValidateKeys(entity);

            // Assert
            isValid.Should().BeFalse();
        }

        [Fact]
        public void ValidateKeys_WithMismatchedPartitionKeyAndAgentId_ReturnsFalse()
        {
            // Arrange
            var datasetId = Guid.NewGuid().ToString();
            var entity = new DataSetTableEntity
            {
                AgentId = TestConstants.Agents.DefaultAgentId, // This sets PartitionKey automatically
                DatasetId = datasetId
            };
            // Manually override PartitionKey after it was auto-set to create mismatch
            entity.PartitionKey = "different-agent";

            // Act
            var isValid = DataSetTableEntityHelper.ValidateKeys(entity);

            // Assert
            isValid.Should().BeFalse();
        }

        [Fact]
        public void ValidateKeys_WithInvalidGuidInDatasetId_ReturnsFalse()
        {
            // Arrange
            var entity = new DataSetTableEntity
            {
                PartitionKey = TestConstants.Agents.DefaultAgentId,
                RowKey = "not-a-guid",
                DatasetId = "not-a-guid",
                AgentId = TestConstants.Agents.DefaultAgentId
            };

            // Act
            var isValid = DataSetTableEntityHelper.ValidateKeys(entity);

            // Assert
            isValid.Should().BeFalse();
        }

        #endregion

        #region IsValidGuid Tests

        [Fact]
        public void IsValidGuid_WithValidGuid_ReturnsTrue()
        {
            // Arrange
            var validGuid = Guid.NewGuid().ToString();

            // Act
            var isValid = DataSetTableEntityHelper.IsValidGuid(validGuid);

            // Assert
            isValid.Should().BeTrue();
        }

        [Theory]
        [InlineData("not-a-guid")]
        [InlineData("12345")]
        [InlineData("")]
        [InlineData("abc-def-ghi")]
        public void IsValidGuid_WithInvalidGuid_ReturnsFalse(string invalidGuid)
        {
            // Act
            var isValid = DataSetTableEntityHelper.IsValidGuid(invalidGuid);

            // Assert
            isValid.Should().BeFalse();
        }

        [Fact]
        public void IsValidGuid_WithGuidInDifferentFormats_ReturnsTrue()
        {
            // Arrange
            var guid = Guid.NewGuid();
            var formats = new[]
            {
                guid.ToString("D"), // 00000000-0000-0000-0000-000000000000
                guid.ToString("N"), // 00000000000000000000000000000000
                guid.ToString("B"), // {00000000-0000-0000-0000-000000000000}
                guid.ToString("P")  // (00000000-0000-0000-0000-000000000000)
            };

            // Act & Assert
            foreach (var format in formats)
            {
                DataSetTableEntityHelper.IsValidGuid(format).Should().BeTrue();
            }
        }

        #endregion

        #region BuildFilterString Tests

        [Fact]
        public void BuildFilterString_WithOnlyAgentId_ReturnsBasicFilter()
        {
            // Arrange
            var agentId = TestConstants.Agents.DefaultAgentId;

            // Act
            var filter = DataSetTableEntityHelper.BuildFilterString(agentId);

            // Assert
            filter.Should().Be($"PartitionKey eq '{agentId}'");
        }

        [Fact]
        public void BuildFilterString_WithAgentIdAndType_ReturnsFilterWithType()
        {
            // Arrange
            var agentId = TestConstants.Agents.DefaultAgentId;
            var datasetType = TestConstants.Datasets.DatasetType;

            // Act
            var filter = DataSetTableEntityHelper.BuildFilterString(agentId, datasetType);

            // Assert
            filter.Should().Be($"PartitionKey eq '{agentId}' and DatasetType eq '{datasetType}'");
        }

        [Fact]
        public void BuildFilterString_WithAllParameters_ReturnsCompleteFilter()
        {
            // Arrange
            var agentId = TestConstants.Agents.DefaultAgentId;
            var datasetType = TestConstants.Datasets.DatasetType;
            var datasetName = TestConstants.Datasets.DatasetName;

            // Act
            var filter = DataSetTableEntityHelper.BuildFilterString(agentId, datasetType, datasetName);

            // Assert
            filter.Should().Be(
                $"PartitionKey eq '{agentId}' and DatasetType eq '{datasetType}' and DatasetName eq '{datasetName}'");
        }

        [Fact]
        public void BuildFilterString_WithNullType_IgnoresType()
        {
            // Arrange
            var agentId = TestConstants.Agents.DefaultAgentId;
            var datasetName = TestConstants.Datasets.DatasetName;

            // Act
            var filter = DataSetTableEntityHelper.BuildFilterString(agentId, null, datasetName);

            // Assert
            filter.Should().Be($"PartitionKey eq '{agentId}' and DatasetName eq '{datasetName}'");
        }

        [Fact]
        public void BuildFilterString_WithEmptyType_IgnoresType()
        {
            // Arrange
            var agentId = TestConstants.Agents.DefaultAgentId;
            var datasetName = TestConstants.Datasets.DatasetName;

            // Act
            var filter = DataSetTableEntityHelper.BuildFilterString(agentId, "", datasetName);

            // Assert
            filter.Should().Be($"PartitionKey eq '{agentId}' and DatasetName eq '{datasetName}'");
        }

        #endregion

        #region CreateBlobFilePath Tests

        [Fact]
        public void CreateBlobFilePath_WithValidInputs_ReturnsCorrectPath()
        {
            // Arrange
            var agentId = TestConstants.Agents.DefaultAgentId;
            var datasetId = Guid.NewGuid().ToString();
            var datasetName = "TestDataset";

            // Act
            var path = DataSetTableEntityHelper.CreateBlobFilePath(agentId, datasetId, datasetName);

            // Assert
            path.Should().Be($"datasets/{agentId}/{datasetId}_{datasetName}.json");
        }

        [Fact]
        public void CreateBlobFilePath_ContainsAllComponents()
        {
            // Arrange
            var agentId = "agent-123";
            var datasetId = Guid.NewGuid().ToString();
            var datasetName = "MyDataset";

            // Act
            var path = DataSetTableEntityHelper.CreateBlobFilePath(agentId, datasetId, datasetName);

            // Assert
            path.Should().Contain("datasets/");
            path.Should().Contain(agentId);
            path.Should().Contain(datasetId);
            path.Should().Contain(datasetName);
            path.Should().EndWith(".json");
        }

        #endregion

        #region CreateContainerName Tests

        [Fact]
        public void CreateContainerName_WithValidAgentId_ReturnsNormalizedName()
        {
            // Arrange
            var agentId = "My Test Agent";

            // Act
            var containerName = DataSetTableEntityHelper.CreateContainerName(agentId);

            // Assert
            containerName.Should().NotBeNullOrEmpty();
            containerName.Should().Be("mytestagent");
        }

        [Fact]
        public void CreateContainerName_WithSpecialCharacters_RemovesInvalidCharacters()
        {
            // Arrange
            var agentId = "Agent@123!Test";

            // Act
            var containerName = DataSetTableEntityHelper.CreateContainerName(agentId);

            // Assert
            containerName.Should().NotContain("@");
            containerName.Should().NotContain("!");
        }

        [Fact]
        public void CreateContainerName_ReturnsAzureBlobStorageCompliantName()
        {
            // Arrange
            var agentId = "My Agent 123";

            // Act
            var containerName = DataSetTableEntityHelper.CreateContainerName(agentId);

            // Assert
            // Azure Blob Storage container naming rules:
            // - All lowercase
            // - Length 3-63
            // - Alphanumeric and hyphens only
            // - Start and end with letter or number
            containerName.Should().MatchRegex("^[a-z0-9][a-z0-9-]{1,61}[a-z0-9]$");
        }

        #endregion

        #region ValidateEntity Tests

        [Fact]
        public void ValidateEntity_WithValidEntity_ReturnsEmptyErrorList()
        {
            // Arrange
            var entity = DataSetTableEntityHelper.CreateEntity(
                TestConstants.Agents.DefaultAgentId,
                Guid.NewGuid().ToString(),
                "test.json",
                "testcontainer",
                "TestType",
                "Test Dataset");

            // Act
            var errors = DataSetTableEntityHelper.ValidateEntity(entity);

            // Assert
            errors.Should().BeEmpty();
        }

        [Fact]
        public void ValidateEntity_WithMissingAgentId_ReturnsError()
        {
            // Arrange
            var entity = new DataSetTableEntity
            {
                AgentId = null,
                DatasetId = Guid.NewGuid().ToString(),
                BlobFilePath = "test.json",
                ContainerName = "container",
                DatasetType = "Type",
                DatasetName = "Name"
            };

            // Act
            var errors = DataSetTableEntityHelper.ValidateEntity(entity);

            // Assert
            errors.Should().Contain("AgentId is required");
        }

        [Fact]
        public void ValidateEntity_WithMissingDatasetId_ReturnsError()
        {
            // Arrange
            var entity = new DataSetTableEntity
            {
                AgentId = TestConstants.Agents.DefaultAgentId,
                DatasetId = null,
                BlobFilePath = "test.json",
                ContainerName = "container",
                DatasetType = "Type",
                DatasetName = "Name"
            };

            // Act
            var errors = DataSetTableEntityHelper.ValidateEntity(entity);

            // Assert
            errors.Should().Contain("DatasetId is required");
        }

        [Fact]
        public void ValidateEntity_WithMultipleMissingFields_ReturnsMultipleErrors()
        {
            // Arrange
            // Note: Constructor automatically generates DatasetId, so we only test other missing fields
            var entity = new DataSetTableEntity();

            // Act
            var errors = DataSetTableEntityHelper.ValidateEntity(entity);

            // Assert
            errors.Should().HaveCountGreaterThan(0);
            errors.Should().Contain(e => e.Contains("AgentId"));
            // DatasetId is auto-generated by constructor, so it won't be in errors
            errors.Should().Contain(e => e.Contains("BlobFilePath"));
            errors.Should().Contain(e => e.Contains("ContainerName"));
            errors.Should().Contain(e => e.Contains("DatasetType"));
            errors.Should().Contain(e => e.Contains("DatasetName"));
        }

        #endregion

        #region CreateUpdatedCopy Tests

        [Fact]
        public void CreateUpdatedCopy_CreatesNewEntityWithSameData()
        {
            // Arrange
            var original = DataSetTableEntityHelper.CreateEntity(
                TestConstants.Agents.DefaultAgentId,
                Guid.NewGuid().ToString(),
                "test.json",
                "testcontainer",
                "TestType",
                "Test Dataset",
                "original-user");

            // Act
            var copy = DataSetTableEntityHelper.CreateUpdatedCopy(original, "new-user");

            // Assert
            copy.AgentId.Should().Be(original.AgentId);
            copy.DatasetId.Should().Be(original.DatasetId);
            copy.BlobFilePath.Should().Be(original.BlobFilePath);
            copy.ContainerName.Should().Be(original.ContainerName);
            copy.DatasetType.Should().Be(original.DatasetType);
            copy.DatasetName.Should().Be(original.DatasetName);
            copy.PartitionKey.Should().Be(original.PartitionKey);
            copy.RowKey.Should().Be(original.RowKey);
        }

        [Fact]
        public void CreateUpdatedCopy_UpdatesLastUpdatedBy()
        {
            // Arrange
            var original = DataSetTableEntityHelper.CreateEntity(
                TestConstants.Agents.DefaultAgentId,
                Guid.NewGuid().ToString(),
                "test.json",
                "testcontainer",
                "TestType",
                "Test Dataset",
                "original-user");

            // Act
            var copy = DataSetTableEntityHelper.CreateUpdatedCopy(original, "new-user");

            // Assert
            copy.LastUpdatedBy.Should().Be("new-user");
            copy.LastUpdatedBy.Should().NotBe(original.LastUpdatedBy);
        }

        [Fact]
        public void CreateUpdatedCopy_UpdatesLastUpdatedOn()
        {
            // Arrange
            var original = DataSetTableEntityHelper.CreateEntity(
                TestConstants.Agents.DefaultAgentId,
                Guid.NewGuid().ToString(),
                "test.json",
                "testcontainer",
                "TestType",
                "Test Dataset");

            // Wait a bit to ensure time difference
            System.Threading.Thread.Sleep(10);

            // Act
            var copy = DataSetTableEntityHelper.CreateUpdatedCopy(original, "new-user");

            // Assert
            if (copy.LastUpdatedOn.HasValue && original.LastUpdatedOn.HasValue)
            {
                copy.LastUpdatedOn.Value.Should().BeAfter(original.LastUpdatedOn.Value);
            }
            else
            {
                copy.LastUpdatedOn.Should().NotBeNull();
            }
        }

        [Fact]
        public void CreateUpdatedCopy_PreservesAzureTableStorageMetadata()
        {
            // Arrange
            var original = DataSetTableEntityHelper.CreateEntity(
                TestConstants.Agents.DefaultAgentId,
                Guid.NewGuid().ToString(),
                "test.json",
                "testcontainer",
                "TestType",
                "Test Dataset");
            
            // Simulate Azure Table Storage metadata
            original.ETag = new ETag("test-etag");
            original.Timestamp = DateTimeOffset.UtcNow;

            // Act
            var copy = DataSetTableEntityHelper.CreateUpdatedCopy(original, "new-user");

            // Assert
            copy.ETag.Should().Be(original.ETag);
            copy.Timestamp.Should().Be(original.Timestamp);
        }

        #endregion
    }
}
