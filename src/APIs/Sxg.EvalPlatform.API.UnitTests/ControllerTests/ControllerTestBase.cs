using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Sxg.EvalPlatform.API.Storage;
using Sxg.EvalPlatform.API.UnitTests.RequestHandlerTests;
using SxgEvalPlatformApi.Controllers;
using SxgEvalPlatformApi.Services;
using System.Security.Claims;
using FluentAssertions;

namespace Sxg.EvalPlatform.API.UnitTests.ControllerTests
{
    /// <summary>
    /// Base class for Controller unit tests providing common setup and helper methods.
    /// Extends the pattern established in RequestHandlerTestBase for consistency.
    /// </summary>
    public abstract class ControllerTestBase<TController> where TController : ControllerBase
    {
        #region Common Mocks

        protected Mock<ILogger<TController>> MockLogger { get; }
        protected Mock<ICallerIdentificationService> MockCallerService { get; }
        protected Mock<IOpenTelemetryService> MockTelemetryService { get; }
        protected Mock<HttpContext> MockHttpContext { get; }
        protected Mock<HttpRequest> MockHttpRequest { get; }
        protected Mock<HttpResponse> MockHttpResponse { get; }

        #endregion

        #region Constructor

        protected ControllerTestBase()
        {
            MockLogger = new Mock<ILogger<TController>>();
            MockCallerService = new Mock<ICallerIdentificationService>();
            MockTelemetryService = new Mock<IOpenTelemetryService>();
            MockHttpContext = new Mock<HttpContext>();
            MockHttpRequest = new Mock<HttpRequest>();
            MockHttpResponse = new Mock<HttpResponse>();

            SetupDefaultMocks();
        }

        #endregion

        #region Setup Methods

        /// <summary>
        /// Sets up default behaviors for common mocks.
        /// </summary>
        protected virtual void SetupDefaultMocks()
        {
            // Setup default caller service behavior
            MockCallerService.Setup(x => x.GetCallerInfo()).Returns(CreateDefaultCallerInfo());
            MockCallerService.Setup(x => x.GetCurrentUserEmail()).Returns(TestConstants.Users.DefaultEmail);
            MockCallerService.Setup(x => x.GetCurrentUserId()).Returns(TestConstants.Users.DefaultUserId);
            MockCallerService.Setup(x => x.IsServicePrincipalCall()).Returns(false);
            MockCallerService.Setup(x => x.HasDelegatedUserContext()).Returns(false);
            MockCallerService.Setup(x => x.GetCallingApplicationName()).Returns(TestConstants.Applications.DefaultApp);
            MockCallerService.Setup(x => x.GetCallerDescription()).Returns($"User: {TestConstants.Users.DefaultEmail}");

            // Setup HttpContext
            MockHttpContext.Setup(x => x.Request).Returns(MockHttpRequest.Object);
            MockHttpContext.Setup(x => x.Response).Returns(MockHttpResponse.Object);
            MockHttpContext.Setup(x => x.User).Returns(CreateDefaultClaimsPrincipal());

            // Setup telemetry service to return null activities (no-op)
            MockTelemetryService.Setup(x => x.StartActivity(It.IsAny<string>()))
                .Returns((System.Diagnostics.Activity?)null);
        }

        /// <summary>
        /// Creates a default CallerInfo object for testing.
        /// </summary>
        protected virtual CallerInfo CreateDefaultCallerInfo()
        {
            return new CallerInfo
            {
                UserId = TestConstants.Users.DefaultUserId,
                UserEmail = TestConstants.Users.DefaultEmail,
                ApplicationName = TestConstants.Applications.DefaultApp,
                IsServicePrincipal = false,
                HasDelegatedUser = false
            };
        }

        /// <summary>
        /// Creates a default ClaimsPrincipal for testing.
        /// </summary>
        protected virtual ClaimsPrincipal CreateDefaultClaimsPrincipal()
        {
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, TestConstants.Users.DefaultUserId),
                new Claim(ClaimTypes.Email, TestConstants.Users.DefaultEmail),
                new Claim("name", "Test User")
            };

