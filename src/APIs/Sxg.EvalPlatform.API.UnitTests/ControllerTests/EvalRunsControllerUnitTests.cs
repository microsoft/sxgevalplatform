using Azure;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Sxg.EvalPlatform.API.Storage.Services;
using Sxg.EvalPlatform.API.UnitTests.RequestHandlerTests;
using SxgEvalPlatformApi.Controllers;
using SxgEvalPlatformApi.Models;
using SxgEvalPlatformApi.Models.Dtos;
using SxgEvalPlatformApi.RequestHandlers;
using SxgEvalPlatformApi.Services;
using System.ComponentModel.DataAnnotations;

namespace Sxg.EvalPlatform.API.UnitTests.ControllerTests
{
    /// <summary>
    /// Comprehensive unit tests for EvalRunsController.
    /// Tests all CRUD operations for evaluation runs.
    /// </summary>
    public class EvalRunsControllerUnitTests : ControllerTestBase<EvalRunsController>
    {
        private readonly Mock<IEvalRunRequestHandler> _mockEvalRunHandler;
        private readonly Mock<IDataSetTableService> _mockDataSetTableService;
        private readonly Mock<IMetricsConfigTableService> _mockMetricsConfigTableService;
        private readonly Mock<IEvalArtifactsRequestHandler> _mockEvalArtifactsHandler;
        private readonly Mock<IEvaluationResultRequestHandler> _mockEvaluationResultHandler;
        private readonly EvalRunsController _controller;

        public EvalRunsControllerUnitTests()
        {
            _mockEvalRunHandler = new Mock<IEvalRunRequestHandler>();
            _mockDataSetTableService = new Mock<IDataSetTableService>();
            _mockMetricsConfigTableService = new Mock<IMetricsConfigTableService>();
            _mockEvalArtifactsHandler = new Mock<IEvalArtifactsRequestHandler>();
            _mockEvaluationResultHandler = new Mock<IEvaluationResultRequestHandler>();

            _controller = new EvalRunsController(
                _mockEvalRunHandler.Object,
                _mockDataSetTableService.Object,
                _mockMetricsConfigTableService.Object,
                _mockEvalArtifactsHandler.Object,
                _mockEvaluationResultHandler.Object,
                MockCallerService.Object,
                MockLogger.Object,
                MockTelemetryService.Object
            );

            SetupControllerContext(_controller);
        }

        #region CreateEvalRun Tests

        [Fact]
        public async Task CreateEvalRun_WithValidData_ReturnsCreated()
        {
            // Arrange
            var createDto = CreateValidCreateEvalRunDto();
            var evalRun = CreateTestEvalRunDto(Guid.NewGuid());

            _mockEvalRunHandler
                .Setup(x => x.CreateEvalRunAsync(createDto))
                .ReturnsAsync(evalRun);

            // Act
            var result = await _controller.CreateEvalRun(createDto);

            // Assert
            result.Should().NotBeNull();
            result.Result.Should().BeOfType<CreatedAtActionResult>();

            var createdResult = result.Result as CreatedAtActionResult;
            createdResult.Should().NotBeNull();
            createdResult!.Value.Should().BeEquivalentTo(evalRun);
            createdResult.ActionName.Should().Be(nameof(EvalRunsController.GetEvalRun));
        }

        [Fact]
        public async Task CreateEvalRun_WithInvalidModelState_ReturnsBadRequest()
        {
            // Arrange
            var createDto = CreateValidCreateEvalRunDto();
            SimulateInvalidModelState(_controller);

            // Act
            var result = await _controller.CreateEvalRun(createDto);

            // Assert
            VerifyBadRequestResult(result);
        }

        [Fact]
        public async Task CreateEvalRun_WithValidationException_ReturnsBadRequest()
        {
            // Arrange
            var createDto = CreateValidCreateEvalRunDto();
            var validationException = new ValidationException("Invalid evaluation run data");

            _mockEvalRunHandler
                .Setup(x => x.CreateEvalRunAsync(createDto))
                .ThrowsAsync(validationException);

            // Act
            var result = await _controller.CreateEvalRun(createDto);

            // Assert
            // Controller returns ObjectResult with 400 status, not BadRequestObjectResult
            VerifyStatusCodeResult(result, 400);
        }

