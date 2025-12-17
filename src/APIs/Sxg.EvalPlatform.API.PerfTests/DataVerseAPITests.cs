using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;
using Azure.Core;
using Azure.Identity;
using Microsoft.Extensions.Configuration;
using Sxg.EvalPlatform.API.Storage.Services;
using SXG.EvalPlatform.Common;

namespace Sxg.EvalPlatform.API.PerfTests
{
    /// <summary>
    /// Performance and load tests for DataVerse API integration
    /// </summary>
    public class DataVerseAPITests : IAsyncLifetime
    {
        #region Test Payload Constants

        /// <summary>
        /// Constant test payload - modify this to change test data for all concurrent requests
        /// </summary>
        private static readonly DataVerseApiRequest TestPayload = new()
        {
            EvalRunId = "550e8400-e29b-41d4-a716-446655440000", // Test GUID
            AgentId = "test-agent-perf-001",
            EnvironmentId = "948a58e0-a265e26e-bbd0-3d0cf7978511",
            AgentSchemaName = "crb32_sxGDriCopilot",
            DatasetId = "dataset-perf-test-001"
        };

        /// <summary>
        /// Number of concurrent requests for load testing
        /// </summary>
        private const int ConcurrentRequestCount = 50;

        /// <summary>
        /// Request timeout in seconds
        /// </summary>
        private const int RequestTimeoutSeconds = 30;

        #endregion

        #region Test Infrastructure

        private IConfiguration? _configuration;
        private HttpClient? _httpClient;
        private string? _apiEndpoint;
        private string? _scope;

        public async Task InitializeAsync()
        {
            // Load configuration from appsettings.json (copied during build)
            _configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false)
                .AddJsonFile("appsettings.Development.json", optional: true)
                .AddJsonFile("appsettings.Local.json", optional: true)
                .Build();

            // Get DataVerse API configuration
            _apiEndpoint = _configuration["DataVerseAPI:DatasetEnrichmentRequestAPIEndPoint"];
            _scope = _configuration["DataVerseAPI:Scope"];

            if (string.IsNullOrEmpty(_apiEndpoint))
            {
                throw new InvalidOperationException(
                    "DataVerse API endpoint not configured. Please set DataVerseAPI:DatasetEnrichmentRequestAPIEndPoint in appsettings.json");
            }

            if (string.IsNullOrEmpty(_scope))
            {
                throw new InvalidOperationException(
                    "DataVerse API scope not configured. Please set DataVerseAPI:Scope in appsettings.json");
            }

            // Initialize HttpClient with authentication
            _httpClient = await CreateAuthenticatedHttpClientAsync();

            await Task.CompletedTask;
        }

        public Task DisposeAsync()
        {
            _httpClient?.Dispose();
            return Task.CompletedTask;
        }

        /// <summary>
        /// Creates an authenticated HttpClient for DataVerse API calls
        /// </summary>
        private async Task<HttpClient> CreateAuthenticatedHttpClientAsync()
        {
            var httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(RequestTimeoutSeconds)
            };

            // Get environment from configuration
            var environment = _configuration?["ApiSettings:Environment"] ?? "Local";
            var credential = CommonUtils.GetTokenCredential(environment);

            try
            {
                // Acquire token for DataVerse API
                var tokenRequestContext = new TokenRequestContext(new[] { _scope! });
                var token = await credential.GetTokenAsync(tokenRequestContext, CancellationToken.None);

                httpClient.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token.Token);

