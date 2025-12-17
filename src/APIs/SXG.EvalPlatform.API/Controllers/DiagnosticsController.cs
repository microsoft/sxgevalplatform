using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SxgEvalPlatformApi.Services;
using System.Security.Claims;
using SXG.EvalPlatform.Common;

namespace SxgEvalPlatformApi.Controllers
{
    /// <summary>
    /// Diagnostic endpoints for troubleshooting authentication and audit trail issues
    /// WARNING: Remove or secure this controller in production!
    /// </summary>
    [ApiController]
    [Route("api/v1/[controller]")]
    [Authorize]
    public class DiagnosticsController : ControllerBase
    {
        private readonly ILogger<DiagnosticsController> _logger;
        private readonly ICallerIdentificationService _callerService;

        public DiagnosticsController(
            ILogger<DiagnosticsController> logger,
            ICallerIdentificationService callerService)
        {
            _logger = logger;
            _callerService = callerService;
        }

        /// <summary>
        /// Get all claims from the current JWT token
        /// </summary>
        [HttpGet("claims")]
        public IActionResult GetClaims()
        {
            var claims = User.Claims.Select(c => new
            {
                Type = CommonUtils.SanitizeForLog(c.Type),
                Value = CommonUtils.SanitizeForLog(c.Value),
                ValueType = CommonUtils.SanitizeForLog(c.ValueType),
                ShortType = CommonUtils.SanitizeForLog(GetShortClaimType(c.Type))
            }).ToList();

            return Ok(new
            {
                IsAuthenticated = User.Identity?.IsAuthenticated ?? false,
                AuthenticationType = User.Identity?.AuthenticationType,
                Name = User.Identity?.Name,
                TotalClaims = claims.Count,
                Claims = claims,
                ImportantClaims = new
                {
                    PreferredUsername = CommonUtils.SanitizeForLog(User.FindFirstValue("preferred_username")),
                    Email = CommonUtils.SanitizeForLog(User.FindFirstValue("email")),
                    Upn = CommonUtils.SanitizeForLog(User.FindFirstValue("upn")),
                    Oid = CommonUtils.SanitizeForLog(User.FindFirstValue("oid")),
                    AppId = CommonUtils.SanitizeForLog(User.FindFirstValue("appid")),
                    Azp = CommonUtils.SanitizeForLog(User.FindFirstValue("azp")),
                    Name = CommonUtils.SanitizeForLog(User.FindFirstValue("name"))
                }
            });
        }

        /// <summary>
        /// Get the audit user that would be saved (simulates GetAuditUser logic)
        /// </summary>
        [HttpGet("audit-user")]
        public IActionResult GetAuditUser()
        {
            try
            {
                var callerInfo = _callerService.GetCallerInfo();
                
                // Simulate the GetAuditUser logic
                string auditUser;
                string decision;

                if (_callerService.IsServicePrincipalCall() && !_callerService.HasDelegatedUserContext())
                {
                    // AppToApp flow
                    var appName = _callerService.GetCallingApplicationName();
                    auditUser = !string.IsNullOrWhiteSpace(appName) && appName != "unknown" ? appName : "System";
                    decision = "AppToApp flow - using application name";
                }
                else
                {
                    // DirectUser or DelegatedAppToApp
                    var userEmail = _callerService.GetCurrentUserEmail();
                    
                    if (!string.IsNullOrWhiteSpace(userEmail) && userEmail != "unknown" && userEmail != "0")
                    {
                        auditUser = userEmail;
                        decision = "DirectUser/Delegated flow - using email";
                    }
                    else
                    {
                        var userId = _callerService.GetCurrentUserId();
                        if (!string.IsNullOrWhiteSpace(userId) && userId != "unknown" && userId != "0")
                        {
                            auditUser = userId;
                            decision = "DirectUser/Delegated flow - using userId (email not available)";
                        }
                        else
                        {
                            auditUser = "System";
                            decision = "Fallback - no valid user information found";
                        }
                    }
                }

                return Ok(new
                {
                    AuditUserThatWillBeSaved = auditUser,
                    Decision = decision,
                    CallerInfo = new
                    {
                        UserId = CommonUtils.SanitizeForLog(callerInfo.UserId),
                        UserEmail = CommonUtils.SanitizeForLog(callerInfo.UserEmail),
                        TenantId = CommonUtils.SanitizeForLog(callerInfo.TenantId),
                        ApplicationId = CommonUtils.SanitizeForLog(callerInfo.ApplicationId),
                        ApplicationName = CommonUtils.SanitizeForLog(callerInfo.ApplicationName),
                        callerInfo.IsServicePrincipal,
                        callerInfo.HasDelegatedUser,
                        AuthenticationType = CommonUtils.SanitizeForLog(callerInfo.AuthenticationType)
                    },
                    RawValues = new
                    {
                        GetCurrentUserId = CommonUtils.SanitizeForLog(_callerService.GetCurrentUserId()),
                        GetCurrentUserEmail = CommonUtils.SanitizeForLog(_callerService.GetCurrentUserEmail()),
                        GetCallingApplicationName = CommonUtils.SanitizeForLog(_callerService.GetCallingApplicationName()),
                        IsServicePrincipalCall = _callerService.IsServicePrincipalCall(),
                        HasDelegatedUserContext = _callerService.HasDelegatedUserContext()
                    },
                    AllClaims = User.Claims.Select(c => new { 
                        Type = CommonUtils.SanitizeForLog(c.Type), 
                        Value = CommonUtils.SanitizeForLog(c.Value) 
                    }).ToList()
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetAuditUser diagnostic endpoint");
                return StatusCode(500, new
                {
                    Error = CommonUtils.SanitizeForLog(ex.Message),
                    StackTrace = CommonUtils.SanitizeForLog(ex.StackTrace)
                });
            }
        }

        /// <summary>
        /// Get detailed information about HttpContext
        /// </summary>
        [HttpGet("context")]
        public IActionResult GetContext()
        {
            return Ok(new
            {
                Request = new
                {
                    Scheme = CommonUtils.SanitizeForLog(Request.Scheme),
                    Host = CommonUtils.SanitizeForLog(Request.Host.Value),
                    Path = CommonUtils.SanitizeForLog(Request.Path.Value),
                    QueryString = CommonUtils.SanitizeForLog(Request.QueryString.Value),
                    Headers = Request.Headers.Select(h => new { 
                        h.Key, 
                        Values = h.Value.Select(v => CommonUtils.SanitizeForLog(v)).ToArray() 
                    }).ToList()
                },
                User = new
                {
                    IsAuthenticated = User.Identity?.IsAuthenticated,
                    AuthenticationType = CommonUtils.SanitizeForLog(User.Identity?.AuthenticationType),
                    Name = CommonUtils.SanitizeForLog(User.Identity?.Name),
                    ClaimCount = User.Claims.Count()
                }
            });
        }

        private string GetShortClaimType(string claimType)
        {
            // Convert full URIs to short names
            if (claimType.Contains("/"))
            {
                return claimType.Split('/').Last();
            }
            return claimType;
        }
    }
}
