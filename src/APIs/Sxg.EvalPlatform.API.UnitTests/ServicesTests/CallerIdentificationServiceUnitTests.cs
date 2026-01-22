using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;
using Sxg.EvalPlatform.API.UnitTests.RequestHandlerTests;
using SxgEvalPlatformApi.Controllers;
using SxgEvalPlatformApi.Services;
using System.Security.Claims;

namespace Sxg.EvalPlatform.API.UnitTests.ServicesTests
{
    /// <summary>
    /// Comprehensive unit tests for CallerIdentificationService.
    /// Tests all authentication flows: DirectUser, AppToApp, and DelegatedAppToApp.
    /// </summary>
    public class CallerIdentificationServiceUnitTests
    {
        private readonly Mock<IHttpContextAccessor> _mockHttpContextAccessor;
        private readonly Mock<ILogger<CallerIdentificationService>> _mockLogger;
        private readonly CallerIdentificationService _service;
        private readonly DefaultHttpContext _httpContext;

        public CallerIdentificationServiceUnitTests()
        {
            _mockHttpContextAccessor = new Mock<IHttpContextAccessor>();
            _mockLogger = new Mock<ILogger<CallerIdentificationService>>();
            _httpContext = new DefaultHttpContext();
            
            _mockHttpContextAccessor.Setup(x => x.HttpContext).Returns(_httpContext);
            _service = new CallerIdentificationService(_mockHttpContextAccessor.Object, _mockLogger.Object);
        }

        #region Constructor Tests

        [Fact]
        public void Constructor_WithValidParameters_CreatesInstance()
        {
            // Arrange & Act
            var service = new CallerIdentificationService(_mockHttpContextAccessor.Object, _mockLogger.Object);

            // Assert
            service.Should().NotBeNull();
            service.Should().BeAssignableTo<ICallerIdentificationService>();
        }

        [Fact]
        public void Constructor_WithNullHttpContextAccessor_ThrowsArgumentNullException()
        {
            // Arrange, Act & Assert
            Assert.Throws<ArgumentNullException>(() => 
                new CallerIdentificationService(null!, _mockLogger.Object));
        }

        [Fact]
        public void Constructor_WithNullLogger_ThrowsArgumentNullException()
        {
            // Arrange, Act & Assert
            Assert.Throws<ArgumentNullException>(() => 
                new CallerIdentificationService(_mockHttpContextAccessor.Object, null!));
        }

        #endregion

        #region GetCurrentUserId Tests

        [Fact]
        public void GetCurrentUserId_WithNullUser_ReturnsUnknown()
        {
            // Arrange
            _httpContext.User = null!;

            // Act
            var result = _service.GetCurrentUserId();

            // Assert
            result.Should().Be("unknown");
        }

        [Fact]
        public void GetCurrentUserId_WithDelegatedUserId_ReturnsDelegatedUserId()
        {
            // Arrange
            var claims = new List<Claim>
            {
                new Claim("delegated_user_id", TestConstants.Users.DelegatedUserId),
                new Claim("oid", "direct-user-id") // Should be ignored
            };
            _httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(claims));

            // Act
            var result = _service.GetCurrentUserId();

            // Assert
            result.Should().Be(TestConstants.Users.DelegatedUserId);
        }

        [Fact]
        public void GetCurrentUserId_WithOidClaim_ReturnsOid()
        {
            // Arrange
            var claims = new List<Claim>
            {
                new Claim("oid", TestConstants.Users.DefaultUserId)
            };
            _httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(claims));

            // Act
            var result = _service.GetCurrentUserId();

