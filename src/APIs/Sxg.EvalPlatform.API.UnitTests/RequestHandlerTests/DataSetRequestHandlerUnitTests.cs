using AutoMapper;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Sxg.EvalPlatform.API.Storage;
using Sxg.EvalPlatform.API.Storage.Services;
using Sxg.EvalPlatform.API.Storage.TableEntities;
using Sxg.EvalPlatform.API.UnitTests.Helpers;
using SxgEvalPlatformApi.Controllers;
using SxgEvalPlatformApi.Models;
using SxgEvalPlatformApi.RequestHandlers;
using SxgEvalPlatformApi.Services;
using System.Text.Json;

namespace Sxg.EvalPlatform.API.UnitTests.RequestHandlerTests
{
    /// <summary>
    /// Unit Tests for DataSetRequestHandler
    /// Tests all public methods and various scenarios including:
    /// - Save operations (create/update)
    /// - Get operations (by ID, by agent)
    /// - Delete operations
    /// - Error handling
    /// - Audit trail functionality
    /// - Cache invalidation
    /// </summary>
    public class DataSetRequestHandlerUnitTests
    {
        private readonly Mock<IDataSetTableService> _mockDataSetTableService;
        private readonly Mock<IAzureBlobStorageService> _mockBlobStorageService;
        private readonly Mock<IConfigHelper> _mockConfigHelper;
        private readonly Mock<ILogger<DataSetRequestHandler>> _mockLogger;
        private readonly Mock<IMapper> _mockMapper;
        private readonly Mock<ICallerIdentificationService> _mockCallerService;
        private readonly Mock<ICacheManager> _mockCacheManager;
        private readonly DataSetRequestHandler _handler;

        public DataSetRequestHandlerUnitTests()
        {
            _mockDataSetTableService = new Mock<IDataSetTableService>();
            _mockBlobStorageService = new Mock<IAzureBlobStorageService>();
            _mockConfigHelper = new Mock<IConfigHelper>();
            _mockLogger = new Mock<ILogger<DataSetRequestHandler>>();
            _mockMapper = new Mock<IMapper>();
            _mockCallerService = new Mock<ICallerIdentificationService>();
            _mockCacheManager = new Mock<ICacheManager>();

            // Setup default config helper behavior
            _mockConfigHelper.Setup(x => x.GetDatasetsFolderName()).Returns("datasets");

            // Setup default caller service behavior
            _mockCallerService.Setup(x => x.GetCallerInfo()).Returns(new CallerInfo
            {
                UserId = "test-user-id",
                UserEmail = "test@example.com",
                ApplicationName = "TestApp",
                IsServicePrincipal = false,
                HasDelegatedUser = false
            });
            _mockCallerService.Setup(x => x.GetCurrentUserEmail()).Returns("test@example.com");
            _mockCallerService.Setup(x => x.IsServicePrincipalCall()).Returns(false);
            _mockCallerService.Setup(x => x.HasDelegatedUserContext()).Returns(false);

            _handler = new DataSetRequestHandler(
                _mockDataSetTableService.Object,
                _mockBlobStorageService.Object,
                _mockLogger.Object,
                _mockMapper.Object,
                _mockConfigHelper.Object,
                _mockCacheManager.Object,
                _mockCallerService.Object
            );
        }

        #region SaveDatasetAsync Tests

        [Fact]
        public async Task SaveDatasetAsync_WhenNewDataset_CreatesSuccessfully()
        {
            // Arrange
            var saveDto = CreateValidSaveDatasetDto();
            var expectedEntity = CreateDataSetTableEntity();

            _mockDataSetTableService
                .Setup(x => x.GetDataSetsByDatasetNameAsync(saveDto.AgentId, saveDto.DatasetName))
                .ReturnsAsync(new List<DataSetTableEntity>());

            _mockMapper
                .Setup(x => x.Map<DataSetTableEntity>(saveDto))
                .Returns(expectedEntity);

            _mockBlobStorageService
                .Setup(x => x.WriteBlobContentAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(true);

            _mockDataSetTableService
                .Setup(x => x.SaveDataSetAsync(It.IsAny<DataSetTableEntity>()))
                .ReturnsAsync(expectedEntity);

            // Act
            var result = await _handler.SaveDatasetAsync(saveDto);

            // Assert
            result.Should().NotBeNull();
            result.Status.Should().Be("created");
            result.Message.Should().Be("Dataset created successfully");
            result.DatasetId.Should().NotBeNullOrEmpty();
            result.CreatedBy.Should().NotBeNullOrEmpty();
            result.CreatedOn.Should().NotBeNull();

            _mockBlobStorageService.Verify(
                x => x.WriteBlobContentAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()),
                Times.Once);
            _mockDataSetTableService.Verify(
                x => x.SaveDataSetAsync(It.IsAny<DataSetTableEntity>()),
                Times.Once);
        }

        [Fact]
        public async Task SaveDatasetAsync_WhenDatasetExists_UpdatesSuccessfully()
        {
            // Arrange
            var saveDto = CreateValidSaveDatasetDto();
            var existingEntity = CreateDataSetTableEntity();

            _mockDataSetTableService
                .Setup(x => x.GetDataSetsByDatasetNameAsync(saveDto.AgentId, saveDto.DatasetName))
                .ReturnsAsync(new List<DataSetTableEntity> { existingEntity });

            _mockBlobStorageService
                .Setup(x => x.WriteBlobContentAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(true);

            _mockDataSetTableService
                .Setup(x => x.SaveDataSetAsync(It.IsAny<DataSetTableEntity>()))
                .ReturnsAsync(existingEntity);

            // Act
            var result = await _handler.SaveDatasetAsync(saveDto);

            // Assert
            result.Should().NotBeNull();
            result.Status.Should().Be("updated");
            result.Message.Should().Be("Dataset updated successfully");
            result.DatasetId.Should().Be(existingEntity.DatasetId);
            result.LastUpdatedBy.Should().NotBeNullOrEmpty();
            result.LastUpdatedOn.Should().NotBeNull();

            _mockBlobStorageService.Verify(
                x => x.WriteBlobContentAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()),
                Times.Once);
            _mockDataSetTableService.Verify(
                x => x.SaveDataSetAsync(It.IsAny<DataSetTableEntity>()),
                Times.Once);
        }

        [Fact]
        public async Task SaveDatasetAsync_WhenExceptionOccurs_ReturnsErrorResponse()
        {
            // Arrange
            var saveDto = CreateValidSaveDatasetDto();
            var exceptionMessage = "Database error";

            _mockDataSetTableService
                .Setup(x => x.GetDataSetsByDatasetNameAsync(It.IsAny<string>(), It.IsAny<string>()))
                .ThrowsAsync(new Exception(exceptionMessage));

            // Act
            var result = await _handler.SaveDatasetAsync(saveDto);

            // Assert
            result.Should().NotBeNull();
            result.Status.Should().Be("error");
            result.Message.Should().Contain("Failed to save dataset");
            result.Message.Should().Contain(exceptionMessage);
        }

