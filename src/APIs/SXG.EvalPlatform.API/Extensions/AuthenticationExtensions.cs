using Microsoft.Identity.ServiceEssentials;
using Microsoft.IdentityModel.S2S;
using Microsoft.AspNetCore.Authentication.JwtBearer;

namespace SxgEvalPlatformApi.Extensions;

/// <summary>
/// Extension methods for authentication configuration using Microsoft Identity Service Essentials (MISE)
/// </summary>
public static class AuthenticationExtensions
{
    /// <summary>
    /// Configure authentication with Microsoft Identity Service Essentials (MISE) with support for:
    /// - MSAuth1.0 PFT/POP protected tokens
    /// - Managed Identity (app-to-app flow)
    /// - Delegated User Authentication (user flow)
    /// 
    /// This is controlled by the FeatureFlags:EnableAuthentication setting.
    /// If disabled, no authentication will be required.
    /// </summary>
    public static IServiceCollection AddAzureAdAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        // Check feature flag first
        var authEnabled = configuration.GetValue<bool>("FeatureFlags:EnableAuthentication", false);

        if (!authEnabled)
        {
            // Authentication is disabled - add a minimal authorization policy that allows anonymous access
            services.AddAuthorization(options =>
             {
                 options.DefaultPolicy = new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder()
                  .RequireAssertion(_ => true) // Always allow
                .Build();
             });

            return services;
        }

        var azureAdSection = configuration.GetSection("AzureAd");
        var authSection = configuration.GetSection("Authentication");

        // Get configuration values
        var clientId = azureAdSection["ClientId"];
        var tenantId = azureAdSection["TenantId"] ?? "common"; // "common" for multi-tenant

        // Validate required configuration
        if (string.IsNullOrEmpty(clientId))
        {
            throw new InvalidOperationException(
        "Azure AD ClientId is not configured in appsettings.json. " +
                "Either provide a ClientId or set FeatureFlags:EnableAuthentication to false.");
        }

        // Get MISE-specific configuration
        var miseSection = authSection.GetSection("MISE");
        var requireProtectedTokens = miseSection.GetValue("RequireProtectedTokens", false);
        var allowStandardBearer = miseSection.GetValue("AllowStandardBearerTokens", true);
        var enableEventLogging = miseSection.GetValue("EnableEventLogging", true);

        // Configure MISE with default modules (best practice)
        // AddMiseWithDefaultModules reads from configuration section "AzureAd"
        // MISE registers its handler with the scheme "S2SAuthentication"
        var authBuilder = services.AddAuthentication("S2SAuthentication")
            .AddMiseWithDefaultModules(configuration);

