using Xunit;
using FluentAssertions;
using Sxg.EvalPlatform.API.Storage.TableEntities;
using Azure;

namespace Sxg.EvalPlatform.API.Storage.UnitTests.TableEntityTests
{
    /// <summary>
    /// Unit tests for DataSetTableEntity
    /// </summary>
    [Trait("Category", TestCategories.Unit)]
    [Trait("Category", TestCategories.TableStorage)]
    public class DataSetTableEntityTests
    {
        #region Constructor Tests

        [Fact]
        public void Constructor_InitializesWithNewGuidAsDatasetId()
        {
            // Act
            var entity = new DataSetTableEntity();

            // Assert
            entity.DatasetId.Should().NotBeNullOrEmpty();
            Guid.TryParse(entity.DatasetId, out _).Should().BeTrue();
        }

        [Fact]
        public void Constructor_InitializesLastUpdatedOn()
        {
            // Arrange
            var beforeCreation = DateTime.UtcNow.AddSeconds(-1);

            // Act
            var entity = new DataSetTableEntity();
            var afterCreation = DateTime.UtcNow.AddSeconds(1);

            // Assert
            entity.LastUpdatedOn.Should().NotBeNull();
            entity.LastUpdatedOn!.Value.Should().BeOnOrAfter(beforeCreation);
            entity.LastUpdatedOn.Value.Should().BeOnOrBefore(afterCreation);
        }

        [Fact]
        public void Constructor_InitializesEmptyStrings()
        {
            // Act
            var entity = new DataSetTableEntity();

            // Assert
            entity.AgentId.Should().BeEmpty();
            entity.BlobFilePath.Should().BeEmpty();
            entity.ContainerName.Should().BeEmpty();
            entity.DatasetType.Should().BeEmpty();
            entity.DatasetName.Should().BeEmpty();
            entity.PartitionKey.Should().BeEmpty();
        }

        [Fact]
        public void Constructor_SetsRowKeyToDatasetId()
        {
            // Act
            var entity = new DataSetTableEntity();

            // Assert
            entity.RowKey.Should().Be(entity.DatasetId);
        }

        #endregion

        #region AgentId Property Tests

        [Fact]
        public void AgentId_WhenSet_UpdatesPartitionKey()
        {
            // Arrange
            var entity = new DataSetTableEntity();
            var agentId = TestConstants.Agents.DefaultAgentId;

            // Act
            entity.AgentId = agentId;

            // Assert
            entity.AgentId.Should().Be(agentId);
            entity.PartitionKey.Should().Be(agentId);
        }

        [Fact]
        public void AgentId_WhenChangedMultipleTimes_AlwaysUpdatesPartitionKey()
        {
            // Arrange
            var entity = new DataSetTableEntity();

            // Act
            entity.AgentId = "agent1";
            entity.AgentId = "agent2";
            entity.AgentId = "agent3";

            // Assert
            entity.AgentId.Should().Be("agent3");
            entity.PartitionKey.Should().Be("agent3");
        }

        [Fact]
        public void AgentId_WhenSetToEmptyString_UpdatesPartitionKeyToEmpty()
        {
            // Arrange
            var entity = new DataSetTableEntity
            {
                AgentId = TestConstants.Agents.DefaultAgentId
            };

            // Act
            entity.AgentId = string.Empty;

            // Assert
            entity.AgentId.Should().BeEmpty();
            entity.PartitionKey.Should().BeEmpty();
        }

        #endregion

        #region DatasetId Property Tests

        [Fact]
        public void DatasetId_WhenSet_UpdatesRowKey()
        {
            // Arrange
            var entity = new DataSetTableEntity();
            var datasetId = Guid.NewGuid().ToString();

            // Act
            entity.DatasetId = datasetId;

            // Assert
            entity.DatasetId.Should().Be(datasetId);
            entity.RowKey.Should().Be(datasetId);
        }

