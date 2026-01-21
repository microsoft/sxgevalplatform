using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Sxg.EvalPlatform.API.UnitTests.RequestHandlerTests;
using SxgEvalPlatformApi.Controllers;
using SxgEvalPlatformApi.Models;
using SxgEvalPlatformApi.Models.Dtos;
using SxgEvalPlatformApi.RequestHandlers;
using SxgEvalPlatformApi.Services;
using System.Text.Json;

namespace Sxg.EvalPlatform.API.UnitTests.ControllerTests
{
    /// <summary>
    /// Comprehensive unit tests for EvalDatasetsController.
    /// Tests all CRUD operations, validation scenarios, and error handling.
    /// </summary>
    [Trait("Category", TestCategories.Unit)]
    [Trait("Category", TestCategories.Controller)]
    public class EvalDatasetsControllerUnitTests : ControllerTestBase<EvalDatasetsController>
    {
        private readonly Mock<IDataSetRequestHandler> _mockDataSetHandler;
        private readonly EvalDatasetsController _controller;

        public EvalDatasetsControllerUnitTests()
        {
            _mockDataSetHandler = new Mock<IDataSetRequestHandler>();

            _controller = new EvalDatasetsController(
                _mockDataSetHandler.Object,
                MockLogger.Object,
                MockCallerService.Object,
                MockTelemetryService.Object
            );

            SetupControllerContext(_controller);
        }

        #region GetDatasetsByAgentId Tests

        [Fact]
        public async Task GetDatasetsByAgentId_WithValidAgentId_ReturnsOkWithDatasets()
        {
            // Arrange
            var agentId = TestConstants.Agents.DefaultAgentId;
            var datasets = new List<DatasetMetadataDto>
            {
                CreateTestDatasetMetadata("dataset-1"),
                CreateTestDatasetMetadata("dataset-2")
            };

            _mockDataSetHandler
                .Setup(x => x.GetDatasetsByAgentIdAsync(agentId))
                .ReturnsAsync(datasets);

            // Act
            var result = await _controller.GetDatasetsByAgentId(agentId);

            // Assert
            VerifyOkResult(result, value =>
            {
                value.Should().HaveCount(2);
                value.Should().BeEquivalentTo(datasets);
            });
        }

        [Fact]
        public async Task GetDatasetsByAgentId_WithNoDatasets_ReturnsNotFound()
        {
            // Arrange
            var agentId = TestConstants.Agents.DefaultAgentId;

            _mockDataSetHandler
                .Setup(x => x.GetDatasetsByAgentIdAsync(agentId))
                .ReturnsAsync(new List<DatasetMetadataDto>());

            // Act
            var result = await _controller.GetDatasetsByAgentId(agentId);

            // Assert
            VerifyNotFoundResult(result, "No datasets found");
        }

        [Theory]
        [InlineData("")]
        [InlineData(null)]
        public async Task GetDatasetsByAgentId_WithInvalidAgentId_ReturnsBadRequest(string? agentId)
        {
            // Arrange
            SimulateInvalidModelState(_controller, "agentId", "Agent ID is required");

            // Act
            var result = await _controller.GetDatasetsByAgentId(agentId!);

            // Assert
            VerifyBadRequestResult(result);
        }

        [Fact]
        public async Task GetDatasetsByAgentId_WhenExceptionOccurs_ReturnsInternalServerError()
        {
            // Arrange
            var agentId = TestConstants.Agents.DefaultAgentId;

            _mockDataSetHandler
                .Setup(x => x.GetDatasetsByAgentIdAsync(agentId))
                .ThrowsAsync(new Exception(TestConstants.ErrorMessages.DatabaseError));

            // Act
            var result = await _controller.GetDatasetsByAgentId(agentId);

            // Assert
            VerifyStatusCodeResult(result, 500);
        }

        [Fact]
        public async Task GetDatasetsByAgentId_LogsInformationMessages()
        {
            // Arrange
            var agentId = TestConstants.Agents.DefaultAgentId;
            var datasets = new List<DatasetMetadataDto> { CreateTestDatasetMetadata() };

            _mockDataSetHandler
                .Setup(x => x.GetDatasetsByAgentIdAsync(agentId))
                .ReturnsAsync(datasets);

            // Act
            await _controller.GetDatasetsByAgentId(agentId);

            // Assert
            MockLogger.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("retrieve all datasets")),
                    null,
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        #endregion

