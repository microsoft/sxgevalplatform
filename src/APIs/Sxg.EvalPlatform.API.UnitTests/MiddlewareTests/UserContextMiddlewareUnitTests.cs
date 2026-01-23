using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;
using Sxg.EvalPlatform.API.UnitTests.RequestHandlerTests;
using SXG.EvalPlatform.API.Middleware;
using System.Security.Claims;

namespace Sxg.EvalPlatform.API.UnitTests.MiddlewareTests
{
    /// <summary>
    /// Comprehensive unit tests for UserContextMiddleware.
    /// Tests user context extraction from headers for service principal scenarios.
    /// </summary>
    public class UserContextMiddlewareUnitTests
    {
        private readonly Mock<ILogger<UserContextMiddleware>> _mockLogger;
        private readonly Mock<RequestDelegate> _mockNext;
        private readonly UserContextMiddleware _middleware;
        private readonly DefaultHttpContext _httpContext;

        public UserContextMiddlewareUnitTests()
        {
            _mockLogger = new Mock<ILogger<UserContextMiddleware>>();
            _mockNext = new Mock<RequestDelegate>();
            _middleware = new UserContextMiddleware(_mockNext.Object, _mockLogger.Object);
            _httpContext = new DefaultHttpContext();
        }

        #region Constructor Tests

        [Fact]
        public void Constructor_WithValidParameters_CreatesInstance()
        {
            // Arrange & Act
            var middleware = new UserContextMiddleware(_mockNext.Object, _mockLogger.Object);

            // Assert
            middleware.Should().NotBeNull();
        }

        [Fact]
        public void Constructor_WithNullNext_DoesNotThrow()
        {
            // Arrange, Act & Assert
            // Note: Constructor doesn't validate parameters, so it won't throw
            var middleware = new UserContextMiddleware(null!, _mockLogger.Object);
            middleware.Should().NotBeNull();
        }

        [Fact]
        public void Constructor_WithNullLogger_DoesNotThrow()
        {
            // Arrange, Act & Assert
            // Note: Constructor doesn't validate parameters, so it won't throw
            var middleware = new UserContextMiddleware(_mockNext.Object, null!);
            middleware.Should().NotBeNull();
        }

        #endregion

        #region InvokeAsync - Service Principal with User Context

        [Fact]
        public async Task InvokeAsync_WithServicePrincipalAndUserHeaders_AddsDelegatedUserClaims()
        {
            // Arrange
            var claims = new List<Claim>
            {
                new Claim("appid", TestConstants.Applications.ServiceApp)
            };
            _httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Bearer"));
            
            _httpContext.Request.Headers["X-User-Id"] = TestConstants.Users.DefaultUserId;
            _httpContext.Request.Headers["X-User-Email"] = TestConstants.Users.DefaultEmail;
            _httpContext.Request.Headers["X-User-Tenant"] = "test-tenant-id";
            
            _mockNext.Setup(next => next(_httpContext)).Returns(Task.CompletedTask);

            // Act
            await _middleware.InvokeAsync(_httpContext);

            // Assert
            _mockNext.Verify(next => next(_httpContext), Times.Once);
            
            var delegatedUserId = _httpContext.User.FindFirst("delegated_user_id");
            delegatedUserId.Should().NotBeNull();
            delegatedUserId!.Value.Should().Be(TestConstants.Users.DefaultUserId);
            
            var delegatedUserEmail = _httpContext.User.FindFirst("delegated_user_email");
            delegatedUserEmail.Should().NotBeNull();
            delegatedUserEmail!.Value.Should().Be(TestConstants.Users.DefaultEmail);
            
            var delegatedUserTenant = _httpContext.User.FindFirst("delegated_user_tenant");
            delegatedUserTenant.Should().NotBeNull();
            delegatedUserTenant!.Value.Should().Be("test-tenant-id");
        }

        [Fact]
        public async Task InvokeAsync_WithServicePrincipalAndUserId_AddsOnlyUserIdClaim()
        {
            // Arrange
            var claims = new List<Claim>
            {
                new Claim("appid", "test-app-id")
            };
            _httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Bearer"));
            
            _httpContext.Request.Headers["X-User-Id"] = "user-123";
            // No email or tenant headers
            
            _mockNext.Setup(next => next(_httpContext)).Returns(Task.CompletedTask);

            // Act
            await _middleware.InvokeAsync(_httpContext);

            // Assert
            var delegatedUserId = _httpContext.User.FindFirst("delegated_user_id");
            delegatedUserId.Should().NotBeNull();
            
            var delegatedUserEmail = _httpContext.User.FindFirst("delegated_user_email");
            delegatedUserEmail.Should().BeNull();
            
            var delegatedUserTenant = _httpContext.User.FindFirst("delegated_user_tenant");
            delegatedUserTenant.Should().BeNull();
        }