            var identity = new ClaimsIdentity(claims, "TestAuthType");
            return new ClaimsPrincipal(identity);
        }

        #endregion

        #region Controller Setup Helpers

        /// <summary>
        /// Sets up the controller's HttpContext.
        /// </summary>
        protected void SetupControllerContext(ControllerBase controller)
        {
            controller.ControllerContext = new ControllerContext
            {
                HttpContext = MockHttpContext.Object
            };
        }

        /// <summary>
        /// Sets up the controller's HttpContext with specific claims.
        /// </summary>
        protected void SetupControllerContext(ControllerBase controller, ClaimsPrincipal user)
        {
            MockHttpContext.Setup(x => x.User).Returns(user);
            controller.ControllerContext = new ControllerContext
            {
                HttpContext = MockHttpContext.Object
            };
        }

        /// <summary>
        /// Sets up the controller's HttpContext with query string parameters.
        /// </summary>
        protected void SetupControllerContextWithQueryString(ControllerBase controller, Dictionary<string, string> queryParams)
        {
            var queryCollection = new QueryCollection(
                queryParams.ToDictionary(kvp => kvp.Key, kvp => new Microsoft.Extensions.Primitives.StringValues(kvp.Value))
            );

            MockHttpRequest.Setup(x => x.Query).Returns(queryCollection);
            SetupControllerContext(controller);
        }

        #endregion

        #region Caller Service Configuration Helpers

        /// <summary>
        /// Configures the caller service to simulate a DirectUser authentication flow.
        /// </summary>
        protected void SetupDirectUserAuth(string userEmail = TestConstants.Users.DefaultEmail, string userId = TestConstants.Users.DefaultUserId)
        {
            MockCallerService.Setup(x => x.IsServicePrincipalCall()).Returns(false);
            MockCallerService.Setup(x => x.HasDelegatedUserContext()).Returns(false);
            MockCallerService.Setup(x => x.GetCurrentUserEmail()).Returns(userEmail);
            MockCallerService.Setup(x => x.GetCurrentUserId()).Returns(userId);
            MockCallerService.Setup(x => x.GetCallerInfo()).Returns(new CallerInfo
            {
                UserId = userId,
                UserEmail = userEmail,
                IsServicePrincipal = false,
                HasDelegatedUser = false
            });
            MockCallerService.Setup(x => x.GetCallerDescription()).Returns($"User: {userEmail}");
        }

        /// <summary>
        /// Configures the caller service to simulate an AppToApp (service principal) authentication flow.
        /// </summary>
        protected void SetupServicePrincipalAuth(string applicationName = TestConstants.Applications.ServiceApp)
        {
            MockCallerService.Setup(x => x.IsServicePrincipalCall()).Returns(true);
            MockCallerService.Setup(x => x.HasDelegatedUserContext()).Returns(false);
            MockCallerService.Setup(x => x.GetCallingApplicationName()).Returns(applicationName);
            MockCallerService.Setup(x => x.GetCallerInfo()).Returns(new CallerInfo
            {
                ApplicationName = applicationName,
                IsServicePrincipal = true,
                HasDelegatedUser = false
            });
            MockCallerService.Setup(x => x.GetCallerDescription()).Returns($"Service Principal: {applicationName}");
        }

        /// <summary>
        /// Configures the caller service to simulate a DelegatedAppToApp authentication flow.
        /// </summary>
        protected void SetupDelegatedUserAuth(
            string userEmail = TestConstants.Users.DelegatedEmail,
            string userId = TestConstants.Users.DelegatedUserId,
            string applicationName = TestConstants.Applications.DelegatedApp)
        {
            MockCallerService.Setup(x => x.IsServicePrincipalCall()).Returns(true);
            MockCallerService.Setup(x => x.HasDelegatedUserContext()).Returns(true);
            MockCallerService.Setup(x => x.GetCurrentUserEmail()).Returns(userEmail);
            MockCallerService.Setup(x => x.GetCurrentUserId()).Returns(userId);
            MockCallerService.Setup(x => x.GetCallingApplicationName()).Returns(applicationName);
            MockCallerService.Setup(x => x.GetCallerInfo()).Returns(new CallerInfo
            {
                UserId = userId,
                UserEmail = userEmail,
                ApplicationName = applicationName,
                IsServicePrincipal = true,
                HasDelegatedUser = true
            });
            MockCallerService.Setup(x => x.GetCallerDescription()).Returns($"Service Principal: {applicationName} on behalf of {userEmail}");
        }

        #endregion

        #region Assertion Helpers

        /// <summary>
        /// Verifies that the result is an OkObjectResult with the expected value.
        /// </summary>
        protected void VerifyOkResult<T>(ActionResult<T> result, Action<T>? valueAssertion = null)
        {
            result.Should().NotBeNull();
            result.Result.Should().BeOfType<OkObjectResult>();
            
            var okResult = result.Result as OkObjectResult;
            okResult.Should().NotBeNull();
            okResult!.Value.Should().NotBeNull();
            
            // Use BeAssignableTo instead of BeOfType to handle derived types like List<T> vs IList<T>
            okResult.Value.Should().BeAssignableTo<T>();
            
            if (valueAssertion != null && okResult.Value is T typedValue)
            {
                valueAssertion(typedValue);
            }
        }

        /// <summary>
        /// Verifies that the result is an OkObjectResult.
        /// </summary>
        protected void VerifyOkResult(IActionResult result)
        {
            result.Should().NotBeNull();
            result.Should().BeOfType<OkObjectResult>();
        }

        /// <summary>
        /// Verifies that the result is a NotFoundObjectResult.
        /// </summary>
        protected void VerifyNotFoundResult<T>(ActionResult<T> result, string? expectedMessage = null)
        {
            result.Should().NotBeNull();
            result.Result.Should().BeOfType<NotFoundObjectResult>();
            
            if (expectedMessage != null)
            {
                var notFoundResult = result.Result as NotFoundObjectResult;
                notFoundResult!.Value.Should().NotBeNull();
                notFoundResult.Value.ToString()!.Should().Contain(expectedMessage);
            }
        }

        /// <summary>
        /// Verifies that the result is a BadRequestObjectResult.
        /// </summary>
        protected void VerifyBadRequestResult<T>(ActionResult<T> result, string? expectedError = null)
        {
            result.Should().NotBeNull();
            result.Result.Should().BeOfType<BadRequestObjectResult>();
            
            if (expectedError != null)
            {
                var badRequestResult = result.Result as BadRequestObjectResult;
                badRequestResult!.Value.Should().NotBeNull();
                
                // Handle structured error responses with errors dictionary
                var valueString = System.Text.Json.JsonSerializer.Serialize(badRequestResult.Value);
                valueString.Should().Contain(expectedError);
            }
        }

        /// <summary>
        /// Verifies that the result is an ObjectResult with the expected status code.
        /// </summary>
        protected void VerifyStatusCodeResult<T>(ActionResult<T> result, int expectedStatusCode)
        {
            result.Should().NotBeNull();
            result.Result.Should().NotBeNull();
            
            // Accept ObjectResult or any derived type (NotFoundObjectResult, BadRequestObjectResult, etc.)
            result.Result.Should().BeAssignableTo<ObjectResult>();
            
            var objectResult = result.Result as ObjectResult;
            objectResult!.StatusCode.Should().Be(expectedStatusCode);
        }

        /// <summary>
        /// Verifies that the result is an ObjectResult with the expected status code.
        /// </summary>
        protected void VerifyStatusCodeResult(IActionResult result, int expectedStatusCode)
        {
            result.Should().NotBeNull();
            
            // Accept ObjectResult or any derived type (NotFoundObjectResult, BadRequestObjectResult, etc.)
            result.Should().BeAssignableTo<ObjectResult>();
            
            var objectResult = result as ObjectResult;
            objectResult!.StatusCode.Should().Be(expectedStatusCode);
        }

        /// <summary>
        /// Extracts the value from an ActionResult.
        /// </summary>
        protected T? GetValueFromResult<T>(ActionResult<T> result)
        {
            if (result.Result is OkObjectResult okResult)
            {
                return (T?)okResult.Value;
            }
            return result.Value;
        }

        #endregion

        #region Validation Testing Helpers

        /// <summary>
        /// Adds a model state error to simulate validation failure.
        /// </summary>
        protected void AddModelStateError(ControllerBase controller, string key, string errorMessage)
        {
            controller.ModelState.AddModelError(key, errorMessage);
        }

        /// <summary>
        /// Simulates invalid model state for testing validation.
        /// </summary>
        protected void SimulateInvalidModelState(ControllerBase controller, string fieldName = "TestField", string errorMessage = "Test validation error")
        {
            controller.ModelState.AddModelError(fieldName, errorMessage);
        }

        #endregion

        #region Mock Creation Helpers

        /// <summary>
        /// Creates a mock logger with the specified type.
        /// </summary>
        protected Mock<ILogger<T>> CreateMockLogger<T>()
        {
            return new Mock<ILogger<T>>();
        }

        #endregion
    }
}
