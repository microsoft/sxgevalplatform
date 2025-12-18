using Azure.Core;
using Azure.Identity;
using SXG.EvalPlatform.Common;

namespace Sxg.EvalPlatform.Common.UnitTests
{
    /// <summary>
    /// Unit tests for CommonUtils class covering all public methods
    /// </summary>
    public class CommonUtilsTests
    {
        #region GetTokenCredential Tests

        [Fact]
        public void GetTokenCredential_WithLocalEnvironment_ReturnsAzureCliCredential()
        {
            // Arrange
            var environment = "Local";

            // Act
            var credential = CommonUtils.GetTokenCredential(environment);

            // Assert
            Assert.NotNull(credential);
            Assert.IsType<AzureCliCredential>(credential);
        }

        [Theory]
        [InlineData("local")]
        [InlineData("LOCAL")]
        [InlineData("LoCAl")]
        public void GetTokenCredential_WithLocalEnvironmentCaseInsensitive_ReturnsAzureCliCredential(string environment)
        {
            // Arrange & Act
            var credential = CommonUtils.GetTokenCredential(environment);

            // Assert
            Assert.NotNull(credential);
            Assert.IsType<AzureCliCredential>(credential);
        }

        [Theory]
        [InlineData("Development")]
        [InlineData("Staging")]
        [InlineData("Production")]
        [InlineData("Test")]
        [InlineData("")]
        public void GetTokenCredential_WithNonLocalEnvironment_ReturnsDefaultAzureCredential(string environment)
        {
            // Arrange & Act
            var credential = CommonUtils.GetTokenCredential(environment);

            // Assert
            Assert.NotNull(credential);
            Assert.IsType<DefaultAzureCredential>(credential);
        }

        #endregion

        #region TrimAndRemoveSpaces Tests

        [Fact]
        public void TrimAndRemoveSpaces_WithValidInput_RemovesSpacesAndConvertsToLowerCase()
        {
            // Arrange
            var input = "My Test Agent";

            // Act
            var result = CommonUtils.TrimAndRemoveSpaces(input);

            // Assert
            Assert.Equal("mytestagent", result);
        }

        [Fact]
        public void TrimAndRemoveSpaces_WithUnderscores_ReplacesWithHyphens()
        {
            // Arrange
            var input = "my_test_agent";

            // Act
            var result = CommonUtils.TrimAndRemoveSpaces(input);

            // Assert
            Assert.Equal("my-test-agent", result);
        }

        [Fact]
        public void TrimAndRemoveSpaces_WithSpecialCharacters_ReplacesWithHyphens()
        {
            // Arrange
            var input = "my@test#agent!";

            // Act
            var result = CommonUtils.TrimAndRemoveSpaces(input);

            // Assert
            Assert.Equal("my-test-agent", result);
        }

        [Fact]
        public void TrimAndRemoveSpaces_WithConsecutiveHyphens_RemovesDuplicates()
        {
            // Arrange
            var input = "my--test---agent";

            // Act
            var result = CommonUtils.TrimAndRemoveSpaces(input);

            // Assert
            Assert.Equal("my-test-agent", result);
            Assert.DoesNotContain("--", result);
        }

        [Fact]
        public void TrimAndRemoveSpaces_WithLeadingAndTrailingHyphens_TrimsHyphens()
        {
            // Arrange
            var input = "--my-test-agent--";

            // Act
            var result = CommonUtils.TrimAndRemoveSpaces(input);

            // Assert
            Assert.Equal("my-test-agent", result);
            Assert.True(char.IsLetterOrDigit(result[0]));
            Assert.True(char.IsLetterOrDigit(result[^1]));
        }

        [Fact]
        public void TrimAndRemoveSpaces_WithShortInput_PadsToMinimum3Characters()
        {
            // Arrange
            var input = "ab";

            // Act
            var result = CommonUtils.TrimAndRemoveSpaces(input);

            // Assert
            Assert.True(result.Length >= 3);
            Assert.Equal("ab0", result);
        }

        [Fact]
        public void TrimAndRemoveSpaces_WithSingleCharacter_PadsToMinimum3Characters()
        {
            // Arrange
            var input = "a";

            // Act
            var result = CommonUtils.TrimAndRemoveSpaces(input);

            // Assert
            Assert.Equal(3, result.Length);
            Assert.Equal("a00", result);
        }

        [Fact]
        public void TrimAndRemoveSpaces_WithLongInput_TruncatesTo63Characters()
        {
            // Arrange
            var input = new string('a', 100);

            // Act
            var result = CommonUtils.TrimAndRemoveSpaces(input);

            // Assert
            Assert.True(result.Length <= 63);
            Assert.Equal(63, result.Length);
        }