        [Fact]
        public async Task SaveDatasetAsync_WithServicePrincipal_UsesApplicationNameForAudit()
        {
            // Arrange
            var saveDto = CreateValidSaveDatasetDto();
            var expectedEntity = CreateDataSetTableEntity();

            _mockCallerService.Setup(x => x.IsServicePrincipalCall()).Returns(true);
            _mockCallerService.Setup(x => x.HasDelegatedUserContext()).Returns(false);
            _mockCallerService.Setup(x => x.GetCallingApplicationName()).Returns("MyServiceApp");
            _mockCallerService.Setup(x => x.GetCallerInfo()).Returns(new CallerInfo
            {
                ApplicationName = "MyServiceApp",
                IsServicePrincipal = true,
                HasDelegatedUser = false
            });

            _mockDataSetTableService
                .Setup(x => x.GetDataSetsByDatasetNameAsync(saveDto.AgentId, saveDto.DatasetName))
                .ReturnsAsync(new List<DataSetTableEntity>());

            _mockMapper
                .Setup(x => x.Map<DataSetTableEntity>(saveDto))
                .Returns(expectedEntity);

            _mockBlobStorageService
                .Setup(x => x.WriteBlobContentAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(true);

            _mockDataSetTableService
                .Setup(x => x.SaveDataSetAsync(It.Is<DataSetTableEntity>(e => e.CreatedBy == "MyServiceApp")))
                .ReturnsAsync((DataSetTableEntity e) => { e.CreatedBy = "MyServiceApp"; return e; });

            // Act
            var result = await _handler.SaveDatasetAsync(saveDto);

            // Assert
            result.Should().NotBeNull();
            result.Status.Should().Be("created");
        }

        [Fact]
        public async Task SaveDatasetAsync_WithDifferentDatasetType_CreatesSeparateDataset()
        {
            // Arrange
            var saveDto = CreateValidSaveDatasetDto();
            saveDto.DatasetType = "Synthetic";
            var existingGoldenEntity = CreateDataSetTableEntity();
            existingGoldenEntity.DatasetType = "Golden";

            _mockDataSetTableService
                .Setup(x => x.GetDataSetsByDatasetNameAsync(saveDto.AgentId, saveDto.DatasetName))
                .ReturnsAsync(new List<DataSetTableEntity> { existingGoldenEntity });

            _mockMapper
                .Setup(x => x.Map<DataSetTableEntity>(saveDto))
                .Returns(CreateDataSetTableEntity());

            _mockBlobStorageService
                .Setup(x => x.WriteBlobContentAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(true);

            _mockDataSetTableService
                .Setup(x => x.SaveDataSetAsync(It.IsAny<DataSetTableEntity>()))
                .ReturnsAsync(CreateDataSetTableEntity());

            // Act
            var result = await _handler.SaveDatasetAsync(saveDto);

            // Assert
            result.Should().NotBeNull();
            result.Status.Should().Be("created");
            result.Message.Should().Be("Dataset created successfully");
        }

        #endregion

        #region GetDatasetsByAgentIdAsync Tests

        [Fact]
        public async Task GetDatasetsByAgentIdAsync_WithValidAgentId_ReturnsDatasets()
        {
            // Arrange
            var agentId = "test-agent-123";
            var entities = new List<DataSetTableEntity>
            {
                CreateDataSetTableEntity(),
                CreateDataSetTableEntity()
            };
            var expectedDtos = new List<DatasetMetadataDto>
            {
                new DatasetMetadataDto { DatasetId = "id-1", AgentId = agentId },
                new DatasetMetadataDto { DatasetId = "id-2", AgentId = agentId }
            };

            _mockDataSetTableService
                .Setup(x => x.GetAllDataSetsByAgentIdAsync(agentId))
                .ReturnsAsync(entities);

            _mockMapper
                .Setup(x => x.Map<DatasetMetadataDto>(It.IsAny<DataSetTableEntity>()))
                .Returns((DataSetTableEntity e) => new DatasetMetadataDto
                {
                    DatasetId = e.DatasetId,
                    AgentId = e.AgentId
                });

            // Act
            var result = await _handler.GetDatasetsByAgentIdAsync(agentId);

            // Assert
            result.Should().NotBeNull();
            result.Should().HaveCount(2);
            result.All(d => d.AgentId == agentId).Should().BeTrue();
        }

        [Fact]
        public async Task GetDatasetsByAgentIdAsync_WithNoDatasets_ReturnsEmptyList()
        {
            // Arrange
            var agentId = "agent-with-no-data";

            _mockDataSetTableService
                .Setup(x => x.GetAllDataSetsByAgentIdAsync(agentId))
                .ReturnsAsync(new List<DataSetTableEntity>());

            // Act
            var result = await _handler.GetDatasetsByAgentIdAsync(agentId);

            // Assert
            result.Should().NotBeNull();
            result.Should().BeEmpty();
        }

        [Fact]
        public async Task GetDatasetsByAgentIdAsync_WhenExceptionOccurs_ThrowsException()
        {
            // Arrange
            var agentId = "test-agent-123";
            var exceptionMessage = "Database connection failed";

            _mockDataSetTableService
                .Setup(x => x.GetAllDataSetsByAgentIdAsync(agentId))
                .ThrowsAsync(new Exception(exceptionMessage));

            // Act & Assert
            await Assert.ThrowsAsync<Exception>(() => _handler.GetDatasetsByAgentIdAsync(agentId));
        }

        #endregion

        #region GetDatasetByIdAsync Tests

        [Fact]
        public async Task GetDatasetByIdAsync_WithValidId_ReturnsDeserializedDataset()
        {
            // Arrange
            var datasetId = Guid.NewGuid().ToString();
            var entity = CreateDataSetTableEntity();
            entity.DatasetId = datasetId;

            var datasetRecords = new List<EvalDataset>
            {
                new EvalDataset { Query = "Q1", GroundTruth = "GT1" },
                new EvalDataset { Query = "Q2", GroundTruth = "GT2" }
            };
            var blobContent = JsonSerializer.Serialize(datasetRecords, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            _mockDataSetTableService
                .Setup(x => x.GetDataSetByIdAsync(datasetId))
                .ReturnsAsync(entity);

            _mockBlobStorageService
                .Setup(x => x.ReadBlobContentAsync(entity.ContainerName, entity.BlobFilePath))
                .ReturnsAsync(blobContent);

            // Act
            var result = await _handler.GetDatasetByIdAsync(datasetId);

            // Assert
            result.Should().NotBeNull();
            result.Should().HaveCount(2);
            result![0].Query.Should().Be("Q1");
            result[1].Query.Should().Be("Q2");
        }

        [Fact]
        public async Task GetDatasetByIdAsync_WithNonExistentId_ReturnsNull()
        {
            // Arrange
            var datasetId = Guid.NewGuid().ToString();

            _mockDataSetTableService
                .Setup(x => x.GetDataSetByIdAsync(datasetId))
                .ReturnsAsync((DataSetTableEntity?)null);

            // Act
            var result = await _handler.GetDatasetByIdAsync(datasetId);

            // Assert
            result.Should().BeNull();
        }

        [Fact]
        public async Task GetDatasetByIdAsync_WithEmptyBlobContent_ThrowsException()
        {
            // Arrange
            var datasetId = Guid.NewGuid().ToString();
            var entity = CreateDataSetTableEntity();
            entity.DatasetId = datasetId;

            _mockDataSetTableService
                .Setup(x => x.GetDataSetByIdAsync(datasetId))
                .ReturnsAsync(entity);

            _mockBlobStorageService
                .Setup(x => x.ReadBlobContentAsync(entity.ContainerName, entity.BlobFilePath))
                .ReturnsAsync(string.Empty);

            // Act & Assert
            var exception = await Assert.ThrowsAsync<Exception>(() => _handler.GetDatasetByIdAsync(datasetId));
            exception.Message.Should().Contain("Dataset blob not found");
        }

        [Fact]
        public async Task GetDatasetByIdAsync_WithInvalidJson_ThrowsInvalidOperationException()
        {
            // Arrange
            var datasetId = Guid.NewGuid().ToString();
            var entity = CreateDataSetTableEntity();
            entity.DatasetId = datasetId;
            var invalidJson = "{ invalid json }";

            _mockDataSetTableService
                .Setup(x => x.GetDataSetByIdAsync(datasetId))
                .ReturnsAsync(entity);

            _mockBlobStorageService
                .Setup(x => x.ReadBlobContentAsync(entity.ContainerName, entity.BlobFilePath))
                .ReturnsAsync(invalidJson);

            // Act & Assert
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(
                () => _handler.GetDatasetByIdAsync(datasetId));
            exception.Message.Should().Contain("invalid JSON format");
            exception.InnerException.Should().BeOfType<JsonException>();
        }

        [Fact]
        public async Task GetDatasetByIdAsync_WithConversationOrdering_ReturnsOrderedDataset()
        {
            // Arrange
            var datasetId = Guid.NewGuid().ToString();
            var entity = CreateDataSetTableEntity();
            entity.DatasetId = datasetId;

            var unorderedRecords = new List<EvalDataset>
            {
                new EvalDataset { Query = "Q3", ConversationId = "conv1", TurnIndex = 2 },
                new EvalDataset { Query = "Q1", ConversationId = "conv1", TurnIndex = 0 },
                new EvalDataset { Query = "Q2", ConversationId = "conv1", TurnIndex = 1 }
            };
            var blobContent = JsonSerializer.Serialize(unorderedRecords, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            _mockDataSetTableService
                .Setup(x => x.GetDataSetByIdAsync(datasetId))
                .ReturnsAsync(entity);

            _mockBlobStorageService
                .Setup(x => x.ReadBlobContentAsync(entity.ContainerName, entity.BlobFilePath))
                .ReturnsAsync(blobContent);

            // Act
            var result = await _handler.GetDatasetByIdAsync(datasetId);

            // Assert
            result.Should().NotBeNull();
            result.Should().HaveCount(3);
            result![0].TurnIndex.Should().Be(0);
            result[1].TurnIndex.Should().Be(1);
            result[2].TurnIndex.Should().Be(2);
        }

        #endregion

        #region GetDatasetByIdAsJsonAsync Tests

        [Fact]
        public async Task GetDatasetByIdAsJsonAsync_WithValidId_ReturnsJsonString()
        {
            // Arrange
            var datasetId = Guid.NewGuid().ToString();
            var entity = CreateDataSetTableEntity();
            entity.DatasetId = datasetId;
            var expectedJson = "[{\"query\":\"test\"}]";

            _mockDataSetTableService
                .Setup(x => x.GetDataSetByIdAsync(datasetId))
                .ReturnsAsync(entity);

            _mockBlobStorageService
                .Setup(x => x.ReadBlobContentAsync(entity.ContainerName, entity.BlobFilePath))
                .ReturnsAsync(expectedJson);

            // Act
            var result = await _handler.GetDatasetByIdAsJsonAsync(datasetId);

            // Assert
            result.Should().NotBeNull();
            result.Should().Be(expectedJson);
        }

        [Fact]
        public async Task GetDatasetByIdAsJsonAsync_WithNonExistentId_ReturnsNull()
        {
            // Arrange
            var datasetId = Guid.NewGuid().ToString();

            _mockDataSetTableService
                .Setup(x => x.GetDataSetByIdAsync(datasetId))
                .ReturnsAsync((DataSetTableEntity?)null);

            // Act
            var result = await _handler.GetDatasetByIdAsJsonAsync(datasetId);

            // Assert
            result.Should().BeNull();
        }

        #endregion

        #region GetDatasetMetadataByIdAsync Tests

        [Fact]
        public async Task GetDatasetMetadataByIdAsync_WithValidId_ReturnsMetadata()
        {
            // Arrange
            var datasetId = Guid.NewGuid().ToString();
            var entity = CreateDataSetTableEntity();
            entity.DatasetId = datasetId;
            var expectedDto = new DatasetMetadataDto { DatasetId = datasetId };

            _mockDataSetTableService
                .Setup(x => x.GetDataSetByIdAsync(datasetId))
                .ReturnsAsync(entity);

            _mockMapper
                .Setup(x => x.Map<DatasetMetadataDto>(entity))
                .Returns(expectedDto);

            // Act
            var result = await _handler.GetDatasetMetadataByIdAsync(datasetId);

            // Assert
            result.Should().NotBeNull();
            result!.DatasetId.Should().Be(datasetId);
        }

        [Fact]
        public async Task GetDatasetMetadataByIdAsync_WithNonExistentId_ReturnsNull()
        {
            // Arrange
            var datasetId = Guid.NewGuid().ToString();

            _mockDataSetTableService
                .Setup(x => x.GetDataSetByIdAsync(datasetId))
                .ReturnsAsync((DataSetTableEntity?)null);

            // Act
            var result = await _handler.GetDatasetMetadataByIdAsync(datasetId);

            // Assert
            result.Should().BeNull();
        }

        #endregion

        #region UpdateDatasetAsync Tests

        [Fact]
        public async Task UpdateDatasetAsync_WithValidId_UpdatesSuccessfully()
        {
            // Arrange
            var datasetId = Guid.NewGuid().ToString();
            var existingEntity = CreateDataSetTableEntity();
            existingEntity.DatasetId = datasetId;
            var updateDto = new UpdateDatasetDto
            {
                DatasetRecords = new List<EvalDataset>
                {
                    new EvalDataset { Query = "Updated", GroundTruth = "Updated" }
                }
            };

            _mockDataSetTableService
                .Setup(x => x.GetDataSetByIdAsync(datasetId))
                .ReturnsAsync(existingEntity);

            _mockBlobStorageService
                .Setup(x => x.WriteBlobContentAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(true);

            _mockDataSetTableService
                .Setup(x => x.SaveDataSetAsync(It.IsAny<DataSetTableEntity>()))
                .ReturnsAsync(existingEntity);

            // Act
            var result = await _handler.UpdateDatasetAsync(datasetId, updateDto);

            // Assert
            result.Should().NotBeNull();
            result.Status.Should().Be("updated");
            result.Message.Should().Be("Dataset updated successfully");
            result.DatasetId.Should().Be(datasetId);

            _mockBlobStorageService.Verify(
                x => x.WriteBlobContentAsync(existingEntity.ContainerName, existingEntity.BlobFilePath, It.IsAny<string>()),
                Times.Once);
        }

        [Fact]
        public async Task UpdateDatasetAsync_WithNonExistentId_ReturnsErrorResponse()
        {
            // Arrange
            var datasetId = Guid.NewGuid().ToString();
            var updateDto = new UpdateDatasetDto
            {
                DatasetRecords = new List<EvalDataset>
                {
                    new EvalDataset { Query = "Updated", GroundTruth = "Updated" }
                }
            };

            _mockDataSetTableService
                .Setup(x => x.GetDataSetByIdAsync(datasetId))
                .ReturnsAsync((DataSetTableEntity?)null);

            // Act
            var result = await _handler.UpdateDatasetAsync(datasetId, updateDto);

            // Assert
            result.Should().NotBeNull();
            result.Status.Should().Be("error");
            result.Message.Should().Contain("not found");
        }

        [Fact]
        public async Task UpdateDatasetAsync_WhenExceptionOccurs_ReturnsErrorResponse()
        {
            // Arrange
            var datasetId = Guid.NewGuid().ToString();
            var updateDto = new UpdateDatasetDto
            {
                DatasetRecords = new List<EvalDataset> { new EvalDataset() }
            };

            _mockDataSetTableService
                .Setup(x => x.GetDataSetByIdAsync(datasetId))
                .ThrowsAsync(new Exception("Database error"));

            // Act
            var result = await _handler.UpdateDatasetAsync(datasetId, updateDto);

            // Assert
            result.Should().NotBeNull();
            result.Status.Should().Be("error");
            result.Message.Should().Contain("Database error");
        }

        #endregion

        #region DeleteDatasetAsync Tests

        [Fact]
        public async Task DeleteDatasetAsync_WithValidId_DeletesSuccessfully()
        {
            // Arrange
            var datasetId = Guid.NewGuid().ToString();
            var entity = CreateDataSetTableEntity();
            entity.DatasetId = datasetId;

            _mockDataSetTableService
                .Setup(x => x.GetDataSetByIdAsync(datasetId))
                .ReturnsAsync(entity);

            _mockDataSetTableService
                .Setup(x => x.DeleteDataSetAsync(entity.AgentId, datasetId))
                .ReturnsAsync(true);

            // Act
            var result = await _handler.DeleteDatasetAsync(datasetId);

            // Assert
            result.Should().BeTrue();
            _mockDataSetTableService.Verify(
                x => x.DeleteDataSetAsync(entity.AgentId, datasetId),
                Times.Once);
        }

        [Fact]
        public async Task DeleteDatasetAsync_WithNonExistentId_ReturnsFalse()
        {
            // Arrange
            var datasetId = Guid.NewGuid().ToString();

            _mockDataSetTableService
                .Setup(x => x.GetDataSetByIdAsync(datasetId))
                .ReturnsAsync((DataSetTableEntity?)null);

            // Act
            var result = await _handler.DeleteDatasetAsync(datasetId);

            // Assert
            result.Should().BeFalse();
            _mockDataSetTableService.Verify(
                x => x.DeleteDataSetAsync(It.IsAny<string>(), It.IsAny<string>()),
                Times.Never);
        }

        [Fact]
        public async Task DeleteDatasetAsync_WhenDeletionFails_ReturnsFalse()
        {
            // Arrange
            var datasetId = Guid.NewGuid().ToString();
            var entity = CreateDataSetTableEntity();
            entity.DatasetId = datasetId;

            _mockDataSetTableService
                .Setup(x => x.GetDataSetByIdAsync(datasetId))
                .ReturnsAsync(entity);

            _mockDataSetTableService
                .Setup(x => x.DeleteDataSetAsync(entity.AgentId, datasetId))
                .ReturnsAsync(false);

            // Act
            var result = await _handler.DeleteDatasetAsync(datasetId);

            // Assert
            result.Should().BeFalse();
        }

        [Fact]
        public async Task DeleteDatasetAsync_WhenExceptionOccurs_ReturnsFalse()
        {
            // Arrange
            var datasetId = Guid.NewGuid().ToString();

            _mockDataSetTableService
                .Setup(x => x.GetDataSetByIdAsync(datasetId))
                .ThrowsAsync(new Exception("Database error"));

            // Act
            var result = await _handler.DeleteDatasetAsync(datasetId);

            // Assert
            result.Should().BeFalse();
        }

        #endregion

        #region Audit Trail Tests

        [Theory]
        [InlineData(false, false, "test@example.com")] // DirectUser
        [InlineData(true, false, "TestServiceApp")]    // AppToApp
        public async Task SaveDatasetAsync_SetsCorrectAuditUser_ForDifferentAuthFlows(
            bool isServicePrincipal, bool hasDelegatedUser, string expectedAuditUser)
        {
            // Arrange
            var saveDto = CreateValidSaveDatasetDto();
            var capturedEntity = (DataSetTableEntity?)null;

            _mockCallerService.Setup(x => x.IsServicePrincipalCall()).Returns(isServicePrincipal);
            _mockCallerService.Setup(x => x.HasDelegatedUserContext()).Returns(hasDelegatedUser);
            _mockCallerService.Setup(x => x.GetCurrentUserEmail()).Returns("test@example.com");
            _mockCallerService.Setup(x => x.GetCallingApplicationName()).Returns("TestServiceApp");
            _mockCallerService.Setup(x => x.GetCallerInfo()).Returns(new CallerInfo
            {
                UserEmail = "test@example.com",
                ApplicationName = "TestServiceApp",
                IsServicePrincipal = isServicePrincipal,
                HasDelegatedUser = hasDelegatedUser
            });

            _mockDataSetTableService
                .Setup(x => x.GetDataSetsByDatasetNameAsync(It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(new List<DataSetTableEntity>());

            _mockMapper
                .Setup(x => x.Map<DataSetTableEntity>(It.IsAny<SaveDatasetDto>()))
                .Returns(CreateDataSetTableEntity());

            _mockBlobStorageService
                .Setup(x => x.WriteBlobContentAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(true);

            _mockDataSetTableService
                .Setup(x => x.SaveDataSetAsync(It.IsAny<DataSetTableEntity>()))
                .Callback<DataSetTableEntity>(e => capturedEntity = e)
                .ReturnsAsync((DataSetTableEntity e) => e);

            // Act
            var result = await _handler.SaveDatasetAsync(saveDto);

            // Assert
            result.Should().NotBeNull();
            result.Status.Should().Be("created");
            capturedEntity.Should().NotBeNull();
            capturedEntity!.CreatedBy.Should().Be(expectedAuditUser);
        }

        #endregion

        #region Blob Storage Tests

        [Fact]
        public async Task SaveDatasetAsync_SerializesRecordsWithCamelCase()
        {
            // Arrange
            var saveDto = CreateValidSaveDatasetDto();
            string? capturedBlobContent = null;

            _mockDataSetTableService
                .Setup(x => x.GetDataSetsByDatasetNameAsync(It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(new List<DataSetTableEntity>());

            _mockMapper
                .Setup(x => x.Map<DataSetTableEntity>(It.IsAny<SaveDatasetDto>()))
                .Returns(CreateDataSetTableEntity());

            _mockBlobStorageService
                .Setup(x => x.WriteBlobContentAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .Callback<string, string, string>((_, __, content) => capturedBlobContent = content)
                .ReturnsAsync(true);

            _mockDataSetTableService
                .Setup(x => x.SaveDataSetAsync(It.IsAny<DataSetTableEntity>()))
                .ReturnsAsync(CreateDataSetTableEntity());

            // Act
            await _handler.SaveDatasetAsync(saveDto);

            // Assert
            capturedBlobContent.Should().NotBeNull();
            capturedBlobContent.Should().Contain("\"query\""); // camelCase
            capturedBlobContent.Should().Contain("\"groundTruth\""); // camelCase
        }

        [Fact]
        public async Task SaveDatasetAsync_CreatesBlobPathCorrectly()
        {
            // Arrange
            var saveDto = CreateValidSaveDatasetDto();
            string? capturedBlobPath = null;

            _mockDataSetTableService
                .Setup(x => x.GetDataSetsByDatasetNameAsync(It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(new List<DataSetTableEntity>());

            _mockMapper
                .Setup(x => x.Map<DataSetTableEntity>(It.IsAny<SaveDatasetDto>()))
                .Returns(CreateDataSetTableEntity());

            _mockBlobStorageService
                .Setup(x => x.WriteBlobContentAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .Callback<string, string, string>((_, blobPath, __) => capturedBlobPath = blobPath)
                .ReturnsAsync(true);

            _mockDataSetTableService
                .Setup(x => x.SaveDataSetAsync(It.IsAny<DataSetTableEntity>()))
                .ReturnsAsync(CreateDataSetTableEntity());

            // Act
            await _handler.SaveDatasetAsync(saveDto);

            // Assert
            capturedBlobPath.Should().NotBeNull();
            capturedBlobPath.Should().StartWith("datasets/");
            capturedBlobPath.Should().Contain(saveDto.DatasetType);
            capturedBlobPath.Should().Contain(saveDto.DatasetName);
            capturedBlobPath.Should().EndWith(".json");
        }

        #endregion

        #region Optional Properties Tests (ConversationId, TurnIndex, CopilotConversationId)

        [Fact]
        public async Task SaveDatasetAsync_WithoutOptionalProperties_SavesSuccessfully()
        {
            // Arrange
            var saveDto = new SaveDatasetDto
            {
                AgentId = "test-agent-123",
                DatasetType = "Golden",
                DatasetName = "Dataset Without Optional Props",
                DatasetRecords = new List<EvalDataset>
                {
                    new EvalDataset
                    {
                        Query = "What is AI?",
                        GroundTruth = "Artificial Intelligence",
                        ActualResponse = "AI is Artificial Intelligence",
                        Context = "Technology context"
                        // ConversationId, TurnIndex, CopilotConversationId are NOT set
                    },
                    new EvalDataset
                    {
                        Query = "Define ML",
                        GroundTruth = "Machine Learning",
                        ActualResponse = "ML is Machine Learning",
                        Context = "Data science context"
                        // ConversationId, TurnIndex, CopilotConversationId are NOT set
                    }
                }
            };

            var expectedEntity = CreateDataSetTableEntity();
            string? capturedBlobContent = null;

            _mockDataSetTableService
                .Setup(x => x.GetDataSetsByDatasetNameAsync(saveDto.AgentId, saveDto.DatasetName))
                .ReturnsAsync(new List<DataSetTableEntity>());

            _mockMapper
                .Setup(x => x.Map<DataSetTableEntity>(saveDto))
                .Returns(expectedEntity);

            _mockBlobStorageService
                .Setup(x => x.WriteBlobContentAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .Callback<string, string, string>((_, __, content) => capturedBlobContent = content)
                .ReturnsAsync(true);

            _mockDataSetTableService
                .Setup(x => x.SaveDataSetAsync(It.IsAny<DataSetTableEntity>()))
                .ReturnsAsync(expectedEntity);

            // Act
            var result = await _handler.SaveDatasetAsync(saveDto);

            // Assert
            result.Should().NotBeNull();
            result.Status.Should().Be("created");
            result.Message.Should().Be("Dataset created successfully");
            
            // Verify blob content was serialized (should contain null or not include optional properties)
            capturedBlobContent.Should().NotBeNull();
            capturedBlobContent.Should().Contain("\"query\"");
            capturedBlobContent.Should().Contain("\"groundTruth\"");
        }

        [Fact]
        public async Task SaveDatasetAsync_WithAllOptionalProperties_SavesSuccessfully()
        {
            // Arrange
            var saveDto = new SaveDatasetDto
            {
                AgentId = "test-agent-123",
                DatasetType = "Synthetic",
                DatasetName = "Dataset With Optional Props",
                DatasetRecords = new List<EvalDataset>
                {
                    new EvalDataset
                    {
                        Query = "What is AI?",
                        GroundTruth = "Artificial Intelligence",
                        ActualResponse = "AI is Artificial Intelligence",
                        Context = "Technology context",
                        ConversationId = "conv-12345",
                        TurnIndex = 0,
                        CopilotConversationId = "copilot-conv-67890"
                    },
                    new EvalDataset
                    {
                        Query = "Define ML",
                        GroundTruth = "Machine Learning",
                        ActualResponse = "ML is Machine Learning",
                        Context = "Data science context",
                        ConversationId = "conv-12345",
                        TurnIndex = 1,
                        CopilotConversationId = "copilot-conv-67890"
                    }
                }
            };

            var expectedEntity = CreateDataSetTableEntity();
            string? capturedBlobContent = null;

            _mockDataSetTableService
                .Setup(x => x.GetDataSetsByDatasetNameAsync(saveDto.AgentId, saveDto.DatasetName))
                .ReturnsAsync(new List<DataSetTableEntity>());

            _mockMapper
                .Setup(x => x.Map<DataSetTableEntity>(saveDto))
                .Returns(expectedEntity);

            _mockBlobStorageService
                .Setup(x => x.WriteBlobContentAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .Callback<string, string, string>((_, __, content) => capturedBlobContent = content)
                .ReturnsAsync(true);

            _mockDataSetTableService
                .Setup(x => x.SaveDataSetAsync(It.IsAny<DataSetTableEntity>()))
                .ReturnsAsync(expectedEntity);

            // Act
            var result = await _handler.SaveDatasetAsync(saveDto);

            // Assert
            result.Should().NotBeNull();
            result.Status.Should().Be("created");
            result.Message.Should().Be("Dataset created successfully");
            
            // Verify blob content includes optional properties
            capturedBlobContent.Should().NotBeNull();
            capturedBlobContent.Should().Contain("\"conversationId\"");
            capturedBlobContent.Should().Contain("\"turnIndex\"");
            capturedBlobContent.Should().Contain("\"copilotConversationId\"");
            capturedBlobContent.Should().Contain("conv-12345");
            capturedBlobContent.Should().Contain("copilot-conv-67890");
        }

        [Fact]
        public async Task SaveDatasetAsync_WithMixedOptionalProperties_SavesSuccessfully()
        {
            // Arrange - Some records with optional properties, some without
            var saveDto = new SaveDatasetDto
            {
                AgentId = "test-agent-123",
                DatasetType = "Golden",
                DatasetName = "Mixed Optional Props Dataset",
                DatasetRecords = new List<EvalDataset>
                {
                    new EvalDataset
                    {
                        Query = "Q1",
                        GroundTruth = "GT1",
                        ActualResponse = "AR1",
                        Context = "C1",
                        ConversationId = "conv-1",
                        TurnIndex = 0
                        // CopilotConversationId is NOT set
                    },
                    new EvalDataset
                    {
                        Query = "Q2",
                        GroundTruth = "GT2",
                        ActualResponse = "AR2",
                        Context = "C2"
                        // No optional properties set
                    },
                    new EvalDataset
                    {
                        Query = "Q3",
                        GroundTruth = "GT3",
                        ActualResponse = "AR3",
                        Context = "C3",
                        ConversationId = "conv-1",
                        TurnIndex = 2,
                        CopilotConversationId = "copilot-conv-1"
                    }
                }
            };

            var expectedEntity = CreateDataSetTableEntity();

            _mockDataSetTableService
                .Setup(x => x.GetDataSetsByDatasetNameAsync(saveDto.AgentId, saveDto.DatasetName))
                .ReturnsAsync(new List<DataSetTableEntity>());

            _mockMapper
                .Setup(x => x.Map<DataSetTableEntity>(saveDto))
                .Returns(expectedEntity);

            _mockBlobStorageService
                .Setup(x => x.WriteBlobContentAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(true);

            _mockDataSetTableService
                .Setup(x => x.SaveDataSetAsync(It.IsAny<DataSetTableEntity>()))
                .ReturnsAsync(expectedEntity);

            // Act
            var result = await _handler.SaveDatasetAsync(saveDto);

            // Assert
            result.Should().NotBeNull();
            result.Status.Should().Be("created");
            result.Message.Should().Be("Dataset created successfully");
        }

        [Fact]
        public async Task GetDatasetByIdAsync_WithOptionalPropertiesPresent_DeserializesCorrectly()
        {
            // Arrange
            var datasetId = Guid.NewGuid().ToString();
            var entity = CreateDataSetTableEntity();
            entity.DatasetId = datasetId;

            var datasetRecords = new List<EvalDataset>
            {
                new EvalDataset 
                { 
                    Query = "Q1", 
                    GroundTruth = "GT1",
                    ConversationId = "conv-123",
                    TurnIndex = 0,
                    CopilotConversationId = "copilot-456"
                },
                new EvalDataset 
                { 
                    Query = "Q2", 
                    GroundTruth = "GT2",
                    ConversationId = "conv-123",
                    TurnIndex = 1,
                    CopilotConversationId = "copilot-456"
                }
            };
            var blobContent = JsonSerializer.Serialize(datasetRecords, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            _mockDataSetTableService
                .Setup(x => x.GetDataSetByIdAsync(datasetId))
                .ReturnsAsync(entity);

            _mockBlobStorageService
                .Setup(x => x.ReadBlobContentAsync(entity.ContainerName, entity.BlobFilePath))
                .ReturnsAsync(blobContent);

            // Act
            var result = await _handler.GetDatasetByIdAsync(datasetId);

            // Assert
            result.Should().NotBeNull();
            result.Should().HaveCount(2);
            result![0].ConversationId.Should().Be("conv-123");
            result[0].TurnIndex.Should().Be(0);
            result[0].CopilotConversationId.Should().Be("copilot-456");
            result[1].ConversationId.Should().Be("conv-123");
            result[1].TurnIndex.Should().Be(1);
            result[1].CopilotConversationId.Should().Be("copilot-456");
        }

        [Fact]
        public async Task GetDatasetByIdAsync_WithOptionalPropertiesAbsent_DeserializesCorrectly()
        {
            // Arrange
            var datasetId = Guid.NewGuid().ToString();
            var entity = CreateDataSetTableEntity();
            entity.DatasetId = datasetId;

            var datasetRecords = new List<EvalDataset>
            {
                new EvalDataset 
                { 
                    Query = "Q1", 
                    GroundTruth = "GT1"
                    // ConversationId, TurnIndex, CopilotConversationId are NOT set
                },
                new EvalDataset 
                { 
                    Query = "Q2", 
                    GroundTruth = "GT2"
                    // ConversationId, TurnIndex, CopilotConversationId are NOT set
                }
            };
            var blobContent = JsonSerializer.Serialize(datasetRecords, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            _mockDataSetTableService
                .Setup(x => x.GetDataSetByIdAsync(datasetId))
                .ReturnsAsync(entity);

            _mockBlobStorageService
                .Setup(x => x.ReadBlobContentAsync(entity.ContainerName, entity.BlobFilePath))
                .ReturnsAsync(blobContent);

            // Act
            var result = await _handler.GetDatasetByIdAsync(datasetId);

            // Assert
            result.Should().NotBeNull();
            result.Should().HaveCount(2);
            result![0].Query.Should().Be("Q1");
            result[0].ConversationId.Should().BeNull();
            result[0].TurnIndex.Should().BeNull();
            result[0].CopilotConversationId.Should().BeNull();
            result[1].Query.Should().Be("Q2");
            result[1].ConversationId.Should().BeNull();
            result[1].TurnIndex.Should().BeNull();
            result[1].CopilotConversationId.Should().BeNull();
        }

        [Fact]
        public async Task UpdateDatasetAsync_WithOptionalProperties_UpdatesSuccessfully()
        {
            // Arrange
            var datasetId = Guid.NewGuid().ToString();
            var existingEntity = CreateDataSetTableEntity();
            existingEntity.DatasetId = datasetId;
            
            var updateDto = new UpdateDatasetDto
            {
                DatasetRecords = new List<EvalDataset>
                {
                    new EvalDataset 
                    { 
                        Query = "Updated Q1", 
                        GroundTruth = "Updated GT1",
                        ConversationId = "new-conv-123",
                        TurnIndex = 5,
                        CopilotConversationId = "new-copilot-456"
                    }
                }
            };

            string? capturedBlobContent = null;

            _mockDataSetTableService
                .Setup(x => x.GetDataSetByIdAsync(datasetId))
                .ReturnsAsync(existingEntity);

            _mockBlobStorageService
                .Setup(x => x.WriteBlobContentAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .Callback<string, string, string>((_, __, content) => capturedBlobContent = content)
                .ReturnsAsync(true);

            _mockDataSetTableService
                .Setup(x => x.SaveDataSetAsync(It.IsAny<DataSetTableEntity>()))
                .ReturnsAsync(existingEntity);

            // Act
            var result = await _handler.UpdateDatasetAsync(datasetId, updateDto);

            // Assert
            result.Should().NotBeNull();
            result.Status.Should().Be("updated");
            
            // Verify optional properties are serialized
            capturedBlobContent.Should().NotBeNull();
            capturedBlobContent.Should().Contain("\"conversationId\"");
            capturedBlobContent.Should().Contain("new-conv-123");
            capturedBlobContent.Should().Contain("\"turnIndex\"");
            capturedBlobContent.Should().Contain("\"copilotConversationId\"");
            capturedBlobContent.Should().Contain("new-copilot-456");
        }

        [Fact]
        public async Task UpdateDatasetAsync_WithoutOptionalProperties_UpdatesSuccessfully()
        {
            // Arrange
            var datasetId = Guid.NewGuid().ToString();
            var existingEntity = CreateDataSetTableEntity();
            existingEntity.DatasetId = datasetId;
            
            var updateDto = new UpdateDatasetDto
            {
                DatasetRecords = new List<EvalDataset>
                {
                    new EvalDataset 
                    { 
                        Query = "Updated Q1", 
                        GroundTruth = "Updated GT1"
                        // No optional properties
                    }
                }
            };

            _mockDataSetTableService
                .Setup(x => x.GetDataSetByIdAsync(datasetId))
                .ReturnsAsync(existingEntity);

            _mockBlobStorageService
                .Setup(x => x.WriteBlobContentAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(true);

            _mockDataSetTableService
                .Setup(x => x.SaveDataSetAsync(It.IsAny<DataSetTableEntity>()))
                .ReturnsAsync(existingEntity);

            // Act
            var result = await _handler.UpdateDatasetAsync(datasetId, updateDto);

            // Assert
            result.Should().NotBeNull();
            result.Status.Should().Be("updated");
            result.Message.Should().Be("Dataset updated successfully");
        }

        #endregion

        #region NormalizeDatasetOrdering Edge Cases Tests

        [Fact]
        public async Task GetDatasetByIdAsync_WithSingleRecord_ReturnsUnmodifiedOrder()
        {
            // Arrange
            var datasetId = Guid.NewGuid().ToString();
            var entity = CreateDataSetTableEntity();
            entity.DatasetId = datasetId;

            var singleRecord = new List<EvalDataset>
            {
                new EvalDataset { Query = "Q1", GroundTruth = "GT1", ConversationId = "conv1", TurnIndex = 5 }
            };
            var blobContent = JsonSerializer.Serialize(singleRecord, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            _mockDataSetTableService
                .Setup(x => x.GetDataSetByIdAsync(datasetId))
                .ReturnsAsync(entity);

            _mockBlobStorageService
                .Setup(x => x.ReadBlobContentAsync(entity.ContainerName, entity.BlobFilePath))
                .ReturnsAsync(blobContent);

            // Act
            var result = await _handler.GetDatasetByIdAsync(datasetId);

            // Assert
            result.Should().NotBeNull();
            result.Should().HaveCount(1);
            result![0].Query.Should().Be("Q1");
            result[0].TurnIndex.Should().Be(5);
        }

        [Fact]
        public async Task GetDatasetByIdAsync_WithoutConversationOrTurnIndex_ReturnsOriginalOrder()
        {
            // Arrange
            var datasetId = Guid.NewGuid().ToString();
            var entity = CreateDataSetTableEntity();
            entity.DatasetId = datasetId;

            var records = new List<EvalDataset>
            {
                new EvalDataset { Query = "Q3", GroundTruth = "GT3" },
                new EvalDataset { Query = "Q1", GroundTruth = "GT1" },
                new EvalDataset { Query = "Q2", GroundTruth = "GT2" }
            };
            var blobContent = JsonSerializer.Serialize(records, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            _mockDataSetTableService
                .Setup(x => x.GetDataSetByIdAsync(datasetId))
                .ReturnsAsync(entity);

            _mockBlobStorageService
                .Setup(x => x.ReadBlobContentAsync(entity.ContainerName, entity.BlobFilePath))
                .ReturnsAsync(blobContent);

            // Act
            var result = await _handler.GetDatasetByIdAsync(datasetId);

            // Assert - Should maintain original order when no ordering metadata present
            result.Should().NotBeNull();
            result.Should().HaveCount(3);
            result![0].Query.Should().Be("Q3");
            result[1].Query.Should().Be("Q1");
            result[2].Query.Should().Be("Q2");
        }

        [Fact]
        public async Task GetDatasetByIdAsync_WithMultipleConversations_OrdersCorrectly()
        {
            // Arrange
            var datasetId = Guid.NewGuid().ToString();
            var entity = CreateDataSetTableEntity();
            entity.DatasetId = datasetId;

            var records = new List<EvalDataset>
            {
                new EvalDataset { Query = "Q5", ConversationId = "conv3", TurnIndex = 0 },
                new EvalDataset { Query = "Q2", ConversationId = "conv1", TurnIndex = 1 },
                new EvalDataset { Query = "Q4", ConversationId = "conv2", TurnIndex = 1 },
                new EvalDataset { Query = "Q1", ConversationId = "conv1", TurnIndex = 0 },
                new EvalDataset { Query = "Q3", ConversationId = "conv2", TurnIndex = 0 }
            };
            var blobContent = JsonSerializer.Serialize(records, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            _mockDataSetTableService
                .Setup(x => x.GetDataSetByIdAsync(datasetId))
                .ReturnsAsync(entity);

            _mockBlobStorageService
                .Setup(x => x.ReadBlobContentAsync(entity.ContainerName, entity.BlobFilePath))
                .ReturnsAsync(blobContent);

            // Act
            var result = await _handler.GetDatasetByIdAsync(datasetId);

            // Assert - Should be ordered by ConversationId, then TurnIndex
            result.Should().NotBeNull();
            result.Should().HaveCount(5);
            
            // conv1
            result![0].ConversationId.Should().Be("conv1");
            result[0].TurnIndex.Should().Be(0);
            result[1].ConversationId.Should().Be("conv1");
            result[1].TurnIndex.Should().Be(1);
            
            // conv2
            result[2].ConversationId.Should().Be("conv2");
            result[2].TurnIndex.Should().Be(0);
            result[3].ConversationId.Should().Be("conv2");
            result[3].TurnIndex.Should().Be(1);
            
            // conv3
            result[4].ConversationId.Should().Be("conv3");
            result[4].TurnIndex.Should().Be(0);
        }

        [Fact]
        public async Task GetDatasetByIdAsync_WithNullConversationId_HandlesGracefully()
        {
            // Arrange
            var datasetId = Guid.NewGuid().ToString();
            var entity = CreateDataSetTableEntity();
            entity.DatasetId = datasetId;

            var records = new List<EvalDataset>
            {
                new EvalDataset { Query = "Q3", ConversationId = "conv1", TurnIndex = 0 },
                new EvalDataset { Query = "Q1", ConversationId = null, TurnIndex = 5 },
                new EvalDataset { Query = "Q2", ConversationId = null, TurnIndex = 2 }
            };
            var blobContent = JsonSerializer.Serialize(records, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            _mockDataSetTableService
                .Setup(x => x.GetDataSetByIdAsync(datasetId))
                .ReturnsAsync(entity);

            _mockBlobStorageService
                .Setup(x => x.ReadBlobContentAsync(entity.ContainerName, entity.BlobFilePath))
                .ReturnsAsync(blobContent);

            // Act
            var result = await _handler.GetDatasetByIdAsync(datasetId);

            // Assert - Nulls should be sorted first (empty string), then by TurnIndex
            result.Should().NotBeNull();
            result.Should().HaveCount(3);
            result![0].ConversationId.Should().BeNull();
            result[0].TurnIndex.Should().Be(2);
            result[1].ConversationId.Should().BeNull();
            result[1].TurnIndex.Should().Be(5);
            result[2].ConversationId.Should().Be("conv1");
            result[2].TurnIndex.Should().Be(0);
        }

        #endregion

        #region GetAuditUser Edge Cases Tests

        [Fact]
        public async Task SaveDatasetAsync_WhenGetCallerInfoThrows_UsesSystemAsAuditUser()
        {
            // Arrange
            var saveDto = CreateValidSaveDatasetDto();
            var expectedEntity = CreateDataSetTableEntity();
            DataSetTableEntity? capturedEntity = null;

            // Setup caller service to throw exception
            _mockCallerService.Setup(x => x.GetCallerInfo())
                .Throws(new Exception("Caller service error"));
            _mockCallerService.Setup(x => x.IsServicePrincipalCall())
                .Throws(new Exception("Caller service error"));

            _mockDataSetTableService
                .Setup(x => x.GetDataSetsByDatasetNameAsync(It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(new List<DataSetTableEntity>());

            _mockMapper
                .Setup(x => x.Map<DataSetTableEntity>(It.IsAny<SaveDatasetDto>()))
                .Returns(expectedEntity);

            _mockBlobStorageService
                .Setup(x => x.WriteBlobContentAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(true);

            _mockDataSetTableService
                .Setup(x => x.SaveDataSetAsync(It.IsAny<DataSetTableEntity>()))
                .Callback<DataSetTableEntity>(e => capturedEntity = e)
                .ReturnsAsync((DataSetTableEntity e) => e);

            // Act
            var result = await _handler.SaveDatasetAsync(saveDto);

            // Assert
            result.Should().NotBeNull();
            result.Status.Should().Be("created");
            capturedEntity.Should().NotBeNull();
            capturedEntity!.CreatedBy.Should().Be("System", 
                "When GetCallerInfo throws exception, audit user should default to 'System'");
        }

        [Fact]
        public async Task SaveDatasetAsync_WhenEmailIsUnknown_FallsBackToUserId()
        {
            // Arrange
            var saveDto = CreateValidSaveDatasetDto();
            var expectedEntity = CreateDataSetTableEntity();
            DataSetTableEntity? capturedEntity = null;

            _mockCallerService.Setup(x => x.IsServicePrincipalCall()).Returns(false);
            _mockCallerService.Setup(x => x.HasDelegatedUserContext()).Returns(false);
            _mockCallerService.Setup(x => x.GetCurrentUserEmail()).Returns("unknown");
            _mockCallerService.Setup(x => x.GetCurrentUserId()).Returns("user-id-12345");
            _mockCallerService.Setup(x => x.GetCallerInfo()).Returns(new CallerInfo
            {
                UserId = "user-id-12345",
                UserEmail = "unknown",
                IsServicePrincipal = false,
                HasDelegatedUser = false
            });

            _mockDataSetTableService
                .Setup(x => x.GetDataSetsByDatasetNameAsync(It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(new List<DataSetTableEntity>());

            _mockMapper
                .Setup(x => x.Map<DataSetTableEntity>(It.IsAny<SaveDatasetDto>()))
                .Returns(expectedEntity);

            _mockBlobStorageService
                .Setup(x => x.WriteBlobContentAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(true);

            _mockDataSetTableService
                .Setup(x => x.SaveDataSetAsync(It.IsAny<DataSetTableEntity>()))
                .Callback<DataSetTableEntity>(e => capturedEntity = e)
                .ReturnsAsync((DataSetTableEntity e) => e);

            // Act
            var result = await _handler.SaveDatasetAsync(saveDto);

            // Assert
            result.Should().NotBeNull();
            result.Status.Should().Be("created");
            capturedEntity.Should().NotBeNull();
            capturedEntity!.CreatedBy.Should().Be("user-id-12345",
                "When email is 'unknown', audit user should fall back to user ID");
        }

        [Fact]
        public async Task SaveDatasetAsync_WhenBothEmailAndUserIdAreUnknown_UsesSystem()
        {
            // Arrange
            var saveDto = CreateValidSaveDatasetDto();
            var expectedEntity = CreateDataSetTableEntity();
            DataSetTableEntity? capturedEntity = null;

            _mockCallerService.Setup(x => x.IsServicePrincipalCall()).Returns(false);
            _mockCallerService.Setup(x => x.HasDelegatedUserContext()).Returns(false);
            _mockCallerService.Setup(x => x.GetCurrentUserEmail()).Returns("unknown");
            _mockCallerService.Setup(x => x.GetCurrentUserId()).Returns("unknown");
            _mockCallerService.Setup(x => x.GetCallerInfo()).Returns(new CallerInfo
            {
                UserId = "unknown",
                UserEmail = "unknown",
                IsServicePrincipal = false,
                HasDelegatedUser = false
            });

            _mockDataSetTableService
                .Setup(x => x.GetDataSetsByDatasetNameAsync(It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(new List<DataSetTableEntity>());

            _mockMapper
                .Setup(x => x.Map<DataSetTableEntity>(It.IsAny<SaveDatasetDto>()))
                .Returns(expectedEntity);

            _mockBlobStorageService
                .Setup(x => x.WriteBlobContentAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(true);

            _mockDataSetTableService
                .Setup(x => x.SaveDataSetAsync(It.IsAny<DataSetTableEntity>()))
                .Callback<DataSetTableEntity>(e => capturedEntity = e)
                .ReturnsAsync((DataSetTableEntity e) => e);

            // Act
            var result = await _handler.SaveDatasetAsync(saveDto);

            // Assert
            result.Should().NotBeNull();
            result.Status.Should().Be("created");
            capturedEntity.Should().NotBeNull();
            capturedEntity!.CreatedBy.Should().Be("System",
                "When both email and user ID are 'unknown', audit user should default to 'System'");
        }

        [Fact]
        public async Task SaveDatasetAsync_WhenEmailIsEmptyString_FallsBackToUserId()
        {
            // Arrange
            var saveDto = CreateValidSaveDatasetDto();
            var expectedEntity = CreateDataSetTableEntity();
            DataSetTableEntity? capturedEntity = null;

            _mockCallerService.Setup(x => x.IsServicePrincipalCall()).Returns(false);
            _mockCallerService.Setup(x => x.HasDelegatedUserContext()).Returns(false);
            _mockCallerService.Setup(x => x.GetCurrentUserEmail()).Returns(string.Empty);
            _mockCallerService.Setup(x => x.GetCurrentUserId()).Returns("user-guid-67890");
            _mockCallerService.Setup(x => x.GetCallerInfo()).Returns(new CallerInfo
            {
                UserId = "user-guid-67890",
                UserEmail = string.Empty,
                IsServicePrincipal = false,
                HasDelegatedUser = false
            });

            _mockDataSetTableService
                .Setup(x => x.GetDataSetsByDatasetNameAsync(It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(new List<DataSetTableEntity>());

            _mockMapper
                .Setup(x => x.Map<DataSetTableEntity>(It.IsAny<SaveDatasetDto>()))
                .Returns(expectedEntity);

            _mockBlobStorageService
                .Setup(x => x.WriteBlobContentAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(true);

            _mockDataSetTableService
                .Setup(x => x.SaveDataSetAsync(It.IsAny<DataSetTableEntity>()))
                .Callback<DataSetTableEntity>(e => capturedEntity = e)
                .ReturnsAsync((DataSetTableEntity e) => e);

            // Act
            var result = await _handler.SaveDatasetAsync(saveDto);

            // Assert
            result.Should().NotBeNull();
            result.Status.Should().Be("created");
            capturedEntity.Should().NotBeNull();
            capturedEntity!.CreatedBy.Should().Be("user-guid-67890",
                "When email is empty, audit user should fall back to user ID");
        }

        [Fact]
        public async Task SaveDatasetAsync_WhenAppNameIsUnknown_UsesSystemAsAuditUser()
        {
            // Arrange
            var saveDto = CreateValidSaveDatasetDto();
            var expectedEntity = CreateDataSetTableEntity();
            DataSetTableEntity? capturedEntity = null;

            _mockCallerService.Setup(x => x.IsServicePrincipalCall()).Returns(true);
            _mockCallerService.Setup(x => x.HasDelegatedUserContext()).Returns(false);
            _mockCallerService.Setup(x => x.GetCallingApplicationName()).Returns("unknown");
            _mockCallerService.Setup(x => x.GetCallerInfo()).Returns(new CallerInfo
            {
                ApplicationName = "unknown",
                IsServicePrincipal = true,
                HasDelegatedUser = false
            });

            _mockDataSetTableService
                .Setup(x => x.GetDataSetsByDatasetNameAsync(It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(new List<DataSetTableEntity>());

            _mockMapper
                .Setup(x => x.Map<DataSetTableEntity>(It.IsAny<SaveDatasetDto>()))
                .Returns(expectedEntity);

            _mockBlobStorageService
                .Setup(x => x.WriteBlobContentAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(true);

            _mockDataSetTableService
                .Setup(x => x.SaveDataSetAsync(It.IsAny<DataSetTableEntity>()))
                .Callback<DataSetTableEntity>(e => capturedEntity = e)
                .ReturnsAsync((DataSetTableEntity e) => e);

            // Act
            var result = await _handler.SaveDatasetAsync(saveDto);

            // Assert
            result.Should().NotBeNull();
            result.Status.Should().Be("created");
            capturedEntity.Should().NotBeNull();
            capturedEntity!.CreatedBy.Should().Be("System",
                "When service principal app name is 'unknown', audit user should default to 'System'");
        }

        [Fact]
        public async Task UpdateDatasetAsync_WhenGetCallerInfoThrows_UsesSystemAsAuditUser()
        {
            // Arrange
            var datasetId = Guid.NewGuid().ToString();
            var existingEntity = CreateDataSetTableEntity();
            existingEntity.DatasetId = datasetId;
            var updateDto = new UpdateDatasetDto
            {
                DatasetRecords = new List<EvalDataset>
                {
                    new EvalDataset { Query = "Updated", GroundTruth = "Updated" }
                }
            };

            DataSetTableEntity? capturedEntity = null;

            _mockCallerService.Setup(x => x.GetCallerInfo())
                .Throws(new InvalidOperationException("Service unavailable"));
            _mockCallerService.Setup(x => x.IsServicePrincipalCall())
                .Throws(new InvalidOperationException("Service unavailable"));

            _mockDataSetTableService
                .Setup(x => x.GetDataSetByIdAsync(datasetId))
                .ReturnsAsync(existingEntity);

            _mockBlobStorageService
                .Setup(x => x.WriteBlobContentAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(true);

            _mockDataSetTableService
                .Setup(x => x.SaveDataSetAsync(It.IsAny<DataSetTableEntity>()))
                .Callback<DataSetTableEntity>(e => capturedEntity = e)
                .ReturnsAsync((DataSetTableEntity e) => e);

            // Act
            var result = await _handler.UpdateDatasetAsync(datasetId, updateDto);

            // Assert
            result.Should().NotBeNull();
            result.Status.Should().Be("updated");
            capturedEntity.Should().NotBeNull();
            capturedEntity!.LastUpdatedBy.Should().Be("System",
                "When GetCallerInfo throws exception during update, audit user should default to 'System'");
        }

        #endregion

        #region GetDatasetByIdAsJsonAsync Edge Cases Tests

        [Fact]
        public async Task GetDatasetByIdAsJsonAsync_WithEmptyBlobContent_ThrowsException()
        {
            // Arrange
            var datasetId = Guid.NewGuid().ToString();
            var entity = CreateDataSetTableEntity();
            entity.DatasetId = datasetId;

            _mockDataSetTableService
                .Setup(x => x.GetDataSetByIdAsync(datasetId))
                .ReturnsAsync(entity);

            _mockBlobStorageService
                .Setup(x => x.ReadBlobContentAsync(entity.ContainerName, entity.BlobFilePath))
                .ReturnsAsync(string.Empty);

            // Act & Assert
            var exception = await Assert.ThrowsAsync<Exception>(
                () => _handler.GetDatasetByIdAsJsonAsync(datasetId));
            exception.Message.Should().Contain("Dataset blob not found");
            exception.Message.Should().Contain(entity.ContainerName);
            exception.Message.Should().Contain(entity.BlobFilePath);
        }

        [Fact]
        public async Task GetDatasetByIdAsJsonAsync_WhenBlobReadThrows_ThrowsException()
        {
            // Arrange
            var datasetId = Guid.NewGuid().ToString();
            var entity = CreateDataSetTableEntity();
            entity.DatasetId = datasetId;

            _mockDataSetTableService
                .Setup(x => x.GetDataSetByIdAsync(datasetId))
                .ReturnsAsync(entity);

            _mockBlobStorageService
                .Setup(x => x.ReadBlobContentAsync(entity.ContainerName, entity.BlobFilePath))
                .ThrowsAsync(new Exception("Blob storage error"));

            // Act & Assert
            var exception = await Assert.ThrowsAsync<Exception>(
                () => _handler.GetDatasetByIdAsJsonAsync(datasetId));
            exception.Message.Should().Contain("Blob storage error");
        }

        #endregion

        #region FindExistingDatasetAsync Coverage Tests

        [Fact]
        public async Task SaveDatasetAsync_FindsExistingDataset_CaseInsensitiveDatasetType()
        {
            // Arrange - Test that DatasetType comparison is case-insensitive
            var saveDto = CreateValidSaveDatasetDto();
            saveDto.DatasetType = "GOLDEN"; // Uppercase

            var existingEntity = CreateDataSetTableEntity();
            existingEntity.DatasetType = "golden"; // Lowercase

            _mockDataSetTableService
                .Setup(x => x.GetDataSetsByDatasetNameAsync(saveDto.AgentId, saveDto.DatasetName))
                .ReturnsAsync(new List<DataSetTableEntity> { existingEntity });

            _mockBlobStorageService
                .Setup(x => x.WriteBlobContentAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(true);

            _mockDataSetTableService
                .Setup(x => x.SaveDataSetAsync(It.IsAny<DataSetTableEntity>()))
                .ReturnsAsync(existingEntity);

            // Act
            var result = await _handler.SaveDatasetAsync(saveDto);

            // Assert
            result.Should().NotBeNull();
            result.Status.Should().Be("updated",
                "Should find existing dataset with case-insensitive DatasetType comparison");
            result.DatasetId.Should().Be(existingEntity.DatasetId);
        }

        [Fact]
        public async Task SaveDatasetAsync_DoesNotFindExistingDataset_WhenDatasetTypesDiffer()
        {
            // Arrange
            var saveDto = CreateValidSaveDatasetDto();
            saveDto.DatasetType = "Synthetic";

            var existingEntity = CreateDataSetTableEntity();
            existingEntity.DatasetType = "Golden";

            _mockDataSetTableService
                .Setup(x => x.GetDataSetsByDatasetNameAsync(saveDto.AgentId, saveDto.DatasetName))
                .ReturnsAsync(new List<DataSetTableEntity> { existingEntity });

            _mockMapper
                .Setup(x => x.Map<DataSetTableEntity>(It.IsAny<SaveDatasetDto>()))
                .Returns(CreateDataSetTableEntity());

            _mockBlobStorageService
                .Setup(x => x.WriteBlobContentAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(true);

            _mockDataSetTableService
                .Setup(x => x.SaveDataSetAsync(It.IsAny<DataSetTableEntity>()))
                .ReturnsAsync(CreateDataSetTableEntity());

            // Act
            var result = await _handler.SaveDatasetAsync(saveDto);

            // Assert
            result.Should().NotBeNull();
            result.Status.Should().Be("created",
                "Should create new dataset when DatasetType differs");
        }

        [Fact]
        public async Task SaveDatasetAsync_FindsCorrectDataset_WhenMultipleDatasetsWithSameName()
        {
            // Arrange
            var saveDto = CreateValidSaveDatasetDto();
            saveDto.DatasetType = "Golden";

            var goldenEntity = CreateDataSetTableEntity();
            goldenEntity.DatasetType = "Golden";
            goldenEntity.DatasetId = "golden-id-123";

            var syntheticEntity = CreateDataSetTableEntity();
            syntheticEntity.DatasetType = "Synthetic";
            syntheticEntity.DatasetId = "synthetic-id-456";

            _mockDataSetTableService
                .Setup(x => x.GetDataSetsByDatasetNameAsync(saveDto.AgentId, saveDto.DatasetName))
                .ReturnsAsync(new List<DataSetTableEntity> { goldenEntity, syntheticEntity });

            _mockBlobStorageService
                .Setup(x => x.WriteBlobContentAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(true);

            _mockDataSetTableService
                .Setup(x => x.SaveDataSetAsync(It.IsAny<DataSetTableEntity>()))
                .ReturnsAsync(goldenEntity);

            // Act
            var result = await _handler.SaveDatasetAsync(saveDto);

            // Assert
            result.Should().NotBeNull();
            result.Status.Should().Be("updated");
            result.DatasetId.Should().Be("golden-id-123",
                "Should find and update the correct dataset based on DatasetType");
        }

        #endregion

        #region CreateBlobPaths Coverage Tests

        [Fact]
        public async Task SaveDatasetAsync_CreatesBlobPath_WithSpecialCharactersInAgentId()
        {
            // Arrange
            var saveDto = CreateValidSaveDatasetDto();
            saveDto.AgentId = "agent with spaces";
            string? capturedContainer = null;
            string? capturedBlobPath = null;

            _mockDataSetTableService
                .Setup(x => x.GetDataSetsByDatasetNameAsync(It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(new List<DataSetTableEntity>());

            _mockMapper
                .Setup(x => x.Map<DataSetTableEntity>(It.IsAny<SaveDatasetDto>()))
                .Returns(CreateDataSetTableEntity());

            _mockBlobStorageService
                .Setup(x => x.WriteBlobContentAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .Callback<string, string, string>((container, blobPath, __) =>
                {
                    capturedContainer = container;
                    capturedBlobPath = blobPath;
                })
                .ReturnsAsync(true);

            _mockDataSetTableService
                .Setup(x => x.SaveDataSetAsync(It.IsAny<DataSetTableEntity>()))
                .ReturnsAsync(CreateDataSetTableEntity());

            // Act
            await _handler.SaveDatasetAsync(saveDto);

            // Assert
            capturedContainer.Should().NotBeNull();
            capturedContainer.Should().NotContain(" ",
                "Container name should have spaces removed by CommonUtils.TrimAndRemoveSpaces");
            capturedBlobPath.Should().NotBeNull();
            capturedBlobPath.Should().StartWith("datasets/");
        }

        [Fact]
        public async Task SaveDatasetAsync_CreatesBlobPath_WithDatasetNameAndType()
        {
            // Arrange
            var saveDto = CreateValidSaveDatasetDto();
            saveDto.DatasetType = "Synthetic";
            saveDto.DatasetName = "MyTestDataset";
            string? capturedBlobPath = null;

            _mockDataSetTableService
                .Setup(x => x.GetDataSetsByDatasetNameAsync(It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(new List<DataSetTableEntity>());

            _mockMapper
                .Setup(x => x.Map<DataSetTableEntity>(It.IsAny<SaveDatasetDto>()))
                .Returns(CreateDataSetTableEntity());

            _mockBlobStorageService
                .Setup(x => x.WriteBlobContentAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .Callback<string, string, string>((_, blobPath, __) => capturedBlobPath = blobPath)
                .ReturnsAsync(true);

            _mockDataSetTableService
                .Setup(x => x.SaveDataSetAsync(It.IsAny<DataSetTableEntity>()))
                .ReturnsAsync(CreateDataSetTableEntity());

            // Act
            await _handler.SaveDatasetAsync(saveDto);

            // Assert
            capturedBlobPath.Should().NotBeNull();
            capturedBlobPath.Should().StartWith("datasets/");
            capturedBlobPath.Should().Contain("Synthetic");
            capturedBlobPath.Should().Contain("MyTestDataset");
            capturedBlobPath.Should().EndWith(".json");
        }

        #endregion

        #region SerializeDatasetRecords Coverage Tests

        [Fact]
        public async Task SaveDatasetAsync_SerializesRecords_WithIndentation()
        {
            // Arrange
            var saveDto = CreateValidSaveDatasetDto();
            string? capturedBlobContent = null;

            _mockDataSetTableService
                .Setup(x => x.GetDataSetsByDatasetNameAsync(It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(new List<DataSetTableEntity>());

            _mockMapper
                .Setup(x => x.Map<DataSetTableEntity>(It.IsAny<SaveDatasetDto>()))
                .Returns(CreateDataSetTableEntity());

            _mockBlobStorageService
                .Setup(x => x.WriteBlobContentAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .Callback<string, string, string>((_, __, content) => capturedBlobContent = content)
                .ReturnsAsync(true);

            _mockDataSetTableService
                .Setup(x => x.SaveDataSetAsync(It.IsAny<DataSetTableEntity>()))
                .ReturnsAsync(CreateDataSetTableEntity());

            // Act
            await _handler.SaveDatasetAsync(saveDto);

            // Assert
            capturedBlobContent.Should().NotBeNull();
            capturedBlobContent.Should().Contain("\n",
                "JSON should be indented (WriteIndented = true)");
            capturedBlobContent.Should().Contain("  ",
                "JSON should contain proper indentation");
        }

        #endregion

        #region Helper Methods

        private SaveDatasetDto CreateValidSaveDatasetDto()
        {
            return new SaveDatasetDto
            {
                AgentId = "test-agent-123",
                DatasetType = "Golden",
                DatasetName = "Test Dataset",
                DatasetRecords = new List<EvalDataset>
                {
                    new EvalDataset
                    {
                        Query = "What is AI?",
                        GroundTruth = "Artificial Intelligence",
                        ActualResponse = "AI is Artificial Intelligence",
                        Context = "Technology context"
                    },
                    new EvalDataset
                    {
                        Query = "Define ML",
                        GroundTruth = "Machine Learning",
                        ActualResponse = "ML is Machine Learning",
                        Context = "Data science context"
                    }
                }
            };
        }

        private DataSetTableEntity CreateDataSetTableEntity()
        {
            var entity = new DataSetTableEntity
            {
                AgentId = "test-agent-123",
                DatasetId = Guid.NewGuid().ToString(),
                DatasetType = "Golden",
                DatasetName = "Test Dataset",
                ContainerName = "testagent123",
                BlobFilePath = "datasets/Golden_TestDataset_123.json",
                CreatedBy = "test@example.com",
                CreatedOn = DateTime.UtcNow,
                LastUpdatedBy = "test@example.com",
                LastUpdatedOn = DateTime.UtcNow
            };
            entity.RowKey = entity.DatasetId;
            entity.PartitionKey = entity.AgentId;
            return entity;
        }

        #endregion
    }
}
