using System.Security.Claims;
using SxgEvalPlatformApi.Controllers;

namespace SxgEvalPlatformApi.Services;

/// <summary>
/// Interface for caller identification service
/// </summary>
public interface ICallerIdentificationService
{
    /// <summary>
    /// Gets the caller's user ID, checking delegated context first (from service principal headers)
    /// then falling back to direct user authentication
    /// </summary>
    string GetCurrentUserId();

    /// <summary>
    /// Gets the caller's email, checking delegated context first
    /// </summary>
    string GetCurrentUserEmail();

    /// <summary>
    /// Gets the caller's tenant ID
    /// </summary>
    string GetCurrentTenantId();

    /// <summary>
    /// Gets the calling application's Client ID (for app-to-app scenarios)
    /// </summary>
    string? GetCallingApplicationId();

    /// <summary>
    /// Gets the calling application's name
    /// </summary>
    string GetCallingApplicationName();

    /// <summary>
    /// Checks if the current request is from a service principal (app-to-app)
    /// </summary>
    bool IsServicePrincipalCall();

    /// <summary>
    /// Checks if the current request has delegated user context
    /// (service principal acting on behalf of a user)
    /// </summary>
    bool HasDelegatedUserContext();

    /// <summary>
    /// Gets comprehensive caller information for logging and auditing
    /// </summary>
    CallerInfo GetCallerInfo();

    /// <summary>
    /// Gets a formatted string describing the caller for logging
    /// </summary>
    string GetCallerDescription();
}

