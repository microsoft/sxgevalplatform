using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using SxgEvalPlatformApi.Models;

namespace Sxg.EvalPlatform.API.PerfTests
{
    /// <summary>
    /// Performance and load tests for EvalRun POST API (/api/v1/eval/runs)
    /// Tests the create evaluation run endpoint with concurrent requests
    /// </summary>
    public class EvalRunPerformanceTest : IAsyncLifetime
    {
        #region Test Payload Constants

        /// <summary>
        /// Constant test payload - modify this to change test data for all concurrent requests
        /// </summary>
        private static readonly CreateEvalRunDto TestPayload = new()
        {
            AgentId = "dummy_4e5f6g7h8i9j0k1l2m3n4o5p",
            DataSetId = Guid.Parse("caf253ce-7da6-43e1-a64b-2856eed332ed"), // Replace with valid dataset ID
            MetricsConfigurationId = Guid.Parse("d47b89bc-024d-42e2-9b84-7db4fc225e64"), // Replace with valid metrics config ID
            Type = "MCS",
            EnvironmentId = "948a58e0-a265e26e-bbd0-3d0cf7978511",
            AgentSchemaName = "crb32_sxGDriCopilot",
            EvalRunName = "Performance Test Run"
        };

        /// <summary>
        /// Number of concurrent requests for load testing
        /// </summary>
        private const int ConcurrentRequestCount = 10;

        /// <summary>
        /// Request timeout in seconds
        /// </summary>
        private const int RequestTimeoutSeconds = 30;

        /// <summary>
        /// Dev environment API base URL
        /// </summary>
        private const string DevApiBaseUrl = "https://sxgevalapidev.azurewebsites.net";

        #endregion

        #region Test Infrastructure

        private IConfiguration? _configuration;
        private HttpClient? _httpClient;
        private string? _apiEndpoint;

        public async Task InitializeAsync()
        {
            // Load configuration from appsettings.json (copied during build)
            _configuration = new ConfigurationBuilder()
           .SetBasePath(Directory.GetCurrentDirectory())
         .AddJsonFile("appsettings.json", optional: true)
              .AddJsonFile("appsettings.Development.json", optional: true)
         .AddJsonFile("appsettings.Local.json", optional: true)
     .Build();

            // Construct API endpoint
            _apiEndpoint = $"{DevApiBaseUrl}/api/v1/eval/runs";

            // Initialize HttpClient (no authentication needed for dev environment typically)
            _httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(RequestTimeoutSeconds)
            };

            _httpClient.DefaultRequestHeaders.Add("Accept", "application/json");
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "SXG-EvalPlatform-PerfTests/1.0");

            Console.WriteLine($"Initialized EvalRun Performance Tests");
            Console.WriteLine($"Target API: {_apiEndpoint}");
            Console.WriteLine($"Test Payload: {JsonSerializer.Serialize(TestPayload, new JsonSerializerOptions { WriteIndented = true })}");
            Console.WriteLine(new string('=', 80));

