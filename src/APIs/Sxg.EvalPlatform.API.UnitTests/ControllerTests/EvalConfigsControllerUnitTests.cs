using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Sxg.EvalPlatform.API.Storage.Entities;
using Sxg.EvalPlatform.API.UnitTests.RequestHandlerTests;
using SXG.EvalPlatform.Common.Exceptions;
using SxgEvalPlatformApi.Controllers;
using SxgEvalPlatformApi.Models.Dtos;
using SxgEvalPlatformApi.RequestHandlers;
using SxgEvalPlatformApi.Services;

namespace Sxg.EvalPlatform.API.UnitTests.ControllerTests
{
    /// <summary>
    /// Comprehensive unit tests for EvalConfigsController.
    /// Tests all CRUD operations for metrics configurations.
    /// </summary>
    public class EvalConfigsControllerUnitTests : ControllerTestBase<EvalConfigsController>
    {
        private readonly Mock<IMetricsConfigurationRequestHandler> _mockMetricsConfigHandler;
        private readonly Mock<IConfiguration> _mockConfiguration;
        private readonly EvalConfigsController _controller;

        public EvalConfigsControllerUnitTests()
        {
            _mockMetricsConfigHandler = new Mock<IMetricsConfigurationRequestHandler>();
            _mockConfiguration = new Mock<IConfiguration>();

            _controller = new EvalConfigsController(
                _mockMetricsConfigHandler.Object,
                _mockConfiguration.Object,
                MockLogger.Object,
                MockCallerService.Object,
                MockTelemetryService.Object
            );

            SetupControllerContext(_controller);
        }

        #region GetDefaultMetricsConfiguration Tests

        [Fact]
        public async Task GetDefaultMetricsConfiguration_ReturnsOkWithConfiguration()
        {
            // Arrange
            var defaultConfig = CreateTestDefaultMetricsConfiguration();

            _mockMetricsConfigHandler
                .Setup(x => x.GetDefaultMetricsConfigurationAsync())
                .ReturnsAsync(defaultConfig);

            // Act
            var result = await _controller.GetDefaultMetricsConfiguration();

            // Assert
            // Note: This endpoint returns the value directly, not wrapped in OkObjectResult
            result.Should().NotBeNull();
            result.Value.Should().NotBeNull();
            result.Value!.Categories.Should().HaveCount(2);
            result.Value.Version.Should().Be("1.0");
        }

        [Fact]
        public async Task GetDefaultMetricsConfiguration_WhenExceptionOccurs_ReturnsInternalServerError()
        {
            // Arrange
            _mockMetricsConfigHandler
                .Setup(x => x.GetDefaultMetricsConfigurationAsync())
                .ThrowsAsync(new Exception(TestConstants.ErrorMessages.BlobReadError));

            // Act
            var result = await _controller.GetDefaultMetricsConfiguration();

            // Assert
            VerifyStatusCodeResult(result, 500);
        }

