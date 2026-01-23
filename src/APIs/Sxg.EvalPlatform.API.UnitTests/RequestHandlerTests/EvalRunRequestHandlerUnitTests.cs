using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;
using Sxg.EvalPlatform.API.Storage;
using Sxg.EvalPlatform.API.Storage.Services;
using Sxg.EvalPlatform.API.Storage.TableEntities;
using Sxg.EvalPlatform.API.Storage.Validators;
using SXG.EvalPlatform.Common;
using SxgEvalPlatformApi.Controllers;
using SxgEvalPlatformApi.Models;
using SxgEvalPlatformApi.RequestHandlers;
using SxgEvalPlatformApi.Services;
using System.ComponentModel.DataAnnotations;

namespace Sxg.EvalPlatform.API.UnitTests.RequestHandlerTests
{
    /// <summary>
    /// Comprehensive unit tests for EvalRunRequestHandler
    /// Tests all public and private methods with various scenarios
    /// </summary>
    public class EvalRunRequestHandlerUnitTests
    {
        private readonly Mock<IEvalRunTableService> _mockEvalRunTableService;
        private readonly Mock<IDataVerseAPIService> _mockDataVerseAPIService;
        private readonly Mock<IConfigHelper> _mockConfigHelper;
        private readonly Mock<ILogger<EvalRunRequestHandler>> _mockLogger;
        private readonly Mock<IDataSetTableService> _mockDataSetTableService;
        private readonly Mock<IMetricsConfigTableService> _mockMetricsConfigTableService;
        private readonly Mock<IEntityValidators> _mockEntityValidators;
        private readonly Mock<ICallerIdentificationService> _mockCallerService;
        private readonly Mock<ICacheManager> _mockCacheManager;
        private readonly EvalRunRequestHandler _handler;

        public EvalRunRequestHandlerUnitTests()
        {
            _mockEvalRunTableService = new Mock<IEvalRunTableService>();
            _mockDataVerseAPIService = new Mock<IDataVerseAPIService>();
            _mockConfigHelper = new Mock<IConfigHelper>();
            _mockLogger = new Mock<ILogger<EvalRunRequestHandler>>();
            _mockDataSetTableService = new Mock<IDataSetTableService>();
            _mockMetricsConfigTableService = new Mock<IMetricsConfigTableService>();
            _mockEntityValidators = new Mock<IEntityValidators>();
            _mockCallerService = new Mock<ICallerIdentificationService>();
            _mockCacheManager = new Mock<ICacheManager>();

            // Setup default behaviors
            _mockConfigHelper.Setup(x => x.EvalResultsFolderName()).Returns("eval-results");

            _mockCallerService.Setup(x => x.GetCallerInfo()).Returns(new CallerInfo
            {
                UserId = "test-user-id",
                UserEmail = "test@example.com",
                ApplicationName = "TestApp",
                IsServicePrincipal = false,
                HasDelegatedUser = false
            });
            _mockCallerService.Setup(x => x.GetCurrentUserEmail()).Returns("test@example.com");
            _mockCallerService.Setup(x => x.GetCurrentUserId()).Returns("test-user-id");
            _mockCallerService.Setup(x => x.IsServicePrincipalCall()).Returns(false);
            _mockCallerService.Setup(x => x.HasDelegatedUserContext()).Returns(false);

            _handler = new EvalRunRequestHandler(
                _mockEvalRunTableService.Object,
                _mockDataVerseAPIService.Object,
                _mockLogger.Object,
                _mockConfigHelper.Object,
                _mockCacheManager.Object,
                _mockDataSetTableService.Object,
                _mockMetricsConfigTableService.Object,
                _mockEntityValidators.Object,
                _mockCallerService.Object
            );
        }

        #region CreateEvalRunAsync Tests

