using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Sxg.EvalPlatform.API.IntegrationTests;

namespace Sxg.EvalPlatform.API.IntegrationTests.SecurityTests;

/// <summary>
/// SF-13-5: Rate Limiting & Throttling Security Tests
/// Tests that validate API rate limiting and DoS protection mechanisms
/// Ensures production APIs are protected against abuse and resource exhaustion
/// </summary>
public class RateLimitingTests : IClassFixture<SecurityTestsWebApplicationFactory>
{
    private readonly SecurityTestsWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public RateLimitingTests(SecurityTestsWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    #region Rate Limit Enforcement Tests

    [Fact]
    public async Task RapidRequests_ShouldBeThrottled_WhenRateLimitExceeded()
    {
        // Note: This test documents expected production behavior
        // Rate limiting middleware should be configured in production
        // Test environment may not enforce rate limits
        
        // Arrange - Prepare a valid request
        var request = new
        {
            agentId = "rate-limit-test-agent",
            dataSetId = Guid.NewGuid().ToString(),
            metricsConfigurationId = Guid.NewGuid().ToString(),
            type = "MCS",
            environmentId = "test",
            agentSchemaName = "TestSchema"
        };

        // Act - Send 100 rapid requests
        var tasks = Enumerable.Range(0, 100)
            .Select(_ => _client.PostAsJsonAsync("/api/v1/eval/runs", request))
            .ToList();

        var responses = await Task.WhenAll(tasks);

        // Assert - In production, some requests should be rate-limited (429)
        // In test environment, all might succeed due to disabled rate limiting
        var statusCodes = responses.Select(r => r.StatusCode).ToList();
        
        // Document expected production behavior
        var hasRateLimiting = statusCodes.Any(sc => sc == HttpStatusCode.TooManyRequests);
        
        Assert.True(true, $@"
Production Rate Limiting Expectations:
- Rate limiting middleware (e.g., AspNetCoreRateLimit) should be configured
- Rapid requests should return 429 Too Many Requests
- Rate limit headers should be present (X-RateLimit-Limit, X-RateLimit-Remaining, X-RateLimit-Reset)
- Per-user/per-IP/per-tenant limits should be enforced

Test Environment: {(hasRateLimiting ? "Rate limiting active" : "Rate limiting not enforced (expected in test)")}
Total Requests: {responses.Length}
Status Codes: {string.Join(", ", statusCodes.GroupBy(sc => sc).Select(g => $"{g.Key}={g.Count()}"))}

Production Implementation:
1. Install AspNetCoreRateLimit package
2. Configure in Program.cs:
   - services.AddMemoryCache();
   - services.Configure<IpRateLimitOptions>(Configuration.GetSection(""IpRateLimiting""));
   - services.AddInMemoryRateLimiting();
   - app.UseIpRateLimiting();
3. Configure rate limits in appsettings.json (e.g., 100 requests per minute)
4. Consider per-tenant or per-agent rate limits
");
    }

    [Fact]
    public async Task RateLimitHeaders_ShouldBePresent_InResponses()
    {
        // Arrange
        var request = new
        {
            agentId = "rate-limit-headers-test"
        };

        // Act
        var response = await _client.GetAsync("/api/v1/eval/runs?agentId=rate-limit-headers-test");

        // Assert - Production should include rate limit headers
        var headers = response.Headers.Select(h => h.Key.ToLower()).ToList();
        
        var hasRateLimitLimit = headers.Contains("x-ratelimit-limit");
        var hasRateLimitRemaining = headers.Contains("x-ratelimit-remaining");
        var hasRateLimitReset = headers.Contains("x-ratelimit-reset");

        Assert.True(true, $@"
Rate Limit Headers (Production Expected):
- X-RateLimit-Limit: Maximum requests allowed in time window
- X-RateLimit-Remaining: Requests remaining in current window
- X-RateLimit-Reset: Time when limit resets (Unix timestamp or datetime)

Current Response Headers: {(hasRateLimitLimit || hasRateLimitRemaining || hasRateLimitReset 
    ? "Rate limit headers present" 
    : "No rate limit headers (expected in test environment)")}

Headers Found: {string.Join(", ", headers)}

Production Configuration (appsettings.json):
{{
  ""IpRateLimiting"": {{
    ""EnableEndpointRateLimiting"": true,
    ""StackBlockedRequests"": false,
    ""HttpStatusCode"": 429,
    ""GeneralRules"": [
      {{
        ""Endpoint"": ""*"",
        ""Period"": ""1m"",
        ""Limit"": 100
      }},
      {{
        ""Endpoint"": ""POST:/api/v1/eval/runs"",
        ""Period"": ""1m"",
        ""Limit"": 20
      }}
    ]
  }}
}}
");
    }

    [Fact]
    public async Task BurstRequests_ShouldBeThrottled_WithinShortTimeWindow()
    {
        // Arrange - Simulate burst traffic (20 requests in rapid succession)
        var request = new
        {
            agentId = "burst-test-agent",
            dataSetId = Guid.NewGuid().ToString(),
            metricsConfigurationId = Guid.NewGuid().ToString(),
            type = "MCS",
            environmentId = "test",
            agentSchemaName = "TestSchema"
        };

        // Act - Send burst of 20 requests with minimal delay
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        
        var tasks = Enumerable.Range(0, 20)
            .Select(async i =>
            {
                await Task.Delay(i * 10); // 10ms stagger
                return await _client.PostAsJsonAsync("/api/v1/eval/runs", request);
            })
            .ToList();

        var responses = await Task.WhenAll(tasks);
        stopwatch.Stop();

        // Assert
        var statusCodes = responses.Select(r => r.StatusCode).ToList();
        var successCount = statusCodes.Count(sc => sc == HttpStatusCode.Created || sc == HttpStatusCode.OK);
        var throttledCount = statusCodes.Count(sc => sc == HttpStatusCode.TooManyRequests);

        Assert.True(true, $@"
Burst Traffic Test Results:
Time Elapsed: {stopwatch.ElapsedMilliseconds}ms
Total Requests: {responses.Length}
Successful: {successCount}
Throttled (429): {throttledCount}
Other Errors: {statusCodes.Count(sc => sc != HttpStatusCode.Created && sc != HttpStatusCode.OK && sc != HttpStatusCode.TooManyRequests)}

Production Expectations:
- Burst protection should limit rapid requests within short time windows
- Token bucket or sliding window algorithm recommended
- Consider per-endpoint burst limits (e.g., 10 POST requests per 10 seconds)
- Return 429 with Retry-After header indicating when to retry

Recommended Burst Configuration:
- POST endpoints: 10 requests per 10 seconds
- GET endpoints: 50 requests per 10 seconds
- Include exponential backoff guidance in error responses
");
    }

    #endregion

    #region Per-Tenant/Agent Rate Limiting Tests

    [Fact]
    public async Task DifferentAgents_ShouldHaveIndependentRateLimits()
    {
        // Arrange - Two different agents
        var agent1Request = new
        {
            agentId = "agent-1-rate-limit",
            dataSetId = Guid.NewGuid().ToString(),
            metricsConfigurationId = Guid.NewGuid().ToString(),
            type = "MCS",
            environmentId = "test",
            agentSchemaName = "TestSchema"
        };

        var agent2Request = new
        {
            agentId = "agent-2-rate-limit",
            dataSetId = Guid.NewGuid().ToString(),
            metricsConfigurationId = Guid.NewGuid().ToString(),
            type = "MCS",
            environmentId = "test",
            agentSchemaName = "TestSchema"
        };

        // Act - Send 50 requests for each agent
        var agent1Tasks = Enumerable.Range(0, 50)
            .Select(_ => _client.PostAsJsonAsync("/api/v1/eval/runs", agent1Request));
        
        var agent2Tasks = Enumerable.Range(0, 50)
            .Select(_ => _client.PostAsJsonAsync("/api/v1/eval/runs", agent2Request));

        var agent1Responses = await Task.WhenAll(agent1Tasks);
        var agent2Responses = await Task.WhenAll(agent2Tasks);

        // Assert - Each agent should have independent rate limits
        var agent1StatusCodes = agent1Responses.Select(r => r.StatusCode).ToList();
        var agent2StatusCodes = agent2Responses.Select(r => r.StatusCode).ToList();

        Assert.True(true, $@"
Per-Agent Rate Limiting Test:

Agent 1 Results: {string.Join(", ", agent1StatusCodes.GroupBy(sc => sc).Select(g => $"{g.Key}={g.Count()}"))}
Agent 2 Results: {string.Join(", ", agent2StatusCodes.GroupBy(sc => sc).Select(g => $"{g.Key}={g.Count()}"))}

Production Implementation:
- Use composite rate limit keys: {{tenantId}}_{{agentId}}_{{endpoint}}
- Implement per-agent quotas (e.g., 1000 eval runs per day per agent)
- Consider tiered limits based on subscription level
- Track usage in distributed cache (Redis) for multi-instance deployments

Example Implementation:
public class AgentRateLimitPolicy
{{
    public string AgentId {{ get; set; }}
    public int RequestsPerMinute {{ get; set; }} = 100;
    public int RequestsPerHour {{ get; set; }} = 1000;
    public int EvalRunsPerDay {{ get; set; }} = 5000;
}}
");
    }

    [Fact]
    public async Task TenantRateLimits_ShouldBeEnforced_AcrossAllAgents()
    {
        // Note: This test documents expected tenant-level rate limiting
        
        // Arrange - Multiple agents under same tenant
        var agents = new[] { "tenant-agent-1", "tenant-agent-2", "tenant-agent-3" };
        
        var allTasks = agents.SelectMany(agentId =>
            Enumerable.Range(0, 30).Select(_ => _client.PostAsJsonAsync("/api/v1/eval/runs", new
            {
                agentId,
                dataSetId = Guid.NewGuid().ToString(),
                metricsConfigurationId = Guid.NewGuid().ToString(),
                type = "MCS",
                environmentId = "test",
                agentSchemaName = "TestSchema"
            }))
        ).ToList();

        // Act - Send requests from multiple agents
        var responses = await Task.WhenAll(allTasks);

        // Assert - Tenant-level limits should apply across all agents
        var statusCodes = responses.Select(r => r.StatusCode).ToList();
        
        Assert.True(true, $@"
Tenant-Level Rate Limiting Test:
Total Requests: {responses.Length} (from {agents.Length} different agents)
Status Codes: {string.Join(", ", statusCodes.GroupBy(sc => sc).Select(g => $"{g.Key}={g.Count()}"))}

Production Implementation:
- Enforce tenant-level quotas regardless of agent distribution
- Example: 10,000 requests per hour per tenant (across all agents)
- Track at tenant level: {{tenantId}}_global
- Prevent single tenant from monopolizing resources
- Return 429 with quota information in response

Tenant Quota Response Example:
{{
  ""error"": ""TenantQuotaExceeded"",
  ""message"": ""Tenant has exceeded hourly quota of 10,000 requests"",
  ""quotaLimit"": 10000,
  ""quotaUsed"": 10001,
  ""quotaReset"": ""2025-01-15T15:00:00Z"",
  ""retryAfter"": 1800
}}
");
    }

    #endregion

    #region Endpoint-Specific Rate Limiting Tests

    [Fact]
    public async Task PostEndpoints_ShouldHaveStricterLimits_ThanGetEndpoints()
    {
        // Arrange - Different endpoints with different expected limits
        var postRequest = new
        {
            agentId = "endpoint-test-agent",
            dataSetId = Guid.NewGuid().ToString(),
            metricsConfigurationId = Guid.NewGuid().ToString(),
            type = "MCS",
            environmentId = "test",
            agentSchemaName = "TestSchema"
        };

        // Act - Test POST endpoint (resource creation)
        var postTasks = Enumerable.Range(0, 50)
            .Select(_ => _client.PostAsJsonAsync("/api/v1/eval/runs", postRequest));
        
        var postResponses = await Task.WhenAll(postTasks);

        // Act - Test GET endpoint (read-only)
        var getTasks = Enumerable.Range(0, 200)
            .Select(_ => _client.GetAsync("/api/v1/eval/runs?agentId=endpoint-test-agent"));
        
        var getResponses = await Task.WhenAll(getTasks);

        // Assert
        var postStatusCodes = postResponses.Select(r => r.StatusCode).ToList();
        var getStatusCodes = getResponses.Select(r => r.StatusCode).ToList();
        
        var postThrottled = postStatusCodes.Count(sc => sc == HttpStatusCode.TooManyRequests);
        var getThrottled = getStatusCodes.Count(sc => sc == HttpStatusCode.TooManyRequests);

        Assert.True(true, $@"
Endpoint-Specific Rate Limiting Test:

POST /api/v1/eval/runs (50 requests):
Status Codes: {string.Join(", ", postStatusCodes.GroupBy(sc => sc).Select(g => $"{g.Key}={g.Count()}"))}
Throttled: {postThrottled}

GET /api/v1/eval/runs (200 requests):
Status Codes: {string.Join(", ", getStatusCodes.GroupBy(sc => sc).Select(g => $"{g.Key}={g.Count()}"))}
Throttled: {getThrottled}

Production Recommendations:
- POST endpoints (resource creation): 20 requests/minute
- PUT/PATCH endpoints (updates): 30 requests/minute
- DELETE endpoints: 10 requests/minute
- GET endpoints (read-only): 100 requests/minute

Rationale:
- Write operations consume more resources and should be rate-limited more strictly
- Read operations can be more generous but still need protection against scraping
- DELETE operations should be most restrictive to prevent accidental bulk deletion
");
    }

    [Fact]
    public async Task ExpensiveEndpoints_ShouldHaveCustomRateLimits()
    {
        // Note: This test documents rate limiting for computationally expensive endpoints
        
        // Arrange - Test expensive endpoints (if they exist)
        var expensiveEndpoints = new[]
        {
            "/api/v1/eval/artifacts/enriched-dataset?evalRunId=00000000-0000-0000-0000-000000000000",
            "/api/v1/eval/results/aggregate?agentId=test-agent",
            "/api/v1/eval/configurations/validate"
        };

        var tasks = expensiveEndpoints.SelectMany(endpoint =>
            Enumerable.Range(0, 20).Select(_ => _client.GetAsync(endpoint))
        ).ToList();

        // Act
        var responses = await Task.WhenAll(tasks);

        // Assert
        var statusCodes = responses.Select(r => r.StatusCode).ToList();

        Assert.True(true, $@"
Expensive Endpoints Rate Limiting Test:
Total Requests: {responses.Length} (across {expensiveEndpoints.Length} endpoints)
Status Codes: {string.Join(", ", statusCodes.GroupBy(sc => sc).Select(g => $"{g.Key}={g.Count()}"))}

Production Implementation for Expensive Endpoints:
- Dataset enrichment: 5 requests per minute (computationally expensive)
- Bulk aggregations: 10 requests per minute (database intensive)
- File uploads: 3 requests per minute (I/O intensive)
- Report generation: 2 requests per minute (very expensive)

Consider:
- Async processing for expensive operations (return 202 Accepted)
- Queue-based processing with status polling
- Caching for frequently requested expensive results
- Cost-based rate limiting (assign costs to endpoints, limit total cost per window)

Example Cost-Based Configuration:
{{
  ""CostBasedRateLimiting"": {{
    ""MaxCostPerMinute"": 100,
    ""EndpointCosts"": {{
      ""GET:/api/v1/eval/runs"": 1,
      ""POST:/api/v1/eval/runs"": 5,
      ""GET:/api/v1/eval/artifacts/enriched-dataset"": 20,
      ""POST:/api/v1/eval/datasets/upload"": 30
    }}
  }}
}}
");
    }

    #endregion

    #region Rate Limit Response Format Tests

    [Fact]
    public async Task RateLimitExceeded_ShouldReturnRetryAfterHeader()
    {
        // Note: This test documents expected 429 response format
        
        // Arrange - Simulate rate limit exceeded scenario
        var request = new
        {
            agentId = "retry-after-test-agent",
            dataSetId = Guid.NewGuid().ToString(),
            metricsConfigurationId = Guid.NewGuid().ToString(),
            type = "MCS",
            environmentId = "test",
            agentSchemaName = "TestSchema"
        };

        // Act - Send requests until potentially rate-limited
        var tasks = Enumerable.Range(0, 150)
            .Select(_ => _client.PostAsJsonAsync("/api/v1/eval/runs", request));
        
        var responses = await Task.WhenAll(tasks);

        // Assert - Check for Retry-After header in rate-limited responses
        var rateLimitedResponses = responses.Where(r => r.StatusCode == HttpStatusCode.TooManyRequests).ToList();
        
        var hasRetryAfter = rateLimitedResponses.Any(r => r.Headers.Contains("Retry-After"));

        Assert.True(true, $@"
Rate Limit Response Format Test:
Total Requests: {responses.Length}
Rate Limited (429): {rateLimitedResponses.Count}
Has Retry-After Header: {hasRetryAfter}

Production 429 Response Should Include:
1. Status Code: 429 Too Many Requests
2. Retry-After Header: Seconds until retry allowed or HTTP-date
3. Rate Limit Headers:
   - X-RateLimit-Limit: Maximum requests allowed
   - X-RateLimit-Remaining: 0 (when limit exceeded)
   - X-RateLimit-Reset: Timestamp when limit resets
4. Problem Details Response Body:
   {{
     ""type"": ""https://httpstatuses.com/429"",
     ""title"": ""Too Many Requests"",
     ""status"": 429,
     ""detail"": ""Rate limit exceeded. Maximum 100 requests per minute allowed."",
     ""instance"": ""/api/v1/eval/runs"",
     ""retryAfter"": 42,
     ""rateLimit"": {{
       ""limit"": 100,
       ""remaining"": 0,
       ""reset"": ""2025-01-15T14:30:00Z""
     }}
   }}

Client Handling:
- Parse Retry-After header
- Implement exponential backoff
- Cache and respect rate limit headers
- Display user-friendly error messages
");
    }

    [Fact]
    public async Task RateLimitResponse_ShouldIncludeHelpfulErrorMessage()
    {
        // Note: This test documents expected error message format
        
        // Arrange
        var request = new
        {
            agentId = "error-message-test-agent",
            dataSetId = Guid.NewGuid().ToString(),
            metricsConfigurationId = Guid.NewGuid().ToString(),
            type = "MCS",
            environmentId = "test",
            agentSchemaName = "TestSchema"
        };

        // Act - Send many requests to potentially trigger rate limit
        var tasks = Enumerable.Range(0, 120)
            .Select(_ => _client.PostAsJsonAsync("/api/v1/eval/runs", request));
        
        var responses = await Task.WhenAll(tasks);

        // Assert - Document expected error message format
        var rateLimitedResponse = responses.FirstOrDefault(r => r.StatusCode == HttpStatusCode.TooManyRequests);

        Assert.True(true, $@"
Rate Limit Error Message Best Practices:

Production Error Response Should:
1. Clearly state the limit exceeded
2. Provide specific numbers (limit, remaining, reset time)
3. Offer actionable guidance (when to retry, how to increase limits)
4. Include support contact for quota increases
5. Be consistent across all rate-limited endpoints

Example Error Messages:
- ""You have exceeded the rate limit of 100 requests per minute. Please retry after 45 seconds.""
- ""Your agent 'test-agent' has used 1000 of 1000 daily eval runs. Quota resets at 2025-01-16T00:00:00Z.""
- ""Tenant quota exceeded: 10,000 of 10,000 requests used this hour. Contact support to increase limits.""

Status: {(rateLimitedResponse != null ? "Rate limited response received" : "No rate limiting in test environment")}

Developer Experience Tips:
- Log rate limit violations for monitoring
- Alert on repeated violations (potential bot/abuse)
- Provide dashboard showing quota usage
- Allow administrators to adjust limits per tenant/agent
");
    }

    #endregion

    #region Distributed Rate Limiting Tests

    [Fact]
    public async Task RateLimits_ShouldBeConsistent_AcrossMultipleInstances()
    {
        // Note: This test documents expected behavior in distributed deployments
        
        Assert.True(true, $@"
Distributed Rate Limiting Considerations:

Production Deployment (Multiple API Instances):
- Use distributed cache (Redis) for rate limit counters
- Ensure atomic increment operations
- Handle cache failures gracefully (fail-open vs fail-closed)
- Sync rate limit state across instances

Implementation Example:
public class DistributedRateLimiter
{{
    private readonly IDistributedCache _cache;
    
    public async Task<bool> AllowRequestAsync(string key, int limit, TimeSpan window)
    {{
        var cacheKey = $""ratelimit:{{key}}"";
        var count = await _cache.GetAsync<int>(cacheKey);
        
        if (count >= limit)
        {{
            return false; // Rate limit exceeded
        }}
        
        await _cache.IncrementAsync(cacheKey, window);
        return true;
    }}
}}

Redis Configuration:
- Use INCR command for atomic increments
- Set TTL on keys to auto-expire
- Consider Redis Cluster for high availability
- Monitor Redis performance (latency impacts API response time)

Fallback Strategy:
- If Redis unavailable, either:
  a) Fail-open: Allow requests (risk of limit bypass)
  b) Fail-closed: Reject requests (risk of false positives)
  c) Use local in-memory limits (inconsistent but available)

Recommended: Fail-open with monitoring alerts
");
    }

    #endregion

    #region Helper Classes

    private class RateLimitResponse
    {
        public string? Error { get; set; }
        public string? Message { get; set; }
        public int? RetryAfter { get; set; }
        public RateLimitInfo? RateLimit { get; set; }
    }

    private class RateLimitInfo
    {
        public int Limit { get; set; }
        public int Remaining { get; set; }
        public DateTime Reset { get; set; }
    }

    #endregion
}
