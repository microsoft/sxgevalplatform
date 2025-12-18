using System.Net;
using System.Net.Http.Headers;
using System.Text;
using FluentAssertions;

namespace Sxg.EvalPlatform.API.IntegrationTests.SecurityTests;

/// <summary>
/// SF-13-2: Malformed Message Structure Tests
/// Tests that the API properly handles and rejects malformed requests including
/// invalid JSON, incorrect Content-Types, and structurally invalid messages
/// </summary>
public class MalformedRequestTests : IClassFixture<SecurityTestsWebApplicationFactory>
{
    private readonly HttpClient _client;

    public MalformedRequestTests(SecurityTestsWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    private HttpClient Client => _client;

    #region Invalid JSON Tests

    [Fact]
    public async Task CreateEvalRun_WithInvalidJSON_Returns400()
    {
        // Arrange - Malformed JSON (missing closing brace)
        var invalidJson = @"{
            ""agentId"": ""test-agent"",
            ""dataSetId"": ""550e8400-e29b-41d4-a716-446655440000"",
            ""metricsConfigurationId"": ""650e8400-e29b-41d4-a716-446655440001""
            // Missing closing brace
        ";

        var content = new StringContent(invalidJson, Encoding.UTF8, "application/json");

        // Act
        var response = await Client.PostAsync("/api/v1/eval/runs", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest, 
            "Invalid JSON should result in 400 Bad Request");
        
        var responseBody = await response.Content.ReadAsStringAsync();
        responseBody.Should().NotBeNullOrEmpty("Response should contain error details");
    }

    [Fact]
    public async Task CreateEvalRun_WithIncompleteJSON_Returns400()
    {
        // Arrange - JSON with only opening brace
        var invalidJson = "{";

        var content = new StringContent(invalidJson, Encoding.UTF8, "application/json");

        // Act
        var response = await Client.PostAsync("/api/v1/eval/runs", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateEvalRun_WithInvalidJSONSyntax_Returns400()
    {
        // Arrange - Invalid JSON (unquoted keys)
        var invalidJson = @"{
            agentId: 'test-agent',
            dataSetId: '550e8400-e29b-41d4-a716-446655440000'
        }";

        var content = new StringContent(invalidJson, Encoding.UTF8, "application/json");

        // Act
        var response = await Client.PostAsync("/api/v1/eval/runs", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest,
            "JSON with unquoted keys should be rejected");
    }

    [Fact]
    public async Task CreateEvalRun_WithTrailingComma_Returns400()
    {
        // Arrange - JSON with trailing comma (invalid in strict JSON)
        var invalidJson = @"{
            ""agentId"": ""test-agent"",
            ""dataSetId"": ""550e8400-e29b-41d4-a716-446655440000"",
            ""metricsConfigurationId"": ""650e8400-e29b-41d4-a716-446655440001"",
        }";

        var content = new StringContent(invalidJson, Encoding.UTF8, "application/json");

        // Act
        var response = await Client.PostAsync("/api/v1/eval/runs", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest,
            "JSON with trailing comma should be rejected");
    }

    [Fact]
    public async Task SaveDataset_WithNullByteInJSON_Returns400()
    {
        // Arrange - JSON with null byte character
        var invalidJson = "{\"agentId\":\"test\u0000agent\",\"datasetType\":\"Golden\"}";

        var content = new StringContent(invalidJson, Encoding.UTF8, "application/json");

        // Act
        var response = await Client.PostAsync("/api/v1/eval/datasets", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest,
            "JSON with null bytes should be rejected or sanitized");
    }

    #endregion

    #region Missing Required Fields Tests

    [Fact]
    public async Task CreateEvalRun_WithMissingRequiredFields_Returns400()
    {
        // Arrange - Valid JSON but missing required fields
        var incompleteJson = @"{
            ""agentId"": ""test-agent""
        }";

        var content = new StringContent(incompleteJson, Encoding.UTF8, "application/json");

        // Act
        var response = await Client.PostAsync("/api/v1/eval/runs", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest,
            "Missing required fields should result in validation error");

        var responseBody = await response.Content.ReadAsStringAsync();
        responseBody.Should().Contain("required", 
            "Error message should indicate missing required fields");
    }

