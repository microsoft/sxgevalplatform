using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using System.Text.Encodings.Web;

namespace Sxg.EvalPlatform.API.UnitTests.Helpers;

/// <summary>
/// Mock authentication handler for testing authorization in controllers
/// Supports SF-13-4 authorization testing
/// </summary>
public class TestAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string AuthenticationScheme = "Test";
    public const string DefaultUserId = "test-user-id";
    public const string DefaultUserEmail = "test@example.com";
    public const string DefaultTenantId = "test-tenant-id";

    private readonly string? _userId;
    private readonly string? _userEmail;
    private readonly string? _tenantId;
    private readonly bool _shouldFail;
    private readonly IEnumerable<Claim>? _customClaims;

    public TestAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        ISystemClock clock,
        string? userId = null,
        string? userEmail = null,
        string? tenantId = null,
        bool shouldFail = false,
        IEnumerable<Claim>? customClaims = null)
        : base(options, logger, encoder, clock)
    {
        _userId = userId;
        _userEmail = userEmail;
        _tenantId = tenantId;
        _shouldFail = shouldFail;
        _customClaims = customClaims;
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (_shouldFail)
        {
            return Task.FromResult(AuthenticateResult.Fail("Authentication failed"));
        }

        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, _userId ?? DefaultUserId),
            new Claim(ClaimTypes.Email, _userEmail ?? DefaultUserEmail),
            new Claim("tid", _tenantId ?? DefaultTenantId),
            new Claim(ClaimTypes.Name, _userEmail ?? DefaultUserEmail)
        };

        if (_customClaims != null)
        {
            claims.AddRange(_customClaims);
        }

        var identity = new ClaimsIdentity(claims, AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, AuthenticationScheme);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}

/// <summary>
/// Builder for creating test authentication configurations
/// </summary>
public class TestAuthenticationBuilder
{
    private string? _userId;
    private string? _userEmail;
    private string? _tenantId;
    private bool _shouldFail;
    private List<Claim> _claims = new();

    public TestAuthenticationBuilder WithUserId(string userId)
    {
        _userId = userId;
        return this;
    }

    public TestAuthenticationBuilder WithUserEmail(string userEmail)
    {
        _userEmail = userEmail;
        return this;
    }

    public TestAuthenticationBuilder WithTenantId(string tenantId)
    {
        _tenantId = tenantId;
        return this;
    }

    public TestAuthenticationBuilder WithClaim(string type, string value)
    {
        _claims.Add(new Claim(type, value));
        return this;
    }

    public TestAuthenticationBuilder WithRole(string role)
    {
        _claims.Add(new Claim(ClaimTypes.Role, role));
        return this;
    }

    public TestAuthenticationBuilder ThatShouldFail()
    {
        _shouldFail = true;
        return this;
    }

    public (string? userId, string? userEmail, string? tenantId, bool shouldFail, IEnumerable<Claim> claims) Build()
    {
        return (_userId, _userEmail, _tenantId, _shouldFail, _claims);
    }
}

/// <summary>
/// Test authentication scenarios for security testing
/// </summary>
public static class TestAuthenticationScenarios
{
    public static TestAuthenticationBuilder ValidUser()
    {
        return new TestAuthenticationBuilder()
            .WithUserId("valid-user-123")
            .WithUserEmail("valid@example.com")
            .WithTenantId("tenant-123");
    }

    public static TestAuthenticationBuilder AdminUser()
    {
        return ValidUser()
            .WithRole("Admin")
            .WithClaim("scope", "eval.admin");
    }

    public static TestAuthenticationBuilder ReadOnlyUser()
    {
        return ValidUser()
            .WithRole("Reader")
            .WithClaim("scope", "eval.read");
    }

    public static TestAuthenticationBuilder ServicePrincipal()
    {
        return new TestAuthenticationBuilder()
            .WithUserId("service-principal-app-id")
            .WithClaim("oid", "app-object-id")
            .WithClaim("app_displayname", "Test Service Principal");
    }

    public static TestAuthenticationBuilder UnauthorizedUser()
    {
        return new TestAuthenticationBuilder()
            .ThatShouldFail();
    }

    public static TestAuthenticationBuilder DifferentTenantUser()
    {
        return ValidUser()
            .WithTenantId("different-tenant-456");
    }
}