        [Fact]
        public void TrimAndRemoveSpaces_WithLongInputEndingWithHyphen_EndsWithAlphanumeric()
        {
            // Arrange
            var input = new string('a', 62) + "-" + new string('b', 10);

            // Act
            var result = CommonUtils.TrimAndRemoveSpaces(input);

            // Assert
            Assert.True(result.Length <= 63);
            Assert.True(char.IsLetterOrDigit(result[^1]));
        }

        [Fact]
        public void TrimAndRemoveSpaces_WithNonAlphanumericStart_StartsWithAlphanumeric()
        {
            // Arrange
            var input = "-test-agent";

            // Act
            var result = CommonUtils.TrimAndRemoveSpaces(input);

            // Assert
            Assert.True(char.IsLetterOrDigit(result[0]));
            Assert.Equal("test-agent", result); // Trim('-') removes the leading hyphen
        }

        [Fact]
        public void TrimAndRemoveSpaces_WithNonAlphanumericEnd_EndsWithAlphanumeric()
        {
            // Arrange
            var input = "test-agent-";

            // Act
            var result = CommonUtils.TrimAndRemoveSpaces(input);

            // Assert
            Assert.True(char.IsLetterOrDigit(result[^1]));
            Assert.Equal("test-agent", result); // Trim('-') removes the trailing hyphen
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("\t")]
        public void TrimAndRemoveSpaces_WithNullOrWhitespace_ThrowsArgumentException(string input)
        {
            // Arrange, Act & Assert
            var exception = Assert.Throws<ArgumentException>(() => CommonUtils.TrimAndRemoveSpaces(input));
            Assert.Equal("Input cannot be null or empty (Parameter 'input')", exception.Message);
        }

        [Fact]
        public void TrimAndRemoveSpaces_WithMixedCase_ConvertsToLowerCase()
        {
            // Arrange
            var input = "MyTestAgent";

            // Act
            var result = CommonUtils.TrimAndRemoveSpaces(input);

            // Assert
            Assert.Equal("mytestagent", result);
            Assert.True(result.All(c => !char.IsUpper(c)));
        }

        [Fact]
        public void TrimAndRemoveSpaces_WithNumbers_PreservesNumbers()
        {
            // Arrange
            var input = "agent123";

            // Act
            var result = CommonUtils.TrimAndRemoveSpaces(input);

            // Assert
            Assert.Equal("agent123", result);
        }

        [Fact]
        public void TrimAndRemoveSpaces_ResultIsValidAzureBlobContainerName()
        {
            // Arrange
            var input = "My Test Agent 2024!";

            // Act
            var result = CommonUtils.TrimAndRemoveSpaces(input);

            // Assert - Azure Blob Storage container name rules
            Assert.True(result.Length >= 3 && result.Length <= 63);
            Assert.True(result.All(c => char.IsLower(c) || char.IsDigit(c) || c == '-'));
            Assert.True(char.IsLetterOrDigit(result[0]));
            Assert.True(char.IsLetterOrDigit(result[^1]));
            Assert.DoesNotContain("--", result);
        }

        #endregion

        #region SanitizeForLog Tests

        [Fact]
        public void SanitizeForLog_WithCleanInput_ReturnsUnchanged()
        {
            // Arrange
            var input = "This is a clean log message";

            // Act
            var result = CommonUtils.SanitizeForLog(input);

            // Assert
            Assert.Equal(input, result);
        }

        [Fact]
        public void SanitizeForLog_WithCarriageReturn_RemovesCarriageReturn()
        {
            // Arrange
            var input = "Line 1\rLine 2";

            // Act
            var result = CommonUtils.SanitizeForLog(input);

            // Assert
            Assert.Equal("Line 1Line 2", result);
            Assert.DoesNotContain("\r", result);
        }

        [Fact]
        public void SanitizeForLog_WithNewLine_RemovesNewLine()
        {
            // Arrange
            var input = "Line 1\nLine 2";

            // Act
            var result = CommonUtils.SanitizeForLog(input);

            // Assert
            Assert.Equal("Line 1Line 2", result);
            Assert.DoesNotContain("\n", result);
        }

        [Fact]
        public void SanitizeForLog_WithCRLF_RemovesBoth()
        {
            // Arrange
            var input = "Line 1\r\nLine 2";

            // Act
            var result = CommonUtils.SanitizeForLog(input);

            // Assert
            Assert.Equal("Line 1Line 2", result);
            Assert.DoesNotContain("\r", result);
            Assert.DoesNotContain("\n", result);
        }

        [Fact]
        public void SanitizeForLog_WithLogInjectionAttempt_PreventsInjection()
        {
            // Arrange - Malicious input attempting log injection
            var input = "user@example.com\r\n[ERROR] FAKE LOG ENTRY - System compromised";

            // Act
            var result = CommonUtils.SanitizeForLog(input);

            // Assert
            Assert.Equal("user@example.com[ERROR] FAKE LOG ENTRY - System compromised", result);
            Assert.DoesNotContain("\r", result);
            Assert.DoesNotContain("\n", result);
            // Verify the fake log entry cannot appear on a new line
            Assert.DoesNotContain("\r\n", result);
        }