        #region GetDatasetById Tests

        [Fact]
        public async Task GetDatasetById_WithValidId_ReturnsOkWithContent()
        {
            // Arrange
            var datasetId = Guid.NewGuid();
            var datasetContent = new List<EvalDataset>
            {
                CreateTestEvalDataset(TestConstants.TestData.Query1, TestConstants.TestData.GroundTruth1),
                CreateTestEvalDataset(TestConstants.TestData.Query2, TestConstants.TestData.GroundTruth2)
            };

            _mockDataSetHandler
                .Setup(x => x.GetDatasetByIdAsync(datasetId.ToString()))
                .ReturnsAsync(datasetContent);

            // Act
            var result = await _controller.GetDatasetById(datasetId);

            // Assert
            VerifyOkResult(result, value =>
            {
                value.Should().HaveCount(2);
                value[0].Query.Should().Be(TestConstants.TestData.Query1);
                value[1].Query.Should().Be(TestConstants.TestData.Query2);
            });
        }

        [Fact]
        public async Task GetDatasetById_WithNonExistentId_ReturnsNotFound()
        {
            // Arrange
            var datasetId = Guid.NewGuid();

            _mockDataSetHandler
                .Setup(x => x.GetDatasetByIdAsync(datasetId.ToString()))
                .ReturnsAsync((List<EvalDataset>?)null);

            // Act
            var result = await _controller.GetDatasetById(datasetId);

            // Assert
            VerifyNotFoundResult(result, "not found");
        }

        [Fact]
        public async Task GetDatasetById_WithInvalidJson_ReturnsInternalServerError()
        {
            // Arrange
            var datasetId = Guid.NewGuid();
            var jsonException = new JsonException("Invalid JSON");
            var invalidOpException = new InvalidOperationException("Deserialization failed", jsonException);

            _mockDataSetHandler
                .Setup(x => x.GetDatasetByIdAsync(datasetId.ToString()))
                .ThrowsAsync(invalidOpException);

            // Act
            var result = await _controller.GetDatasetById(datasetId);

            // Assert
            VerifyStatusCodeResult(result, 500);
        }

        [Fact]
        public async Task GetDatasetById_WhenExceptionOccurs_ReturnsInternalServerError()
        {
            // Arrange
            var datasetId = Guid.NewGuid();

            _mockDataSetHandler
                .Setup(x => x.GetDatasetByIdAsync(datasetId.ToString()))
                .ThrowsAsync(new Exception(TestConstants.ErrorMessages.BlobReadError));

            // Act
            var result = await _controller.GetDatasetById(datasetId);

            // Assert
            VerifyStatusCodeResult(result, 500);
        }

        [Fact]
        public async Task GetDatasetById_WithEmptyDataset_ReturnsOkWithEmptyList()
        {
            // Arrange
            var datasetId = Guid.NewGuid();
            var emptyList = new List<EvalDataset>();

            _mockDataSetHandler
                .Setup(x => x.GetDatasetByIdAsync(datasetId.ToString()))
                .ReturnsAsync(emptyList);

            // Act
            var result = await _controller.GetDatasetById(datasetId);

            // Assert
            VerifyOkResult(result, value =>
            {
                value.Should().NotBeNull();
                value.Should().BeEmpty();
            });
        }

        #endregion

        #region SaveDataset Tests

        [Fact]
        public async Task SaveDataset_WithValidData_ReturnsCreated()
        {
            // Arrange
            var saveDto = CreateValidSaveDatasetDto();
            var response = new DatasetSaveResponseDto
            {
                DatasetId = Guid.NewGuid().ToString(),
                Status = TestConstants.ResponseStatus.Created,
                Message = "Dataset created successfully"
            };

            _mockDataSetHandler
                .Setup(x => x.SaveDatasetAsync(saveDto))
                .ReturnsAsync(response);

            // Act
            var result = await _controller.SaveDataset(saveDto);

            // Assert
            result.Should().NotBeNull();
            result.Result.Should().BeOfType<CreatedAtActionResult>();

            var createdResult = result.Result as CreatedAtActionResult;
            createdResult.Should().NotBeNull();
            createdResult!.Value.Should().BeEquivalentTo(response);
            createdResult.ActionName.Should().Be(nameof(EvalDatasetsController.GetDatasetById));
        }