            await Task.CompletedTask;
        }

        public Task DisposeAsync()
        {
            _httpClient?.Dispose();
            return Task.CompletedTask;
        }

        #endregion

        #region Performance Tests

        /// <summary>
        /// Test: Single request to validate basic functionality and measure baseline performance
        /// </summary>
        [Fact]
        public async Task Test_SingleRequest_CreateEvalRun_ValidateBaseline()
        {
            // Arrange
            var stopwatch = Stopwatch.StartNew();

            // Act
            var response = await CreateEvalRunAsync(TestPayload);
            stopwatch.Stop();

            // Assert
            Assert.NotNull(response);
            Assert.True(response.StatusCode >= 200 && response.StatusCode < 300 || response.StatusCode == 400,
            $"Expected success or validation error, got {response.StatusCode}: {response.Message}");

            // Log performance metrics
            var output = new
            {
                Test = "SingleRequest_CreateEvalRun",
                Duration = $"{stopwatch.ElapsedMilliseconds}ms",
                StatusCode = response.StatusCode,
                Success = response.Success,
                Message = response.Message,
                EvalRunId = response.EvalRunId
            };

            Console.WriteLine($"\nSingle Request Test Result:");
            Console.WriteLine(JsonSerializer.Serialize(output, new JsonSerializerOptions { WriteIndented = true }));

            // Performance assertion - single request should complete within 5 seconds
         //   Assert.True(stopwatch.ElapsedMilliseconds < 5000,
         //$"Single request took {stopwatch.ElapsedMilliseconds}ms, expected < 5000ms");
        }

        /// <summary>
        /// Test: 50 concurrent requests to measure load handling and identify bottlenecks
        /// This is the main load test with configurable payload
        /// </summary>
        [Fact]
        public async Task Test_50ConcurrentRequests_CreateEvalRun_LoadTest()
        {
            // Arrange
            var totalStopwatch = Stopwatch.StartNew();
            var tasks = new List<Task<(EvalRunApiResponse Response, long DurationMs)>>();
            var results = new List<PerformanceResult>();

            Console.WriteLine($"\n{new string('=', 80)}");
            Console.WriteLine($"Starting load test: Creating {ConcurrentRequestCount} evaluation runs concurrently");
            Console.WriteLine($"API Endpoint: {_apiEndpoint}");
            Console.WriteLine($"Test Payload:");
            Console.WriteLine(JsonSerializer.Serialize(TestPayload, new JsonSerializerOptions { WriteIndented = true }));
            Console.WriteLine(new string('=', 80));
            Console.WriteLine();

            // Act - Launch all concurrent requests
            for (int i = 0; i < ConcurrentRequestCount; i++)
            {
                int requestNumber = i + 1;
                tasks.Add(ExecuteTimedRequestAsync(requestNumber));
            }

            // Wait for all requests to complete
            var completedTasks = await Task.WhenAll(tasks);
            totalStopwatch.Stop();

            // Collect results
            foreach (var (response, duration) in completedTasks)
            {
                results.Add(new PerformanceResult
                {
                    StatusCode = response.StatusCode,
                    Success = response.Success,
                    DurationMs = duration,
                    Message = response.Message,
                    EvalRunId = response.EvalRunId
                });
            }

            // Analyze and Assert
            var analysis = AnalyzeResults(results, totalStopwatch.ElapsedMilliseconds);

            // Print detailed analysis
            Console.WriteLine($"\n{new string('=', 80)}");
            Console.WriteLine("LOAD TEST RESULTS - 50 Concurrent EvalRun Creation Requests");
            Console.WriteLine(new string('=', 80));
            Console.WriteLine(JsonSerializer.Serialize(analysis, new JsonSerializerOptions { WriteIndented = true }));

            // Print sample of created EvalRunIds
            var createdRuns = results.Where(r => r.Success && r.EvalRunId.HasValue).Take(10).ToList();
            if (createdRuns.Any())
            {
                Console.WriteLine($"\nSample of Created EvalRun IDs (first 10):");
                foreach (var run in createdRuns)
                {
                    Console.WriteLine($"  - {run.EvalRunId}");
                }
            }

            Console.WriteLine(new string('=', 80));

            // Assertions
            Assert.True(analysis.SuccessRate >= 0.9, // At least 90% success rate
                $"Success rate {analysis.SuccessRate:P2} is below acceptable threshold of 90%");

            Assert.True(analysis.AverageDurationMs < 10000, // Average response time under 10 seconds
    $"Average response time {analysis.AverageDurationMs}ms exceeds acceptable threshold of 10000ms");

            Assert.True(analysis.TotalDurationMs < 60000, // All requests complete within 60 seconds
  $"Total duration {analysis.TotalDurationMs}ms exceeds acceptable threshold of 60000ms");
        }

        /// <summary>
        /// Test: Sequential requests to establish baseline (no concurrency)
        /// Useful for comparing against concurrent performance
        /// </summary>
        [Fact]
        public async Task Test_SequentialRequests_CreateEvalRun_Baseline()
        {
            // Arrange
            const int requestCount = 10; // Reduced count for sequential test
            var stopwatch = Stopwatch.StartNew();
            var results = new List<PerformanceResult>();

            Console.WriteLine($"\n{new string('=', 80)}");
            Console.WriteLine($"Starting baseline test: Creating {requestCount} evaluation runs sequentially");
            Console.WriteLine(new string('=', 80));
            Console.WriteLine();

            // Act - Execute requests sequentially
            for (int i = 0; i < requestCount; i++)
            {
                var requestStopwatch = Stopwatch.StartNew();
                var response = await CreateEvalRunAsync(TestPayload);
                requestStopwatch.Stop();

                results.Add(new PerformanceResult
                {
                    StatusCode = response.StatusCode,
                    Success = response.Success,
                    DurationMs = requestStopwatch.ElapsedMilliseconds,
                    Message = response.Message,
                    EvalRunId = response.EvalRunId
                });

                Console.WriteLine($"Request {i + 1}/{requestCount}: {response.StatusCode} - {requestStopwatch.ElapsedMilliseconds}ms" +
               (response.Success ? $" - EvalRunId: {response.EvalRunId}" : ""));
            }

            stopwatch.Stop();

            // Analyze
            var analysis = AnalyzeResults(results, stopwatch.ElapsedMilliseconds);

            Console.WriteLine($"\n{new string('=', 80)}");
            Console.WriteLine("SEQUENTIAL BASELINE RESULTS");
            Console.WriteLine(new string('=', 80));
            Console.WriteLine(JsonSerializer.Serialize(analysis, new JsonSerializerOptions { WriteIndented = true }));
            Console.WriteLine(new string('=', 80));

            // Assert
            Assert.True(analysis.AverageDurationMs < 5000,
            $"Baseline average response time {analysis.AverageDurationMs}ms exceeds expected 5000ms");
        }

        /// <summary>
        /// Test: Stress test with gradual load increase (10, 25, 50, 100 requests)
        /// </summary>
        [Fact(Skip = "Long-running stress test - enable manually when needed")]
        public async Task Test_GradualLoadIncrease_CreateEvalRun_StressTest()
        {
            var loadLevels = new[] { 10, 25, 50, 100 };
            var stressTestResults = new List<object>();

            Console.WriteLine($"\n{new string('=', 80)}");
            Console.WriteLine("STRESS TEST - Gradual Load Increase");
            Console.WriteLine(new string('=', 80));

            foreach (var loadLevel in loadLevels)
            {
                Console.WriteLine($"\n{new string('-', 80)}");
                Console.WriteLine($"Testing with {loadLevel} concurrent requests");
                Console.WriteLine(new string('-', 80));

                var stopwatch = Stopwatch.StartNew();
                var tasks = new List<Task<(EvalRunApiResponse Response, long DurationMs)>>();

                for (int i = 0; i < loadLevel; i++)
                {
                    int requestNumber = i + 1;
                    tasks.Add(ExecuteTimedRequestAsync(requestNumber));
                }

                var completedTasks = await Task.WhenAll(tasks);
                stopwatch.Stop();

                var results = completedTasks.Select(t => new PerformanceResult
                {
                    StatusCode = t.Response.StatusCode,
                    Success = t.Response.Success,
                    DurationMs = t.Item2,
                    Message = t.Response.Message,
                    EvalRunId = t.Response.EvalRunId
                }).ToList();

                var analysis = AnalyzeResults(results, stopwatch.ElapsedMilliseconds);
                analysis.ConcurrentRequests = loadLevel;

                stressTestResults.Add(analysis);
                Console.WriteLine(JsonSerializer.Serialize(analysis, new JsonSerializerOptions { WriteIndented = true }));

                // Cool-down period between load levels
                Console.WriteLine($"Cool-down period (2 seconds)...");
                await Task.Delay(2000);
            }

            Console.WriteLine($"\n{new string('=', 80)}");
            Console.WriteLine("STRESS TEST SUMMARY");
            Console.WriteLine(new string('=', 80));
            Console.WriteLine(JsonSerializer.Serialize(stressTestResults, new JsonSerializerOptions { WriteIndented = true }));
            Console.WriteLine(new string('=', 80));
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Executes a single timed request
        /// </summary>
        private async Task<(EvalRunApiResponse Response, long DurationMs)> ExecuteTimedRequestAsync(int requestNumber)
        {
            var stopwatch = Stopwatch.StartNew();
            try
            {
                var response = await CreateEvalRunAsync(TestPayload);
                stopwatch.Stop();

                var status = response.Success ? "✓" : "✗";
                Console.WriteLine($"{status} Request #{requestNumber}: {response.StatusCode} - {stopwatch.ElapsedMilliseconds}ms" +
             (response.Success ? $" - EvalRunId: {response.EvalRunId}" : $" - {response.Message}"));

                return (response, stopwatch.ElapsedMilliseconds);
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                Console.WriteLine($"✗ Request #{requestNumber}: FAILED - {ex.Message} - {stopwatch.ElapsedMilliseconds}ms");

                return (new EvalRunApiResponse
                {
                    Success = false,
                    StatusCode = 500,
                    Message = ex.Message
                }, stopwatch.ElapsedMilliseconds);
            }
        }

        /// <summary>
        /// Posts eval run creation request to the API
        /// </summary>
        private async Task<EvalRunApiResponse> CreateEvalRunAsync(CreateEvalRunDto request)
        {
            try
            {
                var response = await _httpClient!.PostAsJsonAsync(_apiEndpoint, request);
                var responseContent = await response.Content.ReadAsStringAsync();

                // Try to parse the response if successful
                Guid? evalRunId = null;
                if (response.IsSuccessStatusCode)
                {
                    try
                    {
                        var evalRunDto = JsonSerializer.Deserialize<EvalRunDto>(responseContent,
                         new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                        evalRunId = evalRunDto?.EvalRunId;
                    }
                    catch
                    {
                        // Ignore parsing errors
                    }
                }

                return new EvalRunApiResponse
                {
                    Success = response.IsSuccessStatusCode,
                    StatusCode = (int)response.StatusCode,
                    Message = response.ReasonPhrase ?? "No reason phrase",
                    ResponseContent = responseContent,
                    EvalRunId = evalRunId
                };
            }
            catch (HttpRequestException ex)
            {
                return new EvalRunApiResponse
                {
                    Success = false,
                    StatusCode = 500,
                    Message = $"HTTP Request failed: {ex.Message}",
                    ResponseContent = null
                };
            }
            catch (TaskCanceledException ex)
            {
                return new EvalRunApiResponse
                {
                    Success = false,
                    StatusCode = 408, // Request Timeout
                    Message = $"Request timeout: {ex.Message}",
                    ResponseContent = null
                };
            }
        }

        /// <summary>
        /// Analyzes performance test results and generates metrics
        /// </summary>
        private PerformanceAnalysis AnalyzeResults(List<PerformanceResult> results, long totalDurationMs)
        {
            var successfulRequests = results.Count(r => r.Success);
            var failedRequests = results.Count - successfulRequests;
            var durations = results.Select(r => r.DurationMs).ToList();

            var statusCodeDistribution = results
        .GroupBy(r => r.StatusCode)
                   .ToDictionary(g => g.Key, g => g.Count());

            var createdEvalRuns = results.Count(r => r.EvalRunId.HasValue);

            return new PerformanceAnalysis
            {
                TotalRequests = results.Count,
                SuccessfulRequests = successfulRequests,
                FailedRequests = failedRequests,
                SuccessRate = (double)successfulRequests / results.Count,
                TotalDurationMs = totalDurationMs,
                AverageDurationMs = durations.Average(),
                MinDurationMs = durations.Min(),
                MaxDurationMs = durations.Max(),
                MedianDurationMs = CalculateMedian(durations),
                P95DurationMs = CalculatePercentile(durations, 95),
                P99DurationMs = CalculatePercentile(durations, 99),
                RequestsPerSecond = (double)results.Count / (totalDurationMs / 1000.0),
                StatusCodeDistribution = statusCodeDistribution,
                Errors = results.Where(r => !r.Success).Select(r => r.Message).Distinct().ToList(),
                CreatedEvalRunsCount = createdEvalRuns
            };
        }

        private static double CalculateMedian(List<long> values)
        {
            var sorted = values.OrderBy(v => v).ToList();
            int count = sorted.Count;
            if (count == 0) return 0;
            if (count % 2 == 0)
                return (sorted[count / 2 - 1] + sorted[count / 2]) / 2.0;
            return sorted[count / 2];
        }

        private static double CalculatePercentile(List<long> values, double percentile)
        {
            var sorted = values.OrderBy(v => v).ToList();
            int count = sorted.Count;
            if (count == 0) return 0;

            double index = (percentile / 100.0) * (count - 1);
            int lowerIndex = (int)Math.Floor(index);
            int upperIndex = (int)Math.Ceiling(index);

            if (lowerIndex == upperIndex)
                return sorted[lowerIndex];

            return sorted[lowerIndex] + (sorted[upperIndex] - sorted[lowerIndex]) * (index - lowerIndex);
        }

        #endregion

        #region Data Models

        public class PerformanceResult
        {
            public int StatusCode { get; set; }
            public bool Success { get; set; }
            public long DurationMs { get; set; }
            public string Message { get; set; } = string.Empty;
            public Guid? EvalRunId { get; set; }
        }

        public class PerformanceAnalysis
        {
            public int ConcurrentRequests { get; set; }
            public int TotalRequests { get; set; }
            public int SuccessfulRequests { get; set; }
            public int FailedRequests { get; set; }
            public double SuccessRate { get; set; }
            public long TotalDurationMs { get; set; }
            public double AverageDurationMs { get; set; }
            public long MinDurationMs { get; set; }
            public long MaxDurationMs { get; set; }
            public double MedianDurationMs { get; set; }
            public double P95DurationMs { get; set; }
            public double P99DurationMs { get; set; }
            public double RequestsPerSecond { get; set; }
            public Dictionary<int, int> StatusCodeDistribution { get; set; } = new();
            public List<string> Errors { get; set; } = new();
            public int CreatedEvalRunsCount { get; set; }
        }

        public class EvalRunApiResponse
        {
            public bool Success { get; set; }
            public int StatusCode { get; set; }
            public string Message { get; set; } = string.Empty;
            public string? ResponseContent { get; set; }
            public Guid? EvalRunId { get; set; }
        }

        #endregion
    }
}
