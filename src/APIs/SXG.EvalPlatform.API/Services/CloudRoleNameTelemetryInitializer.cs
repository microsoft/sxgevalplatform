using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.Extensibility;

namespace SxgEvalPlatformApi.Services;

/// <summary>
/// Telemetry initializer to set cloud role name for Application Insights
/// This enables filtering logs by application in shared App Insights instances
/// </summary>
public class CloudRoleNameTelemetryInitializer : ITelemetryInitializer
{
    private readonly string _cloudRoleName;

    public CloudRoleNameTelemetryInitializer(string cloudRoleName)
    {
      _cloudRoleName = cloudRoleName ?? throw new ArgumentNullException(nameof(cloudRoleName));
    }

    public void Initialize(ITelemetry telemetry)
    {
if (telemetry?.Context?.Cloud != null)
        {
 telemetry.Context.Cloud.RoleName = _cloudRoleName;
        }
    }
}