        [Fact]
        public async Task CreateEvalRunAsync_WithValidData_CreatesSuccessfully()
        {
            // Arrange
            var createDto = CreateValidCreateEvalRunDto();
            var createdEntity = CreateTestEvalRunEntity();
            var datasetEntity = CreateTestDataSetEntity();
            var metricsEntity = CreateTestMetricsConfigEntity();

            _mockEntityValidators.Setup(x => x.IsValidDatasetId(It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(true);
            _mockEntityValidators.Setup(x => x.IsValidMetricsConfigurationId(It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(true);

            _mockEvalRunTableService.Setup(x => x.CreateEvalRunAsync(It.IsAny<EvalRunTableEntity>()))
                .ReturnsAsync(createdEntity);

            _mockDataSetTableService.Setup(x => x.GetDataSetByIdAsync(createDto.DataSetId.ToString()))
                .ReturnsAsync(datasetEntity);

            _mockMetricsConfigTableService.Setup(x => x.GetMetricsConfigurationByConfigurationIdAsync(createDto.MetricsConfigurationId.ToString()))
                .ReturnsAsync(metricsEntity);

            _mockDataVerseAPIService.Setup(x => x.PostEvalRunAsync(It.IsAny<DataVerseApiRequest>()))
                .ReturnsAsync(new DataVerseApiResponse { Success = true, StatusCode = 200 });

            // Act
            var result = await _handler.CreateEvalRunAsync(createDto);

            // Assert
            result.Should().NotBeNull();
            result.EvalRunId.Should().NotBe(Guid.Empty);
            result.Status.Should().Be(CommonConstants.EvalRunStatus.RequestSubmitted);
            result.AgentId.Should().Be(createDto.AgentId);

            _mockEvalRunTableService.Verify(x => x.CreateEvalRunAsync(It.IsAny<EvalRunTableEntity>()), Times.Once);
            _mockDataVerseAPIService.Verify(x => x.PostEvalRunAsync(It.IsAny<DataVerseApiRequest>()), Times.Once);
        }

        [Fact]
        public async Task CreateEvalRunAsync_WithInvalidDatasetId_ThrowsValidationException()
        {
            // Arrange
            var createDto = CreateValidCreateEvalRunDto();

            _mockEntityValidators.Setup(x => x.IsValidDatasetId(It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(false);

            // Act & Assert
            await Assert.ThrowsAsync<ValidationException>(() => _handler.CreateEvalRunAsync(createDto));
        }

        [Fact]
        public async Task CreateEvalRunAsync_WithInvalidMetricsConfigId_ThrowsValidationException()
        {
            // Arrange
            var createDto = CreateValidCreateEvalRunDto();

            _mockEntityValidators.Setup(x => x.IsValidDatasetId(It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(true);
            _mockEntityValidators.Setup(x => x.IsValidMetricsConfigurationId(It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(false);

            // Act & Assert
            await Assert.ThrowsAsync<ValidationException>(() => _handler.CreateEvalRunAsync(createDto));
        }

        [Fact]
        public async Task CreateEvalRunAsync_SetsCorrectAuditUser()
        {
            // Arrange
            var createDto = CreateValidCreateEvalRunDto();
            var createdEntity = CreateTestEvalRunEntity();
            var datasetEntity = CreateTestDataSetEntity();
            var metricsEntity = CreateTestMetricsConfigEntity();
            EvalRunTableEntity? capturedEntity = null;

            _mockEntityValidators.Setup(x => x.IsValidDatasetId(It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(true);
            _mockEntityValidators.Setup(x => x.IsValidMetricsConfigurationId(It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(true);

            _mockEvalRunTableService.Setup(x => x.CreateEvalRunAsync(It.IsAny<EvalRunTableEntity>()))
                .Callback<EvalRunTableEntity>(e => capturedEntity = e)
                .ReturnsAsync(createdEntity);

            _mockDataSetTableService.Setup(x => x.GetDataSetByIdAsync(It.IsAny<string>()))
                .ReturnsAsync(datasetEntity);

            _mockMetricsConfigTableService.Setup(x => x.GetMetricsConfigurationByConfigurationIdAsync(It.IsAny<string>()))
                .ReturnsAsync(metricsEntity);

            _mockDataVerseAPIService.Setup(x => x.PostEvalRunAsync(It.IsAny<DataVerseApiRequest>()))
                .ReturnsAsync(new DataVerseApiResponse { Success = true, StatusCode = 200 });

            // Act
            await _handler.CreateEvalRunAsync(createDto);

            // Assert
            capturedEntity.Should().NotBeNull();
            capturedEntity!.CreatedBy.Should().Be("test@example.com");
        }

        [Fact]
        public async Task CreateEvalRunAsync_WhenDataVerseApiFails_StillCreatesEvalRun()
        {
            // Arrange
            var createDto = CreateValidCreateEvalRunDto();
            var createdEntity = CreateTestEvalRunEntity();
            var datasetEntity = CreateTestDataSetEntity();
            var metricsEntity = CreateTestMetricsConfigEntity();

            _mockEntityValidators.Setup(x => x.IsValidDatasetId(It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(true);
            _mockEntityValidators.Setup(x => x.IsValidMetricsConfigurationId(It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(true);

            _mockEvalRunTableService.Setup(x => x.CreateEvalRunAsync(It.IsAny<EvalRunTableEntity>()))
                .ReturnsAsync(createdEntity);

            _mockDataSetTableService.Setup(x => x.GetDataSetByIdAsync(It.IsAny<string>()))
                .ReturnsAsync(datasetEntity);

            _mockMetricsConfigTableService.Setup(x => x.GetMetricsConfigurationByConfigurationIdAsync(It.IsAny<string>()))
                .ReturnsAsync(metricsEntity);

            _mockDataVerseAPIService.Setup(x => x.PostEvalRunAsync(It.IsAny<DataVerseApiRequest>()))
                .ReturnsAsync(new DataVerseApiResponse { Success = false, StatusCode = 500, Message = "API Error" });

            // Act
            var result = await _handler.CreateEvalRunAsync(createDto);

            // Assert
            result.Should().NotBeNull();
            result.EvalRunId.Should().NotBe(Guid.Empty);
        }

        #endregion

        #region UpdateEvalRunStatusAsync Tests

        [Fact]
        public async Task UpdateEvalRunStatusAsync_WithValidStatus_UpdatesSuccessfully()
        {
            // Arrange
            var updateDto = new UpdateEvalRunStatusDto
            {
                AgentId = "agent-123",
                EvalRunId = Guid.NewGuid(),
                Status = CommonConstants.EvalRunStatus.EvalRunStarted
            };
            var updatedEntity = CreateTestEvalRunEntity();

            _mockEvalRunTableService.Setup(x => x.UpdateEvalRunStatusAsync(
                updateDto.AgentId, updateDto.EvalRunId, It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(updatedEntity);

            _mockDataSetTableService.Setup(x => x.GetDataSetByIdAsync(It.IsAny<string>()))
                .ReturnsAsync(CreateTestDataSetEntity());

            _mockMetricsConfigTableService.Setup(x => x.GetMetricsConfigurationByConfigurationIdAsync(It.IsAny<string>()))
                .ReturnsAsync(CreateTestMetricsConfigEntity());

            // Act
            var result = await _handler.UpdateEvalRunStatusAsync(updateDto);

            // Assert
            result.Should().NotBeNull();
            result!.Status.Should().Be(updatedEntity.Status);
        }

        [Fact]
        public async Task UpdateEvalRunStatusAsync_NormalizesStatusCasing()
        {
            // Arrange
            var updateDto = new UpdateEvalRunStatusDto
            {
                AgentId = "agent-123",
                EvalRunId = Guid.NewGuid(),
                Status = "evalrunstarted" // lowercase
            };
            var updatedEntity = CreateTestEvalRunEntity();
            string? capturedStatus = null;

            _mockEvalRunTableService.Setup(x => x.UpdateEvalRunStatusAsync(
                It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>()))
                .Callback<string, Guid, string, string>((_, __, status, ___) => capturedStatus = status)
                .ReturnsAsync(updatedEntity);

            _mockDataSetTableService.Setup(x => x.GetDataSetByIdAsync(It.IsAny<string>()))
                .ReturnsAsync(CreateTestDataSetEntity());

            _mockMetricsConfigTableService.Setup(x => x.GetMetricsConfigurationByConfigurationIdAsync(It.IsAny<string>()))
                .ReturnsAsync(CreateTestMetricsConfigEntity());

            // Act
            await _handler.UpdateEvalRunStatusAsync(updateDto);

            // Assert
            capturedStatus.Should().Be(CommonConstants.EvalRunStatus.EvalRunStarted);
        }

        [Fact]
        public async Task UpdateEvalRunStatusAsync_WithNonExistentId_ReturnsNull()
        {
            // Arrange
            var updateDto = new UpdateEvalRunStatusDto
            {
                AgentId = "agent-123",
                EvalRunId = Guid.NewGuid(),
                Status = CommonConstants.EvalRunStatus.EvalRunCompleted
            };

            _mockEvalRunTableService.Setup(x => x.UpdateEvalRunStatusAsync(
                It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync((EvalRunTableEntity?)null);

            // Act
            var result = await _handler.UpdateEvalRunStatusAsync(updateDto);

            // Assert
            result.Should().BeNull();
        }

        [Theory]
        [InlineData("RequestSubmitted")]
        [InlineData("EnrichingDataset")]
        [InlineData("DatasetEnrichmentCompleted")]
        [InlineData("EvalRunStarted")]
        [InlineData("EvalRunCompleted")]
        [InlineData("EvalRunFailed")]
        public async Task UpdateEvalRunStatusAsync_HandlesAllValidStatuses(string status)
        {
            // Arrange
            var updateDto = new UpdateEvalRunStatusDto
            {
                AgentId = "agent-123",
                EvalRunId = Guid.NewGuid(),
                Status = status
            };
            var updatedEntity = CreateTestEvalRunEntity();
            updatedEntity.Status = status;

            _mockEvalRunTableService.Setup(x => x.UpdateEvalRunStatusAsync(
                It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(updatedEntity);

            _mockDataSetTableService.Setup(x => x.GetDataSetByIdAsync(It.IsAny<string>()))
                .ReturnsAsync(CreateTestDataSetEntity());

            _mockMetricsConfigTableService.Setup(x => x.GetMetricsConfigurationByConfigurationIdAsync(It.IsAny<string>()))
                .ReturnsAsync(CreateTestMetricsConfigEntity());

            // Act
            var result = await _handler.UpdateEvalRunStatusAsync(updateDto);

            // Assert
            result.Should().NotBeNull();
            result!.Status.Should().Be(status);
        }

        #endregion

        #region GetEvalRunByIdAsync Tests

        [Fact]
        public async Task GetEvalRunByIdAsync_WithAgentId_ReturnsEvalRun()
        {
            // Arrange
            var agentId = "agent-123";
            var evalRunId = Guid.NewGuid();
            var entity = CreateTestEvalRunEntity();

            _mockEvalRunTableService.Setup(x => x.GetEvalRunByIdAsync(agentId, evalRunId))
                .ReturnsAsync(entity);

            _mockDataSetTableService.Setup(x => x.GetDataSetByIdAsync(It.IsAny<string>()))
                .ReturnsAsync(CreateTestDataSetEntity());

            _mockMetricsConfigTableService.Setup(x => x.GetMetricsConfigurationByConfigurationIdAsync(It.IsAny<string>()))
                .ReturnsAsync(CreateTestMetricsConfigEntity());

            // Act
            var result = await _handler.GetEvalRunByIdAsync(agentId, evalRunId);

            // Assert
            result.Should().NotBeNull();
            result!.EvalRunId.Should().Be(entity.EvalRunId);
        }

        [Fact]
        public async Task GetEvalRunByIdAsync_WithoutAgentId_ReturnsEvalRun()
        {
            // Arrange
            var evalRunId = Guid.NewGuid();
            var entity = CreateTestEvalRunEntity();

            _mockEvalRunTableService.Setup(x => x.GetEvalRunByIdAsync(evalRunId))
                .ReturnsAsync(entity);

            _mockDataSetTableService.Setup(x => x.GetDataSetByIdAsync(It.IsAny<string>()))
                .ReturnsAsync(CreateTestDataSetEntity());

            _mockMetricsConfigTableService.Setup(x => x.GetMetricsConfigurationByConfigurationIdAsync(It.IsAny<string>()))
                .ReturnsAsync(CreateTestMetricsConfigEntity());

            // Act
            var result = await _handler.GetEvalRunByIdAsync(evalRunId);

            // Assert
            result.Should().NotBeNull();
            result!.EvalRunId.Should().Be(entity.EvalRunId);
        }

        [Fact]
        public async Task GetEvalRunByIdAsync_WithNonExistentId_ReturnsNull()
        {
            // Arrange
            var evalRunId = Guid.NewGuid();

            _mockEvalRunTableService.Setup(x => x.GetEvalRunByIdAsync(evalRunId))
                .ReturnsAsync((EvalRunTableEntity?)null);

            // Act
            var result = await _handler.GetEvalRunByIdAsync(evalRunId);

            // Assert
            result.Should().BeNull();
        }

        [Fact]
        public async Task GetEvalRunByIdAsync_EnrichesWithDataSetName()
        {
            // Arrange
            var evalRunId = Guid.NewGuid();
            var entity = CreateTestEvalRunEntity();
            var datasetEntity = CreateTestDataSetEntity();
            datasetEntity.DatasetName = "Test Dataset";

            _mockEvalRunTableService.Setup(x => x.GetEvalRunByIdAsync(evalRunId))
                .ReturnsAsync(entity);

            _mockDataSetTableService.Setup(x => x.GetDataSetByIdAsync(It.IsAny<string>()))
                .ReturnsAsync(datasetEntity);

            _mockMetricsConfigTableService.Setup(x => x.GetMetricsConfigurationByConfigurationIdAsync(It.IsAny<string>()))
                .ReturnsAsync(CreateTestMetricsConfigEntity());

            // Act
            var result = await _handler.GetEvalRunByIdAsync(evalRunId);

            // Assert
            result.Should().NotBeNull();
            result!.DataSetName.Should().Be("Test Dataset");
        }

        [Fact]
        public async Task GetEvalRunByIdAsync_EnrichesWithMetricsConfigName()
        {
            // Arrange
            var evalRunId = Guid.NewGuid();
            var entity = CreateTestEvalRunEntity();
            var metricsEntity = CreateTestMetricsConfigEntity();
            metricsEntity.ConfigurationName = "Test Metrics Config";

            _mockEvalRunTableService.Setup(x => x.GetEvalRunByIdAsync(evalRunId))
                .ReturnsAsync(entity);

            _mockDataSetTableService.Setup(x => x.GetDataSetByIdAsync(It.IsAny<string>()))
                .ReturnsAsync(CreateTestDataSetEntity());

            _mockMetricsConfigTableService.Setup(x => x.GetMetricsConfigurationByConfigurationIdAsync(It.IsAny<string>()))
                .ReturnsAsync(metricsEntity);

            // Act
            var result = await _handler.GetEvalRunByIdAsync(evalRunId);

            // Assert
            result.Should().NotBeNull();
            result!.MetricsConfigurationName.Should().Be("Test Metrics Config");
        }

        [Fact]
        public async Task GetEvalRunByIdAsync_WhenDataSetFetchFails_SetsNameToNull()
        {
            // Arrange
            var evalRunId = Guid.NewGuid();
            var entity = CreateTestEvalRunEntity();

            _mockEvalRunTableService.Setup(x => x.GetEvalRunByIdAsync(evalRunId))
                .ReturnsAsync(entity);

            _mockDataSetTableService.Setup(x => x.GetDataSetByIdAsync(It.IsAny<string>()))
                .ThrowsAsync(new Exception("Dataset fetch error"));

            _mockMetricsConfigTableService.Setup(x => x.GetMetricsConfigurationByConfigurationIdAsync(It.IsAny<string>()))
                .ReturnsAsync(CreateTestMetricsConfigEntity());

            // Act
            var result = await _handler.GetEvalRunByIdAsync(evalRunId);

            // Assert
            result.Should().NotBeNull();
            result!.DataSetName.Should().BeNull();
        }

        #endregion

        #region GetEvalRunsByAgentIdAsync Tests

        [Fact]
        public async Task GetEvalRunsByAgentIdAsync_ReturnsMultipleRuns()
        {
            // Arrange
            var agentId = "agent-123";
            var entities = new List<EvalRunTableEntity>
            {
                CreateTestEvalRunEntity(),
                CreateTestEvalRunEntity()
            };

            _mockEvalRunTableService.Setup(x => x.GetEvalRunsByAgentIdAndDateFilterAsync(
                agentId, It.IsAny<DateTime?>(), It.IsAny<DateTime?>()))
                .ReturnsAsync(entities);

            // Act
            var result = await _handler.GetEvalRunsByAgentIdAsync(agentId, null, null);

            // Assert
            result.Should().NotBeNull();
            result.Should().HaveCount(2);
        }

        [Fact]
        public async Task GetEvalRunsByAgentIdAsync_WithDateFilter_PassesParameters()
        {
            // Arrange
            var agentId = "agent-123";
            var startDate = DateTime.UtcNow.AddDays(-7);
            var endDate = DateTime.UtcNow;
            DateTime? capturedStart = null;
            DateTime? capturedEnd = null;

            _mockEvalRunTableService.Setup(x => x.GetEvalRunsByAgentIdAndDateFilterAsync(
                It.IsAny<string>(), It.IsAny<DateTime?>(), It.IsAny<DateTime?>()))
                .Callback<string, DateTime?, DateTime?>((_, start, end) =>
                {
                    capturedStart = start;
                    capturedEnd = end;
                })
                .ReturnsAsync(new List<EvalRunTableEntity>());

            // Act
            await _handler.GetEvalRunsByAgentIdAsync(agentId, startDate, endDate);

            // Assert
            capturedStart.Should().Be(startDate);
            capturedEnd.Should().Be(endDate);
        }

        #endregion

        #region GetEvalRunEntityByIdAsync Tests

        [Fact]
        public async Task GetEvalRunEntityByIdAsync_ReturnsEntity()
        {
            // Arrange
            var evalRunId = Guid.NewGuid();
            var entity = CreateTestEvalRunEntity();

            _mockEvalRunTableService.Setup(x => x.GetEvalRunByIdAsync(evalRunId))
                .ReturnsAsync(entity);

            // Act
            var result = await _handler.GetEvalRunEntityByIdAsync(evalRunId);

            // Assert
            result.Should().NotBeNull();
            result.Should().Be(entity);
        }

        [Fact]
        public async Task GetEvalRunEntityByIdAsync_WithNonExistentId_ReturnsNull()
        {
            // Arrange
            var evalRunId = Guid.NewGuid();

            _mockEvalRunTableService.Setup(x => x.GetEvalRunByIdAsync(evalRunId))
                .ReturnsAsync((EvalRunTableEntity?)null);

            // Act
            var result = await _handler.GetEvalRunEntityByIdAsync(evalRunId);

            // Assert
            result.Should().BeNull();
        }

        #endregion

        #region PlaceEnrichmentRequestToDataVerseAPI Tests

        [Fact]
        public async Task PlaceEnrichmentRequestToDataVerseAPI_WithValidId_SendsRequest()
        {
            // Arrange
            var evalRunId = Guid.NewGuid();
            var entity = CreateTestEvalRunEntity();

            _mockEvalRunTableService.Setup(x => x.GetEvalRunByIdAsync(evalRunId))
                .ReturnsAsync(entity);

            _mockDataVerseAPIService.Setup(x => x.PostEvalRunAsync(It.IsAny<DataVerseApiRequest>()))
                .ReturnsAsync(new DataVerseApiResponse { Success = true, StatusCode = 200 });

            // Act
            var result = await _handler.PlaceEnrichmentRequestToDataVerseAPI(evalRunId);

            // Assert
            result.IsSuccessfull.Should().BeTrue();
            result.HttpStatusCode.Should().Be("200");

            _mockDataVerseAPIService.Verify(x => x.PostEvalRunAsync(It.IsAny<DataVerseApiRequest>()), Times.Once);
        }

        [Fact]
        public async Task PlaceEnrichmentRequestToDataVerseAPI_WithNonExistentId_ReturnsNotFound()
        {
            // Arrange
            var evalRunId = Guid.NewGuid();

            _mockEvalRunTableService.Setup(x => x.GetEvalRunByIdAsync(evalRunId))
                .ReturnsAsync((EvalRunTableEntity?)null);

            // Act
            var result = await _handler.PlaceEnrichmentRequestToDataVerseAPI(evalRunId);

            // Assert
            result.IsSuccessfull.Should().BeFalse();
            result.HttpStatusCode.Should().Be(StatusCodes.Status404NotFound.ToString());
            result.Message.Should().Contain("not found");
        }

        [Fact]
        public async Task PlaceEnrichmentRequestToDataVerseAPI_WhenExceptionOccurs_ReturnsInternalServerError()
        {
            // Arrange
            var evalRunId = Guid.NewGuid();

            _mockEvalRunTableService.Setup(x => x.GetEvalRunByIdAsync(evalRunId))
                .ThrowsAsync(new Exception("Database error"));

            // Act
            var result = await _handler.PlaceEnrichmentRequestToDataVerseAPI(evalRunId);

            // Assert
            result.IsSuccessfull.Should().BeFalse();
            result.HttpStatusCode.Should().Be(StatusCodes.Status500InternalServerError.ToString());
        }

        #endregion

        #region GetAuditUser Tests

        [Theory]
        [InlineData(false, false, "test@example.com")]
        [InlineData(true, false, "TestApp")]
        public async Task CreateEvalRunAsync_SetsCorrectAuditUser_ForDifferentAuthFlows(
            bool isServicePrincipal, bool hasDelegatedUser, string expectedAuditUser)
        {
            // Arrange
            var createDto = CreateValidCreateEvalRunDto();
            var createdEntity = CreateTestEvalRunEntity();
            var datasetEntity = CreateTestDataSetEntity();
            var metricsEntity = CreateTestMetricsConfigEntity();
            EvalRunTableEntity? capturedEntity = null;

            _mockCallerService.Setup(x => x.IsServicePrincipalCall()).Returns(isServicePrincipal);
            _mockCallerService.Setup(x => x.HasDelegatedUserContext()).Returns(hasDelegatedUser);
            _mockCallerService.Setup(x => x.GetCurrentUserEmail()).Returns("test@example.com");
            _mockCallerService.Setup(x => x.GetCallingApplicationName()).Returns("TestApp");

            _mockEntityValidators.Setup(x => x.IsValidDatasetId(It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(true);
            _mockEntityValidators.Setup(x => x.IsValidMetricsConfigurationId(It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(true);

            _mockEvalRunTableService.Setup(x => x.CreateEvalRunAsync(It.IsAny<EvalRunTableEntity>()))
                .Callback<EvalRunTableEntity>(e => capturedEntity = e)
                .ReturnsAsync(createdEntity);

            _mockDataSetTableService.Setup(x => x.GetDataSetByIdAsync(It.IsAny<string>()))
                .ReturnsAsync(datasetEntity);

            _mockMetricsConfigTableService.Setup(x => x.GetMetricsConfigurationByConfigurationIdAsync(It.IsAny<string>()))
                .ReturnsAsync(metricsEntity);

            _mockDataVerseAPIService.Setup(x => x.PostEvalRunAsync(It.IsAny<DataVerseApiRequest>()))
                .ReturnsAsync(new DataVerseApiResponse { Success = true, StatusCode = 200 });

            // Act
            await _handler.CreateEvalRunAsync(createDto);

            // Assert
            capturedEntity.Should().NotBeNull();
            capturedEntity!.CreatedBy.Should().Be(expectedAuditUser);
        }

        [Fact]
        public async Task CreateEvalRunAsync_WhenGetCallerInfoThrows_UsesSystemAsAuditUser()
        {
            // Arrange
            var createDto = CreateValidCreateEvalRunDto();
            var createdEntity = CreateTestEvalRunEntity();
            var datasetEntity = CreateTestDataSetEntity();
            var metricsEntity = CreateTestMetricsConfigEntity();
            EvalRunTableEntity? capturedEntity = null;

            _mockCallerService.Setup(x => x.GetCallerInfo())
                .Throws(new Exception("Caller service error"));
            _mockCallerService.Setup(x => x.IsServicePrincipalCall())
                .Throws(new Exception("Caller service error"));

            _mockEntityValidators.Setup(x => x.IsValidDatasetId(It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(true);
            _mockEntityValidators.Setup(x => x.IsValidMetricsConfigurationId(It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(true);

            _mockEvalRunTableService.Setup(x => x.CreateEvalRunAsync(It.IsAny<EvalRunTableEntity>()))
                .Callback<EvalRunTableEntity>(e => capturedEntity = e)
                .ReturnsAsync(createdEntity);

            _mockDataSetTableService.Setup(x => x.GetDataSetByIdAsync(It.IsAny<string>()))
                .ReturnsAsync(datasetEntity);

            _mockMetricsConfigTableService.Setup(x => x.GetMetricsConfigurationByConfigurationIdAsync(It.IsAny<string>()))
                .ReturnsAsync(metricsEntity);

            _mockDataVerseAPIService.Setup(x => x.PostEvalRunAsync(It.IsAny<DataVerseApiRequest>()))
                .ReturnsAsync(new DataVerseApiResponse { Success = true, StatusCode = 200 });

            // Act
            await _handler.CreateEvalRunAsync(createDto);

            // Assert
            capturedEntity.Should().NotBeNull();
            capturedEntity!.CreatedBy.Should().Be("System");
        }

        #endregion

        #region Helper Methods

        private CreateEvalRunDto CreateValidCreateEvalRunDto()
        {
            return new CreateEvalRunDto
            {
                AgentId = "agent-123",
                DataSetId = Guid.NewGuid(),
                MetricsConfigurationId = Guid.NewGuid(),
                Type = "Standard",
                EnvironmentId = "dev",
                AgentSchemaName = "TestAgent",
                EvalRunName = "Test Eval Run"
            };
        }

        private EvalRunTableEntity CreateTestEvalRunEntity()
        {
            var evalRunId = Guid.NewGuid();
            var entity = new EvalRunTableEntity
            {
                EvalRunId = evalRunId,
                AgentId = "agent-123",
                DataSetId = Guid.NewGuid().ToString(),
                MetricsConfigurationId = Guid.NewGuid().ToString(),
                Status = CommonConstants.EvalRunStatus.RequestSubmitted,
                StartedDatetime = DateTime.UtcNow,
                ContainerName = "agent123",
                BlobFilePath = "eval-results/",
                Type = "Standard",
                EnvironmentId = "dev",
                AgentSchemaName = "TestAgent",
                EvalRunName = "Test Eval Run",
                CreatedBy = "test@example.com",
                CreatedOn = DateTime.UtcNow,
                LastUpdatedBy = "test@example.com",
                LastUpdatedOn = DateTime.UtcNow
            };
            entity.PartitionKey = entity.AgentId;
            entity.RowKey = evalRunId.ToString();
            return entity;
        }

        private DataSetTableEntity CreateTestDataSetEntity()
        {
            return new DataSetTableEntity
            {
                DatasetId = Guid.NewGuid().ToString(),
                AgentId = "agent-123",
                DatasetName = "Test Dataset",
                DatasetType = "Golden",
                ContainerName = "agent123",
                BlobFilePath = "datasets/test.json",
                CreatedBy = "test@example.com",
                CreatedOn = DateTime.UtcNow,
                LastUpdatedBy = "test@example.com",
                LastUpdatedOn = DateTime.UtcNow
            };
        }

        private MetricsConfigurationTableEntity CreateTestMetricsConfigEntity()
        {
            return new MetricsConfigurationTableEntity
            {
                ConfigurationId = Guid.NewGuid().ToString(),
                AgentId = "agent-123",
                ConfigurationName = "Test Metrics Config",
                EnvironmentName = "dev",
                ContainerName = "agent123",
                BlobFilePath = "metrics-configs/test.json",
                CreatedBy = "test@example.com",
                CreatedOn = DateTime.UtcNow,
                LastUpdatedBy = "test@example.com",
                LastUpdatedOn = DateTime.UtcNow
            };
        }

        #endregion
    }
}