        [Fact]
        public async Task CreateEvalRun_WithAzureRequestFailedException_ReturnsAppropriateError()
        {
            // Arrange
            var createDto = CreateValidCreateEvalRunDto();
            var azureException = new RequestFailedException(404, "Resource not found");

            _mockEvalRunHandler
                .Setup(x => x.CreateEvalRunAsync(createDto))
                .ThrowsAsync(azureException);

            // Act
            var result = await _controller.CreateEvalRun(createDto);

            // Assert
            VerifyStatusCodeResult(result, 404);
        }

        [Fact]
        public async Task CreateEvalRun_WithAuthorizationException_ReturnsForbidden()
        {
            // Arrange
            var createDto = CreateValidCreateEvalRunDto();
            var azureException = new RequestFailedException(403, "Access denied");

            _mockEvalRunHandler
                .Setup(x => x.CreateEvalRunAsync(createDto))
                .ThrowsAsync(azureException);

            // Act
            var result = await _controller.CreateEvalRun(createDto);

            // Assert
            VerifyStatusCodeResult(result, 403);
        }

        [Fact]
        public async Task CreateEvalRun_WhenGeneralExceptionOccurs_ReturnsInternalServerError()
        {
            // Arrange
            var createDto = CreateValidCreateEvalRunDto();

            _mockEvalRunHandler
                .Setup(x => x.CreateEvalRunAsync(createDto))
                .ThrowsAsync(new Exception(TestConstants.ErrorMessages.DatabaseError));

            // Act
            var result = await _controller.CreateEvalRun(createDto);

            // Assert
            VerifyStatusCodeResult(result, 500);
        }

        [Fact]
        public async Task CreateEvalRun_LogsInformationMessages()
        {
            // Arrange
            var createDto = CreateValidCreateEvalRunDto();
            var evalRun = CreateTestEvalRunDto(Guid.NewGuid());

            _mockEvalRunHandler
                .Setup(x => x.CreateEvalRunAsync(createDto))
                .ReturnsAsync(evalRun);

            // Act
            await _controller.CreateEvalRun(createDto);

            // Assert
            MockLogger.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Creating evaluation run")),
                    null,
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Theory]
        [InlineData(false, false)]  // DirectUser
        [InlineData(true, false)]   // ServicePrincipal
        public async Task CreateEvalRun_WithDifferentAuthFlows_Succeeds(
            bool isServicePrincipal, bool hasDelegatedUser)
        {
            // Arrange
            if (isServicePrincipal && !hasDelegatedUser)
                SetupServicePrincipalAuth();
            else
                SetupDirectUserAuth();

            var createDto = CreateValidCreateEvalRunDto();
            var evalRun = CreateTestEvalRunDto(Guid.NewGuid());

            _mockEvalRunHandler
                .Setup(x => x.CreateEvalRunAsync(createDto))
                .ReturnsAsync(evalRun);

            // Act
            var result = await _controller.CreateEvalRun(createDto);

            // Assert
            result.Should().NotBeNull();
            result.Result.Should().BeOfType<CreatedAtActionResult>();
        }

        #endregion

        #region GetEvalRun Tests

        [Fact]
        public async Task GetEvalRun_WithValidId_ReturnsOkWithEvalRun()
        {
            // Arrange
            var evalRunId = Guid.NewGuid();
            var evalRun = CreateTestEvalRunDto(evalRunId);

            _mockEvalRunHandler
                .Setup(x => x.GetEvalRunByIdAsync(evalRunId))
                .ReturnsAsync(evalRun);

            // Act
            var result = await _controller.GetEvalRun(evalRunId);

            // Assert
            VerifyOkResult(result, value =>
            {
                value.Should().NotBeNull();
                value.EvalRunId.Should().Be(evalRunId);
            });
        }

        [Fact]
        public async Task GetEvalRun_WithEmptyGuid_ReturnsBadRequest()
        {
            // Arrange
            var evalRunId = Guid.Empty;

            // Act
            var result = await _controller.GetEvalRun(evalRunId);

            // Assert
            VerifyBadRequestResult(result, "Evaluation run ID is required");
        }

