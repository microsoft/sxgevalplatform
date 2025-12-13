using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Identity.Web;
using Microsoft.IdentityModel.Tokens;
using SxgEvalPlatformApi.Services;

namespace SxgEvalPlatformApi.Extensions;

/// <summary>
/// Extension methods for authentication configuration
/// </summary>
public static class AuthenticationExtensions
{
    /// <summary>
    /// Configure multi-tenant Azure AD authentication with support for:
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
        var audience = azureAdSection["Audience"];
        var instance = azureAdSection["Instance"] ?? "https://login.microsoftonline.com/";
        var tenantId = azureAdSection["TenantId"] ?? "common"; // "common" for multi-tenant

        // Validate required configuration
        if (string.IsNullOrEmpty(clientId))
        {
            throw new InvalidOperationException(
        "Azure AD ClientId is not configured in appsettings.json. " +
                "Either provide a ClientId or set FeatureFlags:EnableAuthentication to false.");
        }

        // Build valid issuers for multi-tenant scenario
        var validIssuers = new List<string>
        {
            $"{instance}common/v2.0",
            $"{instance}{tenantId}/v2.0",
            $"https://sts.windows.net/common/",
            $"https://sts.windows.net/{tenantId}/"
        };

        // Add valid audiences
        var validAudiences = new List<string> { clientId };
        if (!string.IsNullOrEmpty(audience) && audience != clientId)
        {
            validAudiences.Add(audience);
        }