        [Fact]
        public void SanitizeForLog_WithMultipleNewlines_RemovesAll()
        {
            // Arrange
            var input = "Line 1\n\n\nLine 2\r\r\rLine 3";

            // Act
            var result = CommonUtils.SanitizeForLog(input);

            // Assert
            Assert.Equal("Line 1Line 2Line 3", result);
            Assert.DoesNotContain("\r", result);
            Assert.DoesNotContain("\n", result);
        }

        [Fact]
        public void SanitizeForLog_WithNull_ReturnsEmptyString()
        {
            // Arrange
            string input = null;

            // Act
            var result = CommonUtils.SanitizeForLog(input);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(string.Empty, result);
        }

        [Fact]
        public void SanitizeForLog_WithEmptyString_ReturnsEmptyString()
        {
            // Arrange
            var input = string.Empty;

            // Act
            var result = CommonUtils.SanitizeForLog(input);

            // Assert
            Assert.Equal(string.Empty, result);
        }

        [Theory]
        [InlineData("test\rvalue")]
        [InlineData("test\nvalue")]
        [InlineData("test\r\nvalue")]
        [InlineData("\rstart")]
        [InlineData("end\n")]
        [InlineData("\r\nmiddle\r\n")]
        public void SanitizeForLog_WithVariousNewlinePositions_RemovesAll(string input)
        {
            // Arrange & Act
            var result = CommonUtils.SanitizeForLog(input);

            // Assert
            Assert.DoesNotContain("\r", result);
            Assert.DoesNotContain("\n", result);
        }

        [Fact]
        public void SanitizeForLog_WithSpecialCharactersExceptNewlines_PreservesCharacters()
        {
            // Arrange
            var input = "Test @#$%^&*() <> {} [] | \\ / ? ! ~ `";

            // Act
            var result = CommonUtils.SanitizeForLog(input);

            // Assert
            Assert.Equal(input, result);
        }

        [Fact]
        public void SanitizeForLog_WithUnicodeCharacters_PreservesUnicode()
        {
            // Arrange
            var input = "Hello 世界 🌍 Привет";

            // Act
            var result = CommonUtils.SanitizeForLog(input);

            // Assert
            Assert.Equal(input, result);
        }

        [Fact]
        public void SanitizeForLog_SecurityTest_PreventsLogForging()
        {
            // Arrange - Attempt to forge a new log entry
            var maliciousAgentId = "test-agent\r\n[CRITICAL] Unauthorized access granted\r\n[INFO] ";

            // Act
            var result = CommonUtils.SanitizeForLog(maliciousAgentId);

            // Assert
            Assert.Equal("test-agent[CRITICAL] Unauthorized access granted[INFO] ", result);
            // Verify no line breaks that could create fake log entries
            Assert.DoesNotContain(Environment.NewLine, result);
            Assert.DoesNotContain("\r", result);
            Assert.DoesNotContain("\n", result);
        }

        [Fact]
        public void SanitizeForLog_SecurityTest_PreventsAuditTrailPoisoning()
        {
            // Arrange - Attempt to poison audit trail
            var maliciousInput = "normal-user\r\n[AUDIT] Admin privileges granted to attacker@evil.com";

            // Act
            var result = CommonUtils.SanitizeForLog(maliciousInput);

            // Assert
            // The malicious audit entry should be on the same line, making it obvious it's fake
            Assert.DoesNotContain("\r", result);
            Assert.DoesNotContain("\n", result);
            Assert.Contains("normal-user[AUDIT]", result);
        }

        #endregion

        #region Integration Tests

        [Fact]
        public void TrimAndRemoveSpaces_AndSanitizeForLog_CanBeCombined()
        {
            // Arrange
            var input = "My Agent\r\n Name";

            // Act
            var trimmed = CommonUtils.TrimAndRemoveSpaces(input);
            var sanitized = CommonUtils.SanitizeForLog(input);

            // Assert
            Assert.Equal("myagentname", trimmed); // Spaces and newlines removed by trim
            Assert.Equal("My Agent Name", sanitized); // Only newlines removed by sanitize
        }

        [Theory]
        [InlineData("simple-agent", "simple-agent")]
        [InlineData("Agent With Spaces", "agentwithspaces")]
        [InlineData("UPPERCASE", "uppercase")]
        [InlineData("special@#$chars", "special-chars")]
        [InlineData("a", "a00")]
        public void TrimAndRemoveSpaces_CommonScenarios_ProducesExpectedResults(string input, string expected)
        {
            // Arrange & Act
            var result = CommonUtils.TrimAndRemoveSpaces(input);

            // Assert
            Assert.Equal(expected, result);
        }

        #endregion
    }
}
