using AutoMapper;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;
using Sxg.EvalPlatform.API.Storage;
using Sxg.EvalPlatform.API.Storage.Entities;
using Sxg.EvalPlatform.API.Storage.Services;
using Sxg.EvalPlatform.API.Storage.TableEntities;
using SXG.EvalPlatform.Common.Exceptions;
using SxgEvalPlatformApi.Controllers;
using SxgEvalPlatformApi.Models.Dtos;
using SxgEvalPlatformApi.RequestHandlers;
using SxgEvalPlatformApi.Services;
using System.Text.Json;

namespace Sxg.EvalPlatform.API.UnitTests.RequestHandlerTests
{
    /// <summary>
    /// Unit Tests for MetricsConfigurationRequestHandler class.
    /// </summary>
    public class MetricsConfigurationRequestHandlerUnitTests
    {
        private readonly Mock<IMetricsConfigTableService> _mockMetricsConfigTableService;
        private readonly Mock<IAzureBlobStorageService> _mockBlobStorageService;
        private readonly Mock<IConfigHelper> _mockConfigHelper;
        private readonly Mock<ILogger<MetricsConfigurationRequestHandler>> _mockLogger;
        private readonly Mock<IMapper> _mockMapper;
        private readonly Mock<ICacheManager> _mockCacheManager;
        private readonly Mock<ICallerIdentificationService> _mockCallerService;
        private readonly Mock<IHttpContextAccessor> _mockHttpContextAccessor;
        private readonly MetricsConfigurationRequestHandler _handler;

        public MetricsConfigurationRequestHandlerUnitTests()
        {
            _mockMetricsConfigTableService = new Mock<IMetricsConfigTableService>();
            _mockBlobStorageService = new Mock<IAzureBlobStorageService>();
            _mockConfigHelper = new Mock<IConfigHelper>();
            _mockLogger = new Mock<ILogger<MetricsConfigurationRequestHandler>>();
            _mockMapper = new Mock<IMapper>();
            _mockCacheManager = new Mock<ICacheManager>();
            _mockCallerService = new Mock<ICallerIdentificationService>();
            _mockHttpContextAccessor = new Mock<IHttpContextAccessor>();

            // Setup default behaviors
            _mockConfigHelper.Setup(x => x.GetPlatformConfigurationsContainer()).Returns("platform-configs");
            _mockConfigHelper.Setup(x => x.GetDefaultMetricsConfiguration()).Returns("default-metrics.json");
            _mockConfigHelper.Setup(x => x.GetMetricsConfigurationsFolderName()).Returns("metrics-configs");

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

            _handler = new MetricsConfigurationRequestHandler(
                _mockMetricsConfigTableService.Object,
                _mockBlobStorageService.Object,
                _mockLogger.Object,
                _mockMapper.Object,
                _mockConfigHelper.Object,
                _mockCacheManager.Object,
                _mockCallerService.Object,
                _mockHttpContextAccessor.Object
            );
        }

        #region GetDefaultMetricsConfigurationAsync Tests