        [Fact]
        public async Task SaveDataset_WithExistingDataset_ReturnsOk()
        {
            // Arrange
            var saveDto = CreateValidSaveDatasetDto();
            var response = new DatasetSaveResponseDto
            {
                DatasetId = Guid.NewGuid().ToString(),
                Status = TestConstants.ResponseStatus.Updated,
                Message = "Dataset updated successfully"
            };

            _mockDataSetHandler
                .Setup(x => x.SaveDatasetAsync(saveDto))
                .ReturnsAsync(response);

            // Act
            var result = await _controller.SaveDataset(saveDto);

            // Assert
            VerifyOkResult(result, value =>
            {
                value.Status.Should().Be(TestConstants.ResponseStatus.Updated);
                value.DatasetId.Should().NotBeNullOrEmpty();
            });
        }

        [Fact]
        public async Task SaveDataset_WithInvalidModelState_ReturnsBadRequest()
        {
            // Arrange
            var saveDto = CreateValidSaveDatasetDto();
            SimulateInvalidModelState(_controller);

            // Act
            var result = await _controller.SaveDataset(saveDto);

            // Assert
            VerifyBadRequestResult(result);
        }

        [Fact]
        public async Task SaveDataset_WithErrorStatus_ReturnsInternalServerError()
        {
            // Arrange
            var saveDto = CreateValidSaveDatasetDto();
            var response = new DatasetSaveResponseDto
            {
                Status = TestConstants.ResponseStatus.Error,
                Message = "Failed to save dataset"
            };

            _mockDataSetHandler
                .Setup(x => x.SaveDatasetAsync(saveDto))
                .ReturnsAsync(response);

            // Act
            var result = await _controller.SaveDataset(saveDto);

            // Assert
            VerifyStatusCodeResult(result, 500);
        }

