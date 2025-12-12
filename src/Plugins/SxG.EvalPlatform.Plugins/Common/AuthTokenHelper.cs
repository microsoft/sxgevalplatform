namespace SxG.EvalPlatform.Plugins.Common
{
    using System;
    using Microsoft.Xrm.Sdk;
    using SxG.EvalPlatform.Plugins.Services;

    /// <summary>
    /// Helper class for acquiring authentication tokens using Managed Identity Service
    /// </summary>
    public static class AuthTokenHelper
    {
        /// <summary>
        /// Acquires an authentication token for external API calls using Managed Identity
        /// </summary>
        /// <param name="managedIdentityService">Managed Identity Service instance</param>
        /// <param name="scope">OAuth scope/resource to request token for</param>
        /// <param name="loggingService">Logging service for trace and error logging</param>
        /// <returns>Bearer token string, or null if acquisition fails</returns>
        public static string AcquireToken(IManagedIdentityService managedIdentityService, IPluginLoggingService loggingService, string scope)
        {
            try
            {
                // Validate inputs
                if (managedIdentityService == null)
                {
                    loggingService?.Trace($"{nameof(AuthTokenHelper)}: ManagedIdentityService is not available", TraceSeverity.Warning);
                    return null;
                }

                if (string.IsNullOrWhiteSpace(scope))
                {
                    loggingService?.Trace($"{nameof(AuthTokenHelper)}: Scope is null or empty", TraceSeverity.Error);
                    return null;
                }

                loggingService?.Trace($"{nameof(AuthTokenHelper)}: Acquiring token for scope: {scope}");

                // Acquire token using Managed Identity Service
                string token = managedIdentityService.AcquireToken(new string[] { scope });

                if (string.IsNullOrEmpty(token))
                {
                    loggingService?.Trace($"{nameof(AuthTokenHelper)}: Failed to acquire token - token is null or empty", TraceSeverity.Warning);
                    return null;
                }

                loggingService?.Trace($"{nameof(AuthTokenHelper)}: Successfully acquired authentication token");
                return token;
            }
            catch (Exception ex)
            {
                loggingService?.LogException(ex, $"{nameof(AuthTokenHelper)}: Exception acquiring authentication token");
                return null;
            }
        }

        /// <summary>
        /// Adds authorization header to an HTTP web request if token is available
        /// </summary>
        /// <param name="webRequest">HTTP web request to add header to</param>
        /// <param name="token">Bearer token to add</param>
        /// <param name="loggingService">Logging service</param>
        public static void AddAuthorizationHeader(System.Net.HttpWebRequest webRequest, IPluginLoggingService loggingService, string token)
        {
            if (webRequest == null)
            {
                throw new ArgumentNullException(nameof(webRequest));
            }

            if (!string.IsNullOrEmpty(token))
            {
                webRequest.Headers.Add("Authorization", $"Bearer {token}");
                loggingService?.Trace($"{nameof(AuthTokenHelper)}: Authorization header added to request");
            }
            else
            {
                loggingService?.Trace($"{nameof(AuthTokenHelper)}: No token available - skipping authorization header", TraceSeverity.Warning);
            }
        }
    }
}
