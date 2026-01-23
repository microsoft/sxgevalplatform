using FluentAssertions;
using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.DataContracts;
using Moq;
using SxgEvalPlatformApi.Services;

namespace Sxg.EvalPlatform.API.UnitTests.ServicesTests
{
    /// <summary>
    /// Comprehensive unit tests for CloudRoleNameTelemetryInitializer.
    /// Tests Application Insights cloud role name initialization.
    /// </summary>
    public class CloudRoleNameTelemetryInitializerUnitTests
    {
        #region Constructor Tests

        [Fact]
        public void Constructor_WithValidCloudRoleName_CreatesInstance()
        {
            // Arrange
            var cloudRoleName = "SXG.EvalPlatform.API";

            // Act
            var initializer = new CloudRoleNameTelemetryInitializer(cloudRoleName);

            // Assert
            initializer.Should().NotBeNull();
        }

        [Fact]
        public void Constructor_WithNullCloudRoleName_ThrowsArgumentNullException()
        {
            // Arrange, Act & Assert
            Assert.Throws<ArgumentNullException>(() => 
                new CloudRoleNameTelemetryInitializer(null!));
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        public void Constructor_WithEmptyOrWhitespaceCloudRoleName_DoesNotThrow(string cloudRoleName)
        {
            // Arrange, Act & Assert
            var exception = Record.Exception(() => new CloudRoleNameTelemetryInitializer(cloudRoleName));
            exception.Should().BeNull();
        }

        #endregion

        #region Initialize Tests

        [Fact]
        public void Initialize_WithValidTelemetry_SetsCloudRoleName()
        {
            // Arrange
            var cloudRoleName = "SXG.EvalPlatform.API";
            var initializer = new CloudRoleNameTelemetryInitializer(cloudRoleName);
            var telemetry = new RequestTelemetry();

            // Act
            initializer.Initialize(telemetry);

            // Assert
            telemetry.Context.Cloud.RoleName.Should().Be(cloudRoleName);
        }

        [Fact]
        public void Initialize_WithRequestTelemetry_SetsCloudRoleName()
        {
            // Arrange
            var cloudRoleName = "TestRoleName";
            var initializer = new CloudRoleNameTelemetryInitializer(cloudRoleName);
            var telemetry = new RequestTelemetry
            {
                Name = "TestRequest",
                Duration = TimeSpan.FromMilliseconds(100)
            };

            // Act
            initializer.Initialize(telemetry);

            // Assert
            telemetry.Context.Cloud.RoleName.Should().Be(cloudRoleName);
        }

        [Fact]
        public void Initialize_WithDependencyTelemetry_SetsCloudRoleName()
        {
            // Arrange
            var cloudRoleName = "TestRoleName";
            var initializer = new CloudRoleNameTelemetryInitializer(cloudRoleName);
            var telemetry = new DependencyTelemetry
            {
                Name = "TestDependency",
                Duration = TimeSpan.FromMilliseconds(50)
            };

            // Act
            initializer.Initialize(telemetry);

            // Assert
            telemetry.Context.Cloud.RoleName.Should().Be(cloudRoleName);
        }

        [Fact]
        public void Initialize_WithTraceTelemetry_SetsCloudRoleName()
        {
            // Arrange
            var cloudRoleName = "TestRoleName";
            var initializer = new CloudRoleNameTelemetryInitializer(cloudRoleName);
            var telemetry = new TraceTelemetry
            {
                Message = "Test trace message"
            };

            // Act
            initializer.Initialize(telemetry);

            // Assert
            telemetry.Context.Cloud.RoleName.Should().Be(cloudRoleName);
        }

        [Fact]
        public void Initialize_WithEventTelemetry_SetsCloudRoleName()
        {
            // Arrange
            var cloudRoleName = "TestRoleName";
            var initializer = new CloudRoleNameTelemetryInitializer(cloudRoleName);
            var telemetry = new EventTelemetry
            {
                Name = "TestEvent"
            };

            // Act
            initializer.Initialize(telemetry);

            // Assert
            telemetry.Context.Cloud.RoleName.Should().Be(cloudRoleName);
        }

        [Fact]
        public void Initialize_WithExceptionTelemetry_SetsCloudRoleName()
        {
            // Arrange
            var cloudRoleName = "TestRoleName";
            var initializer = new CloudRoleNameTelemetryInitializer(cloudRoleName);
            var telemetry = new ExceptionTelemetry
            {
                Exception = new InvalidOperationException("Test exception")
            };

            // Act
            initializer.Initialize(telemetry);

            // Assert
            telemetry.Context.Cloud.RoleName.Should().Be(cloudRoleName);
        }

        [Fact]
        public void Initialize_WithMetricTelemetry_SetsCloudRoleName()
        {
            // Arrange
            var cloudRoleName = "TestRoleName";
            var initializer = new CloudRoleNameTelemetryInitializer(cloudRoleName);
            var telemetry = new MetricTelemetry
            {
                Name = "TestMetric",
                Sum = 100
            };

            // Act
            initializer.Initialize(telemetry);

            // Assert
            telemetry.Context.Cloud.RoleName.Should().Be(cloudRoleName);
        }

        [Fact]
        public void Initialize_WithNullTelemetry_DoesNotThrow()
        {
            // Arrange
            var cloudRoleName = "TestRoleName";
            var initializer = new CloudRoleNameTelemetryInitializer(cloudRoleName);

            // Act & Assert
            var exception = Record.Exception(() => initializer.Initialize(null!));
            exception.Should().BeNull();
        }

        [Fact]
        public void Initialize_WithTelemetryWithNullContext_DoesNotThrow()
        {
            // Arrange
            var cloudRoleName = "TestRoleName";
            var initializer = new CloudRoleNameTelemetryInitializer(cloudRoleName);
            var mockTelemetry = new Mock<ITelemetry>();
            mockTelemetry.Setup(t => t.Context).Returns((TelemetryContext)null!);

            // Act & Assert
            var exception = Record.Exception(() => initializer.Initialize(mockTelemetry.Object));
            exception.Should().BeNull();
        }

        [Fact]
        public void Initialize_WithTelemetryWithNullCloud_DoesNotThrow()
        {
            // Arrange
            var cloudRoleName = "TestRoleName";
            var initializer = new CloudRoleNameTelemetryInitializer(cloudRoleName);
            var telemetry = new RequestTelemetry();
            
            // Set Cloud to null through reflection (shouldn't normally happen)
            var contextType = typeof(TelemetryContext);
            var cloudField = contextType.GetField("_cloud", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            cloudField?.SetValue(telemetry.Context, null);

            // Act & Assert
            var exception = Record.Exception(() => initializer.Initialize(telemetry));
            exception.Should().BeNull();
        }

        #endregion

        #region Multiple Initialize Calls Tests

        [Fact]
        public void Initialize_CalledMultipleTimes_KeepsLastValue()
        {
            // Arrange
            var cloudRoleName1 = "FirstRoleName";
            var cloudRoleName2 = "SecondRoleName";
            var initializer1 = new CloudRoleNameTelemetryInitializer(cloudRoleName1);
            var initializer2 = new CloudRoleNameTelemetryInitializer(cloudRoleName2);
            var telemetry = new RequestTelemetry();

            // Act
            initializer1.Initialize(telemetry);
            initializer2.Initialize(telemetry);

            // Assert
            telemetry.Context.Cloud.RoleName.Should().Be(cloudRoleName2);
        }

        [Fact]
        public void Initialize_WithSameInitializer_MultipleTelemetries_SetsSameRoleNameForAll()
        {
            // Arrange
            var cloudRoleName = "SharedRoleName";
            var initializer = new CloudRoleNameTelemetryInitializer(cloudRoleName);
            var telemetry1 = new RequestTelemetry();
            var telemetry2 = new DependencyTelemetry();
            var telemetry3 = new TraceTelemetry();

            // Act
            initializer.Initialize(telemetry1);
            initializer.Initialize(telemetry2);
            initializer.Initialize(telemetry3);

            // Assert
            telemetry1.Context.Cloud.RoleName.Should().Be(cloudRoleName);
            telemetry2.Context.Cloud.RoleName.Should().Be(cloudRoleName);
            telemetry3.Context.Cloud.RoleName.Should().Be(cloudRoleName);
        }

        #endregion

        #region Cloud Role Name Value Tests

        [Theory]
        [InlineData("SXG.EvalPlatform.API")]
        [InlineData("TestService")]
        [InlineData("MyApplication-Production")]
        [InlineData("Service123")]
        public void Initialize_WithVariousCloudRoleNames_SetsCorrectly(string cloudRoleName)
        {
            // Arrange
            var initializer = new CloudRoleNameTelemetryInitializer(cloudRoleName);
            var telemetry = new RequestTelemetry();

            // Act
            initializer.Initialize(telemetry);

            // Assert
            telemetry.Context.Cloud.RoleName.Should().Be(cloudRoleName);
        }

        [Fact]
        public void Initialize_WithEmptyCloudRoleName_SetsEmptyString()
        {
            // Arrange
            var cloudRoleName = "";
            var initializer = new CloudRoleNameTelemetryInitializer(cloudRoleName);
            var telemetry = new RequestTelemetry();

            // Act
            initializer.Initialize(telemetry);

            // Assert
            // Note: Application Insights may normalize empty string to null
            telemetry.Context.Cloud.RoleName.Should().BeNullOrEmpty();
        }

        [Fact]
        public void Initialize_WithWhitespaceCloudRoleName_SetsWhitespace()
        {
            // Arrange
            var cloudRoleName = "   ";
            var initializer = new CloudRoleNameTelemetryInitializer(cloudRoleName);
            var telemetry = new RequestTelemetry();

            // Act
            initializer.Initialize(telemetry);

            // Assert
            telemetry.Context.Cloud.RoleName.Should().Be("   ");
        }

        [Fact]
        public void Initialize_WithLongCloudRoleName_SetsFullValue()
        {
            // Arrange
            var cloudRoleName = new string('A', 500);
            var initializer = new CloudRoleNameTelemetryInitializer(cloudRoleName);
            var telemetry = new RequestTelemetry();

            // Act
            initializer.Initialize(telemetry);

            // Assert
            telemetry.Context.Cloud.RoleName.Should().Be(cloudRoleName);
            telemetry.Context.Cloud.RoleName.Length.Should().Be(500);
        }

        [Fact]
        public void Initialize_WithSpecialCharacters_SetsCorrectly()
        {
            // Arrange
            var cloudRoleName = "Service-Name_With.Special@Characters!123";
            var initializer = new CloudRoleNameTelemetryInitializer(cloudRoleName);
            var telemetry = new RequestTelemetry();

            // Act
            initializer.Initialize(telemetry);

            // Assert
            telemetry.Context.Cloud.RoleName.Should().Be(cloudRoleName);
        }

        #endregion

        #region Integration Tests

        [Fact]
        public void Initialize_WithCompleteRequestTelemetry_PreservesOtherProperties()
        {
            // Arrange
            var cloudRoleName = "TestRoleName";
            var initializer = new CloudRoleNameTelemetryInitializer(cloudRoleName);
            var telemetry = new RequestTelemetry
            {
                Name = "GET /api/test",
                Duration = TimeSpan.FromMilliseconds(150),
                ResponseCode = "200",
                Success = true
            };
            
            telemetry.Context.Operation.Id = "operation-123";
            telemetry.Context.Operation.Name = "TestOperation";
            telemetry.Context.User.Id = "user-456";

            // Act
            initializer.Initialize(telemetry);

            // Assert
            telemetry.Context.Cloud.RoleName.Should().Be(cloudRoleName);
            telemetry.Name.Should().Be("GET /api/test");
            telemetry.Duration.Should().Be(TimeSpan.FromMilliseconds(150));
            telemetry.ResponseCode.Should().Be("200");
            telemetry.Success.Should().BeTrue();
            telemetry.Context.Operation.Id.Should().Be("operation-123");
            telemetry.Context.Operation.Name.Should().Be("TestOperation");
            telemetry.Context.User.Id.Should().Be("user-456");
        }

        [Fact]
        public void Initialize_WithTelemetryAlreadyHavingRoleName_OverwritesIt()
        {
            // Arrange
            var oldRoleName = "OldRoleName";
            var newRoleName = "NewRoleName";
            var initializer = new CloudRoleNameTelemetryInitializer(newRoleName);
            var telemetry = new RequestTelemetry();
            telemetry.Context.Cloud.RoleName = oldRoleName;

            // Act
            initializer.Initialize(telemetry);

            // Assert
            telemetry.Context.Cloud.RoleName.Should().Be(newRoleName);
            telemetry.Context.Cloud.RoleName.Should().NotBe(oldRoleName);
        }

        #endregion

        #region Thread Safety Tests

        [Fact]
        public void Initialize_CalledConcurrently_AllSucceed()
        {
            // Arrange
            var cloudRoleName = "ConcurrentRoleName";
            var initializer = new CloudRoleNameTelemetryInitializer(cloudRoleName);
            var telemetries = Enumerable.Range(0, 100)
                .Select(_ => new RequestTelemetry())
                .ToList();

            // Act
            Parallel.ForEach(telemetries, telemetry =>
            {
                initializer.Initialize(telemetry);
            });

            // Assert
            telemetries.Should().OnlyContain(t => t.Context.Cloud.RoleName == cloudRoleName);
        }

        #endregion
    }
}