        [Fact]
        public async Task SaveDataset_WhenExceptionOccurs_ReturnsInternalServerError()
        {
            // Arrange
            var saveDto = CreateValidSaveDatasetDto();

            _mockDataSetHandler
                .Setup(x => x.SaveDatasetAsync(saveDto))
                .ThrowsAsync(new Exception(TestConstants.ErrorMessages.BlobWriteError));

            // Act
            var result = await _controller.SaveDataset(saveDto);

            // Assert
            VerifyStatusCodeResult(result, 500);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public async Task SaveDataset_WithInvalidAgentId_ReturnsBadRequest(string? agentId)
        {
            // Arrange
            var saveDto = CreateValidSaveDatasetDto();
            saveDto.AgentId = agentId!;
            SimulateInvalidModelState(_controller, "AgentId", "Agent ID is required");

            // Act
            var result = await _controller.SaveDataset(saveDto);

            // Assert
            VerifyBadRequestResult(result);
        }

        [Fact]
        public async Task SaveDataset_WithEmptyDatasetRecords_ReturnsBadRequest()
        {
            // Arrange
            var saveDto = CreateValidSaveDatasetDto();
            saveDto.DatasetRecords = new List<EvalDataset>();
            SimulateInvalidModelState(_controller, "DatasetRecords", "Dataset records cannot be empty");

            // Act
            var result = await _controller.SaveDataset(saveDto);

            // Assert
            VerifyBadRequestResult(result);
        }

        [Theory]
        [InlineData(false, false)]  // DirectUser
        [InlineData(true, false)]   // ServicePrincipal
        public async Task SaveDataset_WithDifferentAuthFlows_Succeeds(
            bool isServicePrincipal, bool hasDelegatedUser)
        {
            // Arrange
            if (isServicePrincipal && !hasDelegatedUser)
                SetupServicePrincipalAuth();
            else
                SetupDirectUserAuth();

            var saveDto = CreateValidSaveDatasetDto();
            var response = new DatasetSaveResponseDto
            {
                DatasetId = Guid.NewGuid().ToString(),
                Status = TestConstants.ResponseStatus.Created,
                CreatedBy = isServicePrincipal ? TestConstants.Applications.ServiceApp : TestConstants.Users.DefaultEmail
            };

            _mockDataSetHandler
                .Setup(x => x.SaveDatasetAsync(saveDto))
                .ReturnsAsync(response);

            // Act
            var result = await _controller.SaveDataset(saveDto);

            // Assert
            result.Should().NotBeNull();
            result.Result.Should().BeOfType<CreatedAtActionResult>();
        }

        #endregion

        #region UpdateDataset Tests

        [Fact]
        public async Task UpdateDataset_WithValidData_ReturnsOk()
        {
            // Arrange
            var datasetId = Guid.NewGuid();
            var updateDto = CreateValidUpdateDatasetDto();
            var response = new DatasetSaveResponseDto
            {
                DatasetId = datasetId.ToString(),
                Status = TestConstants.ResponseStatus.Updated,
                Message = "Dataset updated successfully"
            };

            _mockDataSetHandler
                .Setup(x => x.UpdateDatasetAsync(datasetId.ToString(), updateDto))
                .ReturnsAsync(response);

            // Act
            var result = await _controller.UpdateDataset(datasetId, updateDto);

            // Assert
            VerifyOkResult(result, value =>
            {
                value.Status.Should().Be(TestConstants.ResponseStatus.Updated);
                value.DatasetId.Should().Be(datasetId.ToString());
            });
        }

        [Fact]
        public async Task UpdateDataset_WithNonExistentId_ReturnsError()
        {
            // Arrange
            var datasetId = Guid.NewGuid();
            var updateDto = CreateValidUpdateDatasetDto();
            var response = new DatasetSaveResponseDto
            {
                Status = TestConstants.ResponseStatus.Error,
                Message = "Dataset not found"
            };

            _mockDataSetHandler
                .Setup(x => x.UpdateDatasetAsync(datasetId.ToString(), updateDto))
                .ReturnsAsync(response);

            // Act
            var result = await _controller.UpdateDataset(datasetId, updateDto);

            // Assert
            VerifyStatusCodeResult(result, 500);
        }

        [Fact]
        public async Task UpdateDataset_WithInvalidModelState_ReturnsBadRequest()
        {
            // Arrange
            var datasetId = Guid.NewGuid();
            var updateDto = CreateValidUpdateDatasetDto();
            SimulateInvalidModelState(_controller);

            // Act
            var result = await _controller.UpdateDataset(datasetId, updateDto);

            // Assert
            VerifyBadRequestResult(result);
        }

        [Fact]
        public async Task UpdateDataset_WhenExceptionOccurs_ReturnsInternalServerError()
        {
            // Arrange
            var datasetId = Guid.NewGuid();
            var updateDto = CreateValidUpdateDatasetDto();

            _mockDataSetHandler
                .Setup(x => x.UpdateDatasetAsync(datasetId.ToString(), updateDto))
                .ThrowsAsync(new Exception(TestConstants.ErrorMessages.DatabaseError));

            // Act
            var result = await _controller.UpdateDataset(datasetId, updateDto);

            // Assert
            VerifyStatusCodeResult(result, 500);
        }

        [Fact]
        public async Task UpdateDataset_WithEmptyRecords_ReturnsBadRequest()
        {
            // Arrange
            var datasetId = Guid.NewGuid();
            var updateDto = CreateValidUpdateDatasetDto();
            updateDto.DatasetRecords = new List<EvalDataset>();
            SimulateInvalidModelState(_controller, "DatasetRecords", "Dataset records cannot be empty");

            // Act
            var result = await _controller.UpdateDataset(datasetId, updateDto);

            // Assert
            VerifyBadRequestResult(result);
        }

        #endregion

        #region DeleteDataset Tests

        [Fact]
        public async Task DeleteDataset_WithValidId_ReturnsOk()
        {
            // Arrange
            var datasetId = Guid.NewGuid();

            _mockDataSetHandler
                .Setup(x => x.DeleteDatasetAsync(datasetId.ToString()))
                .ReturnsAsync(true);

            // Act
            var result = await _controller.DeleteDataset(datasetId);

            // Assert
            VerifyOkResult(result);
        }

        [Fact]
        public async Task DeleteDataset_WithNonExistentId_ReturnsNotFound()
        {
            // Arrange
            var datasetId = Guid.NewGuid();

            _mockDataSetHandler
                .Setup(x => x.DeleteDatasetAsync(datasetId.ToString()))
                .ReturnsAsync(false);

            // Act
            var result = await _controller.DeleteDataset(datasetId);

            // Assert
            VerifyStatusCodeResult(result, 404);
        }

        [Fact]
        public async Task DeleteDataset_WhenExceptionOccurs_ReturnsInternalServerError()
        {
            // Arrange
            var datasetId = Guid.NewGuid();

            _mockDataSetHandler
                .Setup(x => x.DeleteDatasetAsync(datasetId.ToString()))
                .ThrowsAsync(new Exception(TestConstants.ErrorMessages.DatabaseError));

            // Act
            var result = await _controller.DeleteDataset(datasetId);

            // Assert
            VerifyStatusCodeResult(result, 500);
        }

        [Fact]
        public async Task DeleteDataset_LogsInformationMessages()
        {
            // Arrange
            var datasetId = Guid.NewGuid();

            _mockDataSetHandler
                .Setup(x => x.DeleteDatasetAsync(datasetId.ToString()))
                .ReturnsAsync(true);

            // Act
            await _controller.DeleteDataset(datasetId);

            // Assert
            MockLogger.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("delete dataset")),
                    null,
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.AtLeastOnce);
        }