        [Fact]
        public async Task GetEvalRun_WithNonExistentId_ReturnsNotFound()
        {
            // Arrange
            var evalRunId = Guid.NewGuid();

            _mockEvalRunHandler
                .Setup(x => x.GetEvalRunByIdAsync(evalRunId))
                .ReturnsAsync((EvalRunDto?)null);

            // Act
            var result = await _controller.GetEvalRun(evalRunId);

            // Assert
            VerifyNotFoundResult(result, "not found");
        }

        [Fact]
        public async Task GetEvalRun_WithAzureException_ReturnsAppropriateError()
        {
            // Arrange
            var evalRunId = Guid.NewGuid();
            var azureException = new RequestFailedException(404, "Resource not found");

            _mockEvalRunHandler
                .Setup(x => x.GetEvalRunByIdAsync(evalRunId))
                .ThrowsAsync(azureException);

            // Act
            var result = await _controller.GetEvalRun(evalRunId);

            // Assert
            VerifyStatusCodeResult(result, 404);
        }

        [Fact]
        public async Task GetEvalRun_WhenExceptionOccurs_ReturnsInternalServerError()
        {
            // Arrange
            var evalRunId = Guid.NewGuid();

            _mockEvalRunHandler
                .Setup(x => x.GetEvalRunByIdAsync(evalRunId))
                .ThrowsAsync(new Exception(TestConstants.ErrorMessages.DatabaseError));

            // Act
            var result = await _controller.GetEvalRun(evalRunId);

            // Assert
            VerifyStatusCodeResult(result, 500);
        }

        #endregion

        #region GetEvalRunsByAgent Tests

        [Fact]
        public async Task GetEvalRunsByAgent_WithValidAgentId_ReturnsOkWithEvalRuns()
        {
            // Arrange
            var agentId = TestConstants.Agents.DefaultAgentId;
            var evalRuns = new List<EvalRunDto>
            {
                CreateTestEvalRunDto(Guid.NewGuid()),
                CreateTestEvalRunDto(Guid.NewGuid())
            };

            _mockEvalRunHandler
                .Setup(x => x.GetEvalRunsByAgentIdAsync(agentId, null, null))
                .ReturnsAsync(evalRuns);

            // Act
            var result = await _controller.GetEvalRunsByAgent(agentId, null, null);

            // Assert
            VerifyOkResult(result, value =>
            {
                value.Should().HaveCount(2);
                value.All(e => e.AgentId == agentId).Should().BeTrue();
            });
        }

        [Fact]
        public async Task GetEvalRunsByAgent_WithDateRange_ReturnsFilteredRuns()
        {
            // Arrange
            var agentId = TestConstants.Agents.DefaultAgentId;
            var startDate = DateTime.UtcNow.AddDays(-7);
            var endDate = DateTime.UtcNow;
            var evalRuns = new List<EvalRunDto> { CreateTestEvalRunDto(Guid.NewGuid()) };

            _mockEvalRunHandler
                .Setup(x => x.GetEvalRunsByAgentIdAsync(agentId, startDate, endDate))
                .ReturnsAsync(evalRuns);

            // Act
            var result = await _controller.GetEvalRunsByAgent(agentId, startDate, endDate);

            // Assert
            VerifyOkResult(result, value =>
            {
                value.Should().NotBeEmpty();
            });
        }

        [Theory]
        [InlineData("")]
        [InlineData(null)]
        [InlineData("   ")]
        public async Task GetEvalRunsByAgent_WithInvalidAgentId_ReturnsBadRequest(string? agentId)
        {
            // Act
            var result = await _controller.GetEvalRunsByAgent(agentId!, null, null);

            // Assert
            VerifyBadRequestResult(result, "AgentId is required");
        }

        [Fact]
        public async Task GetEvalRunsByAgent_WithInvalidDateRange_ReturnsBadRequest()
        {
            // Arrange
            var agentId = TestConstants.Agents.DefaultAgentId;
            var startDate = DateTime.UtcNow;
            var endDate = DateTime.UtcNow.AddDays(-1);  // End before start

            // Act
            var result = await _controller.GetEvalRunsByAgent(agentId, startDate, endDate);

            // Assert
            VerifyBadRequestResult(result, "StartDateTime cannot be later than EndDateTime");
        }

