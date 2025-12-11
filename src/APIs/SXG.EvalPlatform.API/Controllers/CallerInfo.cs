namespace SxgEvalPlatformApi.Controllers;

/// <summary>
/// Information about the caller making the API request
/// </summary>
public class CallerInfo
{
    /// <summary>
    /// User ID (from delegated context or direct user)
    /// </summary>
    public string UserId { get; set; } = string.Empty;

    /// <summary>
    /// User email (from delegated context or direct user)
    /// </summary>
    public string UserEmail { get; set; } = string.Empty;

    /// <summary>
    /// Tenant ID
    /// </summary>
    public string TenantId { get; set; } = string.Empty;

    /// <summary>
    /// Calling application's Client ID (for app-to-app scenarios)
    /// </summary>
    public string? ApplicationId { get; set; }

    /// <summary>
    /// Calling application's display name
    /// </summary>
    public string ApplicationName { get; set; } = string.Empty;

    /// <summary>
    /// True if the request is from a service principal (app-to-app)
    /// </summary>
    public bool IsServicePrincipal { get; set; }

    /// <summary>
    /// True if the request has delegated user context
    /// </summary>
    public bool HasDelegatedUser { get; set; }

    /// <summary>
    /// Type of authentication: DirectUser, AppToApp, or DelegatedAppToApp
    /// </summary>
    public string AuthenticationType { get; set; } = string.Empty;

    public override string ToString()
    {
        if (IsServicePrincipal && HasDelegatedUser)
        {
            return $"ServicePrincipal={ApplicationName} ({ApplicationId}), User={UserEmail} ({UserId})";
        }

        if (IsServicePrincipal)
        {
            return $"ServicePrincipal={ApplicationName} ({ApplicationId})";
        }

        return $"User={UserEmail} ({UserId})";
    }
}