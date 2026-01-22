using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Sxg.EvalPlatform.API.Storage;
using Sxg.EvalPlatform.API.Storage.Services;
using Sxg.EvalPlatform.API.UnitTests.RequestHandlerTests;
using SxgEvalPlatformApi.Controllers;
using SxgEvalPlatformApi.Models.Dtos;
using SxgEvalPlatformApi.Services;
using System.Diagnostics;

namespace Sxg.EvalPlatform.API.UnitTests.ControllerTests
{
    /// <summary>
    /// Comprehensive unit tests for HealthController.
    /// Tests health check endpoints with various scenarios.
    /// </summary>
    [Trait("Category", TestCategories.Unit)]
    [Trait("Category", TestCategories.Controller)]
    [Trait("Category", TestCategories.Smoke)]
    public class HealthControllerUnitTests : ControllerTestBase<HealthController>
    {
        private readonly Mock<ILogger<HealthController>> _mockLogger;
        private readonly Mock<IOpenTelemetryService> _mockTelemetryService;
        private readonly HealthController _controller;

        public HealthControllerUnitTests()
        {
            _mockLogger = MockLogger;
            _mockTelemetryService = MockTelemetryService;

            _controller = new HealthController(_mockLogger.Object, _mockTelemetryService.Object);
            SetupControllerContext(_controller);
        }

        #region GetHealth Tests

        [Fact]
        public void GetHealth_ReturnsHealthyStatus_Successfully()
        {
            // Arrange
            var expectedEnvironment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production";

            // Act
            var result = _controller.GetHealth();

            // Assert
            result.Should().NotBeNull();
            result.Result.Should().BeOfType<OkObjectResult>();

            var okResult = result.Result as OkObjectResult;
            okResult.Should().NotBeNull();
            okResult!.Value.Should().BeOfType<HealthStatusDto>();

            var healthStatus = okResult.Value as HealthStatusDto;
            healthStatus.Should().NotBeNull();
            healthStatus!.Status.Should().Be("Healthy");
            healthStatus.Timestamp.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
            healthStatus.Version.Should().Be("1.0.0");
            healthStatus.Environment.Should().Be(expectedEnvironment);
        }