        // Configure MISE options post-registration if needed
        if (!requireProtectedTokens && allowStandardBearer)
        {
            // Configure to accept standard bearer tokens (not just PFT/POP)
            services.Configure<Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerOptions>(
                "S2SAuthentication",
                options =>
                {
                    // MISE token validation is already configured by AddMiseWithDefaultModules
                    // Additional event handlers for logging if enabled
                    if (enableEventLogging)
                    {
                        var existingOnTokenValidated = options.Events?.OnTokenValidated;
                        var existingOnAuthFailed = options.Events?.OnAuthenticationFailed;
                        var existingOnChallenge = options.Events?.OnChallenge;

                        options.Events = new Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerEvents
                        {
                            OnTokenValidated = async context =>
                            {
                                // Call existing handler first
                                if (existingOnTokenValidated != null)
                                {
                                    await existingOnTokenValidated(context);
                                }

                                // Enhanced MISE-compliant security logging
                                var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
                                var claimsPrincipal = context.Principal;
                                var httpContext = context.HttpContext;

                                // Extract standard claims
                                var userId = claimsPrincipal?.FindFirst("oid")?.Value ??
                                    claimsPrincipal?.FindFirst("sub")?.Value ?? "unknown";
                                var appId = claimsPrincipal?.FindFirst("appid")?.Value ??
                                    claimsPrincipal?.FindFirst("azp")?.Value;
                                var tid = claimsPrincipal?.FindFirst("tid")?.Value;
                                var authType = !string.IsNullOrEmpty(appId) ? "ManagedIdentity" : "DelegatedUser";
                                
                                // Extract additional security context
                                var clientIp = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
                                var requestPath = httpContext.Request.Path.Value ?? "unknown";
                                var userAgent = httpContext.Request.Headers["User-Agent"].ToString() ?? "unknown";
                                
                                // Extract token metadata
                                var tokenType = claimsPrincipal?.FindFirst("token_type")?.Value ?? "Bearer";
                                var exp = claimsPrincipal?.FindFirst("exp")?.Value;
                                var roles = claimsPrincipal?.FindAll("roles").Select(c => c.Value).ToList();
                                var scopes = claimsPrincipal?.FindFirst("scp")?.Value ?? 
                                           claimsPrincipal?.FindFirst("scope")?.Value;
                                
                                // Determine if token is protected (PFT/POP) based on token claims
                                var isProtectedToken = claimsPrincipal?.FindFirst("cnf")?.Value != null || // Proof of Possession
                                                     claimsPrincipal?.FindFirst("pop_jwk")?.Value != null; // POP JWK

                                // Log comprehensive authentication event for MISE compliance
                                logger.LogInformation(
                                    "[MISE Security Event] Token validated successfully | " +
                                    "AuthType: {AuthType} | UserId: {UserId} | AppId: {AppId} | TenantId: {TenantId} | " +
                                    "ClientIP: {ClientIP} | RequestPath: {RequestPath} | TokenType: {TokenType} | " +
                                    "IsProtectedToken: {IsProtectedToken} | RequireProtectedTokens: {RequireProtectedTokens} | " +
                                    "Roles: {Roles} | Scopes: {Scopes} | Expires: {TokenExpiration}",
                                    authType, userId, appId ?? "N/A", tid ?? "N/A",
                                    clientIp, requestPath, tokenType,
                                    isProtectedToken, requireProtectedTokens,
                                    roles.Any() ? string.Join(",", roles) : "none",
                                    scopes ?? "none",
                                    exp ?? "unknown");
                            },

                            OnAuthenticationFailed = async context =>
                            {
                                // Call existing handler first
                                if (existingOnAuthFailed != null)
                                {
                                    await existingOnAuthFailed(context);
                                }

                                // Enhanced failure logging with security context
                                var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
                                var httpContext = context.HttpContext;
                                
                                var clientIp = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
                                var requestPath = httpContext.Request.Path.Value ?? "unknown";
                                var authHeader = httpContext.Request.Headers["Authorization"].ToString();
                                var hasToken = !string.IsNullOrEmpty(authHeader);
                                
                                // Log authentication failure as security warning
                                logger.LogWarning(
                                    "[MISE Security Event] Authentication failed | " +
                                    "Reason: {Reason} | ClientIP: {ClientIP} | RequestPath: {RequestPath} | " +
                                    "HasAuthorizationHeader: {HasToken} | AllowStandardBearer: {AllowStandardBearer} | " +
                                    "ExceptionType: {ExceptionType}",
                                    context.Exception.Message, clientIp, requestPath,
                                    hasToken, allowStandardBearer,
                                    context.Exception.GetType().Name);
                            },

                            OnChallenge = async context =>
                            {
                                // Call existing handler first
                                if (existingOnChallenge != null)
                                {
                                    await existingOnChallenge(context);
                                }

                                // Log authentication challenge with context
                                var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
                                var httpContext = context.HttpContext;
                                
                                var clientIp = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
                                var requestPath = httpContext.Request.Path.Value ?? "unknown";
                                
                                logger.LogWarning(
                                    "[MISE Security Event] Authentication challenged | " +
                                    "Error: {Error} | ErrorDescription: {ErrorDescription} | " +
                                    "ClientIP: {ClientIP} | RequestPath: {RequestPath} | " +
                                    "StatusCode: {StatusCode}",
                                    context.Error ?? "none", context.ErrorDescription ?? "none",
                                    clientIp, requestPath,
                                    context.Response?.StatusCode ?? 401);
                            }
                        };
                    }
                });
        }