        [Fact]
        public void DatasetId_WhenChangedMultipleTimes_AlwaysUpdatesRowKey()
        {
            // Arrange
            var entity = new DataSetTableEntity();
            var id1 = Guid.NewGuid().ToString();
            var id2 = Guid.NewGuid().ToString();
            var id3 = Guid.NewGuid().ToString();

            // Act
            entity.DatasetId = id1;
            entity.DatasetId = id2;
            entity.DatasetId = id3;

            // Assert
            entity.DatasetId.Should().Be(id3);
            entity.RowKey.Should().Be(id3);
        }

        #endregion

        #region Property Assignment Tests

        [Fact]
        public void Properties_CanBeSetAndRetrieved()
        {
            // Arrange
            var entity = new DataSetTableEntity();

            // Act
            entity.BlobFilePath = "datasets/test.json";
            entity.ContainerName = "testcontainer";
            entity.DatasetType = "Synthetic";
            entity.DatasetName = "Test Dataset";
            entity.CreatedBy = "test-user";
            entity.CreatedOn = DateTime.UtcNow;
            entity.LastUpdatedBy = "update-user";

            // Assert
            entity.BlobFilePath.Should().Be("datasets/test.json");
            entity.ContainerName.Should().Be("testcontainer");
            entity.DatasetType.Should().Be("Synthetic");
            entity.DatasetName.Should().Be("Test Dataset");
            entity.CreatedBy.Should().Be("test-user");
            entity.CreatedOn.Should().NotBeNull();
            entity.LastUpdatedBy.Should().Be("update-user");
        }

        [Fact]
        public void CreatedBy_CanBeNull()
        {
            // Arrange
            var entity = new DataSetTableEntity();

            // Act
            entity.CreatedBy = null;

            // Assert
            entity.CreatedBy.Should().BeNull();
        }

        [Fact]
        public void LastUpdatedBy_CanBeNull()
        {
            // Arrange
            var entity = new DataSetTableEntity();

            // Act
            entity.LastUpdatedBy = null;

            // Assert
            entity.LastUpdatedBy.Should().BeNull();
        }

        #endregion

        #region SetPartitionKey Tests

        [Fact]
        public void SetPartitionKey_SetsPartitionKeyToAgentId()
        {
            // Arrange
            var entity = new DataSetTableEntity
            {
                AgentId = TestConstants.Agents.DefaultAgentId
            };

            // Act
            entity.SetPartitionKey();

            // Assert
            entity.PartitionKey.Should().Be(TestConstants.Agents.DefaultAgentId);
        }

        [Fact]
        public void SetPartitionKey_WithEmptyAgentId_SetsEmptyPartitionKey()
        {
            // Arrange
            var entity = new DataSetTableEntity();

            // Act
            entity.SetPartitionKey();

            // Assert
            entity.PartitionKey.Should().BeEmpty();
        }

        #endregion

        #region SetRowKey Tests

        [Fact]
        public void SetRowKey_SetsRowKeyToDatasetId()
        {
            // Arrange
            var entity = new DataSetTableEntity();
            var originalDatasetId = entity.DatasetId;

            // Act
            entity.SetRowKey();

            // Assert
            entity.RowKey.Should().Be(originalDatasetId);
        }

        #endregion

        #region SetKeys Tests

        [Fact]
        public void SetKeys_SetsBothPartitionKeyAndRowKey()
        {
            // Arrange
            var entity = new DataSetTableEntity
            {
                AgentId = TestConstants.Agents.DefaultAgentId
            };
            var originalDatasetId = entity.DatasetId;

            // Act
            entity.SetKeys();

            // Assert
            entity.PartitionKey.Should().Be(TestConstants.Agents.DefaultAgentId);
            entity.RowKey.Should().Be(originalDatasetId);
        }

        #endregion

        #region SetDatasetId Tests

        [Fact]
        public void SetDatasetId_UpdatesDatasetIdAndRowKey()
        {
            // Arrange
            var entity = new DataSetTableEntity();
            var newDatasetId = Guid.NewGuid().ToString();

            // Act
            entity.SetDatasetId(newDatasetId);

            // Assert
            entity.DatasetId.Should().Be(newDatasetId);
            entity.RowKey.Should().Be(newDatasetId);
        }