                httpClient.DefaultRequestHeaders.Add("Accept", "application/json");
                httpClient.DefaultRequestHeaders.Add("User-Agent", "SXG-EvalPlatform-PerfTests/1.0");
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"Failed to acquire authentication token for DataVerse API. Environment: {environment}, Scope: {_scope}", ex);
            }

            return httpClient;
        }

        #endregion

        #region Performance Tests

        /// <summary>
        /// Test: Single request to validate basic functionality and measure baseline performance
        /// </summary>
        [Fact(Skip = "This is perf test and should be invoked manually.")]
        public async Task Test_SingleRequest_ValidateBasicFunctionality()
        {
            // Arrange
            var stopwatch = Stopwatch.StartNew();

            // Act
            var response = await PostEvalRunAsync(TestPayload);
            stopwatch.Stop();

            // Assert
            Assert.NotNull(response);
            Assert.True(response.StatusCode >= 200 && response.StatusCode < 300 || response.StatusCode == 400,
                $"Expected success or validation error, got {response.StatusCode}: {response.Message}");

            // Log performance metrics
            var output = new
            {
                Test = "SingleRequest",
                Duration = $"{stopwatch.ElapsedMilliseconds}ms",
                StatusCode = response.StatusCode,
                Success = response.Success,
                Message = response.Message
            };

            Console.WriteLine(
                $"Single Request Test Result: {JsonSerializer.Serialize(output, new JsonSerializerOptions { WriteIndented = true })}");

            // Performance assertion - single request should complete within 5 seconds
            Assert.True(stopwatch.ElapsedMilliseconds < 5000,
                $"Single request took {stopwatch.ElapsedMilliseconds}ms, expected < 5000ms");
        }

        /// <summary>
        /// Test: 50 concurrent requests to measure load handling and identify bottlenecks
        /// This is the main load test with configurable payload
        /// </summary>
        [Fact(Skip = "This is perf test and should be invoked manually.")]
        public async Task Test_50ConcurrentRequests_LoadTest()
        {
            // Arrange
            var totalStopwatch = Stopwatch.StartNew();
            var tasks = new List<Task<(DataVerseApiResponse Response, long DurationMs)>>();
            var results = new List<PerformanceResult>();

            Console.WriteLine($"Starting load test with {ConcurrentRequestCount} concurrent requests");
            Console.WriteLine($"Test Payload: {JsonSerializer.Serialize(TestPayload, new JsonSerializerOptions { WriteIndented = true })}");
            Console.WriteLine($"API Endpoint: {_apiEndpoint}");
            Console.WriteLine(new string('-', 80));

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
                    Message = response.Message
                });
            }

            // Analyze and Assert
            var analysis = AnalyzeResults(results, totalStopwatch.ElapsedMilliseconds);

            // Print detailed analysis
            Console.WriteLine(JsonSerializer.Serialize(analysis, new JsonSerializerOptions { WriteIndented = true }));

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
        [Fact(Skip = "Run it only when need to do performance testing")]
        public async Task Test_SequentialRequests_Baseline()
        {
            // Arrange
            const int requestCount = 10; // Reduced count for sequential test
            var stopwatch = Stopwatch.StartNew();
            var results = new List<PerformanceResult>();

            Console.WriteLine($"Starting baseline test with {requestCount} sequential requests");
            Console.WriteLine(new string('-', 80));

            // Act - Execute requests sequentially
            for (int i = 0; i < requestCount; i++)
            {
                var requestStopwatch = Stopwatch.StartNew();
                var response = await PostEvalRunAsync(TestPayload);
                requestStopwatch.Stop();

                results.Add(new PerformanceResult
                {
                    StatusCode = response.StatusCode,
                    Success = response.Success,
                    DurationMs = requestStopwatch.ElapsedMilliseconds,
                    Message = response.Message
                });

                Console.WriteLine($"Request {i + 1}/{requestCount}: {response.StatusCode} - {requestStopwatch.ElapsedMilliseconds}ms");
            }

            stopwatch.Stop();

            // Analyze
            var analysis = AnalyzeResults(results, stopwatch.ElapsedMilliseconds);

            Console.WriteLine(new string('-', 80));
            Console.WriteLine($"Sequential Baseline Results:");
            Console.WriteLine(JsonSerializer.Serialize(analysis, new JsonSerializerOptions { WriteIndented = true }));

            // Assert
            Assert.True(analysis.AverageDurationMs < 5000,
                $"Baseline average response time {analysis.AverageDurationMs}ms exceeds expected 5000ms");
        }

        /// <summary>
        /// Test: Stress test with gradual load increase (10, 25, 50, 100 requests)
        /// </summary>
        [Fact(Skip = "Long-running stress test - enable manually when needed")]
        public async Task Test_GradualLoadIncrease_StressTest()
        {
            var loadLevels = new[] { 10, 25, 50, 100 };
            var stressTestResults = new List<object>();

            foreach (var loadLevel in loadLevels)
            {
                Console.WriteLine($"\n{new string('=', 80)}");
                Console.WriteLine($"Testing with {loadLevel} concurrent requests");
                Console.WriteLine(new string('=', 80));

                var stopwatch = Stopwatch.StartNew();
                var tasks = new List<Task<(DataVerseApiResponse Response, long DurationMs)>>();

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
                    Message = t.Response.Message
                }).ToList();

                var analysis = AnalyzeResults(results, stopwatch.ElapsedMilliseconds);
                analysis.ConcurrentRequests = loadLevel;

                stressTestResults.Add(analysis);
                Console.WriteLine(JsonSerializer.Serialize(analysis, new JsonSerializerOptions { WriteIndented = true }));

                // Cool-down period between load levels
                await Task.Delay(2000);
            }

            Console.WriteLine($"\n{new string('=', 80)}");
            Console.WriteLine("Stress Test Summary:");
            Console.WriteLine(JsonSerializer.Serialize(stressTestResults, new JsonSerializerOptions { WriteIndented = true }));
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Executes a single timed request
        /// </summary>
        private async Task<(DataVerseApiResponse Response, long DurationMs)> ExecuteTimedRequestAsync(int requestNumber)
        {
            var stopwatch = Stopwatch.StartNew();
            try
            {
                var response = await PostEvalRunAsync(TestPayload);
                stopwatch.Stop();

                Console.WriteLine($"Request #{requestNumber}: {response.StatusCode} - {stopwatch.ElapsedMilliseconds}ms");

                return (response, stopwatch.ElapsedMilliseconds);
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                Console.WriteLine($"Request #{requestNumber}: FAILED - {ex.Message} - {stopwatch.ElapsedMilliseconds}ms");

                return (new DataVerseApiResponse
                {
                    Success = false,
                    StatusCode = 500,
                    Message = ex.Message
                }, stopwatch.ElapsedMilliseconds);
            }
        }

        /// <summary>
        /// Posts eval run data to DataVerse API
        /// </summary>
        private async Task<DataVerseApiResponse> PostEvalRunAsync(DataVerseApiRequest request)
        {
            try
            {
                var response = await _httpClient!.PostAsJsonAsync(_apiEndpoint, request);

                var responseContent = await response.Content.ReadAsStringAsync();

                return new DataVerseApiResponse
                {
                    Success = response.IsSuccessStatusCode,
                    StatusCode = (int)response.StatusCode,
                    Message = response.ReasonPhrase ?? "No reason phrase",
                    ResponseContent = responseContent
                };
            }
            catch (HttpRequestException ex)
            {
                return new DataVerseApiResponse
                {
                    Success = false,
                    StatusCode = 500,
                    Message = $"HTTP Request failed: {ex.Message}",
                    ResponseContent = null
                };
            }
            catch (TaskCanceledException ex)
            {
                return new DataVerseApiResponse
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
                Errors = results.Where(r => !r.Success).Select(r => r.Message).Distinct().ToList()
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

        private class PerformanceResult
        {
            public int StatusCode { get; set; }
            public bool Success { get; set; }
            public long DurationMs { get; set; }
            public string Message { get; set; } = string.Empty;
        }

        private class PerformanceAnalysis
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
        }

        #endregion
    }
}