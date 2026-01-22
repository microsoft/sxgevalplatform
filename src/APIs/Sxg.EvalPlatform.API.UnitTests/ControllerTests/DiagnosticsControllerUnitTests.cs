using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Sxg.EvalPlatform.API.Storage;
using Sxg.EvalPlatform.API.UnitTests.RequestHandlerTests;
using SxgEvalPlatformApi.Controllers;
using SxgEvalPlatformApi.Services;
using System.Security.Claims;

namespace Sxg.EvalPlatform.API.UnitTests.ControllerTests
{
    /// <summary>
    /// Comprehensive unit tests for DiagnosticsController.
    /// Tests diagnostic endpoints for authentication troubleshooting.
    /// </summary>
    [Trait("Category", TestCategories.Unit)]
    [Trait("Category", TestCategories.Controller)]
    [Trait("Category", TestCategories.Security)]
    public class DiagnosticsControllerUnitTests : ControllerTestBase<DiagnosticsController>
    {
        private readonly Mock<ILogger<DiagnosticsController>> _mockLogger;
        private readonly Mock<ICallerIdentificationService> _mockCallerService;
        private readonly DiagnosticsController _controller;

        public DiagnosticsControllerUnitTests()
        {
            _mockLogger = MockLogger;
            _mockCallerService = MockCallerService;

            _controller = new DiagnosticsController(
                _mockLogger.Object,
                _mockCallerService.Object
            );

            SetupControllerContext(_controller);
        }

        #region GetClaims Tests

        [Fact]
        public void GetClaims_WithAuthenticatedUser_ReturnsOkWithClaims()
        {
            // Arrange
            var claims = CreateTestClaims(
                TestConstants.Users.DefaultUserId, 
                TestConstants.Users.DefaultEmail);

            var identity = new ClaimsIdentity(claims, "TestAuthType");
            var user = new ClaimsPrincipal(identity);

            SetupControllerContext(_controller, user);

            // Act
            var result = _controller.GetClaims();

            // Assert
            VerifyOkResult(result);
            
            var okResult = result as OkObjectResult;
            okResult.Should().NotBeNull();
            okResult!.Value.Should().NotBeNull();
        }

        [Fact]
        public void GetClaims_WithMultipleClaims_ReturnsAllClaims()
        {
            // Arrange
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, "user-123"),
                new Claim(ClaimTypes.Email, "test@example.com"),
                new Claim(ClaimTypes.Name, "Test User"),
                new Claim("preferred_username", "testuser"),
                new Claim("appid", "app-123")
            };

            var identity = new ClaimsIdentity(claims, "Bearer");
            var user = new ClaimsPrincipal(identity);

            SetupControllerContext(_controller, user);

            // Act
            var result = _controller.GetClaims();

            // Assert
            var okResult = result as OkObjectResult;
            okResult.Should().NotBeNull();
            
            var value = okResult!.Value;
            value.Should().NotBeNull();

