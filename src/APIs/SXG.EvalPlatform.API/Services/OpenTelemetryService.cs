using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Collections.Concurrent;

namespace SxgEvalPlatformApi.Services
{
    /// <summary>
  /// Service for OpenTelemetry operations and custom metrics
    /// </summary>
    public interface IOpenTelemetryService
    {
   /// <summary>
      /// Track evaluation run operation
        /// </summary>
        void TrackEvaluationRunOperation(string operation, string evalRunId, string agentId, bool success, TimeSpan duration);

        /// <summary>
     /// Track dataset operation
        /// </summary>
        void TrackDatasetOperation(string operation, string datasetId, string agentId, bool success, TimeSpan duration);

/// <summary>
 /// Track metrics configuration operation
    /// </summary>
        void TrackMetricsConfigOperation(string operation, string configId, bool success, TimeSpan duration);

 /// <summary>
  /// Track evaluation result operation
        /// </summary>
   void TrackEvaluationResultOperation(string operation, string evalRunId, bool success, TimeSpan duration);

        /// <summary>
        /// Track DataVerse API call
        /// </summary>
        void TrackDataVerseApiCall(string evalRunId, string agentId, bool success, int statusCode, TimeSpan duration);

        /// <summary>
        /// Create activity for operation tracking
        /// </summary>
        Activity? StartActivity(string operationName);

   /// <summary>
        /// Add tags to current activity
        /// </summary>
    void AddActivityTags(Dictionary<string, object> tags);

        /// <summary>
        /// Record custom metric
      /// </summary>
        void RecordMetric(string metricName, double value, Dictionary<string, object>? tags = null);
    }

    /// <summary>
 /// Implementation of OpenTelemetry service with performance optimizations
 /// </summary>
    public sealed class OpenTelemetryService : IOpenTelemetryService, IDisposable
    {
        private static readonly ActivitySource ActivitySource = new("SXG.EvalPlatform.API");
        private static readonly Meter Meter = new("SXG.EvalPlatform.API");

   // Cache for custom histograms to avoid repeated creation
        private readonly ConcurrentDictionary<string, Histogram<double>> _customHistograms = new();

        // Pre-created counters for better performance
        private readonly Counter<long> _evaluationRunOperationsCounter;
        private readonly Counter<long> _datasetOperationsCounter;
        private readonly Counter<long> _metricsConfigOperationsCounter;
      private readonly Counter<long> _evaluationResultOperationsCounter;
        private readonly Counter<long> _dataVerseApiCallsCounter;

        // Pre-created histogram for durations
     private readonly Histogram<double> _operationDurationHistogram;

        private readonly ILogger<OpenTelemetryService> _logger;
        private bool _disposed;

 public OpenTelemetryService(ILogger<OpenTelemetryService> logger)
        {
_logger = logger;

            // Initialize counters with better descriptions and units
     _evaluationRunOperationsCounter = Meter.CreateCounter<long>(
       "sxg_evaluation_run_operations_total",
      "count",
                "Total number of evaluation run operations");

    _datasetOperationsCounter = Meter.CreateCounter<long>(
          "sxg_dataset_operations_total",
      "count",
            "Total number of dataset operations");

         _metricsConfigOperationsCounter = Meter.CreateCounter<long>(
         "sxg_metrics_config_operations_total",
 "count",
    "Total number of metrics configuration operations");

            _evaluationResultOperationsCounter = Meter.CreateCounter<long>(
    "sxg_evaluation_result_operations_total",
         "count",
    "Total number of evaluation result operations");

      _dataVerseApiCallsCounter = Meter.CreateCounter<long>(
     "sxg_dataverse_api_calls_total",
          "count",
        "Total number of DataVerse API calls");

            // Initialize histogram with reasonable buckets
            _operationDurationHistogram = Meter.CreateHistogram<double>(
   "sxg_operation_duration_seconds",
      "s",
      "Duration of operations in seconds");
 }

   public void TrackEvaluationRunOperation(string operation, string evalRunId, string agentId, bool success, TimeSpan duration)
        {
      try
      {
     var tags = CreateTagList(
       ("operation", operation),
   ("eval_run_id", evalRunId),
      ("agent_id", agentId),
         ("success", success.ToString().ToLowerInvariant())
        );

        _evaluationRunOperationsCounter.Add(1, tags);
           _operationDurationHistogram.Record(duration.TotalSeconds, tags);

     _logger.LogDebug("Tracked evaluation run operation: {Operation}, EvalRunId={EvalRunId}, Success={Success}, Duration={Duration}ms",
        operation, evalRunId, success, duration.TotalMilliseconds);
       }
    catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to track evaluation run operation");
     }
        }

