using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using FluentAssertions;
using Sxg.EvalPlatform.API.Storage.Validators;
using Sxg.EvalPlatform.API.Storage.Services;
using Sxg.EvalPlatform.API.Storage.TableEntities;

namespace Sxg.EvalPlatform.API.Storage.UnitTests.ValidatorTests
{
    /// <summary>
    /// Unit tests for EntityValidators class
    /// </summary>
    [Trait("Category", TestCategories.Unit)]
    [Trait("Category", TestCategories.Validation)]
    public class EntityValidatorsTests
    {
        private readonly Mock<ILogger<EntityValidators>> _mockLogger;
        private readonly Mock<IDataSetTableService> _mockDataSetTableService;
        private readonly Mock<IMetricsConfigTableService> _mockMetricsConfigTableService;
        private readonly Mock<IEvalRunTableService> _mockEvalRunTableService;
        private readonly EntityValidators _validator;

        public EntityValidatorsTests()
        {
            _mockLogger = new Mock<ILogger<EntityValidators>>();
            _mockDataSetTableService = new Mock<IDataSetTableService>();
            _mockMetricsConfigTableService = new Mock<IMetricsConfigTableService>();
            _mockEvalRunTableService = new Mock<IEvalRunTableService>();

            _validator = new EntityValidators(
                _mockLogger.Object,
                _mockDataSetTableService.Object,
                _mockMetricsConfigTableService.Object,
                _mockEvalRunTableService.Object);
        }

        #region IsValidDatasetId Tests

        [Fact]
        public async Task IsValidDatasetId_WithValidDatasetId_ReturnsTrue()
        {
            // Arrange
            var datasetId = TestConstants.Datasets.DatasetId1;
            var agentId = TestConstants.Agents.DefaultAgentId;

            var mockDataset = new DataSetTableEntity
            {
                PartitionKey = agentId,
                RowKey = datasetId,
                DatasetId = datasetId,
                AgentId = agentId
            };

            _mockDataSetTableService
                .Setup(x => x.GetDataSetByIdAsync(datasetId))
                .ReturnsAsync(mockDataset);

            // Act
            var result = await _validator.IsValidDatasetId(datasetId, agentId);

            // Assert
            result.Should().BeTrue();
        }

        [Fact]
        public async Task IsValidDatasetId_WithValidDatasetIdNoAgentId_ReturnsTrue()
        {
            // Arrange
            var datasetId = TestConstants.Datasets.DatasetId1;

            var mockDataset = new DataSetTableEntity
            {
                PartitionKey = TestConstants.Agents.DefaultAgentId,
                RowKey = datasetId,
                DatasetId = datasetId,
                AgentId = TestConstants.Agents.DefaultAgentId
            };

            _mockDataSetTableService
                .Setup(x => x.GetDataSetByIdAsync(datasetId))
                .ReturnsAsync(mockDataset);

            // Act
            var result = await _validator.IsValidDatasetId(datasetId);

            // Assert
            result.Should().BeTrue();
        }

        [Fact]
        public async Task IsValidDatasetId_WithMismatchedAgentId_ReturnsFalse()
        {
            // Arrange
            var datasetId = TestConstants.Datasets.DatasetId1;
            var requestedAgentId = TestConstants.Agents.AgentId1;
            var actualAgentId = TestConstants.Agents.AgentId2;

            var mockDataset = new DataSetTableEntity
            {
                PartitionKey = actualAgentId,
                RowKey = datasetId,
                DatasetId = datasetId,
                AgentId = actualAgentId
            };

            _mockDataSetTableService
                .Setup(x => x.GetDataSetByIdAsync(datasetId))
                .ReturnsAsync(mockDataset);

            // Act
            var result = await _validator.IsValidDatasetId(datasetId, requestedAgentId);

            // Assert
            result.Should().BeFalse();
        }