        [Fact]
        public void SetDatasetId_WithCustomString_AcceptsNonGuid()
        {
            // Arrange
            var entity = new DataSetTableEntity();
            var customId = "custom-dataset-id";

            // Act
            entity.SetDatasetId(customId);

            // Assert
            entity.DatasetId.Should().Be(customId);
            entity.RowKey.Should().Be(customId);
        }

        #endregion

        #region GenerateNewDatasetId Tests

        [Fact]
        public void GenerateNewDatasetId_CreatesNewGuid()
        {
            // Arrange
            var entity = new DataSetTableEntity();
            var originalId = entity.DatasetId;

            // Act
            entity.GenerateNewDatasetId();

            // Assert
            entity.DatasetId.Should().NotBe(originalId);
            Guid.TryParse(entity.DatasetId, out _).Should().BeTrue();
        }

        [Fact]
        public void GenerateNewDatasetId_UpdatesRowKey()
        {
            // Arrange
            var entity = new DataSetTableEntity();

            // Act
            entity.GenerateNewDatasetId();

            // Assert
            entity.RowKey.Should().Be(entity.DatasetId);
        }

        [Fact]
        public void GenerateNewDatasetId_CalledMultipleTimes_CreatesDifferentIds()
        {
            // Arrange
            var entity = new DataSetTableEntity();

            // Act
            entity.GenerateNewDatasetId();
            var id1 = entity.DatasetId;

            entity.GenerateNewDatasetId();
            var id2 = entity.DatasetId;

            entity.GenerateNewDatasetId();
            var id3 = entity.DatasetId;

            // Assert
            id1.Should().NotBe(id2);
            id2.Should().NotBe(id3);
            id1.Should().NotBe(id3);
        }

        #endregion

        #region ITableEntity Implementation Tests

        [Fact]
        public void ITableEntity_TimestampProperty_CanBeSetAndRetrieved()
        {
            // Arrange
            var entity = new DataSetTableEntity();
            var timestamp = DateTimeOffset.UtcNow;

            // Act
            entity.Timestamp = timestamp;

            // Assert
            entity.Timestamp.Should().Be(timestamp);
        }

        [Fact]
        public void ITableEntity_ETagProperty_CanBeSetAndRetrieved()
        {
            // Arrange
            var entity = new DataSetTableEntity();
            var etag = new ETag("test-etag-value");

            // Act
            entity.ETag = etag;

            // Assert
            entity.ETag.Should().Be(etag);
        }

        [Fact]
        public void ITableEntity_PartitionKeyAndRowKey_CanBeDirectlySet()
        {
            // Arrange
            var entity = new DataSetTableEntity();

            // Act
            entity.PartitionKey = "direct-partition-key";
            entity.RowKey = "direct-row-key";

            // Assert
            entity.PartitionKey.Should().Be("direct-partition-key");
            entity.RowKey.Should().Be("direct-row-key");
        }

        #endregion

        #region IAuditableEntity Implementation Tests

        [Fact]
        public void IAuditableEntity_LastUpdatedOn_CanBeSet()
        {
            // Arrange
            var entity = new DataSetTableEntity();
            var updateTime = DateTime.UtcNow.AddHours(-1);

            // Act
            entity.LastUpdatedOn = updateTime;

            // Assert
            entity.LastUpdatedOn.Should().Be(updateTime);
        }

        [Fact]
        public void IAuditableEntity_LastUpdatedBy_CanBeSet()
        {
            // Arrange
            var entity = new DataSetTableEntity();
            var updater = "test-updater";

            // Act
            entity.LastUpdatedBy = updater;

            // Assert
            entity.LastUpdatedBy.Should().Be(updater);
        }

        #endregion

        #region Complete Entity Creation Tests