   public void TrackDatasetOperation(string operation, string datasetId, string agentId, bool success, TimeSpan duration)
        {
            try
            {
 var tags = CreateTagList(
     ("operation", operation),
            ("dataset_id", datasetId),
           ("agent_id", agentId),
  ("success", success.ToString().ToLowerInvariant())
  );

   _datasetOperationsCounter.Add(1, tags);
      _operationDurationHistogram.Record(duration.TotalSeconds, tags);

          _logger.LogDebug("Tracked dataset operation: {Operation}, DatasetId={DatasetId}, Success={Success}, Duration={Duration}ms",
   operation, datasetId, success, duration.TotalMilliseconds);
         }
            catch (Exception ex)
            {
    _logger.LogError(ex, "Failed to track dataset operation");
 }
     }

        public void TrackMetricsConfigOperation(string operation, string configId, bool success, TimeSpan duration)
     {
            try
      {
                var tags = CreateTagList(
 ("operation", operation),
      ("config_id", configId),
              ("success", success.ToString().ToLowerInvariant())
         );

        _metricsConfigOperationsCounter.Add(1, tags);
   _operationDurationHistogram.Record(duration.TotalSeconds, tags);

_logger.LogDebug("Tracked metrics config operation: {Operation}, ConfigId={ConfigId}, Success={Success}, Duration={Duration}ms",
         operation, configId, success, duration.TotalMilliseconds);
     }
            catch (Exception ex)
         {
       _logger.LogError(ex, "Failed to track metrics config operation");
    }
        }

      public void TrackEvaluationResultOperation(string operation, string evalRunId, bool success, TimeSpan duration)
        {
   try
   {
                var tags = CreateTagList(
         ("operation", operation),
      ("eval_run_id", evalRunId),
       ("success", success.ToString().ToLowerInvariant())
    );

       _evaluationResultOperationsCounter.Add(1, tags);
         _operationDurationHistogram.Record(duration.TotalSeconds, tags);

  _logger.LogDebug("Tracked evaluation result operation: {Operation}, EvalRunId={EvalRunId}, Success={Success}, Duration={Duration}ms",
       operation, evalRunId, success, duration.TotalMilliseconds);
    }
          catch (Exception ex)
       {
       _logger.LogError(ex, "Failed to track evaluation result operation");
            }
        }

        public void TrackDataVerseApiCall(string evalRunId, string agentId, bool success, int statusCode, TimeSpan duration)
      {
        try
       {
          var tags = CreateTagList(
     ("eval_run_id", evalRunId),
             ("agent_id", agentId),
           ("success", success.ToString().ToLowerInvariant()),
   ("status_code", statusCode.ToString())
       );

     _dataVerseApiCallsCounter.Add(1, tags);
    _operationDurationHistogram.Record(duration.TotalSeconds, tags);

         _logger.LogDebug("Tracked DataVerse API call: EvalRunId={EvalRunId}, Success={Success}, StatusCode={StatusCode}, Duration={Duration}ms",
     evalRunId, success, statusCode, duration.TotalMilliseconds);
            }
     catch (Exception ex)
       {
    _logger.LogError(ex, "Failed to track DataVerse API call");
  }
        }

        public Activity? StartActivity(string operationName)
        {
    var activity = ActivitySource.StartActivity(operationName);
    if (activity != null)
            {
         activity.SetTag("service.name", "SXG.EvalPlatform.API");
    activity.SetTag("service.version", "1.0.0");
  }
  return activity;
 }

        public void AddActivityTags(Dictionary<string, object> tags)
   {
            var currentActivity = Activity.Current;
          if (currentActivity != null && tags != null)
   {
                foreach (var tag in tags)
    {
  currentActivity.SetTag(tag.Key, tag.Value?.ToString());
         }
          }
        }

        public void RecordMetric(string metricName, double value, Dictionary<string, object>? tags = null)
   {
      try
   {
           var histogram = _customHistograms.GetOrAdd(metricName, name => 
        Meter.CreateHistogram<double>(name, "unit", $"Custom metric: {name}"));

            var tagList = CreateTagListFromDictionary(tags);
 histogram.Record(value, tagList);

          _logger.LogDebug("Recorded custom metric: {MetricName}={Value}", metricName, value);
            }
catch (Exception ex)
          {
    _logger.LogError(ex, "Failed to record custom metric: {MetricName}", metricName);
   }
        }

        private static TagList CreateTagList(params (string key, string value)[] tags)
        {
     var tagList = new TagList();
        foreach (var (key, value) in tags)
   {
         tagList.Add(key, value);
     }
            return tagList;
  }

   private static TagList CreateTagListFromDictionary(Dictionary<string, object>? tags)
        {
            var tagList = new TagList();
      if (tags != null)
    {
       foreach (var tag in tags)
      {
       tagList.Add(tag.Key, tag.Value?.ToString());
                }
         }
          return tagList;
        }

        public void Dispose()
        {
       if (_disposed) return;

    try
   {
        _customHistograms.Clear();
          ActivitySource.Dispose();
  Meter.Dispose();
   }
            catch (Exception ex)
      {
             _logger.LogError(ex, "Error during OpenTelemetryService disposal");
            }
            finally
{
            _disposed = true;
      }
 }
    }
}