        [Fact]
        public async Task InvokeAsync_WithServicePrincipalAndUserContext_LogsInformation()
        {
            // Arrange
            var claims = new List<Claim>
            {
                new Claim("appid", "test-app-id")
            };
            _httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Bearer"));
            
            _httpContext.Request.Headers["X-User-Id"] = "user-123";
            _httpContext.Request.Headers["X-User-Email"] = "user@example.com";
            
            _mockNext.Setup(next => next(_httpContext)).Returns(Task.CompletedTask);

            // Act
            await _middleware.InvokeAsync(_httpContext);

            // Assert
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("acting on behalf of user")),
                    null,
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        #endregion

        #region InvokeAsync - Service Principal without User Context

        [Fact]
        public async Task InvokeAsync_WithServicePrincipalNoUserHeaders_DoesNotAddClaims()
        {
            // Arrange
            var claims = new List<Claim>
            {
                new Claim("appid", "test-app-id")
            };
            _httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Bearer"));
            // No user headers
            
            _mockNext.Setup(next => next(_httpContext)).Returns(Task.CompletedTask);

            // Act
            await _middleware.InvokeAsync(_httpContext);

            // Assert
            var delegatedUserId = _httpContext.User.FindFirst("delegated_user_id");
            delegatedUserId.Should().BeNull();
        }

        [Fact]
        public async Task InvokeAsync_WithServicePrincipalNoUserHeaders_LogsDebugMessage()
        {
            // Arrange
            var claims = new List<Claim>
            {
                new Claim("appid", "test-app-id")
            };
            _httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Bearer"));
            
            _mockNext.Setup(next => next(_httpContext)).Returns(Task.CompletedTask);

            // Act
            await _middleware.InvokeAsync(_httpContext);

            // Assert
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Debug,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("without user context")),
                    null,
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task InvokeAsync_WithServicePrincipalEmptyUserId_DoesNotAddClaims()
        {
            // Arrange
            var claims = new List<Claim>
            {
                new Claim("appid", "test-app-id")
            };
            _httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Bearer"));
            
            _httpContext.Request.Headers["X-User-Id"] = string.Empty;
            _httpContext.Request.Headers["X-User-Email"] = "user@example.com";
            
            _mockNext.Setup(next => next(_httpContext)).Returns(Task.CompletedTask);

            // Act
            await _middleware.InvokeAsync(_httpContext);

            // Assert
            var delegatedUserId = _httpContext.User.FindFirst("delegated_user_id");
            delegatedUserId.Should().BeNull();
        }

        #endregion

        #region InvokeAsync - Direct User Authentication