        [Fact]
        public void CompleteEntity_WithAllProperties_IsValid()
        {
            // Arrange & Act
            var entity = new DataSetTableEntity
            {
                AgentId = TestConstants.Agents.DefaultAgentId,
                BlobFilePath = "datasets/agent1/test.json",
                ContainerName = "agent-container",
                DatasetType = "Golden",
                DatasetName = "Production Dataset",
                CreatedBy = "creator@example.com",
                CreatedOn = DateTime.UtcNow.AddDays(-30),
                LastUpdatedBy = "updater@example.com",
                LastUpdatedOn = DateTime.UtcNow
            };

            // Assert
            entity.AgentId.Should().Be(TestConstants.Agents.DefaultAgentId);
            entity.PartitionKey.Should().Be(TestConstants.Agents.DefaultAgentId);
            entity.DatasetId.Should().NotBeNullOrEmpty();
            entity.RowKey.Should().Be(entity.DatasetId);
            entity.BlobFilePath.Should().Be("datasets/agent1/test.json");
            entity.ContainerName.Should().Be("agent-container");
            entity.DatasetType.Should().Be("Golden");
            entity.DatasetName.Should().Be("Production Dataset");
            entity.CreatedBy.Should().Be("creator@example.com");
            entity.CreatedOn.Should().NotBeNull();
            entity.LastUpdatedBy.Should().Be("updater@example.com");
            entity.LastUpdatedOn.Should().NotBeNull();
        }

        [Fact]
        public void TwoEntities_WithDifferentAgents_HaveDifferentPartitionKeys()
        {
            // Arrange
            var entity1 = new DataSetTableEntity { AgentId = "agent1" };
            var entity2 = new DataSetTableEntity { AgentId = "agent2" };

            // Assert
            entity1.PartitionKey.Should().NotBe(entity2.PartitionKey);
        }

        [Fact]
        public void TwoEntities_CreatedSeparately_HaveDifferentDatasetIds()
        {
            // Arrange & Act
            var entity1 = new DataSetTableEntity();
            var entity2 = new DataSetTableEntity();

            // Assert
            entity1.DatasetId.Should().NotBe(entity2.DatasetId);
            entity1.RowKey.Should().NotBe(entity2.RowKey);
        }

        #endregion

        #region Edge Cases and Validation

        [Fact]
        public void AgentId_SetToNull_HandlesGracefully()
        {
            // Arrange
            var entity = new DataSetTableEntity
            {
                AgentId = TestConstants.Agents.DefaultAgentId
            };

            // Act
            entity.AgentId = null!;

            // Assert
            entity.AgentId.Should().BeNull();
            entity.PartitionKey.Should().BeNull();
        }

        [Fact]
        public void DatasetId_SetToNull_HandlesGracefully()
        {
            // Arrange
            var entity = new DataSetTableEntity();

            // Act
            entity.DatasetId = null!;

            // Assert
            entity.DatasetId.Should().BeNull();
            entity.RowKey.Should().BeNull();
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("agent-with-spaces")]
        [InlineData("agent_with_underscore")]
        [InlineData("agent-with-hyphens")]
        public void AgentId_WithVariousFormats_SetsCorrectly(string agentId)
        {
            // Arrange
            var entity = new DataSetTableEntity();

            // Act
            entity.AgentId = agentId;

            // Assert
            entity.AgentId.Should().Be(agentId);
            entity.PartitionKey.Should().Be(agentId);
        }

        #endregion

        #region Key Consistency Tests

        [Fact]
        public void KeyConsistency_AgentIdAndPartitionKey_AlwaysMatch()
        {
            // Arrange
            var entity = new DataSetTableEntity();

            // Act - Multiple updates
            entity.AgentId = "agent1";
            entity.AgentId.Should().Be(entity.PartitionKey);

            entity.AgentId = "agent2";
            entity.AgentId.Should().Be(entity.PartitionKey);

            entity.AgentId = "agent3";

            // Assert
            entity.AgentId.Should().Be(entity.PartitionKey);
        }

        [Fact]
        public void KeyConsistency_DatasetIdAndRowKey_AlwaysMatch()
        {
            // Arrange
            var entity = new DataSetTableEntity();

            // Act - Multiple updates
            entity.DatasetId = "id1";
            entity.DatasetId.Should().Be(entity.RowKey);

            entity.DatasetId = "id2";
            entity.DatasetId.Should().Be(entity.RowKey);

            entity.DatasetId = "id3";

            // Assert
            entity.DatasetId.Should().Be(entity.RowKey);
        }

        #endregion
    }
}