        #endregion

        #region Telemetry Tests

        [Fact]
        public async Task GetDatasetsByAgentId_CallsTelemetryService()
        {
            // Arrange
            var agentId = TestConstants.Agents.DefaultAgentId;
            _mockDataSetHandler
                .Setup(x => x.GetDatasetsByAgentIdAsync(agentId))
                .ReturnsAsync(new List<DatasetMetadataDto> { CreateTestDatasetMetadata() });

            // Act
            await _controller.GetDatasetsByAgentId(agentId);

            // Assert
            MockTelemetryService.Verify(
                x => x.StartActivity("EvalDatasetsController.GetDatasetsByAgentId"),
                Times.Once);
        }

        [Fact]
        public async Task SaveDataset_CallsTelemetryService()
        {
            // Arrange
            var saveDto = CreateValidSaveDatasetDto();
            _mockDataSetHandler
                .Setup(x => x.SaveDatasetAsync(saveDto))
                .ReturnsAsync(new DatasetSaveResponseDto { Status = TestConstants.ResponseStatus.Created });

            // Act
            await _controller.SaveDataset(saveDto);

            // Assert
            MockTelemetryService.Verify(
                x => x.StartActivity("EvalDatasetsController.SaveDataset"),
                Times.Once);
        }

        #endregion

        #region Helper Methods

        private SaveDatasetDto CreateValidSaveDatasetDto()
        {
            return new SaveDatasetDto
            {
                AgentId = TestConstants.Agents.DefaultAgentId,
                DatasetType = TestConstants.Datasets.GoldenType,
                DatasetName = TestConstants.Datasets.DefaultName,
                DatasetRecords = new List<EvalDataset>
                {
                    CreateTestEvalDataset(TestConstants.TestData.DefaultQuery, TestConstants.TestData.DefaultGroundTruth),
                    CreateTestEvalDataset(TestConstants.TestData.Query1, TestConstants.TestData.GroundTruth1)
                }
            };
        }

        private UpdateDatasetDto CreateValidUpdateDatasetDto()
        {
            return new UpdateDatasetDto
            {
                DatasetRecords = new List<EvalDataset>
                {
                    CreateTestEvalDataset("Updated Query", "Updated Ground Truth")
                }
            };
        }

        private DatasetMetadataDto CreateTestDatasetMetadata(string? datasetId = null)
        {
            return new DatasetMetadataDto
            {
                DatasetId = datasetId ?? Guid.NewGuid().ToString(),
                AgentId = TestConstants.Agents.DefaultAgentId,
                DatasetName = TestConstants.Datasets.DefaultName,
                DatasetType = TestConstants.Datasets.GoldenType,
                CreatedBy = TestConstants.Users.DefaultEmail,
                CreatedOn = DateTime.UtcNow
            };
        }

        private EvalDataset CreateTestEvalDataset(string query, string groundTruth)
        {
            return new EvalDataset
            {
                Query = query,
                GroundTruth = groundTruth,
                ActualResponse = TestConstants.TestData.DefaultActualResponse,
                Context = TestConstants.TestData.DefaultContext
            };
        }

        #endregion
    }
}