        [Fact]
        public async Task InvokeAsync_WithDirectUserAuthentication_LogsDebugMessage()
        {
            // Arrange
            var claims = new List<Claim>
            {
                new Claim("oid", TestConstants.Users.DefaultUserId),
                new Claim("preferred_username", TestConstants.Users.DefaultEmail)
            };
            _httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Bearer"));
            
            _mockNext.Setup(next => next(_httpContext)).Returns(Task.CompletedTask);

            // Act
            await _middleware.InvokeAsync(_httpContext);

            // Assert
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Debug,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Direct user authentication")),
                    null,
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task InvokeAsync_WithDirectUserAuthenticationUpn_LogsDebugMessage()
        {
            // Arrange
            var claims = new List<Claim>
            {
                new Claim("oid", "user-123"),
                new Claim("upn", "user@example.com") // UPN instead of preferred_username
            };
            _httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Bearer"));
            
            _mockNext.Setup(next => next(_httpContext)).Returns(Task.CompletedTask);

            // Act
            await _middleware.InvokeAsync(_httpContext);

            // Assert
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Debug,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Direct user authentication")),
                    null,
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task InvokeAsync_WithDirectUserNoUsername_LogsDebugMessage()
        {
            // Arrange
            var claims = new List<Claim>
            {
                new Claim("oid", "user-123")
                // No preferred_username or upn
            };
            _httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Bearer"));
            
            _mockNext.Setup(next => next(_httpContext)).Returns(Task.CompletedTask);

            // Act
            await _middleware.InvokeAsync(_httpContext);

            // Assert
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Debug,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("unknown")),
                    null,
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task InvokeAsync_WithDirectUserNoOid_DoesNotLog()
        {
            // Arrange
            var claims = new List<Claim>
            {
                new Claim("preferred_username", "user@example.com")
                // No oid
            };
            _httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Bearer"));
            
            _mockNext.Setup(next => next(_httpContext)).Returns(Task.CompletedTask);

            // Act
            await _middleware.InvokeAsync(_httpContext);

            // Assert
            _mockLogger.Verify(
                x => x.Log(
                    It.IsAny<LogLevel>(),
                    It.IsAny<EventId>(),
                    It.IsAny<It.IsAnyType>(),
                    It.IsAny<Exception?>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Never);
        }

        #endregion

        #region InvokeAsync - Edge Cases

        [Fact]
        public async Task InvokeAsync_WithNoClaims_ProcessesSuccessfully()
        {
            // Arrange
            _httpContext.User = new ClaimsPrincipal(new ClaimsIdentity());
            _mockNext.Setup(next => next(_httpContext)).Returns(Task.CompletedTask);

            // Act
            await _middleware.InvokeAsync(_httpContext);

            // Assert
            _mockNext.Verify(next => next(_httpContext), Times.Once);
        }

        [Fact]
        public async Task InvokeAsync_WithNullUserIdentity_ProcessesSuccessfully()
        {
            // Arrange
            _httpContext.User = new ClaimsPrincipal();
            _mockNext.Setup(next => next(_httpContext)).Returns(Task.CompletedTask);

            // Act
            await _middleware.InvokeAsync(_httpContext);

            // Assert
            _mockNext.Verify(next => next(_httpContext), Times.Once);
        }

        [Fact]
        public async Task InvokeAsync_WithMultipleUserHeaders_UsesFirstValue()
        {
            // Arrange
            var claims = new List<Claim>
            {
                new Claim("appid", "test-app-id")
            };
            _httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Bearer"));
            
            // Add multiple values (though HTTP spec doesn't really allow this for custom headers)
            _httpContext.Request.Headers["X-User-Id"] = "user-123";
            
            _mockNext.Setup(next => next(_httpContext)).Returns(Task.CompletedTask);

            // Act
            await _middleware.InvokeAsync(_httpContext);

            // Assert
            var delegatedUserId = _httpContext.User.FindFirst("delegated_user_id");
            delegatedUserId.Should().NotBeNull();
            delegatedUserId!.Value.Should().Be("user-123");
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public async Task InvokeAsync_WithEmptyOrNullUserId_DoesNotAddClaims(string? userId)
        {
            // Arrange
            var claims = new List<Claim>
            {
                new Claim("appid", "test-app-id")
            };
            _httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Bearer"));
            
            if (userId != null)
            {
                _httpContext.Request.Headers["X-User-Id"] = userId;
            }
            
            _mockNext.Setup(next => next(_httpContext)).Returns(Task.CompletedTask);

            // Act
            await _middleware.InvokeAsync(_httpContext);

            // Assert
            var delegatedUserId = _httpContext.User.FindFirst("delegated_user_id");
            delegatedUserId.Should().BeNull();
        }

        #endregion

        #region InvokeAsync - Special Characters and Encoding

        [Fact]
        public async Task InvokeAsync_WithSpecialCharactersInHeaders_HandlesCorrectly()
        {
            // Arrange
            var claims = new List<Claim>
            {
                new Claim("appid", "test-app-id")
            };
            _httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Bearer"));
            
            _httpContext.Request.Headers["X-User-Id"] = "user-123-הצü";
            _httpContext.Request.Headers["X-User-Email"] = "test@example.com";
            
            _mockNext.Setup(next => next(_httpContext)).Returns(Task.CompletedTask);

            // Act
            await _middleware.InvokeAsync(_httpContext);

            // Assert
            var delegatedUserId = _httpContext.User.FindFirst("delegated_user_id");
            delegatedUserId.Should().NotBeNull();
        }

        [Fact]
        public async Task InvokeAsync_WithLongHeaderValues_HandlesCorrectly()
        {
            // Arrange
            var claims = new List<Claim>
            {
                new Claim("appid", "test-app-id")
            };
            _httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Bearer"));
            
            var longUserId = new string('a', 500);
            _httpContext.Request.Headers["X-User-Id"] = longUserId;
            
            _mockNext.Setup(next => next(_httpContext)).Returns(Task.CompletedTask);

            // Act
            await _middleware.InvokeAsync(_httpContext);

            // Assert
            var delegatedUserId = _httpContext.User.FindFirst("delegated_user_id");
            delegatedUserId.Should().NotBeNull();
            delegatedUserId!.Value.Length.Should().BeLessOrEqualTo(500);
        }

        #endregion

        #region InvokeAsync - Call Next Middleware

        [Fact]
        public async Task InvokeAsync_AlwaysCallsNextMiddleware()
        {
            // Arrange
            _mockNext.Setup(next => next(_httpContext)).Returns(Task.CompletedTask);

            // Act
            await _middleware.InvokeAsync(_httpContext);

            // Assert
            _mockNext.Verify(next => next(_httpContext), Times.Once);
        }

        [Fact]
        public async Task InvokeAsync_WhenNextThrowsException_PropagatesException()
        {
            // Arrange
            var expectedException = new InvalidOperationException("Test exception");
            _mockNext.Setup(next => next(_httpContext)).ThrowsAsync(expectedException);

            // Act & Assert
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(
                () => _middleware.InvokeAsync(_httpContext));

            exception.Should().Be(expectedException);
        }

        #endregion

        #region Extension Method Tests

        [Fact]
        public void UseUserContext_WithValidApplicationBuilder_AddsMiddleware()
        {
            // Arrange
            var mockApplicationBuilder = new Mock<IApplicationBuilder>();
            mockApplicationBuilder
                .Setup(x => x.Use(It.IsAny<Func<RequestDelegate, RequestDelegate>>()))
                .Returns(mockApplicationBuilder.Object);

            // Act
            var result = mockApplicationBuilder.Object.UseUserContext();

            // Assert
            result.Should().NotBeNull();
            mockApplicationBuilder.Verify(
                x => x.Use(It.IsAny<Func<RequestDelegate, RequestDelegate>>()), 
                Times.Once);
        }

        #endregion

        #region Integration Tests

        [Fact]
        public async Task InvokeAsync_ServicePrincipalWithAllHeaders_CompleteFlow()
        {
            // Arrange
            var claims = new List<Claim>
            {
                new Claim("appid", TestConstants.Applications.ServiceApp),
                new Claim("azp", TestConstants.Applications.ServiceApp)
            };
            _httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Bearer"));
            
            _httpContext.Request.Headers["X-User-Id"] = TestConstants.Users.DefaultUserId;
            _httpContext.Request.Headers["X-User-Email"] = TestConstants.Users.DefaultEmail;
            _httpContext.Request.Headers["X-User-Tenant"] = "test-tenant-123";
            
            _mockNext.Setup(next => next(_httpContext)).Returns(Task.CompletedTask);

            // Act
            await _middleware.InvokeAsync(_httpContext);

            // Assert
            _mockNext.Verify(next => next(_httpContext), Times.Once);
            
            // Verify all claims added
            _httpContext.User.Claims.Should().Contain(c => c.Type == "delegated_user_id");
            _httpContext.User.Claims.Should().Contain(c => c.Type == "delegated_user_email");
            _httpContext.User.Claims.Should().Contain(c => c.Type == "delegated_user_tenant");
            
            // Verify logging
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.IsAny<It.IsAnyType>(),
                    null,
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task InvokeAsync_DirectUserWithAllClaims_CompleteFlow()
        {
            // Arrange
            var claims = new List<Claim>
            {
                new Claim("oid", TestConstants.Users.DefaultUserId),
                new Claim("preferred_username", TestConstants.Users.DefaultEmail),
                new Claim("name", "Test User")
            };
            _httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Bearer"));
            
            _mockNext.Setup(next => next(_httpContext)).Returns(Task.CompletedTask);

            // Act
            await _middleware.InvokeAsync(_httpContext);

            // Assert
            _mockNext.Verify(next => next(_httpContext), Times.Once);
            
            // Should not add delegated claims
            _httpContext.User.FindFirst("delegated_user_id").Should().BeNull();
            
            // Verify logging
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Debug,
                    It.IsAny<EventId>(),
                    It.IsAny<It.IsAnyType>(),
                    null,
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        #endregion
    }
}
