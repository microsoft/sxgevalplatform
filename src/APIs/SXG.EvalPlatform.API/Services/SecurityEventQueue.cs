using System.Collections.Concurrent;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;
using SxgEvalPlatformApi.Models.Security;

namespace SxgEvalPlatformApi.Services
{
    public interface ISecurityEventQueue
    {
        void Enqueue(SecurityEvent securityEvent);
    }

    public class SecurityEventQueue : BackgroundService, ISecurityEventQueue
    {
        private readonly ILogger<SecurityEventQueue> _logger;
        private readonly TelemetryClient? _telemetryClient;
        private readonly BlockingCollection<SecurityEvent> _queue = new(new ConcurrentQueue<SecurityEvent>());
        private readonly int _shutdownFlushDelayMs;
        private long _enqueuedCount;
        private long _droppedCount;

        public SecurityEventQueue(ILogger<SecurityEventQueue> logger, IConfiguration configuration, TelemetryClient? telemetryClient = null)
        {
            _logger = logger;
            _telemetryClient = telemetryClient;
            // Read configurable shutdown flush delay (milliseconds). Default 5000 ms (5s)
            _shutdownFlushDelayMs = configuration.GetValue<int>("SecurityLogging:ShutdownFlushDelayMs", 5000);
        }

        public void Enqueue(SecurityEvent securityEvent)
        {
            if (!_queue.IsAddingCompleted)
            {
                try
                {
                    _queue.Add(securityEvent);
                    Interlocked.Increment(ref _enqueuedCount);

                    // Debug log current queue length occasionally
                    if ((Interlocked.Read(ref _enqueuedCount) % 100) == 0)
                    {
                        _logger.LogInformation("SecurityEventQueue: enqueued={Enqueued} queueLength={QueueLength}", Interlocked.Read(ref _enqueuedCount), _queue.Count);
                    }
                }
                catch (InvalidOperationException)
                {
                    // queue completed - count as dropped
                    Interlocked.Increment(ref _droppedCount);
                    _logger.LogWarning("SecurityEventQueue: drop event because queue is completed. dropped={Dropped}", Interlocked.Read(ref _droppedCount));
                }
            }
            else
            {
                // queue not accepting - dropped
                Interlocked.Increment(ref _droppedCount);
                _logger.LogWarning("SecurityEventQueue: dropped event because queue is closed. dropped={Dropped}", Interlocked.Read(ref _droppedCount));
            }
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            try
            {
                foreach (var evt in _queue.GetConsumingEnumerable(stoppingToken))
                {
                    try
                    {
                        if (_telemetryClient != null)
                        {
                            var eventTelemetry = new EventTelemetry($"SecurityEvent_{evt.EventType}")
                            {
                                Timestamp = evt.Timestamp
                            };

                            eventTelemetry.Properties["EventType"] = evt.EventType.ToString();
                            eventTelemetry.Properties["Severity"] = evt.Severity.ToString();
                            if (!string.IsNullOrWhiteSpace(evt.UserId)) eventTelemetry.Properties["UserId"] = evt.UserId;
                            if (!string.IsNullOrWhiteSpace(evt.IpAddress)) eventTelemetry.Properties["IpAddress"] = evt.IpAddress;

                            if (evt.Details != null && evt.Details.Any())
                            {
                                eventTelemetry.Properties["Details"] = System.Text.Json.JsonSerializer.Serialize(evt.Details);
                            }

                            // Add severity as property for SDK compatibility
                            eventTelemetry.Properties["SeverityLevel"] = evt.Severity.ToString();

                            _telemetryClient.TrackEvent(eventTelemetry);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to process queued security event");
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // graceful shutdown
            }

            await Task.CompletedTask;
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("SecurityEventQueue: stopping - enqueued={Enqueued} dropped={Dropped} queueLength={QueueLength}",
                Interlocked.Read(ref _enqueuedCount), Interlocked.Read(ref _droppedCount), _queue.Count);

            _queue.CompleteAdding();

            // Wait up to configured flush delay for consumer to drain the queue
            var sw = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                while (_queue.Count > 0 && sw.ElapsedMilliseconds < _shutdownFlushDelayMs)
                {
                    _logger.LogDebug("SecurityEventQueue: waiting for queue to drain. remaining={Remaining}", _queue.Count);
                    await Task.Delay(200, cancellationToken);
                }
            }
            catch (TaskCanceledException) { }

            // Flush telemetry and give it a short time to send
            _telemetryClient?.Flush();
            try
            {
                var remaining = Math.Max(0, _shutdownFlushDelayMs - (int)sw.ElapsedMilliseconds);
                if (remaining > 0)
                {
                    await Task.Delay(Math.Min(remaining, 2000), cancellationToken);
                }
            }
            catch (TaskCanceledException) { }

            _logger.LogInformation("SecurityEventQueue: stopped - enqueued={Enqueued} dropped={Dropped}", Interlocked.Read(ref _enqueuedCount), Interlocked.Read(ref _droppedCount));

            await base.StopAsync(cancellationToken);
        }
    }
}