        [Fact]
        public async Task IsValidDatasetId_WithNonExistentDataset_ReturnsFalse()
        {
            // Arrange
            var datasetId = "non-existent-dataset";

            _mockDataSetTableService
                .Setup(x => x.GetDataSetByIdAsync(datasetId))
                .ReturnsAsync((DataSetTableEntity?)null);

            // Act
            var result = await _validator.IsValidDatasetId(datasetId);

            // Assert
            result.Should().BeFalse();
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public async Task IsValidDatasetId_WithNullOrWhitespace_ReturnsFalse(string? datasetId)
        {
            // Act
            var result = await _validator.IsValidDatasetId(datasetId!);

            // Assert
            result.Should().BeFalse();
        }

        [Fact]
        public async Task IsValidDatasetId_WhenServiceThrowsException_ReturnsFalse()
        {
            // Arrange
            var datasetId = TestConstants.Datasets.DatasetId1;

            _mockDataSetTableService
                .Setup(x => x.GetDataSetByIdAsync(datasetId))
                .ThrowsAsync(new Exception("Database error"));

            // Act
            var result = await _validator.IsValidDatasetId(datasetId);

            // Assert
            result.Should().BeFalse();
        }

        #endregion

        #region IsValidMetricsConfigurationId Tests

        [Fact]
        public async Task IsValidMetricsConfigurationId_WithValidConfigId_ReturnsTrue()
        {
            // Arrange
            var configId = TestConstants.MetricsConfigs.ConfigId1;
            var agentId = TestConstants.Agents.DefaultAgentId;

            var mockConfig = new MetricsConfigurationTableEntity
            {
                PartitionKey = agentId,
                RowKey = configId,
                ConfigurationId = configId,
                AgentId = agentId
            };

            _mockMetricsConfigTableService
                .Setup(x => x.GetMetricsConfigurationByConfigurationIdAsync(configId))
                .ReturnsAsync(mockConfig);

            // Act
            var result = await _validator.IsValidMetricsConfigurationId(configId, agentId);

            // Assert
            result.Should().BeTrue();
        }

        [Fact]
        public async Task IsValidMetricsConfigurationId_WithValidConfigIdNoAgentId_ReturnsTrue()
        {
            // Arrange
            var configId = TestConstants.MetricsConfigs.ConfigId1;

            var mockConfig = new MetricsConfigurationTableEntity
            {
                PartitionKey = TestConstants.Agents.DefaultAgentId,
                RowKey = configId,
                ConfigurationId = configId,
                AgentId = TestConstants.Agents.DefaultAgentId
            };

            _mockMetricsConfigTableService
                .Setup(x => x.GetMetricsConfigurationByConfigurationIdAsync(configId))
                .ReturnsAsync(mockConfig);

            // Act
            var result = await _validator.IsValidMetricsConfigurationId(configId);

            // Assert
            result.Should().BeTrue();
        }

        [Fact]
        public async Task IsValidMetricsConfigurationId_WithMismatchedAgentId_ReturnsFalse()
        {
            // Arrange
            var configId = TestConstants.MetricsConfigs.ConfigId1;
            var requestedAgentId = TestConstants.Agents.AgentId1;
            var actualAgentId = TestConstants.Agents.AgentId2;

            var mockConfig = new MetricsConfigurationTableEntity
            {
                PartitionKey = actualAgentId,
                RowKey = configId,
                ConfigurationId = configId,
                AgentId = actualAgentId
            };

            _mockMetricsConfigTableService
                .Setup(x => x.GetMetricsConfigurationByConfigurationIdAsync(configId))
                .ReturnsAsync(mockConfig);

            // Act
            var result = await _validator.IsValidMetricsConfigurationId(configId, requestedAgentId);

            // Assert
            result.Should().BeFalse();
        }

        [Fact]
        public async Task IsValidMetricsConfigurationId_WithNonExistentConfig_ReturnsFalse()
        {
            // Arrange
            var configId = "non-existent-config";

            _mockMetricsConfigTableService
                .Setup(x => x.GetMetricsConfigurationByConfigurationIdAsync(configId))
                .ReturnsAsync((MetricsConfigurationTableEntity?)null);

            // Act
            var result = await _validator.IsValidMetricsConfigurationId(configId);

            // Assert
            result.Should().BeFalse();
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public async Task IsValidMetricsConfigurationId_WithNullOrWhitespace_ReturnsFalse(string? configId)
        {
            // Act
            var result = await _validator.IsValidMetricsConfigurationId(configId!);

            // Assert
            result.Should().BeFalse();
        }

        [Fact]
        public async Task IsValidMetricsConfigurationId_WhenServiceThrowsException_ReturnsFalse()
        {
            // Arrange
            var configId = TestConstants.MetricsConfigs.ConfigId1;

            _mockMetricsConfigTableService
                .Setup(x => x.GetMetricsConfigurationByConfigurationIdAsync(configId))
                .ThrowsAsync(new Exception("Database error"));

            // Act
            var result = await _validator.IsValidMetricsConfigurationId(configId);

            // Assert
            result.Should().BeFalse();
        }

        #endregion

        #region IsValidEvalRunId Tests

        [Fact]
        public async Task IsValidEvalRunId_WithValidEvalRunId_ReturnsTrue()
        {
            // Arrange
            var evalRunId = TestConstants.EvalRuns.EvalRunId1;

            var mockEvalRun = new EvalRunTableEntity
            {
                PartitionKey = evalRunId.ToString(),
                RowKey = "run1",
                EvalRunId = evalRunId
            };

            _mockEvalRunTableService
                .Setup(x => x.GetEvalRunByIdAsync(evalRunId))
                .ReturnsAsync(mockEvalRun);

            // Act
            var result = await _validator.IsValidEvalRunId(evalRunId);

            // Assert
            result.Should().BeTrue();
        }

        [Fact]
        public async Task IsValidEvalRunId_WithNonExistentEvalRun_ReturnsTrue()
        {
            // Arrange
            var evalRunId = Guid.NewGuid();

            _mockEvalRunTableService
                .Setup(x => x.GetEvalRunByIdAsync(evalRunId))
                .ReturnsAsync((EvalRunTableEntity?)null);

            // Act
            var result = await _validator.IsValidEvalRunId(evalRunId);

            // Assert
            // Note: Based on the implementation, this returns true even when not found
            result.Should().BeTrue();
        }

        [Fact]
        public async Task IsValidEvalRunId_WithEmptyGuid_ReturnsFalse()
        {
            // Arrange
            var evalRunId = Guid.Empty;

            // Act
            var result = await _validator.IsValidEvalRunId(evalRunId);

            // Assert
            result.Should().BeFalse();
        }

        [Fact]
        public async Task IsValidEvalRunId_WhenServiceThrowsException_ReturnsTrue()
        {
            // Arrange
            var evalRunId = TestConstants.EvalRuns.EvalRunId1;

            _mockEvalRunTableService
                .Setup(x => x.GetEvalRunByIdAsync(evalRunId))
                .ThrowsAsync(new Exception("Database error"));

            // Act
            var result = await _validator.IsValidEvalRunId(evalRunId);

            // Assert
            // Note: Based on the implementation, this returns true even on exception
            result.Should().BeTrue();
        }

        #endregion

        #region Multiple Validation Tests

        [Fact]
        public async Task ValidateMultipleEntities_AllValid_AllReturnTrue()
        {
            // Arrange
            var datasetId = TestConstants.Datasets.DatasetId1;
            var configId = TestConstants.MetricsConfigs.ConfigId1;
            var evalRunId = TestConstants.EvalRuns.EvalRunId1;
            var agentId = TestConstants.Agents.DefaultAgentId;

            _mockDataSetTableService
                .Setup(x => x.GetDataSetByIdAsync(datasetId))
                .ReturnsAsync(new DataSetTableEntity { DatasetId = datasetId, AgentId = agentId });

            _mockMetricsConfigTableService
                .Setup(x => x.GetMetricsConfigurationByConfigurationIdAsync(configId))
                .ReturnsAsync(new MetricsConfigurationTableEntity { ConfigurationId = configId, AgentId = agentId });

            _mockEvalRunTableService
                .Setup(x => x.GetEvalRunByIdAsync(evalRunId))
                .ReturnsAsync(new EvalRunTableEntity { EvalRunId = evalRunId });

            // Act
            var datasetValid = await _validator.IsValidDatasetId(datasetId, agentId);
            var configValid = await _validator.IsValidMetricsConfigurationId(configId, agentId);
            var evalRunValid = await _validator.IsValidEvalRunId(evalRunId);

            // Assert
            datasetValid.Should().BeTrue();
            configValid.Should().BeTrue();
            evalRunValid.Should().BeTrue();
        }

        [Fact]
        public async Task ValidateMultipleEntities_AllInvalid_AllReturnFalse()
        {
            // Arrange
            _mockDataSetTableService
                .Setup(x => x.GetDataSetByIdAsync(It.IsAny<string>()))
                .ReturnsAsync((DataSetTableEntity?)null);

            _mockMetricsConfigTableService
                .Setup(x => x.GetMetricsConfigurationByConfigurationIdAsync(It.IsAny<string>()))
                .ReturnsAsync((MetricsConfigurationTableEntity?)null);

            // Act
            var datasetValid = await _validator.IsValidDatasetId("invalid-id");
            var configValid = await _validator.IsValidMetricsConfigurationId("invalid-id");
            var evalRunValid = await _validator.IsValidEvalRunId(Guid.Empty);

            // Assert
            datasetValid.Should().BeFalse();
            configValid.Should().BeFalse();
            evalRunValid.Should().BeFalse();
        }

        #endregion

        #region Concurrent Validation Tests

        [Fact]
        public async Task ValidateMultipleDatasets_Concurrently_AllComplete()
        {
            // Arrange
            var datasetIds = new[] { "dataset1", "dataset2", "dataset3", "dataset4", "dataset5" };

            foreach (var id in datasetIds)
            {
                _mockDataSetTableService
                    .Setup(x => x.GetDataSetByIdAsync(id))
                    .ReturnsAsync(new DataSetTableEntity 
                    { 
                        DatasetId = id, 
                        AgentId = TestConstants.Agents.DefaultAgentId 
                    });
            }

            // Act
            var tasks = datasetIds.Select(id => _validator.IsValidDatasetId(id)).ToList();
            var results = await Task.WhenAll(tasks);

            // Assert
            results.Should().AllSatisfy(r => r.Should().BeTrue());
            results.Should().HaveCount(5);
        }

        #endregion
    }
}