            var isAuthProp = value!.GetType().GetProperty("IsAuthenticated");
            isAuthProp.Should().NotBeNull();
            isAuthProp!.GetValue(value).Should().Be(true);
        }

        [Fact]
        public void GetClaims_WithUnauthenticatedUser_ReturnsOkWithEmptyClaims()
        {
            // Arrange
            var identity = new ClaimsIdentity(); // Not authenticated
            var user = new ClaimsPrincipal(identity);

            SetupControllerContext(_controller, user);

            // Act
            var result = _controller.GetClaims();

            // Assert
            var okResult = result as OkObjectResult;
            okResult.Should().NotBeNull();

            var value = okResult!.Value;
            var isAuthProp = value!.GetType().GetProperty("IsAuthenticated");
            isAuthProp!.GetValue(value).Should().Be(false);
        }

        [Fact]
        public void GetClaims_WithSpecialClaims_ReturnsImportantClaims()
        {
            // Arrange
            var claims = CreateSpecialClaims();

            var identity = new ClaimsIdentity(claims, "Bearer");
            var user = new ClaimsPrincipal(identity);

            SetupControllerContext(_controller, user);

            // Act
            var result = _controller.GetClaims();

            // Assert
            var okResult = result as OkObjectResult;
            okResult.Should().NotBeNull();
            okResult!.Value.Should().NotBeNull();
        }

        #endregion

        #region GetAuditUser Tests

        [Fact]
        public void GetAuditUser_WithDirectUser_ReturnsUserEmail()
        {
            // Arrange
            SetupDirectUserAuth();

            // Act
            var result = _controller.GetAuditUser();

            // Assert
            VerifyOkResult(result);
            
            var okResult = result as OkObjectResult;
            okResult.Should().NotBeNull();
            okResult!.Value.Should().NotBeNull();

            var auditUser = GetPropertyValue<string>(okResult.Value, "AuditUserThatWillBeSaved");
            auditUser.Should().Be(TestConstants.Users.DefaultEmail);
        }

        [Fact]
        public void GetAuditUser_WithServicePrincipal_ReturnsApplicationName()
        {
            // Arrange
            SetupServicePrincipalAuth();

            // Act
            var result = _controller.GetAuditUser();

            // Assert
            VerifyOkResult(result);
            
            var okResult = result as OkObjectResult;
            okResult.Should().NotBeNull();

            var auditUser = GetPropertyValue<string>(okResult.Value, "AuditUserThatWillBeSaved");
            auditUser.Should().Be(TestConstants.Applications.ServiceApp);
        }

        [Fact]
        public void GetAuditUser_WithDelegatedUser_ReturnsUserEmail()
        {
            // Arrange
            SetupDelegatedUserAuth();

            // Act
            var result = _controller.GetAuditUser();

            // Assert
            VerifyOkResult(result);
            
            var okResult = result as OkObjectResult;
            okResult.Should().NotBeNull();

            var auditUser = GetPropertyValue<string>(okResult.Value, "AuditUserThatWillBeSaved");
            auditUser.Should().NotBeNullOrEmpty();
        }

        [Fact]
        public void GetAuditUser_WithNoValidUserInfo_ReturnsSystem()
        {
            // Arrange
            SetupNoValidUserInfo();

            // Act
            var result = _controller.GetAuditUser();

            // Assert
            VerifyOkResult(result);
            
            var okResult = result as OkObjectResult;
            okResult.Should().NotBeNull();

            var auditUser = GetPropertyValue<string>(okResult.Value, "AuditUserThatWillBeSaved");
            auditUser.Should().Be(TestConstants.Users.SystemUser);
        }

        [Fact]
        public void GetAuditUser_WhenExceptionOccurs_Returns500()
        {
            // Arrange
            _mockCallerService
                .Setup(x => x.GetCallerInfo())
                .Throws(new Exception(TestConstants.ErrorMessages.CallerServiceError));

            // Act
            var result = _controller.GetAuditUser();

            // Assert
            VerifyStatusCodeResult(result, TestConstants.HttpStatusCodes.InternalServerError);
        }

        [Fact]
        public void GetAuditUser_IncludesCallerInfo()
        {
            // Arrange
            SetupDirectUserAuth();

            // Act
            var result = _controller.GetAuditUser();

            // Assert
            var okResult = result as OkObjectResult;
            okResult.Should().NotBeNull();

            var value = okResult!.Value;
            var callerInfoProp = value!.GetType().GetProperty("CallerInfo");
            callerInfoProp.Should().NotBeNull();
        }

        [Fact]
        public void GetAuditUser_IncludesRawValues()
        {
            // Arrange
            SetupDirectUserAuth();

            // Act
            var result = _controller.GetAuditUser();

            // Assert
            var okResult = result as OkObjectResult;
            okResult.Should().NotBeNull();

            var value = okResult!.Value;
            var rawValuesProp = value!.GetType().GetProperty("RawValues");
            rawValuesProp.Should().NotBeNull();
        }

        [Fact]
        public void GetAuditUser_IncludesAllClaims()
        {
            // Arrange
            var claims = CreateTestClaims(
                TestConstants.Users.DefaultUserId, 
                TestConstants.Users.DefaultEmail);
            
            var identity = new ClaimsIdentity(claims, "Bearer");
            var user = new ClaimsPrincipal(identity);

            SetupControllerContext(_controller, user);
            SetupDirectUserAuth();

            // Act
            var result = _controller.GetAuditUser();

            // Assert
            var okResult = result as OkObjectResult;
            okResult.Should().NotBeNull();

            var value = okResult!.Value;
            var allClaimsProp = value!.GetType().GetProperty("AllClaims");
            allClaimsProp.Should().NotBeNull();
        }

        #endregion

        #region GetContext Tests

        [Fact]
        public void GetContext_ReturnsOkWithContextInformation()
        {
            // Arrange
            SetupControllerContextWithQueryString(_controller, new Dictionary<string, string>
            {
                { "param1", "value1" }
            });
            
            SetupHeaderDictionary();

            // Act
            var result = _controller.GetContext();

            // Assert
            VerifyOkResult(result);
        }

        [Fact]
        public void GetContext_IncludesRequestInformation()
        {
            // Arrange
            SetupHttpRequestDetails(
                scheme: "https",
                host: "localhost:5000",
                path: "/api/v1/diagnostics/context",
                queryString: "?test=value");

            SetupHeaderDictionary();

            // Act
            var result = _controller.GetContext();

            // Assert
            var okResult = result as OkObjectResult;
            okResult.Should().NotBeNull();

            var value = okResult!.Value;
            var requestProp = value!.GetType().GetProperty("Request");
            requestProp.Should().NotBeNull();
        }

        [Fact]
        public void GetContext_IncludesUserInformation()
        {
            // Arrange
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, "user-123"),
                new Claim(ClaimTypes.Name, "Test User")
            };
            var identity = new ClaimsIdentity(claims, "Bearer");
            var user = new ClaimsPrincipal(identity);

            SetupControllerContext(_controller, user);
            SetupHeaderDictionary();

            // Act
            var result = _controller.GetContext();

            // Assert
            var okResult = result as OkObjectResult;
            okResult.Should().NotBeNull();

            var value = okResult!.Value;
            var userProp = value!.GetType().GetProperty("User");
            userProp.Should().NotBeNull();
        }

        [Fact]
        public void GetContext_IncludesHeaders()
        {
            // Arrange
            var headers = new HeaderDictionary
            {
                { "Authorization", "Bearer token" },
                { "Content-Type", "application/json" }
            };

            MockHttpRequest.Setup(x => x.Headers).Returns(headers);
            SetupHttpRequestDetails();

            // Act
            var result = _controller.GetContext();

            // Assert
            var okResult = result as OkObjectResult;
            okResult.Should().NotBeNull();
            okResult!.Value.Should().NotBeNull();
        }

        #endregion

        #region Integration Tests

        [Theory]
        [InlineData(false, false)]  // DirectUser
        [InlineData(true, false)]   // ServicePrincipal
        [InlineData(true, true)]    // DelegatedUser
        public void GetAuditUser_WithDifferentAuthFlows_ReturnsAppropriateAuditUser(
            bool isServicePrincipal, bool hasDelegatedUser)
        {
            // Arrange
            if (isServicePrincipal && !hasDelegatedUser)
                SetupServicePrincipalAuth();
            else if (isServicePrincipal && hasDelegatedUser)
                SetupDelegatedUserAuth();
            else
                SetupDirectUserAuth();

            // Act
            var result = _controller.GetAuditUser();

            // Assert
            VerifyOkResult(result);
            
            var okResult = result as OkObjectResult;
            okResult.Should().NotBeNull();

            var auditUser = GetPropertyValue<string>(okResult!.Value, "AuditUserThatWillBeSaved");
            
            if (isServicePrincipal && !hasDelegatedUser)
            {
                auditUser.Should().Be(TestConstants.Applications.ServiceApp);
            }
            else
            {
                auditUser.Should().NotBeNullOrEmpty();
                auditUser.Should().NotBe(TestConstants.Users.SystemUser);
            }
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Creates test claims for a user.
        /// </summary>
        private List<Claim> CreateTestClaims(string userId, string userEmail)
        {
            return new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, userId),
                new Claim(ClaimTypes.Email, userEmail),
                new Claim("preferred_username", userEmail),
                new Claim("oid", userId)
            };
        }

        /// <summary>
        /// Creates special claims for testing important claim scenarios.
        /// </summary>
        private List<Claim> CreateSpecialClaims()
        {
            return new List<Claim>
            {
                new Claim("preferred_username", "testuser@example.com"),
                new Claim("email", "test@example.com"),
                new Claim("upn", "test@example.com"),
                new Claim("oid", "object-id-123"),
                new Claim("appid", "app-id-456")
            };
        }

        /// <summary>
        /// Sets up the caller service to return no valid user info.
        /// </summary>
        private void SetupNoValidUserInfo()
        {
            _mockCallerService.Setup(x => x.IsServicePrincipalCall()).Returns(false);
            _mockCallerService.Setup(x => x.GetCurrentUserEmail()).Returns(TestConstants.Applications.UnknownApp);
            _mockCallerService.Setup(x => x.GetCurrentUserId()).Returns(TestConstants.Applications.UnknownApp);
            _mockCallerService.Setup(x => x.GetCallerInfo()).Returns(new CallerInfo
            {
                UserEmail = TestConstants.Applications.UnknownApp,
                UserId = TestConstants.Applications.UnknownApp
            });
        }

        /// <summary>
        /// Sets up an empty header dictionary.
        /// </summary>
        private void SetupHeaderDictionary()
        {
            var headers = new HeaderDictionary();
            MockHttpRequest.Setup(x => x.Headers).Returns(headers);
        }

        /// <summary>
        /// Sets up HTTP request details with default or specified values.
        /// </summary>
        private void SetupHttpRequestDetails(
            string scheme = "https",
            string host = "localhost",
            string path = "/api/v1/diagnostics/context",
            string queryString = "")
        {
            MockHttpRequest.Setup(x => x.Scheme).Returns(scheme);
            MockHttpRequest.Setup(x => x.Host).Returns(new HostString(host));
            MockHttpRequest.Setup(x => x.Path).Returns(new PathString(path));
            MockHttpRequest.Setup(x => x.QueryString).Returns(new QueryString(queryString));
        }

        /// <summary>
        /// Gets a property value from an anonymous type using reflection.
        /// </summary>
        private T? GetPropertyValue<T>(object? obj, string propertyName)
        {
            if (obj == null) return default;
            
            var property = obj.GetType().GetProperty(propertyName);
            property.Should().NotBeNull($"Property '{propertyName}' should exist");
            
            return (T?)property!.GetValue(obj);
        }

        #endregion
    }
}
