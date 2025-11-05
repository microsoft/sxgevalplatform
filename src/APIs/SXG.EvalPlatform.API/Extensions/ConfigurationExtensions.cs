namespace SxgEvalPlatformApi.Extensions;

/// <summary>
/// Extension methods for IConfiguration to provide strongly-typed configuration access
/// </summary>
public static class ConfigurationExtensions
{
    /// <summary>
  /// Gets OpenTelemetry configuration settings
    /// </summary>
    public static OpenTelemetrySettings GetOpenTelemetrySettings(this IConfiguration configuration)
    {
        return new OpenTelemetrySettings
   {
            ServiceName = configuration["OpenTelemetry:ServiceName"] ?? "SXG-EvalPlatform-API",
      ServiceVersion = configuration["OpenTelemetry:ServiceVersion"] ?? "1.0.0",
            AppInsightsConnectionString = configuration["Telemetry:AppInsightsConnectionString"],
 EnableConsoleExporter = configuration.GetValue<bool>("OpenTelemetry:EnableConsoleExporter", false),
       SamplingRatio = configuration.GetValue<double>("OpenTelemetry:SamplingRatio", 1.0)
       };
 }

    /// <summary>
   /// Gets Azure Storage configuration settings
  /// </summary>
    public static AzureStorageSettings GetAzureStorageSettings(this IConfiguration configuration)
 {
    return new AzureStorageSettings
        {
     AccountName = configuration["AzureStorage:AccountName"] ?? throw new InvalidOperationException("AzureStorage:AccountName is required"),
    ConnectionString = configuration.GetConnectionString("AzureStorage"),
        DefaultTimeout = configuration.GetValue<TimeSpan>("AzureStorage:DefaultTimeout", TimeSpan.FromSeconds(30)),
       MaxRetries = configuration.GetValue<int>("AzureStorage:MaxRetries", 3)
     };
}

    /// <summary>
    /// Gets API configuration settings
    /// </summary>
    public static ApiSettings GetApiSettings(this IConfiguration configuration)
    {
     return new ApiSettings
        {
     RequestTimeout = configuration.GetValue<TimeSpan>("Api:RequestTimeout", TimeSpan.FromSeconds(30)),
      MaxRequestSize = configuration.GetValue<long>("Api:MaxRequestSize", 10 * 1024 * 1024), // 10MB
  EnableRequestLogging = configuration.GetValue<bool>("Api:EnableRequestLogging", true),
   EnablePerformanceLogging = configuration.GetValue<bool>("Api:EnablePerformanceLogging", true)
        };
  }
}

/// <summary>
/// OpenTelemetry configuration settings
/// </summary>
public record OpenTelemetrySettings
{
    public string ServiceName { get; init; } = string.Empty;
    public string ServiceVersion { get; init; } = string.Empty;
    public string? AppInsightsConnectionString { get; init; }
    public bool EnableConsoleExporter { get; init; }
    public double SamplingRatio { get; init; }
}

/// <summary>
/// Azure Storage configuration settings
/// </summary>
public record AzureStorageSettings
{
   public string AccountName { get; init; } = string.Empty;
    public string? ConnectionString { get; init; }
  public TimeSpan DefaultTimeout { get; init; }
    public int MaxRetries { get; init; }
}

/// <summary>
/// API configuration settings
/// </summary>
public record ApiSettings
{
    public TimeSpan RequestTimeout { get; init; }
    public long MaxRequestSize { get; init; }
    public bool EnableRequestLogging { get; init; }
    public bool EnablePerformanceLogging { get; init; }
}