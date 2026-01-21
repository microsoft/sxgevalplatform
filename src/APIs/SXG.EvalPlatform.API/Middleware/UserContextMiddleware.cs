using Microsoft.AspNetCore.Http;
using System.Security.Claims;
using SXG.EvalPlatform.Common;

namespace SXG.EvalPlatform.API.Middleware;

/// <summary>
/// Middleware to extract user context from custom headers when requests come from service principals.
/// This enables user tracking and auditing when PME service principal calls the API on behalf of users.
/// </summary>
public class UserContextMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<UserContextMiddleware> _logger;

    public UserContextMiddleware(RequestDelegate next, ILogger<UserContextMiddleware> logger)
    {
        _next = next;
     _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
// Check if request is authenticated with a service principal (app-to-app token)
    var appId = context.User.FindFirst("appid")?.Value;

 if (!string.IsNullOrEmpty(appId))
        {
 // This is a service principal call - extract user context from custom headers
            var userId = CommonUtils.SanitizeForLog(context.Request.Headers["X-User-Id"].FirstOrDefault());
   var userEmail = CommonUtils.SanitizeForLog(context.Request.Headers["X-User-Email"].FirstOrDefault());
            var userTenant = CommonUtils.SanitizeForLog(context.Request.Headers["X-User-Tenant"].FirstOrDefault());

        if (!string.IsNullOrWhiteSpace(userId))
      {
    // Add user context to claims for downstream use in controllers
          var identity = (ClaimsIdentity)context.User.Identity!;
                
   identity.AddClaim(new Claim("delegated_user_id", userId ?? string.Empty));

 if (!string.IsNullOrWhiteSpace(userEmail))
   {
    identity.AddClaim(new Claim("delegated_user_email", userEmail));
 }

           if (!string.IsNullOrWhiteSpace(userTenant))
       {
              identity.AddClaim(new Claim("delegated_user_tenant", userTenant));
     }

        _logger.LogInformation(
      "Service Principal {AppId} acting on behalf of user {UserId} ({UserEmail})",
    CommonUtils.SanitizeForLog(appId), CommonUtils.SanitizeForLog(userId), CommonUtils.SanitizeForLog(userEmail ?? "unknown"));
            }
            else
     {
         // Service principal call without user context
      _logger.LogDebug(
         "Service Principal {AppId} making request without user context (app-to-app only)",
  CommonUtils.SanitizeForLog(appId));
            }
        }
  else
   {
            // Direct user authentication (not service principal)
            var userObjectId = context.User.FindFirst("oid")?.Value;
  var userPrincipalName = context.User.FindFirst("preferred_username")?.Value 
        ?? context.User.FindFirst("upn")?.Value;

   if (!string.IsNullOrEmpty(userObjectId))
            {
          _logger.LogDebug(
     "Direct user authentication: {UserId} ({UserName})",
          CommonUtils.SanitizeForLog(userObjectId), CommonUtils.SanitizeForLog(userPrincipalName ?? "unknown"));
            }
        }

        await _next(context);
    }
}

/// <summary>
/// Extension methods for UserContextMiddleware registration
/// </summary>
public static class UserContextMiddlewareExtensions
{
    /// <summary>
    /// Adds UserContextMiddleware to the application pipeline.
    /// Should be called after UseAuthentication() and before UseAuthorization().
    /// </summary>
public static IApplicationBuilder UseUserContext(this IApplicationBuilder builder)
    {
    return builder.UseMiddleware<UserContextMiddleware>();
    }
}