        // Configure authorization policies
        services.AddAuthorization(options =>
        {
            // Default policy - require authenticated user
            options.DefaultPolicy = new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder("S2SAuthentication")
                .RequireAuthenticatedUser()
                .Build();

            // Policy for managed identity access (app-to-app)
            options.AddPolicy("ManagedIdentityOnly", policy =>
            {
                policy.AuthenticationSchemes.Add("S2SAuthentication");
                policy.RequireAuthenticatedUser();
                policy.RequireClaim("appid"); // Managed Identity tokens have 'appid' claim
            });

            // Policy for user access (delegated)
            options.AddPolicy("DelegatedUserOnly", policy =>
            {
                policy.AuthenticationSchemes.Add("S2SAuthentication");
                policy.RequireAuthenticatedUser();
                policy.RequireClaim("oid"); // User tokens have 'oid' (object ID) claim
            });

            // Policy for specific app role (for managed identity with specific role)
            options.AddPolicy("RequireEvalPlatformFullAccess", policy =>
            {
                policy.AuthenticationSchemes.Add("S2SAuthentication");
                policy.RequireAuthenticatedUser();
                policy.RequireClaim("roles", "EvalPlatform.FullAccess");
            });
        });

        return services;
    }
}

/// <summary>
/// Extension methods for authentication-related helper methods
/// </summary>
public static class HttpContextExtensions
{
    /// <summary>
    /// Get the authenticated user's Object ID (OID)
    /// </summary>
    public static string? GetUserObjectId(this HttpContext httpContext)
    {
        return httpContext.User?.FindFirst("oid")?.Value ??
     httpContext.User?.FindFirst("sub")?.Value;
    }

    /// <summary>
    /// Get the authenticated application ID (for managed identity scenarios)
    /// </summary>
    public static string? GetApplicationId(this HttpContext httpContext)
    {
        return httpContext.User?.FindFirst("appid")?.Value ??
             httpContext.User?.FindFirst("azp")?.Value;
    }

    /// <summary>
    /// Get the tenant ID from the token
    /// </summary>
    public static string? GetTenantId(this HttpContext httpContext)
    {
        return httpContext.User?.FindFirst("tid")?.Value;
    }

    /// <summary>
    /// Check if the request is from a managed identity (app-to-app flow)
    /// </summary>
    public static bool IsManagedIdentity(this HttpContext httpContext)
    {
        var appId = httpContext.GetApplicationId();
        return !string.IsNullOrEmpty(appId);
    }

    /// <summary>
    /// Check if the request is from a delegated user
    /// </summary>
    public static bool IsDelegatedUser(this HttpContext httpContext)
    {
        var oid = httpContext.GetUserObjectId();
        var appId = httpContext.GetApplicationId();
        // User tokens have oid but typically not appid (or different appid structure)
        return !string.IsNullOrEmpty(oid) && string.IsNullOrEmpty(appId);
    }

    /// <summary>
    /// Get the user's email from the token
    /// </summary>
    public static string? GetUserEmail(this HttpContext httpContext)
    {
        return httpContext.User?.FindFirst("email")?.Value ??
               httpContext.User?.FindFirst("upn")?.Value ??
       httpContext.User?.FindFirst("preferred_username")?.Value;
    }

    /// <summary>
    /// Get the user's name from the token
    /// </summary>
    public static string? GetUserName(this HttpContext httpContext)
    {
        return httpContext.User?.FindFirst("name")?.Value ??
                      httpContext.GetUserEmail();
    }

    /// <summary>
    /// Get all claims as a dictionary for logging
    /// </summary>
    public static Dictionary<string, string> GetAllClaims(this HttpContext httpContext)
    {
        return httpContext.User?.Claims
            .GroupBy(c => c.Type)
          .ToDictionary(
   g => g.Key,
  g => string.Join(", ", g.Select(c => c.Value))
            ) ?? new Dictionary<string, string>();
    }
}