        [Fact]
        public async Task GetDefaultMetricsConfiguration_LogsInformationMessages()
        {
            // Arrange
            var defaultConfig = CreateTestDefaultMetricsConfiguration();
            _mockMetricsConfigHandler
                .Setup(x => x.GetDefaultMetricsConfigurationAsync())
                .ReturnsAsync(defaultConfig);

            // Act
            await _controller.GetDefaultMetricsConfiguration();

            // Assert
            MockLogger.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("retrieve default Metrics configuration")),
                    null,
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.AtLeastOnce);
        }

        #endregion

        #region GetConfigurationsByMetricsConfigurationId Tests

        [Fact]
        public async Task GetConfigurationsByMetricsConfigurationId_WithValidId_ReturnsOkWithConfigurations()
        {
            // Arrange
            var configId = Guid.NewGuid();
            var configurations = new List<SelectedMetricsConfiguration>
            {
                CreateTestSelectedMetricsConfiguration(TestConstants.Metrics.Accuracy),
                CreateTestSelectedMetricsConfiguration(TestConstants.Metrics.Precision)
            };

            _mockMetricsConfigHandler
                .Setup(x => x.GetMetricsConfigurationByConfigurationIdAsync(configId.ToString()))
                .ReturnsAsync(configurations);

            // Act
            var result = await _controller.GetConfigurationsByMetricsConfigurationId(configId);

            // Assert
            VerifyOkResult(result, value =>
            {
                value.Should().HaveCount(2);
                value.Should().Contain(c => c.MetricName == TestConstants.Metrics.Accuracy);
            });
        }

        [Fact]
        public async Task GetConfigurationsByMetricsConfigurationId_WithNonExistentId_ReturnsNotFound()
        {
            // Arrange
            var configId = Guid.NewGuid();

            _mockMetricsConfigHandler
                .Setup(x => x.GetMetricsConfigurationByConfigurationIdAsync(configId.ToString()))
                .ReturnsAsync((IList<SelectedMetricsConfiguration>?)null);

            // Act
            var result = await _controller.GetConfigurationsByMetricsConfigurationId(configId);

            // Assert
            VerifyNotFoundResult(result, "No configurations found");
        }

        [Fact]
        public async Task GetConfigurationsByMetricsConfigurationId_WithEmptyConfigurations_ReturnsNotFound()
        {
            // Arrange
            var configId = Guid.NewGuid();

            _mockMetricsConfigHandler
                .Setup(x => x.GetMetricsConfigurationByConfigurationIdAsync(configId.ToString()))
                .ReturnsAsync(new List<SelectedMetricsConfiguration>());

            // Act
            var result = await _controller.GetConfigurationsByMetricsConfigurationId(configId);

            // Assert
            VerifyNotFoundResult(result);
        }

        [Fact]
        public async Task GetConfigurationsByMetricsConfigurationId_WhenExceptionOccurs_ReturnsInternalServerError()
        {
            // Arrange
            var configId = Guid.NewGuid();

            _mockMetricsConfigHandler
                .Setup(x => x.GetMetricsConfigurationByConfigurationIdAsync(configId.ToString()))
                .ThrowsAsync(new Exception(TestConstants.ErrorMessages.DatabaseError));

            // Act
            var result = await _controller.GetConfigurationsByMetricsConfigurationId(configId);

            // Assert
            VerifyStatusCodeResult(result, 500);
        }

        #endregion

        #region GetConfigurationsByAgentId Tests

        [Fact]
        public async Task GetConfigurationsByAgentId_WithValidAgentId_ReturnsOkWithConfigurations()
        {
            // Arrange
            var agentId = TestConstants.Agents.DefaultAgentId;
            var configurations = new List<MetricsConfigurationMetadataDto>
            {
                CreateTestMetricsConfigurationMetadata("config-1"),
                CreateTestMetricsConfigurationMetadata("config-2")
            };

            _mockMetricsConfigHandler
                .Setup(x => x.GetAllMetricsConfigurationsByAgentIdAndEnvironmentAsync(agentId, ""))
                .ReturnsAsync(configurations);

            // Act
            var result = await _controller.GetConfigurationsByAgentId(agentId);

            // Assert
            VerifyOkResult(result, value =>
            {
                value.Should().HaveCount(2);
                value.All(c => c.AgentId == agentId).Should().BeTrue();
            });
        }

        [Fact]
        public async Task GetConfigurationsByAgentId_WithEnvironmentFilter_ReturnsFilteredConfigurations()
        {
            // Arrange
            var agentId = TestConstants.Agents.DefaultAgentId;
            var environment = TestConstants.Environments.Dev;
            var configurations = new List<MetricsConfigurationMetadataDto>
            {
                CreateTestMetricsConfigurationMetadata("config-1", environment)
            };

            _mockMetricsConfigHandler
                .Setup(x => x.GetAllMetricsConfigurationsByAgentIdAndEnvironmentAsync(agentId, environment))
                .ReturnsAsync(configurations);

            // Act
            var result = await _controller.GetConfigurationsByAgentId(agentId, environment);

            // Assert
            VerifyOkResult(result, value =>
            {
                value.Should().HaveCount(1);
                value.All(c => c.EnvironmentName == environment).Should().BeTrue();
            });
        }

        [Fact]
        public async Task GetConfigurationsByAgentId_WithNoConfigurations_ReturnsNotFound()
        {
            // Arrange
            var agentId = TestConstants.Agents.DefaultAgentId;

            _mockMetricsConfigHandler
                .Setup(x => x.GetAllMetricsConfigurationsByAgentIdAndEnvironmentAsync(agentId, ""))
                .ReturnsAsync(new List<MetricsConfigurationMetadataDto>());

            // Act
            var result = await _controller.GetConfigurationsByAgentId(agentId);

            // Assert
            VerifyNotFoundResult(result, "No configurations found");
        }

        [Fact]
        public async Task GetConfigurationsByAgentId_WithInvalidModelState_ReturnsBadRequest()
        {
            // Arrange
            var agentId = TestConstants.Agents.DefaultAgentId;
            SimulateInvalidModelState(_controller);

            // Act
            var result = await _controller.GetConfigurationsByAgentId(agentId);

            // Assert
            VerifyBadRequestResult(result);
        }

        [Fact]
        public async Task GetConfigurationsByAgentId_WhenExceptionOccurs_ReturnsInternalServerError()
        {
            // Arrange
            var agentId = TestConstants.Agents.DefaultAgentId;

            _mockMetricsConfigHandler
                .Setup(x => x.GetAllMetricsConfigurationsByAgentIdAndEnvironmentAsync(agentId, ""))
                .ThrowsAsync(new Exception(TestConstants.ErrorMessages.DatabaseError));

            // Act
            var result = await _controller.GetConfigurationsByAgentId(agentId);

            // Assert
            VerifyStatusCodeResult(result, 500);
        }

        #endregion

        #region CreateConfiguration Tests

        [Fact]
        public async Task CreateConfiguration_WithValidData_ReturnsCreated()
        {
            // Arrange
            var createDto = CreateValidCreateConfigurationDto();
            var response = new ConfigurationSaveResponseDto
            {
                ConfigurationId = Guid.NewGuid().ToString(),
                Status = TestConstants.ResponseStatus.Success,
                Message = "Configuration created successfully"
            };

            _mockMetricsConfigHandler
                .Setup(x => x.CreateConfigurationAsync(createDto))
                .ReturnsAsync(response);

            // Act
            var result = await _controller.CreateConfiguration(createDto);

            // Assert
            result.Should().NotBeNull();
            result.Result.Should().BeOfType<CreatedAtActionResult>();

            var createdResult = result.Result as CreatedAtActionResult;
            createdResult.Should().NotBeNull();
            createdResult!.Value.Should().BeEquivalentTo(response);
        }

        [Fact]
        public async Task CreateConfiguration_WithInvalidModelState_ReturnsBadRequest()
        {
            // Arrange
            var createDto = CreateValidCreateConfigurationDto();
            SimulateInvalidModelState(_controller);

            // Act
            var result = await _controller.CreateConfiguration(createDto);

            // Assert
            VerifyBadRequestResult(result);
        }

        [Fact]
        public async Task CreateConfiguration_WithErrorStatus_ReturnsInternalServerError()
        {
            // Arrange
            var createDto = CreateValidCreateConfigurationDto();
            var response = new ConfigurationSaveResponseDto
            {
                Status = TestConstants.ResponseStatus.Error,
                Message = "Failed to create configuration"
            };

            _mockMetricsConfigHandler
                .Setup(x => x.CreateConfigurationAsync(createDto))
                .ReturnsAsync(response);

            // Act
            var result = await _controller.CreateConfiguration(createDto);

            // Assert
            VerifyStatusCodeResult(result, 500);
        }

        [Fact]
        public async Task CreateConfiguration_WithDataValidationException_ReturnsBadRequest()
        {
            // Arrange
            var createDto = CreateValidCreateConfigurationDto();
            var validationException = new DataValidationException("Invalid metrics configuration");

            _mockMetricsConfigHandler
                .Setup(x => x.CreateConfigurationAsync(createDto))
                .ThrowsAsync(validationException);

            // Act
            var result = await _controller.CreateConfiguration(createDto);

            // Assert
            VerifyBadRequestResult(result);
        }

        [Fact]
        public async Task CreateConfiguration_WhenExceptionOccurs_ReturnsInternalServerError()
        {
            // Arrange
            var createDto = CreateValidCreateConfigurationDto();

            _mockMetricsConfigHandler
                .Setup(x => x.CreateConfigurationAsync(createDto))
                .ThrowsAsync(new Exception(TestConstants.ErrorMessages.DatabaseError));

            // Act
            var result = await _controller.CreateConfiguration(createDto);

            // Assert
            VerifyStatusCodeResult(result, 500);
        }

        [Theory]
        [InlineData(false, false)]  // DirectUser
        [InlineData(true, false)]   // ServicePrincipal
        public async Task CreateConfiguration_WithDifferentAuthFlows_Succeeds(
            bool isServicePrincipal, bool hasDelegatedUser)
        {
            // Arrange
            if (isServicePrincipal && !hasDelegatedUser)
                SetupServicePrincipalAuth();
            else
                SetupDirectUserAuth();

            var createDto = CreateValidCreateConfigurationDto();
            var response = new ConfigurationSaveResponseDto
            {
                ConfigurationId = Guid.NewGuid().ToString(),
                Status = TestConstants.ResponseStatus.Success
            };

            _mockMetricsConfigHandler
                .Setup(x => x.CreateConfigurationAsync(createDto))
                .ReturnsAsync(response);

            // Act
            var result = await _controller.CreateConfiguration(createDto);

            // Assert
            result.Should().NotBeNull();
            result.Result.Should().BeOfType<CreatedAtActionResult>();
        }

        #endregion

        #region UpdateConfiguration Tests

        [Fact]
        public async Task UpdateConfiguration_WithValidData_ReturnsOk()
        {
            // Arrange
            var configId = Guid.NewGuid();
            var updateDto = CreateValidCreateConfigurationDto();
            var response = new ConfigurationSaveResponseDto
            {
                ConfigurationId = configId.ToString(),
                Status = TestConstants.ResponseStatus.Success,
                Message = "Configuration updated successfully"
            };

            _mockMetricsConfigHandler
                .Setup(x => x.UpdateConfigurationAsync(configId.ToString(), updateDto))
                .ReturnsAsync(response);

            // Act
            var result = await _controller.UpdateConfiguration(configId, updateDto);

            // Assert
            VerifyOkResult(result, value =>
            {
                value.Status.Should().Be(TestConstants.ResponseStatus.Success);
                value.ConfigurationId.Should().Be(configId.ToString());
            });
        }

        [Fact]
        public async Task UpdateConfiguration_WithNonExistentId_ReturnsNotFound()
        {
            // Arrange
            var configId = Guid.NewGuid();
            var updateDto = CreateValidCreateConfigurationDto();
            var response = new ConfigurationSaveResponseDto
            {
                Status = TestConstants.ResponseStatus.NotFound,
                Message = "Configuration not found"
            };

            _mockMetricsConfigHandler
                .Setup(x => x.UpdateConfigurationAsync(configId.ToString(), updateDto))
                .ReturnsAsync(response);

            // Act
            var result = await _controller.UpdateConfiguration(configId, updateDto);

            // Assert
            VerifyStatusCodeResult(result, 404);
        }

        [Fact]
        public async Task UpdateConfiguration_WithInvalidModelState_ReturnsBadRequest()
        {
            // Arrange
            var configId = Guid.NewGuid();
            var updateDto = CreateValidCreateConfigurationDto();
            SimulateInvalidModelState(_controller);

            // Act
            var result = await _controller.UpdateConfiguration(configId, updateDto);

            // Assert
            VerifyBadRequestResult(result);
        }

        [Fact]
        public async Task UpdateConfiguration_WithErrorStatus_ReturnsInternalServerError()
        {
            // Arrange
            var configId = Guid.NewGuid();
            var updateDto = CreateValidCreateConfigurationDto();
            var response = new ConfigurationSaveResponseDto
            {
                Status = TestConstants.ResponseStatus.Error,
                Message = "Update failed"
            };

            _mockMetricsConfigHandler
                .Setup(x => x.UpdateConfigurationAsync(configId.ToString(), updateDto))
                .ReturnsAsync(response);

            // Act
            var result = await _controller.UpdateConfiguration(configId, updateDto);

            // Assert
            VerifyStatusCodeResult(result, 500);
        }

        [Fact]
        public async Task UpdateConfiguration_WithDataValidationException_ReturnsBadRequest()
        {
            // Arrange
            var configId = Guid.NewGuid();
            var updateDto = CreateValidCreateConfigurationDto();

            _mockMetricsConfigHandler
                .Setup(x => x.UpdateConfigurationAsync(configId.ToString(), updateDto))
                .ThrowsAsync(new DataValidationException("Invalid configuration data"));

            // Act
            var result = await _controller.UpdateConfiguration(configId, updateDto);

            // Assert
            VerifyBadRequestResult(result);
        }

        [Fact]
        public async Task UpdateConfiguration_WhenExceptionOccurs_ReturnsInternalServerError()
        {
            // Arrange
            var configId = Guid.NewGuid();
            var updateDto = CreateValidCreateConfigurationDto();

            _mockMetricsConfigHandler
                .Setup(x => x.UpdateConfigurationAsync(configId.ToString(), updateDto))
                .ThrowsAsync(new Exception(TestConstants.ErrorMessages.DatabaseError));

            // Act
            var result = await _controller.UpdateConfiguration(configId, updateDto);

            // Assert
            VerifyStatusCodeResult(result, 500);
        }

        #endregion

        #region DeleteConfiguration Tests

        [Fact]
        public async Task DeleteConfiguration_WithValidId_ReturnsOk()
        {
            // Arrange
            var configId = Guid.NewGuid();

            _mockMetricsConfigHandler
                .Setup(x => x.DeleteConfigurationAsync(configId.ToString()))
                .ReturnsAsync(true);

            // Act
            var result = await _controller.DeleteConfiguration(configId);

            // Assert
            VerifyOkResult(result);
        }

        [Fact]
        public async Task DeleteConfiguration_WithNonExistentId_ReturnsNotFound()
        {
            // Arrange
            var configId = Guid.NewGuid();

            _mockMetricsConfigHandler
                .Setup(x => x.DeleteConfigurationAsync(configId.ToString()))
                .ReturnsAsync(false);

            // Act
            var result = await _controller.DeleteConfiguration(configId);

            // Assert
            VerifyStatusCodeResult(result, 404);
        }

        [Fact]
        public async Task DeleteConfiguration_WhenExceptionOccurs_ReturnsInternalServerError()
        {
            // Arrange
            var configId = Guid.NewGuid();

            _mockMetricsConfigHandler
                .Setup(x => x.DeleteConfigurationAsync(configId.ToString()))
                .ThrowsAsync(new Exception(TestConstants.ErrorMessages.DatabaseError));

            // Act
            var result = await _controller.DeleteConfiguration(configId);

            // Assert
            VerifyStatusCodeResult(result, 500);
        }

        [Fact]
        public async Task DeleteConfiguration_LogsInformationMessages()
        {
            // Arrange
            var configId = Guid.NewGuid();

            _mockMetricsConfigHandler
                .Setup(x => x.DeleteConfigurationAsync(configId.ToString()))
                .ReturnsAsync(true);

            // Act
            await _controller.DeleteConfiguration(configId);

            // Assert
            MockLogger.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("delete configuration")),
                    null,
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.AtLeastOnce);
        }

        #endregion

        #region Telemetry Tests

        [Fact]
        public async Task GetDefaultMetricsConfiguration_CallsTelemetryService()
        {
            // Arrange
            _mockMetricsConfigHandler
                .Setup(x => x.GetDefaultMetricsConfigurationAsync())
                .ReturnsAsync(CreateTestDefaultMetricsConfiguration());

            // Act
            await _controller.GetDefaultMetricsConfiguration();

            // Assert
            MockTelemetryService.Verify(
                x => x.StartActivity("EvalConfigs.GetDefaultMetricsConfiguration"),
                Times.Once);
        }

        [Fact]
        public async Task CreateConfiguration_CallsTelemetryService()
        {
            // Arrange
            var createDto = CreateValidCreateConfigurationDto();
            _mockMetricsConfigHandler
                .Setup(x => x.CreateConfigurationAsync(createDto))
                .ReturnsAsync(new ConfigurationSaveResponseDto { Status = TestConstants.ResponseStatus.Success });

            // Act
            await _controller.CreateConfiguration(createDto);

            // Assert
            MockTelemetryService.Verify(
                x => x.StartActivity("EvalConfigs.CreateConfiguration"),
                Times.Once);
        }

        #endregion

        #region Helper Methods

        private CreateConfigurationRequestDto CreateValidCreateConfigurationDto()
        {
            return new CreateConfigurationRequestDto
            {
                AgentId = TestConstants.Agents.DefaultAgentId,
                ConfigurationName = TestConstants.MetricsConfigs.DefaultName,
                EnvironmentName = TestConstants.Environments.Dev,
                Description = "Test configuration",
                MetricsConfiguration = new List<SelectedMetricsConfigurationDto>
                {
                    new SelectedMetricsConfigurationDto
                    {
                        MetricName = TestConstants.Metrics.Accuracy,
                        Threshold = TestConstants.Metrics.DefaultThreshold
                    }
                }
            };
        }

        private DefaultMetricsConfiguration CreateTestDefaultMetricsConfiguration()
        {
            return new DefaultMetricsConfiguration
            {
                Version = "1.0",
                LastUpdated = DateTime.UtcNow,
                Categories = new List<Category>
                {
                    new Category
                    {
                        CategoryName = TestConstants.Categories.Quality,
                        Metrics = new List<Metric>
                        {
                            new Metric { MetricName = TestConstants.Metrics.Accuracy }
                        }
                    },
                    new Category
                    {
                        CategoryName = TestConstants.Categories.Performance,
                        Metrics = new List<Metric>
                        {
                            new Metric { MetricName = TestConstants.Metrics.Precision }
                        }
                    }
                }
            };
        }

        private SelectedMetricsConfiguration CreateTestSelectedMetricsConfiguration(string metricName)
        {
            return new SelectedMetricsConfiguration
            {
                MetricName = metricName,
                Threshold = TestConstants.Metrics.DefaultThreshold
            };
        }

        private MetricsConfigurationMetadataDto CreateTestMetricsConfigurationMetadata(
            string? configId = null,
            string environment = TestConstants.Environments.Dev)
        {
            return new MetricsConfigurationMetadataDto
            {
                ConfigurationId = configId ?? Guid.NewGuid().ToString(),
                AgentId = TestConstants.Agents.DefaultAgentId,
                ConfigurationName = TestConstants.MetricsConfigs.DefaultName,
                EnvironmentName = environment,
                CreatedBy = TestConstants.Users.DefaultEmail,
                CreatedOn = DateTime.UtcNow
            };
        }

        #endregion
    }
}