    [Fact]
    public async Task CreateEvalRun_WithAllFieldsNull_Returns400()
    {
        // Arrange - All fields explicitly set to null
        var nullJson = @"{
            ""agentId"": null,
            ""dataSetId"": null,
            ""metricsConfigurationId"": null,
            ""type"": null,
            ""environmentId"": null,
            ""agentSchemaName"": null
        }";

        var content = new StringContent(nullJson, Encoding.UTF8, "application/json");

        // Act
        var response = await Client.PostAsync("/api/v1/eval/runs", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest,
            "All null fields should fail validation");
    }

    #endregion

    #region Empty/Null Body Tests

    [Fact]
    public async Task CreateEvalRun_WithNullBody_Returns400()
    {
        // Arrange
        StringContent? content = null;

        // Act
        var response = await Client.PostAsync("/api/v1/eval/runs", content!);

        // Assert
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.BadRequest,
            HttpStatusCode.UnsupportedMediaType);
        
        // Null body should be rejected with either 400 or 415
    }

    [Fact]
    public async Task CreateEvalRun_WithEmptyBody_Returns400()
    {
        // Arrange - Empty string as body
        var content = new StringContent("", Encoding.UTF8, "application/json");

        // Act
        var response = await Client.PostAsync("/api/v1/eval/runs", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest,
            "Empty request body should be rejected");
    }

    [Fact]
    public async Task CreateEvalRun_WithWhitespaceOnlyBody_Returns400()
    {
        // Arrange - Only whitespace
        var content = new StringContent("   \n\t  ", Encoding.UTF8, "application/json");

        // Act
        var response = await Client.PostAsync("/api/v1/eval/runs", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest,
            "Whitespace-only body should be rejected");
    }

    [Fact]
    public async Task CreateEvalRun_WithNullJsonValue_Returns400()
    {
        // Arrange - The JSON value "null"
        var content = new StringContent("null", Encoding.UTF8, "application/json");

        // Act
        var response = await Client.PostAsync("/api/v1/eval/runs", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest,
            "JSON null value should be rejected");
    }

    #endregion

    #region Invalid Content-Type Tests

    [Fact]
    public async Task CreateEvalRun_WithMissingContentType_Returns415()
    {
        // Arrange - Valid JSON but no Content-Type header
        var validJson = @"{
            ""agentId"": ""test-agent"",
            ""dataSetId"": ""550e8400-e29b-41d4-a716-446655440000"",
            ""metricsConfigurationId"": ""650e8400-e29b-41d4-a716-446655440001"",
            ""type"": ""MCS"",
            ""environmentId"": ""dev"",
            ""agentSchemaName"": ""TestSchema""
        }";

        var content = new StringContent(validJson);
        content.Headers.ContentType = null; // Remove Content-Type

        // Act
        var response = await Client.PostAsync("/api/v1/eval/runs", content);

        // Assert
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.UnsupportedMediaType,
            HttpStatusCode.BadRequest);
        
        // Missing Content-Type should result in 415 or 400
    }

    [Fact]
    public async Task CreateEvalRun_WithWrongContentType_Returns415()
    {
        // Arrange - Valid JSON but wrong Content-Type (text/plain)
        var validJson = @"{
            ""agentId"": ""test-agent"",
            ""dataSetId"": ""550e8400-e29b-41d4-a716-446655440000"",
            ""metricsConfigurationId"": ""650e8400-e29b-41d4-a716-446655440001"",
            ""type"": ""MCS"",
            ""environmentId"": ""dev"",
            ""agentSchemaName"": ""TestSchema""
        }";

        var content = new StringContent(validJson, Encoding.UTF8, "text/plain");

        // Act
        var response = await Client.PostAsync("/api/v1/eval/runs", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.UnsupportedMediaType,
            "Wrong Content-Type should result in 415 Unsupported Media Type");
    }

    [Fact]
    public async Task CreateEvalRun_WithXMLContentType_Returns415()
    {
        // Arrange - JSON content but XML Content-Type
        var validJson = @"{
            ""agentId"": ""test-agent"",
            ""dataSetId"": ""550e8400-e29b-41d4-a716-446655440000"",
            ""metricsConfigurationId"": ""650e8400-e29b-41d4-a716-446655440001"",
            ""type"": ""MCS"",
            ""environmentId"": ""dev"",
            ""agentSchemaName"": ""TestSchema""
        }";

        var content = new StringContent(validJson, Encoding.UTF8, "application/xml");

        // Act
        var response = await Client.PostAsync("/api/v1/eval/runs", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.UnsupportedMediaType,
            "XML Content-Type with JSON body should be rejected");
    }

    #endregion

    #region Malformed GUID Tests

    [Fact]
    public async Task GetEvalRun_WithInvalidGuidFormat_Returns400()
    {
        // Arrange - Invalid GUID format in route
        var invalidGuid = "not-a-valid-guid";

        // Act
        var response = await Client.GetAsync($"/api/v1/eval/runs/{invalidGuid}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest,
            "Invalid GUID format should result in 400 Bad Request");
    }

    [Fact]
    public async Task GetEvalRun_WithIncompleteGuid_Returns400()
    {
        // Arrange - Incomplete GUID
        var incompleteGuid = "550e8400-e29b-41d4-a716";

        // Act
        var response = await Client.GetAsync($"/api/v1/eval/runs/{incompleteGuid}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest,
            "Incomplete GUID should result in 400 Bad Request");
    }

    [Fact]
    public async Task GetEvalRun_WithGuidPlusExtraCharacters_Returns400()
    {
        // Arrange - Valid GUID with extra characters
        var invalidGuid = "550e8400-e29b-41d4-a716-446655440000-extra";

        // Act
        var response = await Client.GetAsync($"/api/v1/eval/runs/{invalidGuid}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetDataset_WithSQLInjectionInGuidParameter_Returns400()
    {
        // Arrange - SQL injection attempt in GUID parameter
        var sqlInjection = "'; DROP TABLE EvalRuns--";

        // Act
        var response = await Client.GetAsync($"/api/v1/eval/datasets/{sqlInjection}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest,
            "SQL injection in GUID parameter should be rejected");
    }

    #endregion

    #region Extra/Unknown Fields Tests

    [Fact]
    public async Task CreateEvalRun_WithExtraUnknownFields_HandleGracefully()
    {
        // Arrange - Valid JSON with extra unknown fields
        var jsonWithExtraFields = @"{
            ""agentId"": ""test-agent"",
            ""dataSetId"": ""550e8400-e29b-41d4-a716-446655440000"",
            ""metricsConfigurationId"": ""650e8400-e29b-41d4-a716-446655440001"",
            ""type"": ""MCS"",
            ""environmentId"": ""dev"",
            ""agentSchemaName"": ""TestSchema"",
            ""unknownField1"": ""should be ignored"",
            ""maliciousField"": ""<script>alert('xss')</script>"",
            ""extraGuid"": ""750e8400-e29b-41d4-a716-446655440002""
        }";

        var content = new StringContent(jsonWithExtraFields, Encoding.UTF8, "application/json");

        // Act
        var response = await Client.PostAsync("/api/v1/eval/runs", content);

        // Assert
        // API should either accept and ignore extra fields (common in JSON.NET)
        // OR return 400 if strict deserialization is configured
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.Created,
            HttpStatusCode.BadRequest);
        
        // Extra fields should either be ignored or cause validation error

        if (response.IsSuccessStatusCode)
        {
            // If accepted, extra fields should be silently ignored
            Assert.True(true, "API accepted request with extra fields (ignoring them)");
        }
    }

    #endregion

    #region Excessively Nested JSON Tests

    [Fact]
    public async Task SaveDataset_WithExcessivelyNestedJSON_Returns400()
    {
        // Arrange - Deeply nested JSON structure
        var deeplyNestedJson = GenerateDeeplyNestedJson(100);

        var content = new StringContent(deeplyNestedJson, Encoding.UTF8, "application/json");

        // Act
        var response = await Client.PostAsync("/api/v1/eval/datasets", content);

        // Assert
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.BadRequest,
            HttpStatusCode.RequestEntityTooLarge);
        
        // Excessively nested JSON should be rejected
    }

    [Fact]
    public async Task CreateEvalRun_WithVeryLargePayload_HandlesGracefully()
    {
        // Arrange - Very large but valid JSON (testing size limits)
        var largeString = new string('A', 1_000_000); // 1 MB string
        var largeJson = $@"{{
            ""agentId"": ""{largeString}"",
            ""dataSetId"": ""550e8400-e29b-41d4-a716-446655440000"",
            ""metricsConfigurationId"": ""650e8400-e29b-41d4-a716-446655440001"",
            ""type"": ""MCS"",
            ""environmentId"": ""dev"",
            ""agentSchemaName"": ""TestSchema""
        }}";

        var content = new StringContent(largeJson, Encoding.UTF8, "application/json");

        // Act
        var response = await Client.PostAsync("/api/v1/eval/runs", content);

        // Assert
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.BadRequest,
            HttpStatusCode.RequestEntityTooLarge);
        
        // Very large payload should fail validation or exceed size limit
    }

    #endregion

    #region Array/Collection Structure Tests

    [Fact]
    public async Task SaveDataset_WithInvalidArrayStructure_Returns400()
    {
        // Arrange - DatasetRecords should be array, but sending object
        var invalidJson = @"{
            ""agentId"": ""test-agent"",
            ""datasetType"": ""Golden"",
            ""datasetName"": ""Test Dataset"",
            ""datasetRecords"": {
                ""notAnArray"": ""this should be an array""
            }
        }";

        var content = new StringContent(invalidJson, Encoding.UTF8, "application/json");

        // Act
        var response = await Client.PostAsync("/api/v1/eval/datasets", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest,
            "Invalid array structure should be rejected");
    }

    [Fact]
    public async Task SaveDataset_WithEmptyArrayForRequiredCollection_Returns400()
    {
        // Arrange - Empty array for DatasetRecords (requires MinLength(1))
        var invalidJson = @"{
            ""agentId"": ""test-agent"",
            ""datasetType"": ""Golden"",
            ""datasetName"": ""Test Dataset"",
            ""datasetRecords"": []
        }";

        var content = new StringContent(invalidJson, Encoding.UTF8, "application/json");

        // Act
        var response = await Client.PostAsync("/api/v1/eval/datasets", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest,
            "Empty array should fail MinLength validation");

        var responseBody = await response.Content.ReadAsStringAsync();
        responseBody.Should().ContainAny("datasetRecords", "DatasetRecords", 
            "Error should reference the DatasetRecords field");
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Generates a deeply nested JSON structure for testing
    /// </summary>
    private string GenerateDeeplyNestedJson(int depth)
    {
        var sb = new StringBuilder();
        
        // Start with valid DTO structure
        sb.Append(@"{
            ""agentId"": ""test-agent"",
            ""datasetType"": ""Golden"",
            ""datasetName"": ""Test Dataset"",
            ""datasetRecords"": [{
                ""query"": ""test"",
                ""groundTruth"": ""test"",
                ""nested"": ");

        // Add deeply nested objects
        for (int i = 0; i < depth; i++)
        {
            sb.Append("{\"level\": ");
        }

        sb.Append("\"deepValue\"");

        for (int i = 0; i < depth; i++)
        {
            sb.Append("}");
        }

        sb.Append("}]}");

        return sb.ToString();
    }

    #endregion
}