        // Configure JWT Bearer authentication
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                    .AddMicrosoftIdentityWebApi(options =>
                 {
                     // Token validation parameters
                     options.TokenValidationParameters = new TokenValidationParameters
                     {
                         ValidateIssuer = authSection.GetValue("ValidateIssuer", true),
                         ValidateAudience = authSection.GetValue("ValidateAudience", true),
                         ValidateLifetime = authSection.GetValue("ValidateLifetime", true),
                         ValidateIssuerSigningKey = authSection.GetValue("ValidateIssuerSigningKey", true),

                         // Multi-tenant support - validate against multiple issuers
                         ValidIssuers = validIssuers,

                         // Support both Client ID and custom audience
                         ValidAudiences = validAudiences,

                         // Clock skew for token expiration validation
                         ClockSkew = TimeSpan.FromMinutes(authSection.GetValue("ClockSkewMinutes", 5)),

                         // Map claim types for easy access
                         NameClaimType = "name",
                         RoleClaimType = "roles"
                     };

                     options.Events = new JwtBearerEvents
                     {
                         OnTokenValidated = context =>
           {
               // Log successful authentication
               var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
               var securityLogger = context.HttpContext.RequestServices.GetService<ISecurityEventLogger>();
               var claimsPrincipal = context.Principal;

               // Extract identity information
               var userId = claimsPrincipal?.FindFirst("oid")?.Value ??
   claimsPrincipal?.FindFirst("sub")?.Value ?? "unknown";
               var userEmail = claimsPrincipal?.FindFirst("email")?.Value ??
                               claimsPrincipal?.FindFirst("preferred_username")?.Value;
               var appId = claimsPrincipal?.FindFirst("appid")?.Value ??
  claimsPrincipal?.FindFirst("azp")?.Value;
               var tenantId = claimsPrincipal?.FindFirst("tid")?.Value;
               var authType = !string.IsNullOrEmpty(appId) ? "ManagedIdentity" : "DelegatedUser";
               var ipAddress = context.HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

               logger.LogInformation("Token validated successfully - AuthType: {AuthType}, UserId: {UserId}, AppId: {AppId}, TenantId: {TenantId}",
       authType, userId, appId ?? "N/A", tenantId ?? "N/A");

               // MISE Compliance: Log successful authentication to SIEM
               if (securityLogger != null)
               {
                   _ = Task.Run(async () =>
                   {
                       try
                       {
                           await securityLogger.LogAuthenticationSuccessAsync(
                               userId: userId,
                               userEmail: userEmail,
                               authenticationType: authType,
                               ipAddress: ipAddress);
                       }
                       catch (Exception ex)
                       {
                           logger.LogError(ex, "Failed to log authentication success event");
                       }
                   });
               }

               return Task.CompletedTask;
           },

                         OnAuthenticationFailed = context =>
          {
              // Log authentication failures
              var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
              var securityLogger = context.HttpContext.RequestServices.GetService<ISecurityEventLogger>();
              var ipAddress = context.HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
              var userAgent = context.HttpContext.Request.Headers.UserAgent.ToString();

              logger.LogWarning(context.Exception, "Authentication failed - Reason: {Reason}",
context.Exception.Message);

              // MISE Compliance: Log authentication failure to SIEM
              if (securityLogger != null)
              {
                  var details = new Dictionary<string, object>
                  {
                      ["ExceptionType"] = context.Exception?.GetType().Name ?? "Unknown",
                      ["RequestPath"] = context.HttpContext.Request.Path.Value ?? "unknown"
                  };

                  _ = Task.Run(async () =>
                  {
                      try
                      {
                          await securityLogger.LogAuthenticationFailureAsync(
                              reason: context.Exception?.Message ?? "Authentication failed",
                              ipAddress: ipAddress,
                              userAgent: userAgent,
                              details: details);
                      }
                      catch (Exception ex)
                      {
                          logger.LogError(ex, "Failed to log authentication failure event");
                      }
                  });
              }

              return Task.CompletedTask;
          },

                         OnChallenge = context =>
                  {
                      // Log when authentication is challenged
                      var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
                      var securityLogger = context.HttpContext.RequestServices.GetService<ISecurityEventLogger>();
                      var ipAddress = context.HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

                      logger.LogWarning("Authentication challenged - Error: {Error}, ErrorDescription: {ErrorDescription}",
                        context.Error, context.ErrorDescription);

                      // MISE Compliance: Log token validation challenges
                      if (securityLogger != null && !string.IsNullOrWhiteSpace(context.Error))
                      {
                          var reason = $"{context.Error}: {context.ErrorDescription}";
                          _ = Task.Run(async () =>
                          {
                              try
                              {
                                  await securityLogger.LogInvalidTokenAsync(
                                      reason: reason,
                                      ipAddress: ipAddress,
                                      tokenHint: context.Error);
                              }
                              catch (Exception ex)
                              {
                                  logger.LogError(ex, "Failed to log invalid token event");
                              }
                          });
                      }

                      return Task.CompletedTask;
                  }
                     };

                     // Save token in authentication properties (optional)
                     options.SaveToken = authSection.GetValue("SaveToken", false);

                     // Require HTTPS metadata in production
                     options.RequireHttpsMetadata = authSection.GetValue("RequireHttpsMetadata", true);

                 }, options =>
          {
              // Microsoft Identity options
              options.Instance = instance;
              options.TenantId = tenantId;
              options.ClientId = clientId;
          });

        // Configure authorization policies
        services.AddAuthorization(options =>
        {
            // Default policy - require authenticated user
            options.DefaultPolicy = new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder()
          .RequireAuthenticatedUser()
        .Build();

            // Policy for managed identity access (app-to-app)
            options.AddPolicy("ManagedIdentityOnly", policy =>
 {
     policy.RequireAuthenticatedUser();
     policy.RequireClaim("appid"); // Managed Identity tokens have 'appid' claim
 });

            // Policy for user access (delegated)
            options.AddPolicy("DelegatedUserOnly", policy =>
        {
            policy.RequireAuthenticatedUser();
            policy.RequireClaim("oid"); // User tokens have 'oid' (object ID) claim
        });

            // Policy for specific app role (for managed identity with specific role)
            options.AddPolicy("RequireEvalPlatformFullAccess", policy =>
                  {
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