            // Assert
            result.Should().Be(TestConstants.Users.DefaultUserId);
        }

        [Fact]
        public void GetCurrentUserId_WithSubClaim_ReturnsSub()
        {
            // Arrange
            var claims = new List<Claim>
            {
                new Claim("sub", "subject-user-id")
            };
            _httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(claims));

            // Act
            var result = _service.GetCurrentUserId();

            // Assert
            result.Should().Be("subject-user-id");
        }

        [Theory]
        [InlineData("0")]
        [InlineData("unknown")]
        public void GetCurrentUserId_WithInvalidDelegatedUserId_FallsBackToOid(string invalidValue)
        {
            // Arrange
            var claims = new List<Claim>
            {
                new Claim("delegated_user_id", invalidValue),
                new Claim("oid", TestConstants.Users.DefaultUserId)
            };
            _httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(claims));

            // Act
            var result = _service.GetCurrentUserId();

            // Assert
            result.Should().Be(TestConstants.Users.DefaultUserId);
        }

        [Fact]
        public void GetCurrentUserId_WithNoClaims_ReturnsUnknown()
        {
            // Arrange
            _httpContext.User = new ClaimsPrincipal(new ClaimsIdentity());

            // Act
            var result = _service.GetCurrentUserId();

            // Assert
            result.Should().Be("unknown");
        }

        #endregion

        #region GetCurrentUserEmail Tests

        [Fact]
        public void GetCurrentUserEmail_WithNullUser_ReturnsUnknown()
        {
            // Arrange
            _httpContext.User = null!;

            // Act
            var result = _service.GetCurrentUserEmail();

            // Assert
            result.Should().Be("unknown");
        }

        [Fact]
        public void GetCurrentUserEmail_WithDelegatedEmail_ReturnsDelegatedEmail()
        {
            // Arrange
            var claims = new List<Claim>
            {
                new Claim("delegated_user_email", TestConstants.Users.DelegatedEmail),
                new Claim("preferred_username", "other@example.com")
            };
            _httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(claims));

            // Act
            var result = _service.GetCurrentUserEmail();

            // Assert
            result.Should().Be(TestConstants.Users.DelegatedEmail);
        }

        [Fact]
        public void GetCurrentUserEmail_WithPreferredUsername_ReturnsPreferredUsername()
        {
            // Arrange
            var claims = new List<Claim>
            {
                new Claim("preferred_username", TestConstants.Users.DefaultEmail)
            };
            _httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(claims));

            // Act
            var result = _service.GetCurrentUserEmail();

            // Assert
            result.Should().Be(TestConstants.Users.DefaultEmail);
        }

        [Fact]
        public void GetCurrentUserEmail_WithEmailClaim_ReturnsEmail()
        {
            // Arrange
            var claims = new List<Claim>
            {
                new Claim("email", "user@example.com")
            };
            _httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(claims));

            // Act
            var result = _service.GetCurrentUserEmail();

            // Assert
            result.Should().Be("user@example.com");
        }

        [Fact]
        public void GetCurrentUserEmail_WithUpnClaim_ReturnsUpn()
        {
            // Arrange
            var claims = new List<Claim>
            {
                new Claim("upn", "user@domain.com")
            };
            _httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(claims));

            // Act
            var result = _service.GetCurrentUserEmail();

            // Assert
            result.Should().Be("user@domain.com");
        }

        [Fact]
        public void GetCurrentUserEmail_WithLegacyEmailAddressClaim_ReturnsEmail()
        {
            // Arrange
            var claims = new List<Claim>
            {
                new Claim("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress", "legacy@example.com")
            };
            _httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(claims));

            // Act
            var result = _service.GetCurrentUserEmail();

            // Assert
            result.Should().Be("legacy@example.com");
        }

        [Fact]
        public void GetCurrentUserEmail_WithLegacyUpnClaim_ReturnsUpn()
        {
            // Arrange
            var claims = new List<Claim>
            {
                new Claim("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/upn", "legacy.upn@example.com")
            };
            _httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(claims));

            // Act
            var result = _service.GetCurrentUserEmail();

            // Assert
            result.Should().Be("legacy.upn@example.com");
        }

        [Fact]
        public void GetCurrentUserEmail_WithLegacyNameClaim_ReturnsName()
        {
            // Arrange
            var claims = new List<Claim>
            {
                new Claim("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name", "legacy.name@example.com")
            };
            _httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(claims));

            // Act
            var result = _service.GetCurrentUserEmail();

            // Assert
            result.Should().Be("legacy.name@example.com");
        }

        [Theory]
        [InlineData("0")]
        [InlineData("unknown")]
        public void GetCurrentUserEmail_WithInvalidEmailValues_SkipsThem(string invalidValue)
        {
            // Arrange
            var claims = new List<Claim>
            {
                new Claim("preferred_username", invalidValue),
                new Claim("email", TestConstants.Users.DefaultEmail)
            };
            _httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(claims));

            // Act
            var result = _service.GetCurrentUserEmail();

            // Assert
            result.Should().Be(TestConstants.Users.DefaultEmail);
        }

        [Fact]
        public void GetCurrentUserEmail_WithNoClaims_ReturnsUnknown()
        {
            // Arrange
            _httpContext.User = new ClaimsPrincipal(new ClaimsIdentity());

            // Act
            var result = _service.GetCurrentUserEmail();

            // Assert
            result.Should().Be("unknown");
        }

        #endregion

        #region GetCurrentTenantId Tests

        [Fact]
        public void GetCurrentTenantId_WithNullUser_ReturnsUnknown()
        {
            // Arrange
            _httpContext.User = null!;

            // Act
            var result = _service.GetCurrentTenantId();

            // Assert
            result.Should().Be("unknown");
        }

        [Fact]
        public void GetCurrentTenantId_WithDelegatedTenant_ReturnsDelegatedTenant()
        {
            // Arrange
            var claims = new List<Claim>
            {
                new Claim("delegated_user_tenant", "delegated-tenant-id"),
                new Claim("tid", "direct-tenant-id")
            };
            _httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(claims));

            // Act
            var result = _service.GetCurrentTenantId();

            // Assert
            result.Should().Be("delegated-tenant-id");
        }

        [Fact]
        public void GetCurrentTenantId_WithTidClaim_ReturnsTid()
        {
            // Arrange
            var claims = new List<Claim>
            {
                new Claim("tid", "tenant-id-123")
            };
            _httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(claims));

            // Act
            var result = _service.GetCurrentTenantId();

            // Assert
            result.Should().Be("tenant-id-123");
        }

        [Fact]
        public void GetCurrentTenantId_WithNoClaims_ReturnsUnknown()
        {
            // Arrange
            _httpContext.User = new ClaimsPrincipal(new ClaimsIdentity());

            // Act
            var result = _service.GetCurrentTenantId();

            // Assert
            result.Should().Be("unknown");
        }

        #endregion

        #region GetCallingApplicationId Tests

        [Fact]
        public void GetCallingApplicationId_WithNullUser_ReturnsNull()
        {
            // Arrange
            _httpContext.User = null!;

            // Act
            var result = _service.GetCallingApplicationId();

            // Assert
            result.Should().BeNull();
        }

        [Fact]
        public void GetCallingApplicationId_WithAppidClaim_ReturnsAppid()
        {
            // Arrange
            var claims = new List<Claim>
            {
                new Claim("appid", "app-id-123")
            };
            _httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(claims));

            // Act
            var result = _service.GetCallingApplicationId();

            // Assert
            result.Should().Be("app-id-123");
        }

        [Fact]
        public void GetCallingApplicationId_WithAzpClaim_ReturnsAzp()
        {
            // Arrange
            var claims = new List<Claim>
            {
                new Claim("azp", "azp-app-id-456")
            };
            _httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(claims));

            // Act
            var result = _service.GetCallingApplicationId();

            // Assert
            result.Should().Be("azp-app-id-456");
        }

        [Fact]
        public void GetCallingApplicationId_WithBothAppidAndAzp_PrefersAppid()
        {
            // Arrange
            var claims = new List<Claim>
            {
                new Claim("appid", "app-id-123"),
                new Claim("azp", "azp-app-id-456")
            };
            _httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(claims));

            // Act
            var result = _service.GetCallingApplicationId();

            // Assert
            result.Should().Be("app-id-123");
        }

        [Fact]
        public void GetCallingApplicationId_WithNoClaims_ReturnsNull()
        {
            // Arrange
            _httpContext.User = new ClaimsPrincipal(new ClaimsIdentity());

            // Act
            var result = _service.GetCallingApplicationId();

            // Assert
            result.Should().BeNull();
        }

        #endregion

        #region GetCallingApplicationName Tests

        [Fact]
        public void GetCallingApplicationName_WithNullUser_ReturnsUnknown()
        {
            // Arrange
            _httpContext.User = null!;

            // Act
            var result = _service.GetCallingApplicationName();

            // Assert
            result.Should().Be("unknown");
        }

        [Fact]
        public void GetCallingApplicationName_WithAppDisplayName_ReturnsAppDisplayName()
        {
            // Arrange
            var claims = new List<Claim>
            {
                new Claim("app_displayname", "My Application")
            };
            _httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(claims));

            // Act
            var result = _service.GetCallingApplicationName();

            // Assert
            result.Should().Be("My Application");
        }

        [Fact]
        public void GetCallingApplicationName_WithAzpacr_ReturnsAzpacr()
        {
            // Arrange
            var claims = new List<Claim>
            {
                new Claim("azpacr", "azpacr-value")
            };
            _httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(claims));

            // Act
            var result = _service.GetCallingApplicationName();

            // Assert
            result.Should().Be("azpacr-value");
        }

        [Fact]
        public void GetCallingApplicationName_WithAppId_ReturnsAppId()
        {
            // Arrange
            var claims = new List<Claim>
            {
                new Claim("appid", "app-id-789")
            };
            _httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(claims));

            // Act
            var result = _service.GetCallingApplicationName();

            // Assert
            result.Should().Be("app-id-789");
        }

        [Theory]
        [InlineData("0")]
        [InlineData("unknown")]
        public void GetCallingApplicationName_WithInvalidValues_SkipsThem(string invalidValue)
        {
            // Arrange
            var claims = new List<Claim>
            {
                new Claim("app_displayname", invalidValue),
                new Claim("appid", "app-id-valid")
            };
            _httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(claims));

            // Act
            var result = _service.GetCallingApplicationName();

            // Assert
            result.Should().Be("app-id-valid");
        }

        [Fact]
        public void GetCallingApplicationName_WithNoClaims_ReturnsUnknown()
        {
            // Arrange
            _httpContext.User = new ClaimsPrincipal(new ClaimsIdentity());

            // Act
            var result = _service.GetCallingApplicationName();

            // Assert
            result.Should().Be("unknown");
        }

        #endregion

        #region IsServicePrincipalCall Tests

        [Fact]
        public void IsServicePrincipalCall_WithNullUser_ReturnsFalse()
        {
            // Arrange
            _httpContext.User = null!;

            // Act
            var result = _service.IsServicePrincipalCall();

            // Assert
            result.Should().BeFalse();
        }

        [Fact]
        public void IsServicePrincipalCall_WithAppIdAndNoUserClaims_ReturnsTrue()
        {
            // Arrange
            var claims = new List<Claim>
            {
                new Claim("appid", "app-id-123")
            };
            _httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(claims));

            // Act
            var result = _service.IsServicePrincipalCall();

            // Assert
            result.Should().BeTrue();
        }

        [Fact]
        public void IsServicePrincipalCall_WithAppIdAndOid_ReturnsFalse()
        {
            // Arrange
            var claims = new List<Claim>
            {
                new Claim("appid", "app-id-123"),
                new Claim("oid", "user-id-456")
            };
            _httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(claims));

            // Act
            var result = _service.IsServicePrincipalCall();

            // Assert
            result.Should().BeFalse();
        }

        [Fact]
        public void IsServicePrincipalCall_WithAppIdAndPreferredUsername_ReturnsFalse()
        {
            // Arrange
            var claims = new List<Claim>
            {
                new Claim("appid", "app-id-123"),
                new Claim("preferred_username", "user@example.com")
            };
            _httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(claims));

            // Act
            var result = _service.IsServicePrincipalCall();

            // Assert
            result.Should().BeFalse();
        }

        [Fact]
        public void IsServicePrincipalCall_WithNoAppId_ReturnsFalse()
        {
            // Arrange
            var claims = new List<Claim>
            {
                new Claim("oid", "user-id-123")
            };
            _httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(claims));

            // Act
            var result = _service.IsServicePrincipalCall();

            // Assert
            result.Should().BeFalse();
        }

        #endregion

        #region HasDelegatedUserContext Tests

        [Fact]
        public void HasDelegatedUserContext_WithNullUser_ReturnsFalse()
        {
            // Arrange
            _httpContext.User = null!;

            // Act
            var result = _service.HasDelegatedUserContext();

            // Assert
            result.Should().BeFalse();
        }

        [Fact]
        public void HasDelegatedUserContext_WithDelegatedUserId_ReturnsTrue()
        {
            // Arrange
            var claims = new List<Claim>
            {
                new Claim("delegated_user_id", "delegated-user-123")
            };
            _httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(claims));

            // Act
            var result = _service.HasDelegatedUserContext();

            // Assert
            result.Should().BeTrue();
        }

        [Fact]
        public void HasDelegatedUserContext_WithNoDelegatedUserId_ReturnsFalse()
        {
            // Arrange
            _httpContext.User = new ClaimsPrincipal(new ClaimsIdentity());

            // Act
            var result = _service.HasDelegatedUserContext();

            // Assert
            result.Should().BeFalse();
        }

        #endregion

        #region GetCallerInfo Tests

        [Fact]
        public void GetCallerInfo_WithDirectUserAuth_ReturnsCorrectInfo()
        {
            // Arrange
            var claims = new List<Claim>
            {
                new Claim("oid", TestConstants.Users.DefaultUserId),
                new Claim("preferred_username", TestConstants.Users.DefaultEmail),
                new Claim("tid", "tenant-123")
            };
            _httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(claims));

            // Act
            var result = _service.GetCallerInfo();

            // Assert
            result.Should().NotBeNull();
            result.UserId.Should().Be(TestConstants.Users.DefaultUserId);
            result.UserEmail.Should().Be(TestConstants.Users.DefaultEmail);
            result.TenantId.Should().Be("tenant-123");
            result.IsServicePrincipal.Should().BeFalse();
            result.HasDelegatedUser.Should().BeFalse();
            result.AuthenticationType.Should().Be("DirectUser");
        }

        [Fact]
        public void GetCallerInfo_WithAppToAppAuth_ReturnsCorrectInfo()
        {
            // Arrange
            var claims = new List<Claim>
            {
                new Claim("appid", TestConstants.Applications.ServiceApp),
                new Claim("tid", "tenant-456")
            };
            _httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(claims));

            // Act
            var result = _service.GetCallerInfo();

            // Assert
            result.Should().NotBeNull();
            result.ApplicationId.Should().Be(TestConstants.Applications.ServiceApp);
            result.IsServicePrincipal.Should().BeTrue();
            result.HasDelegatedUser.Should().BeFalse();
            result.AuthenticationType.Should().Be("AppToApp");
        }

        [Fact]
        public void GetCallerInfo_WithDelegatedAppToAppAuth_ReturnsCorrectInfo()
        {
            // Arrange
            var claims = new List<Claim>
            {
                new Claim("appid", TestConstants.Applications.ServiceApp),
                new Claim("delegated_user_id", TestConstants.Users.DelegatedUserId),
                new Claim("delegated_user_email", TestConstants.Users.DelegatedEmail),
                new Claim("tid", "tenant-789")
            };
            _httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(claims));

            // Act
            var result = _service.GetCallerInfo();

            // Assert
            result.Should().NotBeNull();
            result.UserId.Should().Be(TestConstants.Users.DelegatedUserId);
            result.UserEmail.Should().Be(TestConstants.Users.DelegatedEmail);
            result.ApplicationId.Should().Be(TestConstants.Applications.ServiceApp);
            result.IsServicePrincipal.Should().BeTrue();
            result.HasDelegatedUser.Should().BeTrue();
            result.AuthenticationType.Should().Be("DelegatedAppToApp");
        }

        #endregion

        #region GetCallerDescription Tests

        [Fact]
        public void GetCallerDescription_WithDirectUser_ReturnsUserDescription()
        {
            // Arrange
            var claims = new List<Claim>
            {
                new Claim("oid", TestConstants.Users.DefaultUserId),
                new Claim("preferred_username", TestConstants.Users.DefaultEmail)
            };
            _httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(claims));

            // Act
            var result = _service.GetCallerDescription();

            // Assert
            result.Should().Contain("User");
            result.Should().Contain(TestConstants.Users.DefaultEmail);
            result.Should().Contain(TestConstants.Users.DefaultUserId);
        }

        [Fact]
        public void GetCallerDescription_WithServicePrincipal_ReturnsAppDescription()
        {
            // Arrange
            var claims = new List<Claim>
            {
                new Claim("appid", TestConstants.Applications.ServiceApp),
                new Claim("app_displayname", "Test Service App")
            };
            _httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(claims));

            // Act
            var result = _service.GetCallerDescription();

            // Assert
            result.Should().Contain("Service Principal");
            result.Should().Contain(TestConstants.Applications.ServiceApp);
        }

        [Fact]
        public void GetCallerDescription_WithDelegatedUser_ReturnsCompleteDescription()
        {
            // Arrange
            var claims = new List<Claim>
            {
                new Claim("appid", TestConstants.Applications.ServiceApp),
                new Claim("delegated_user_id", TestConstants.Users.DelegatedUserId),
                new Claim("delegated_user_email", TestConstants.Users.DelegatedEmail),
                new Claim("app_displayname", "Delegating App")
            };
            _httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(claims));

            // Act
            var result = _service.GetCallerDescription();

            // Assert
            result.Should().Contain("Service Principal");
            result.Should().Contain("acting on behalf of user");
            result.Should().Contain(TestConstants.Users.DelegatedEmail);
        }

        #endregion

        #region Edge Cases and Integration Tests

        [Fact]
        public void GetCurrentUserId_WithMultipleClaims_PrioritizesDelegated()
        {
            // Arrange
            var claims = new List<Claim>
            {
                new Claim("delegated_user_id", "delegated-123"),
                new Claim("oid", "direct-456"),
                new Claim("sub", "sub-789")
            };
            _httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(claims));

            // Act
            var result = _service.GetCurrentUserId();

            // Assert
            result.Should().Be("delegated-123");
        }

        [Fact]
        public void GetCurrentUserEmail_WithMultipleClaims_PrioritizesDelegated()
        {
            // Arrange
            var claims = new List<Claim>
            {
                new Claim("delegated_user_email", "delegated@example.com"),
                new Claim("preferred_username", "direct@example.com"),
                new Claim("email", "email@example.com")
            };
            _httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(claims));

            // Act
            var result = _service.GetCurrentUserEmail();

            // Assert
            result.Should().Be("delegated@example.com");
        }

        [Fact]
        public void CallerInfo_WithCompleteClaimSet_PopulatesAllProperties()
        {
            // Arrange
            var claims = new List<Claim>
            {
                new Claim("appid", "app-123"),
                new Claim("delegated_user_id", "user-456"),
                new Claim("delegated_user_email", "user@example.com"),
                new Claim("delegated_user_tenant", "tenant-789"),
                new Claim("app_displayname", "Test Application")
            };
            _httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(claims));

            // Act
            var result = _service.GetCallerInfo();

            // Assert
            result.UserId.Should().Be("user-456");
            result.UserEmail.Should().Be("user@example.com");
            result.TenantId.Should().Be("tenant-789");
            result.ApplicationId.Should().Be("app-123");
            result.ApplicationName.Should().Be("Test Application");
            result.IsServicePrincipal.Should().BeTrue();
            result.HasDelegatedUser.Should().BeTrue();
            result.AuthenticationType.Should().Be("DelegatedAppToApp");
        }

        #endregion
    }
}