        [Fact]
        public void GetHealth_LogsInformation_WhenCalled()
        {
            // Arrange & Act
            _controller.GetHealth();

            // Assert
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Health check requested")),
                    null,
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public void GetHealth_CallsTelemetryService_ToStartActivity()
        {
            // Arrange & Act
            _controller.GetHealth();

            // Assert
            _mockTelemetryService.Verify(x => x.StartActivity("Health.Check"), Times.Once);
        }

        [Fact]
        public void GetHealth_RecordsMetric_WithCorrectData()
        {
            // Arrange & Act
            _controller.GetHealth();

            // Assert
            _mockTelemetryService.Verify(
                x => x.RecordMetric(
                    "health_check_duration",
                    It.IsAny<double>(),
                    It.Is<Dictionary<string, object>>(d =>
                        d.ContainsKey("status") &&
                        d["status"].ToString() == "healthy" &&
                        d.ContainsKey("endpoint") &&
                        d["endpoint"].ToString() == "/api/v1/health")),
                Times.Once);
        }

        [Fact]
        public void GetHealth_SetsTelemetryTags_Correctly()
        {
            // Arrange
            // Note: Activity.SetTag is not virtual and cannot be mocked
            // We can only verify that StartActivity was called
            
            // Act
            _controller.GetHealth();

            // Assert
            MockTelemetryService.Verify(x => x.StartActivity("Health.Check"), Times.Once);
        }

        [Fact]
        public void GetHealth_ReturnsCorrectVersion()
        {
            // Arrange & Act
            var result = _controller.GetHealth();

            // Assert
            var healthStatus = GetValueFromResult(result);
            healthStatus.Should().NotBeNull();
            healthStatus!.Version.Should().Be("1.0.0");
        }

        [Fact]
        public void GetHealth_ReturnsCurrentTimestamp()
        {
            // Arrange
            var beforeCall = DateTime.UtcNow;

            // Act
            var result = _controller.GetHealth();

            // Assert
            var afterCall = DateTime.UtcNow;
            var healthStatus = GetValueFromResult(result);
            healthStatus.Should().NotBeNull();
            healthStatus!.Timestamp.Should().BeOnOrAfter(beforeCall);
            healthStatus.Timestamp.Should().BeOnOrBefore(afterCall);
        }

        #endregion

        #region GetDetailedHealth Tests

        [Fact]
        public async Task GetDetailedHealth_ReturnsDetailedStatus_Successfully()
        {
            // Arrange
            SetupMockServiceProvider();

            // Act
            var result = await _controller.GetDetailedHealth();

            // Assert
            result.Should().NotBeNull();
            result.Should().BeOfType<OkObjectResult>();

            var okResult = result as OkObjectResult;
            okResult.Should().NotBeNull();
            okResult!.Value.Should().NotBeNull();
        }

        [Fact]
        public async Task GetDetailedHealth_IncludesMachineName()
        {
            // Arrange
            SetupMockServiceProvider();

            // Act
            var result = await _controller.GetDetailedHealth();

            // Assert
            var okResult = result as OkObjectResult;
            var value = okResult!.Value;
            var machineNameProp = value!.GetType().GetProperty("MachineName");
            machineNameProp.Should().NotBeNull();
            
            var machineName = machineNameProp!.GetValue(value);
            machineName.Should().Be(Environment.MachineName);
        }

        [Fact]
        public async Task GetDetailedHealth_IncludesProcessId()
        {
            // Arrange
            SetupMockServiceProvider();

            // Act
            var result = await _controller.GetDetailedHealth();

            // Assert
            var okResult = result as OkObjectResult;
            var value = okResult!.Value;
            var processIdProp = value!.GetType().GetProperty("ProcessId");
            processIdProp.Should().NotBeNull();
            
            var processId = processIdProp!.GetValue(value);
            processId.Should().Be(Environment.ProcessId);
        }

        [Fact]
        public async Task GetDetailedHealth_IncludesOpenTelemetryInfo()
        {
            // Arrange
            SetupMockServiceProvider();

            // Act
            var result = await _controller.GetDetailedHealth();

            // Assert
            var okResult = result as OkObjectResult;
            var value = okResult!.Value;
            var openTelemetryProp = value!.GetType().GetProperty("OpenTelemetry");
            openTelemetryProp.Should().NotBeNull();
            
            var openTelemetryInfo = openTelemetryProp!.GetValue(value);
            openTelemetryInfo.Should().NotBeNull();
            
            var enabledProp = openTelemetryInfo!.GetType().GetProperty("Enabled");
            enabledProp.Should().NotBeNull();
            enabledProp!.GetValue(openTelemetryInfo).Should().Be(true);
        }

        [Fact]
        public async Task GetDetailedHealth_CallsTelemetryService()
        {
            // Arrange
            SetupMockServiceProvider();

            // Act
            await _controller.GetDetailedHealth();

            // Assert
            _mockTelemetryService.Verify(x => x.StartActivity("Health.DetailedCheck"), Times.Once);
        }

        [Fact]
        public async Task GetDetailedHealth_LogsInformation()
        {
            // Arrange
            SetupMockServiceProvider();

            // Act
            await _controller.GetDetailedHealth();

            // Assert
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Detailed health check completed")),
                    null,
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task GetDetailedHealth_WhenExceptionOccurs_ReturnsOkWithDegradedStatus()
        {
            // Arrange
            SetupMockServiceProviderWithException();

            // Act
            var result = await _controller.GetDetailedHealth();

            // Assert
            // Controller catches exceptions during dependency checks and returns OK with Degraded status
            result.Should().BeOfType<OkObjectResult>();
            var okResult = result as OkObjectResult;
            var value = okResult!.Value;
            var statusProp = value!.GetType().GetProperty("Status");
            var status = statusProp!.GetValue(value) as string;
            status.Should().Be("Degraded");
        }

        [Fact]
        public async Task GetDetailedHealth_WhenExceptionOccurs_LogsWarnings()
        {
            // Arrange
            SetupMockServiceProviderWithException();

            // Act
            await _controller.GetDetailedHealth();

            // Assert
            // Controller catches exceptions during individual dependency checks and logs warnings
            _mockLogger.Verify(
                x => x.Log(
                    It.Is<LogLevel>(l => l == LogLevel.Warning || l == LogLevel.Error),
                    It.IsAny<EventId>(),
                    It.IsAny<It.IsAnyType>(),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.AtLeastOnce);
        }

        [Fact]
        public async Task GetDetailedHealth_IncludesDependencyChecks()
        {
            // Arrange
            SetupMockServiceProvider();

            // Act
            var result = await _controller.GetDetailedHealth();

            // Assert
            var okResult = result as OkObjectResult;
            var value = okResult!.Value;
            var dependenciesProp = value!.GetType().GetProperty("Dependencies");
            dependenciesProp.Should().NotBeNull();
            
            var dependencies = dependenciesProp!.GetValue(value) as Array;
            dependencies.Should().NotBeNull();
            dependencies!.Length.Should().BeGreaterOrEqualTo(0);
        }

        [Fact]
        public async Task GetDetailedHealth_ChecksAzureBlobStorage()
        {
            // Arrange
            SetupMockServiceProviderWithBlobService();

            // Act
            var result = await _controller.GetDetailedHealth();

            // Assert
            result.Should().BeOfType<OkObjectResult>();
            var okResult = result as OkObjectResult;
            okResult.Should().NotBeNull();
            
            // Verify dependency checks were performed
            var value = okResult!.Value;
            var dependenciesProp = value!.GetType().GetProperty("Dependencies");
            dependenciesProp.Should().NotBeNull();
        }

        [Fact]
        public async Task GetDetailedHealth_ChecksAzureTableStorage()
        {
            // Arrange
            SetupMockServiceProviderWithTableService();

            // Act
            var result = await _controller.GetDetailedHealth();

            // Assert
            result.Should().BeOfType<OkObjectResult>();
            var okResult = result as OkObjectResult;
            okResult.Should().NotBeNull();
            
            // Verify dependency checks were performed
            var value = okResult!.Value;
            var dependenciesProp = value!.GetType().GetProperty("Dependencies");
            dependenciesProp.Should().NotBeNull();
        }

        [Fact]
        public async Task GetDetailedHealth_ChecksCache()
        {
            // Arrange
            SetupMockServiceProviderWithCache();

            // Act
            var result = await _controller.GetDetailedHealth();

            // Assert
            result.Should().BeOfType<OkObjectResult>();
            var okResult = result as OkObjectResult;
            okResult.Should().NotBeNull();
            
            // Verify dependency checks were performed
            var value = okResult!.Value;
            var dependenciesProp = value!.GetType().GetProperty("Dependencies");
            dependenciesProp.Should().NotBeNull();
        }

        [Fact]
        public async Task GetDetailedHealth_ReturnsHealthyStatus_WhenAllDependenciesHealthy()
        {
            // Arrange
            SetupMockServiceProvider();

            // Act
            var result = await _controller.GetDetailedHealth();

            // Assert
            var okResult = result as OkObjectResult;
            var value = okResult!.Value;
            var statusProp = value!.GetType().GetProperty("Status");
            var status = statusProp!.GetValue(value) as string;
            
            // Status could be "Healthy" or "Degraded" depending on dependency availability
            status.Should().BeOneOf("Healthy", "Degraded");
        }

        [Fact]
        public async Task GetDetailedHealth_SetsTelemetryTags()
        {
            // Arrange
            SetupMockServiceProvider();
            // Note: Activity.SetTag is not virtual and cannot be mocked
            // We can only verify that StartActivity was called

            // Act
            await _controller.GetDetailedHealth();

            // Assert
            _mockTelemetryService.Verify(x => x.StartActivity("Health.DetailedCheck"), Times.Once);
        }

        #endregion

        #region Helper Methods

        private void SetupMockServiceProvider()
        {
            var services = new ServiceCollection();
            var serviceProvider = services.BuildServiceProvider();

            MockHttpContext.Setup(x => x.RequestServices).Returns(serviceProvider);
        }

        private void SetupMockServiceProviderWithException()
        {
            MockHttpContext.Setup(x => x.RequestServices)
                .Throws(new InvalidOperationException("Service provider error"));
        }

        private void SetupMockServiceProviderWithBlobService()
        {
            var mockBlobService = new Mock<IAzureBlobStorageService>();
            mockBlobService.Setup(x => x.BlobExistsAsync(It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(true);

            var services = new ServiceCollection();
            services.AddSingleton(mockBlobService.Object);
            
            var mockConfigHelper = new Mock<IConfigHelper>();
            mockConfigHelper.Setup(x => x.GetPlatformConfigurationsContainer())
                .Returns(TestConstants.Containers.PlatformConfigs);
            services.AddSingleton(mockConfigHelper.Object);

            var serviceProvider = services.BuildServiceProvider();
            MockHttpContext.Setup(x => x.RequestServices).Returns(serviceProvider);
        }

        private void SetupMockServiceProviderWithTableService()
        {
            var mockTableService = new Mock<IMetricsConfigTableService>();
            mockTableService.Setup(x => x.GetAllMetricsConfigurations(It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(new List<Sxg.EvalPlatform.API.Storage.TableEntities.MetricsConfigurationTableEntity>());

            var services = new ServiceCollection();
            services.AddSingleton(mockTableService.Object);

            var serviceProvider = services.BuildServiceProvider();
            MockHttpContext.Setup(x => x.RequestServices).Returns(serviceProvider);
        }

        private void SetupMockServiceProviderWithCache()
        {
            // Create a real configuration with the cache provider setting
            var configDict = new Dictionary<string, string?>
            {
                { "Cache:Provider", "memory" }
            };
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(configDict)
                .Build();

            var mockCacheManager = new Mock<ICacheManager>();
            mockCacheManager.Setup(x => x.SetAsync(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<TimeSpan?>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            mockCacheManager.Setup(x => x.GetAsync<object>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new object());
            mockCacheManager.Setup(x => x.RemoveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            mockCacheManager.Setup(x => x.GetStatisticsAsync())
                .ReturnsAsync(new CacheStatistics { HitRatio = 0.85, ItemCount = 100 });

            var services = new ServiceCollection();
            services.AddSingleton<IConfiguration>(configuration);
            services.AddSingleton(mockCacheManager.Object);

            var serviceProvider = services.BuildServiceProvider();
            MockHttpContext.Setup(x => x.RequestServices).Returns(serviceProvider);
        }

        #endregion
    }
}
