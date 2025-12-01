using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SxgEvalPlatformApi.Services;
using System.Security.Claims;

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
                Type = c.Type,
                Value = c.Value,
                ValueType = c.ValueType,
                ShortType = GetShortClaimType(c.Type)
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
                    PreferredUsername = User.FindFirstValue("preferred_username"),
                    Email = User.FindFirstValue("email"),
                    Upn = User.FindFirstValue("upn"),
                    Oid = User.FindFirstValue("oid"),
                    AppId = User.FindFirstValue("appid"),
                    Azp = User.FindFirstValue("azp"),
                    Name = User.FindFirstValue("name")
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
                        callerInfo.UserId,
                        callerInfo.UserEmail,
                        callerInfo.TenantId,
                        callerInfo.ApplicationId,
                        callerInfo.ApplicationName,
                        callerInfo.IsServicePrincipal,
                        callerInfo.HasDelegatedUser,
                        callerInfo.AuthenticationType
                    },
                    RawValues = new
                    {
                        GetCurrentUserId = _callerService.GetCurrentUserId(),
                        GetCurrentUserEmail = _callerService.GetCurrentUserEmail(),
                        GetCallingApplicationName = _callerService.GetCallingApplicationName(),
                        IsServicePrincipalCall = _callerService.IsServicePrincipalCall(),
                        HasDelegatedUserContext = _callerService.HasDelegatedUserContext()
                    },
                    AllClaims = User.Claims.Select(c => new { c.Type, c.Value }).ToList()
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetAuditUser diagnostic endpoint");
                return StatusCode(500, new
                {
                    Error = ex.Message,
                    StackTrace = ex.StackTrace
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
                    Scheme = Request.Scheme,
                    Host = Request.Host.Value,
                    Path = Request.Path.Value,
                    QueryString = Request.QueryString.Value,
                    Headers = Request.Headers.Select(h => new { h.Key, Values = h.Value.ToArray() }).ToList()
                },
                User = new
                {
                    IsAuthenticated = User.Identity?.IsAuthenticated,
                    AuthenticationType = User.Identity?.AuthenticationType,
                    Name = User.Identity?.Name,
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