        [Fact]
        public async Task GetEvalRunsByAgent_WithNoResults_ReturnsEmptyList()
        {
            // Arrange
            var agentId = TestConstants.Agents.DefaultAgentId;

            _mockEvalRunHandler
                .Setup(x => x.GetEvalRunsByAgentIdAsync(agentId, null, null))
                .ReturnsAsync(new List<EvalRunDto>());

            // Act
            var result = await _controller.GetEvalRunsByAgent(agentId, null, null);

            // Assert
            VerifyOkResult(result, value =>
            {
                value.Should().NotBeNull();
                value.Should().BeEmpty();
            });
        }

        [Fact]
        public async Task GetEvalRunsByAgent_WithAzureException_ReturnsAppropriateError()
        {
            // Arrange
            var agentId = TestConstants.Agents.DefaultAgentId;
            var azureException = new RequestFailedException(500, "Internal server error");

            _mockEvalRunHandler
                .Setup(x => x.GetEvalRunsByAgentIdAsync(agentId, null, null))
                .ThrowsAsync(azureException);

            // Act
            var result = await _controller.GetEvalRunsByAgent(agentId, null, null);

            // Assert
            VerifyStatusCodeResult(result, 500);
        }

        [Fact]
        public async Task GetEvalRunsByAgent_WhenExceptionOccurs_ReturnsInternalServerError()
        {
            // Arrange
            var agentId = TestConstants.Agents.DefaultAgentId;

            _mockEvalRunHandler
                .Setup(x => x.GetEvalRunsByAgentIdAsync(agentId, null, null))
                .ThrowsAsync(new Exception(TestConstants.ErrorMessages.DatabaseError));

            // Act
            var result = await _controller.GetEvalRunsByAgent(agentId, null, null);

            // Assert
            VerifyStatusCodeResult(result, 500);
        }

        #endregion

        #region Telemetry Tests

        [Fact]
        public async Task CreateEvalRun_CallsTelemetryService()
        {
            // Arrange
            var createDto = CreateValidCreateEvalRunDto();
            _mockEvalRunHandler
                .Setup(x => x.CreateEvalRunAsync(createDto))
                .ReturnsAsync(CreateTestEvalRunDto(Guid.NewGuid()));

            // Act
            await _controller.CreateEvalRun(createDto);

            // Assert
            MockTelemetryService.Verify(
                x => x.StartActivity("EvalRunsController.CreateEvalRun"),
                Times.Once);
        }

        [Fact]
        public async Task GetEvalRun_CallsTelemetryService()
        {
            // Arrange
            var evalRunId = Guid.NewGuid();
            _mockEvalRunHandler
                .Setup(x => x.GetEvalRunByIdAsync(evalRunId))
                .ReturnsAsync(CreateTestEvalRunDto(evalRunId));

            // Act
            await _controller.GetEvalRun(evalRunId);

            // Assert
            MockTelemetryService.Verify(
                x => x.StartActivity("EvalRunsController.GetEvalRun"),
                Times.Once);
        }

        #endregion

        #region Helper Methods

        private CreateEvalRunDto CreateValidCreateEvalRunDto()
        {
            return new CreateEvalRunDto
            {
                AgentId = TestConstants.Agents.DefaultAgentId,
                DataSetId = Guid.NewGuid(),
                MetricsConfigurationId = Guid.NewGuid(),
                Type = "MCS",
                EnvironmentId = TestConstants.Environments.Dev,
                AgentSchemaName = "TestSchema",
                EvalRunName = "Test Eval Run"
            };
        }

        private EvalRunDto CreateTestEvalRunDto(Guid evalRunId)
        {
            return new EvalRunDto
            {
                EvalRunId = evalRunId,
                AgentId = TestConstants.Agents.DefaultAgentId,
                DataSetId = Guid.NewGuid().ToString(),
                MetricsConfigurationId = Guid.NewGuid().ToString(),
                Status = "Pending",
                EvalRunName = "Test Eval Run",
                DataSetName = TestConstants.Datasets.DefaultName,
                MetricsConfigurationName = TestConstants.MetricsConfigs.DefaultName,
                LastUpdatedBy = TestConstants.Users.DefaultEmail,
                LastUpdatedOn = DateTime.UtcNow
            };
        }

        #endregion
    }
}