        [Fact]
        public async Task GetDefaultMetricsConfigurationAsync_ReturnsConfiguration_Successfully()
        {
            // Arrange
            var defaultMetrics = new DefaultMetricsConfiguration
            {
                Version = "1.0",
                LastUpdated = DateTime.UtcNow,
                Categories = new List<Category>
                {
                    new Category { CategoryName = "Quality" }
                }
            };
            var blobContent = JsonSerializer.Serialize(defaultMetrics, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            _mockBlobStorageService
                .Setup(x => x.ReadBlobContentAsync("platform-configs", "default-metrics.json"))
                .ReturnsAsync(blobContent);

            // Act
            var result = await _handler.GetDefaultMetricsConfigurationAsync();

            // Assert
            result.Should().NotBeNull();
            result.Categories.Should().HaveCount(1);
        }

        [Fact]
        public async Task GetDefaultMetricsConfigurationAsync_WithWrappedJson_DeserializesCorrectly()
        {
            // Arrange
            var wrappedJson = @"{
                ""metricConfiguration"": {
                    ""categories"": [
                        { ""categoryName"": ""Quality"", ""metrics"": [] }
                    ]
                }
            }";

            _mockBlobStorageService
                .Setup(x => x.ReadBlobContentAsync(It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(wrappedJson);

            // Act
            var result = await _handler.GetDefaultMetricsConfigurationAsync();

            // Assert
            result.Should().NotBeNull();
            result.Categories.Should().HaveCount(1);
        }

        [Fact]
        public async Task GetDefaultMetricsConfigurationAsync_WhenBlobNotFound_ThrowsException()
        {
            // Arrange
            _mockBlobStorageService
                .Setup(x => x.ReadBlobContentAsync(It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync((string?)null);

            // Act & Assert
            await Assert.ThrowsAsync<Exception>(() => _handler.GetDefaultMetricsConfigurationAsync());
        }

        [Fact]
        public async Task GetDefaultMetricsConfigurationAsync_WithInvalidJson_ThrowsDeserializationException()
        {
            // Arrange
            _mockBlobStorageService
                .Setup(x => x.ReadBlobContentAsync(It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync("{ invalid json }");

            // Act & Assert
            await Assert.ThrowsAsync<DeserializationException>(() => _handler.GetDefaultMetricsConfigurationAsync());
        }

        #endregion

        #region GetAllMetricsConfigurationsByAgentIdAndEnvironmentAsync Tests

        [Fact]
        public async Task GetAllMetricsConfigurations_WithValidAgentId_ReturnsConfigurations()
        {
            // Arrange
            var agentId = "agent-123";
            var entities = new List<MetricsConfigurationTableEntity>
            {
                CreateTestEntity(),
                CreateTestEntity()
            };

            // Setup mock to handle the call with agentId and default empty string for environmentName
            _mockMetricsConfigTableService
                .Setup(x => x.GetAllMetricsConfigurations(agentId, ""))
                .ReturnsAsync(entities);

            _mockMapper
                .Setup(x => x.Map<MetricsConfigurationMetadataDto>(It.IsAny<MetricsConfigurationTableEntity>()))
                .Returns((MetricsConfigurationTableEntity e) => new MetricsConfigurationMetadataDto 
                { 
                    AgentId = e.AgentId,
                    ConfigurationId = e.ConfigurationId,
                    ConfigurationName = e.ConfigurationName,
                    EnvironmentName = e.EnvironmentName
                });

            // Act
            var result = await _handler.GetAllMetricsConfigurationsByAgentIdAndEnvironmentAsync(agentId);

            // Assert
            result.Should().NotBeNull();
            result.Should().HaveCount(2);
        }

        [Fact]
        public async Task GetAllMetricsConfigurations_WithEnvironmentFilter_ReturnsFilteredResults()
        {
            // Arrange
            var agentId = "agent-123";
            var environment = "dev";
            var entities = new List<MetricsConfigurationTableEntity>
            {
                CreateTestEntity("config1", environment),
                CreateTestEntity("config2", "prod")
            };

            // Setup mock to handle the call with only agentId (implementation filters in-memory)
            // Must explicitly pass empty string for the optional parameter to avoid CS0854
            _mockMetricsConfigTableService
                .Setup(x => x.GetAllMetricsConfigurations(agentId, ""))
                .ReturnsAsync(entities);

            _mockMapper
                .Setup(x => x.Map<MetricsConfigurationMetadataDto>(It.IsAny<MetricsConfigurationTableEntity>()))
                .Returns((MetricsConfigurationTableEntity e) => new MetricsConfigurationMetadataDto
                {
                    AgentId = agentId,
                    EnvironmentName = e.EnvironmentName
                });

            // Act
            var result = await _handler.GetAllMetricsConfigurationsByAgentIdAndEnvironmentAsync(agentId, environment);

            // Assert
            result.Should().HaveCount(1);
            result.All(x => x.EnvironmentName == environment).Should().BeTrue();
        }

        #endregion

        #region GetMetricsConfigurationByConfigurationIdAsync Tests

        [Fact]
        public async Task GetMetricsConfigurationById_WithValidId_ReturnsConfiguration()
        {
            // Arrange
            var configId = Guid.NewGuid().ToString();
            var entity = CreateTestEntity(configId);
            var metricsConfig = new List<SelectedMetricsConfiguration>
            {
                new SelectedMetricsConfiguration { MetricName = "Accuracy", Threshold = 0.85 }
            };

            _mockMetricsConfigTableService
                .Setup(x => x.GetMetricsConfigurationByConfigurationIdAsync(configId))
                .ReturnsAsync(entity);

            _mockBlobStorageService
                .Setup(x => x.ReadBlobContentAsync(entity.ContainerName, entity.BlobFilePath))
                .ReturnsAsync(JsonSerializer.Serialize(metricsConfig));

            // Act
            var result = await _handler.GetMetricsConfigurationByConfigurationIdAsync(configId);

            // Assert
            result.Should().NotBeNull();
            result.Should().HaveCount(1);
            result![0].MetricName.Should().Be("Accuracy");
        }

        [Fact]
        public async Task GetMetricsConfigurationById_WithNonExistentId_ReturnsNull()
        {
            // Arrange
            var configId = Guid.NewGuid().ToString();

            _mockMetricsConfigTableService
                .Setup(x => x.GetMetricsConfigurationByConfigurationIdAsync(configId))
                .ReturnsAsync((MetricsConfigurationTableEntity?)null);

            // Act
            var result = await _handler.GetMetricsConfigurationByConfigurationIdAsync(configId);

            // Assert
            result.Should().BeNull();
        }

        [Fact]
        public async Task GetMetricsConfigurationById_WithWrappedFormat_DeserializesCorrectly()
        {
            // Arrange
            var configId = Guid.NewGuid().ToString();
            var entity = CreateTestEntity(configId);
            var wrappedJson = @"{
                ""metricsConfiguration"": [
                    { ""metricName"": ""Accuracy"", ""threshold"": 0.85 }
                ]
            }";

            _mockMetricsConfigTableService
                .Setup(x => x.GetMetricsConfigurationByConfigurationIdAsync(configId))
                .ReturnsAsync(entity);

            _mockBlobStorageService
                .Setup(x => x.ReadBlobContentAsync(entity.ContainerName, entity.BlobFilePath))
                .ReturnsAsync(wrappedJson);

            // Act
            var result = await _handler.GetMetricsConfigurationByConfigurationIdAsync(configId);

            // Assert
            result.Should().NotBeNull();
            result.Should().HaveCount(1);
        }

        #endregion

        #region CreateConfigurationAsync Tests

        [Fact]
        public async Task CreateConfiguration_WithNewConfig_CreatesSuccessfully()
        {
            // Arrange
            var createDto = CreateValidCreateConfigDto();
            var savedEntity = CreateTestEntity();

            _mockMetricsConfigTableService
                .Setup(x => x.GetAllMetricsConfigurations(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(new List<MetricsConfigurationTableEntity>());

            _mockBlobStorageService
                .Setup(x => x.WriteBlobContentAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(true);

            _mockMetricsConfigTableService
                .Setup(x => x.SaveMetricsConfigurationAsync(It.IsAny<MetricsConfigurationTableEntity>()))
                .ReturnsAsync(savedEntity);

            // Act
            var result = await _handler.CreateConfigurationAsync(createDto);

            // Assert
            result.Should().NotBeNull();
            result.Status.Should().Be("success");
            result.Message.Should().Contain("created successfully");
        }

        [Fact]
        public async Task CreateConfiguration_WithExistingConfig_UpdatesSuccessfully()
        {
            // Arrange
            var createDto = CreateValidCreateConfigDto();
            var existingEntity = CreateTestEntity();

            _mockMetricsConfigTableService
                .Setup(x => x.GetAllMetricsConfigurations(createDto.AgentId, createDto.ConfigurationName, createDto.EnvironmentName))
                .ReturnsAsync(new List<MetricsConfigurationTableEntity> { existingEntity });

            _mockBlobStorageService
                .Setup(x => x.WriteBlobContentAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(true);

            _mockMetricsConfigTableService
                .Setup(x => x.SaveMetricsConfigurationAsync(It.IsAny<MetricsConfigurationTableEntity>()))
                .ReturnsAsync(existingEntity);

            // Act
            var result = await _handler.CreateConfigurationAsync(createDto);

            // Assert
            result.Should().NotBeNull();
            result.Status.Should().Be("success");
            result.Message.Should().Contain("updated successfully");
        }

        [Fact]
        public async Task CreateConfiguration_WithMissingAgentId_ThrowsDataValidationException()
        {
            // Arrange
            var createDto = CreateValidCreateConfigDto();
            createDto.AgentId = string.Empty;

            // Act & Assert
            var result = await _handler.CreateConfigurationAsync(createDto);
            result.Status.Should().Be("error");
            result.Message.Should().Contain("AgentId is required");
        }

        [Fact]
        public async Task CreateConfiguration_WithMissingConfigurationName_ThrowsException()
        {
            // Arrange
            var createDto = CreateValidCreateConfigDto();
            createDto.ConfigurationName = string.Empty;

            // Act & Assert
            var result = await _handler.CreateConfigurationAsync(createDto);
            result.Status.Should().Be("error");
        }

        [Fact]
        public async Task CreateConfiguration_WithEmptyMetrics_ThrowsException()
        {
            // Arrange
            var createDto = CreateValidCreateConfigDto();
            createDto.MetricsConfiguration = new List<SelectedMetricsConfigurationDto>();

            // Act & Assert
            var result = await _handler.CreateConfigurationAsync(createDto);
            result.Status.Should().Be("error");
        }

        #endregion

        #region UpdateConfigurationAsync Tests

        [Fact]
        public async Task UpdateConfiguration_WithValidId_UpdatesSuccessfully()
        {
            // Arrange
            var configId = Guid.NewGuid().ToString();
            var updateDto = CreateValidCreateConfigDto();
            var existingEntity = CreateTestEntity(configId);

            _mockMetricsConfigTableService
                .Setup(x => x.GetMetricsConfigurationByConfigurationIdAsync(configId))
                .ReturnsAsync(existingEntity);

            _mockBlobStorageService
                .Setup(x => x.WriteBlobContentAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(true);

            _mockMetricsConfigTableService
                .Setup(x => x.SaveMetricsConfigurationAsync(It.IsAny<MetricsConfigurationTableEntity>()))
                .ReturnsAsync(existingEntity);

            // Act
            var result = await _handler.UpdateConfigurationAsync(configId, updateDto);

            // Assert
            result.Should().NotBeNull();
            result.Status.Should().Be("success");
            result.Message.Should().Contain("updated successfully");
        }

        [Fact]
        public async Task UpdateConfiguration_WithNonExistentId_ReturnsNotFound()
        {
            // Arrange
            var configId = Guid.NewGuid().ToString();
            var updateDto = CreateValidCreateConfigDto();

            _mockMetricsConfigTableService
                .Setup(x => x.GetMetricsConfigurationByConfigurationIdAsync(configId))
                .ReturnsAsync((MetricsConfigurationTableEntity?)null);

            // Act
            var result = await _handler.UpdateConfigurationAsync(configId, updateDto);

            // Assert
            result.Should().NotBeNull();
            result.Status.Should().Be("not_found");
            result.Message.Should().Contain("not found");
        }

        #endregion

        #region DeleteConfigurationAsync Tests

        [Fact]
        public async Task DeleteConfiguration_WithValidId_DeletesSuccessfully()
        {
            // Arrange
            var configId = Guid.NewGuid().ToString();
            var entity = CreateTestEntity(configId);

            _mockMetricsConfigTableService
                .Setup(x => x.GetMetricsConfigurationByConfigurationIdAsync(configId))
                .ReturnsAsync(entity);

            _mockMetricsConfigTableService
                .Setup(x => x.DeleteMetricsConfigurationByIdAsync(entity.AgentId, configId))
                .ReturnsAsync(true);

            _mockBlobStorageService
                .Setup(x => x.BlobExistsAsync(entity.ContainerName, entity.BlobFilePath))
                .ReturnsAsync(true);

            _mockBlobStorageService
                .Setup(x => x.DeleteBlobAsync(entity.ContainerName, entity.BlobFilePath))
                .ReturnsAsync(true);

            // Act
            var result = await _handler.DeleteConfigurationAsync(configId);

            // Assert
            result.Should().BeTrue();
        }

        [Fact]
        public async Task DeleteConfiguration_WithNonExistentId_ReturnsFalse()
        {
            // Arrange
            var configId = Guid.NewGuid().ToString();

            _mockMetricsConfigTableService
                .Setup(x => x.GetMetricsConfigurationByConfigurationIdAsync(configId))
                .ReturnsAsync((MetricsConfigurationTableEntity?)null);

            // Act
            var result = await _handler.DeleteConfigurationAsync(configId);

            // Assert
            result.Should().BeFalse();
        }

        [Fact]
        public async Task DeleteConfiguration_WhenBlobDeleteFails_StillReturnsTrue()
        {
            // Arrange
            var configId = Guid.NewGuid().ToString();
            var entity = CreateTestEntity(configId);

            _mockMetricsConfigTableService
                .Setup(x => x.GetMetricsConfigurationByConfigurationIdAsync(configId))
                .ReturnsAsync(entity);

            _mockMetricsConfigTableService
                .Setup(x => x.DeleteMetricsConfigurationByIdAsync(entity.AgentId, configId))
                .ReturnsAsync(true);

            _mockBlobStorageService
                .Setup(x => x.BlobExistsAsync(entity.ContainerName, entity.BlobFilePath))
                .ReturnsAsync(true);

            _mockBlobStorageService
                .Setup(x => x.DeleteBlobAsync(entity.ContainerName, entity.BlobFilePath))
                .ThrowsAsync(new Exception("Blob delete failed"));

            // Act
            var result = await _handler.DeleteConfigurationAsync(configId);

            // Assert
            result.Should().BeTrue(); // Table deletion succeeded
        }

        #endregion

        #region GetAuditUser Tests

        [Theory]
        [InlineData(false, false, "test@example.com")]
        [InlineData(true, false, "TestApp")]
        public async Task CreateConfiguration_SetsCorrectAuditUser_ForDifferentAuthFlows(
            bool isServicePrincipal, bool hasDelegatedUser, string expectedAuditUser)
        {
            // Arrange
            var createDto = CreateValidCreateConfigDto();
            MetricsConfigurationTableEntity? capturedEntity = null;

            _mockCallerService.Setup(x => x.IsServicePrincipalCall()).Returns(isServicePrincipal);
            _mockCallerService.Setup(x => x.HasDelegatedUserContext()).Returns(hasDelegatedUser);
            _mockCallerService.Setup(x => x.GetCurrentUserEmail()).Returns("test@example.com");
            _mockCallerService.Setup(x => x.GetCallingApplicationName()).Returns("TestApp");

            _mockMetricsConfigTableService
                .Setup(x => x.GetAllMetricsConfigurations(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(new List<MetricsConfigurationTableEntity>());

            // Setup mapper to not modify CreatedOn (mapper should not touch audit fields)
            _mockMapper
                .Setup(x => x.Map(It.IsAny<CreateConfigurationRequestDto>(), It.IsAny<MetricsConfigurationTableEntity>()))
                .Returns((CreateConfigurationRequestDto src, MetricsConfigurationTableEntity dest) =>
                {
                    dest.AgentId = src.AgentId;
                    dest.ConfigurationName = src.ConfigurationName;
                    dest.EnvironmentName = src.EnvironmentName;
                    dest.Description = src.Description;
                    // Don't set CreatedOn here - let SetAudit handle it
                    dest.CreatedOn = null; // Reset CreatedOn so SetAudit recognizes it as new
                    return dest;
                });

            _mockBlobStorageService
                .Setup(x => x.WriteBlobContentAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(true);

            _mockMetricsConfigTableService
                .Setup(x => x.SaveMetricsConfigurationAsync(It.IsAny<MetricsConfigurationTableEntity>()))
                .Callback<MetricsConfigurationTableEntity>(e => capturedEntity = e)
                .ReturnsAsync((MetricsConfigurationTableEntity e) => e);

            // Act
            await _handler.CreateConfigurationAsync(createDto);

            // Assert
            capturedEntity.Should().NotBeNull();
            // SetAudit will call SetCreationAudit when CreatedOn is null
            capturedEntity!.CreatedBy.Should().Be(expectedAuditUser);
            capturedEntity.LastUpdatedBy.Should().Be(expectedAuditUser);
        }

        #endregion

        #region Helper Methods

        private CreateConfigurationRequestDto CreateValidCreateConfigDto()
        {
            return new CreateConfigurationRequestDto
            {
                AgentId = "agent-123",
                ConfigurationName = "Test Config",
                EnvironmentName = "dev",
                MetricsConfiguration = new List<SelectedMetricsConfigurationDto>
                {
                    new SelectedMetricsConfigurationDto { MetricName = "Accuracy", Threshold = 0.85 }
                }
            };
        }

        private MetricsConfigurationTableEntity CreateTestEntity(string? configId = null, string environment = "dev")
        {
            var entity = new MetricsConfigurationTableEntity
            {
                ConfigurationId = configId ?? Guid.NewGuid().ToString(),
                AgentId = "agent-123",
                ConfigurationName = "Test Config",
                EnvironmentName = environment,
                ContainerName = "agent123",
                BlobFilePath = "metrics-configs/TestConfig_dev_123.json",
                CreatedBy = "test@example.com",
                CreatedOn = DateTime.UtcNow,
                LastUpdatedBy = "test@example.com",
                LastUpdatedOn = DateTime.UtcNow
            };
            entity.PartitionKey = entity.AgentId;
            entity.RowKey = entity.ConfigurationId;
            return entity;
        }

        #endregion
    }
}