/// <summary>
/// Service for identifying the caller (user or service principal) in API requests
/// Supports DirectUser, AppToApp, and DelegatedAppToApp authentication flows
/// </summary>
public class CallerIdentificationService : ICallerIdentificationService
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<CallerIdentificationService> _logger;

    public CallerIdentificationService(
        IHttpContextAccessor httpContextAccessor,
        ILogger<CallerIdentificationService> logger)
    {
        _httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    private ClaimsPrincipal? User => _httpContextAccessor.HttpContext?.User;

    /// <summary>
    /// Gets the caller's user ID, checking delegated context first (from service principal headers)
    /// then falling back to direct user authentication
    /// </summary>
    public string GetCurrentUserId()
    {
        if (User == null) return "unknown";

        // Try delegated user first (from service principal via middleware)
        var delegatedUserId = User.FindFirst("delegated_user_id")?.Value;
        if (!string.IsNullOrEmpty(delegatedUserId) && delegatedUserId != "0" && delegatedUserId != "unknown")
        {
            _logger.LogDebug("Using delegated user ID: {UserId}", delegatedUserId);
            return delegatedUserId;
        }

        // Fall back to direct user authentication
        var directUserId = User.FindFirst("oid")?.Value;
        if (!string.IsNullOrEmpty(directUserId) && directUserId != "0" && directUserId != "unknown")
        {
            _logger.LogDebug("Using direct user ID (oid): {UserId}", directUserId);
            return directUserId;
        }

        // Try alternative user ID claims
        var sub = User.FindFirst("sub")?.Value;
        if (!string.IsNullOrEmpty(sub) && sub != "0" && sub != "unknown")
        {
            _logger.LogDebug("Using sub claim: {UserId}", sub);
            return sub;
        }

        _logger.LogWarning("No valid user ID claim found in token");
        return "unknown";
    }

    /// <summary>
    /// Gets the caller's email, checking delegated context first
    /// </summary>
    public string GetCurrentUserEmail()
    {
        if (User == null) return "unknown";

        // Try delegated email first
        var delegatedEmail = User.FindFirst("delegated_user_email")?.Value;
        if (!string.IsNullOrEmpty(delegatedEmail) && delegatedEmail != "0")
        {
            return delegatedEmail;
        }

        // Try each claim in order, skipping invalid values
        var preferredUsername = User.FindFirst("preferred_username")?.Value;
        if (!string.IsNullOrEmpty(preferredUsername) && preferredUsername != "0" && preferredUsername != "unknown")
        {
            _logger.LogDebug("Using preferred_username: {Email}", preferredUsername);
            return preferredUsername;
        }

        var email = User.FindFirst("email")?.Value;
        if (!string.IsNullOrEmpty(email) && email != "0" && email != "unknown")
        {
            _logger.LogDebug("Using email: {Email}", email);
            return email;
        }

        var upn = User.FindFirst("upn")?.Value;
        if (!string.IsNullOrEmpty(upn) && upn != "0" && upn != "unknown")
        {
            _logger.LogDebug("Using upn: {Email}", upn);
            return upn;
        }

        // Try alternative claim types with full URIs (for older tokens)
        var emailClaim = User.FindFirst("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress")?.Value;
        if (!string.IsNullOrEmpty(emailClaim) && emailClaim != "0" && emailClaim != "unknown")
        {
            _logger.LogDebug("Using emailaddress claim: {Email}", emailClaim);
            return emailClaim;
        }

        var upnClaim = User.FindFirst("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/upn")?.Value;
        if (!string.IsNullOrEmpty(upnClaim) && upnClaim != "0" && upnClaim != "unknown")
        {
            _logger.LogDebug("Using upn claim: {Email}", upnClaim);
            return upnClaim;
        }

        var nameClaim = User.FindFirst("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name")?.Value;
        if (!string.IsNullOrEmpty(nameClaim) && nameClaim != "0" && nameClaim != "unknown")
        {
            _logger.LogDebug("Using name claim: {Email}", nameClaim);
            return nameClaim;
        }

        _logger.LogWarning("No valid email claim found in token. All claims returned invalid values.");
        return "unknown";
    }

    /// <summary>
    /// Gets the caller's tenant ID
    /// </summary>
    public string GetCurrentTenantId()
    {
        if (User == null) return "unknown";

        // Try delegated tenant first
        var delegatedTenant = User.FindFirst("delegated_user_tenant")?.Value;
        if (!string.IsNullOrEmpty(delegatedTenant))
        {
            return delegatedTenant;
        }

        // Fall back to token tenant
        return User.FindFirst("tid")?.Value ?? "unknown";
    }

    /// <summary>
    /// Gets the calling application's Client ID (for app-to-app scenarios)
    /// </summary>
    public string? GetCallingApplicationId()
    {
        if (User == null) return null;
        return User.FindFirst("appid")?.Value ?? User.FindFirst("azp")?.Value;
    }

    /// <summary>
    /// Gets the calling application's name
    /// </summary>
    public string GetCallingApplicationName()
    {
        if (User == null) return "unknown";

        var appDisplayName = User.FindFirst("app_displayname")?.Value;
        if (!string.IsNullOrEmpty(appDisplayName) && appDisplayName != "0" && appDisplayName != "unknown")
        {
            return appDisplayName;
        }

        var azpacr = User.FindFirst("azpacr")?.Value;
        if (!string.IsNullOrEmpty(azpacr) && azpacr != "0" && azpacr != "unknown")
        {
            return azpacr;
        }

        var appId = GetCallingApplicationId();
        if (!string.IsNullOrEmpty(appId) && appId != "0" && appId != "unknown")
        {
            return appId;
        }

        return "unknown";
    }

    /// <summary>
    /// Checks if the current request is from a service principal (app-to-app)
    /// Returns true only if there's an appid but NO user identity claims
    /// </summary>
    public bool IsServicePrincipalCall()
    {
        if (User == null) return false;
        
        var hasAppId = !string.IsNullOrEmpty(GetCallingApplicationId());
        
        // Check for ANY user identity claim (oid, sub, preferred_username, email, upn)
        var hasUserOid = !string.IsNullOrEmpty(User.FindFirst("oid")?.Value);
        var hasUserSub = !string.IsNullOrEmpty(User.FindFirst("sub")?.Value);
        var hasPreferredUsername = !string.IsNullOrEmpty(User.FindFirst("preferred_username")?.Value);
        var hasEmail = !string.IsNullOrEmpty(User.FindFirst("email")?.Value);
        var hasUpn = !string.IsNullOrEmpty(User.FindFirst("upn")?.Value);
        
        var hasAnyUserClaim = hasUserOid || hasUserSub || hasPreferredUsername || hasEmail || hasUpn;
        
        // True app-to-app: has appid but NO user claims
        // Delegated auth (like Swagger): has both appid AND user claims
        return hasAppId && !hasAnyUserClaim;
    }

    /// <summary>
    /// Checks if the current request has delegated user context
    /// (service principal acting on behalf of a user)
    /// </summary>
    public bool HasDelegatedUserContext()
    {
        if (User == null) return false;
        return !string.IsNullOrEmpty(User.FindFirst("delegated_user_id")?.Value);
    }

    /// <summary>
    /// Gets comprehensive caller information for logging and auditing
    /// </summary>
    public CallerInfo GetCallerInfo()
    {
        // ENHANCED DEBUGGING - Log all claims
        if (User != null)
        {
            _logger.LogWarning("=== ALL TOKEN CLAIMS ===");
            foreach (var claim in User.Claims)
            {
                _logger.LogWarning("Claim: {Type} = '{Value}'", claim.Type, claim.Value);
            }
            _logger.LogWarning("=== END TOKEN CLAIMS ===");
        }
        else
        {
            _logger.LogWarning("No User/Claims principal available!");
        }

        var isServicePrincipal = IsServicePrincipalCall();
        var hasDelegatedUser = HasDelegatedUserContext();

        return new CallerInfo
        {
            UserId = GetCurrentUserId(),
            UserEmail = GetCurrentUserEmail(),
            TenantId = GetCurrentTenantId(),
            ApplicationId = GetCallingApplicationId(),
            ApplicationName = GetCallingApplicationName(),
            IsServicePrincipal = isServicePrincipal,
            HasDelegatedUser = hasDelegatedUser,
            AuthenticationType = GetAuthenticationType(isServicePrincipal, hasDelegatedUser)
        };
    }

    /// <summary>
    /// Gets a formatted string describing the caller for logging
    /// </summary>
    public string GetCallerDescription()
    {
        var callerInfo = GetCallerInfo();

        if (callerInfo.IsServicePrincipal && callerInfo.HasDelegatedUser)
        {
            return $"Service Principal '{callerInfo.ApplicationName}' ({callerInfo.ApplicationId}) " +
                   $"acting on behalf of user '{callerInfo.UserEmail}' ({callerInfo.UserId})";
        }

        if (callerInfo.IsServicePrincipal)
        {
            return $"Service Principal '{callerInfo.ApplicationName}' ({callerInfo.ApplicationId})";
        }

        return $"User '{callerInfo.UserEmail}' ({callerInfo.UserId})";
    }

    private static string GetAuthenticationType(bool isServicePrincipal, bool hasDelegatedUser)
    {
        if (isServicePrincipal && hasDelegatedUser)
            return "DelegatedAppToApp";

        if (isServicePrincipal)
            return "AppToApp";

        return "DirectUser";
    }
}

