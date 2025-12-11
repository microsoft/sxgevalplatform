using System.Diagnostics;

namespace SxgEvalPlatformApi.Middleware
{
    /// <summary>
    /// Middleware to capture request telemetry using OpenTelemetry with optimized performance
  /// </summary>
    public sealed class TelemetryMiddleware
    {
      private readonly RequestDelegate _next;
    private readonly ILogger<TelemetryMiddleware> _logger;

        // Pre-define log message templates for better performance
      private static readonly Action<ILogger, string, string, string, Exception?> LogRequestStarted =
      LoggerMessage.Define<string, string, string>(
       LogLevel.Information,
   new EventId(1, "RequestStarted"),
  "API Request Started: {Method} {Path} - RequestId: {RequestId}");

        private static readonly Action<ILogger, string, string, int, long, string, Exception?> LogRequestCompleted =
            LoggerMessage.Define<string, string, int, long, string>(
     LogLevel.Information,
            new EventId(2, "RequestCompleted"),
    "API Request Completed: {Method} {Path} - Status: {StatusCode} - Duration: {Duration}ms - RequestId: {RequestId}");

        private static readonly Action<ILogger, string, string, int, long, string, Exception?> LogRequestFailed =
         LoggerMessage.Define<string, string, int, long, string>(
  LogLevel.Warning,
            new EventId(3, "RequestFailed"),
         "API Request Failed: {Method} {Path} - Status: {StatusCode} - Duration: {Duration}ms - RequestId: {RequestId}");

        public TelemetryMiddleware(RequestDelegate next, ILogger<TelemetryMiddleware> logger)
        {
    _next = next;
 _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
   {
            var stopwatch = Stopwatch.StartNew();
     var requestId = Activity.Current?.Id ?? context.TraceIdentifier;
            
 // Get current activity and enrich it
            var activity = Activity.Current;
            if (activity != null)
            {
     EnrichActivity(activity, context, requestId);
     }

            LogRequestStarted(_logger, context.Request.Method, context.Request.Path, requestId, null);

            Exception? exception = null;
       try
      {
                await _next(context);
}
            catch (Exception ex)
   {
          exception = ex;
   if (activity != null)
     {
        EnrichActivityWithError(activity, ex);
         }
    throw;
       }
    finally
   {
        stopwatch.Stop();
    var duration = stopwatch.ElapsedMilliseconds;
          
                if (activity != null)
          {
          EnrichActivityWithResponse(activity, context, duration, exception == null);
                }

LogRequestResult(context, requestId, duration, exception);
       }
  }

        private static void EnrichActivity(Activity activity, HttpContext context, string requestId)
    {
            activity.SetTag("http.request.id", requestId);
   activity.SetTag("http.request.path", context.Request.Path.Value);
            activity.SetTag("http.request.method", context.Request.Method);
   
         if (context.Request.QueryString.HasValue)
            {
                activity.SetTag("http.request.query", context.Request.QueryString.Value);
   }
            
      var userAgent = context.Request.Headers.UserAgent.ToString();
 if (!string.IsNullOrEmpty(userAgent))
    {
        activity.SetTag("user.agent", userAgent);
            }
    
            var clientIp = context.Connection.RemoteIpAddress?.ToString();
            if (!string.IsNullOrEmpty(clientIp))
            {
    activity.SetTag("client.ip", clientIp);
  }
    }

        private static void EnrichActivityWithError(Activity activity, Exception exception)
        {
   activity.SetTag("error.message", exception.Message);
 activity.SetTag("error.type", exception.GetType().Name);
            activity.SetStatus(ActivityStatusCode.Error, exception.Message);
          
    // Only include stack trace in development/staging
 #if DEBUG
     activity.SetTag("error.stack", exception.StackTrace);
 #endif
        }

     private static void EnrichActivityWithResponse(Activity activity, HttpContext context, long durationMs, bool success)
        {
        activity.SetTag("http.response.status_code", context.Response.StatusCode);
  activity.SetTag("http.response.duration_ms", durationMs);
       activity.SetTag("success", success && context.Response.StatusCode < 400);
    
    if (!success || context.Response.StatusCode >= 400)
  {
                activity.SetStatus(ActivityStatusCode.Error, $"Request failed with status {context.Response.StatusCode}");
       }
        }

   private void LogRequestResult(HttpContext context, string requestId, long duration, Exception? exception)
        {
      var isError = exception != null || context.Response.StatusCode >= 400;
    
       if (isError)
   {
     LogRequestFailed(_logger, context.Request.Method, context.Request.Path, 
              context.Response.StatusCode, duration, requestId, exception);
            }
  else
{
    LogRequestCompleted(_logger, context.Request.Method, context.Request.Path, 
     context.Response.StatusCode, duration, requestId, null);
         }
        }
    }
}