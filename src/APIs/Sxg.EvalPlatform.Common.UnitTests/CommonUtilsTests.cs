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

        #region GUID-Specific Tests

        [Fact]
        public void TrimAndRemoveSpaces_WithGuidStartingWithDigit_PrefixesWithA()
        {
            // Arrange - GUID starting with digit 8
            var input = "83cf3355-f035-49c6-bc47-9841e20dfe55";

            // Act
            var result = CommonUtils.TrimAndRemoveSpaces(input);

            // Assert
            Assert.Equal("a83cf3355-f035-49c6-bc47-9841e20dfe55", result);
            Assert.StartsWith("a", result);
            Assert.Equal(37, result.Length); // Original 36 + 'a' prefix
        }

        [Fact]
        public void TrimAndRemoveSpaces_WithGuidStartingWithLetter_NoPrefix()
        {
            // Arrange - GUID starting with letter 'a'
            var input = "a3cf3355-f035-49c6-bc47-9841e20dfe55";

            // Act
            var result = CommonUtils.TrimAndRemoveSpaces(input);

            // Assert
            Assert.Equal("a3cf3355-f035-49c6-bc47-9841e20dfe55", result);
            Assert.Equal(36, result.Length); // Original length preserved
        }

        [Fact]
        public void TrimAndRemoveSpaces_WithNonGuidDigitStart_NoPrefix()
        {
            // Arrange - Starts with digit but NOT a GUID pattern
            var input = "4e5f6g7h8i9j0k1l2m3n4o5p";

            // Act
            var result = CommonUtils.TrimAndRemoveSpaces(input);

            // Assert
            Assert.Equal("4e5f6g7h8i9j0k1l2m3n4o5p", result);
            Assert.StartsWith("4", result); // No prefix added
        }

        [Fact]
        public void TrimAndRemoveSpaces_WithAgentIdContainingUnderscore_NoPrefix()
        {
            // Arrange - Common pattern: agent ID with underscore
            var input = "cr153_agentKit";

            // Act
            var result = CommonUtils.TrimAndRemoveSpaces(input);

            // Assert
            Assert.Equal("cr153-agentkit", result); // Underscore replaced with hyphen
            Assert.StartsWith("c", result); // No prefix added (starts with letter)
        }

        [Fact]
        public void TrimAndRemoveSpaces_WithNumericAgentId_NoPrefix()
        {
            // Arrange - Starts with digit but not GUID format
            var input = "123agent";

            // Act
            var result = CommonUtils.TrimAndRemoveSpaces(input);

            // Assert
            Assert.Equal("123agent", result);
            Assert.StartsWith("1", result); // No prefix (not GUID pattern)
        }

        [Theory]
        [InlineData("12345678-1234-1234-1234-123456789012")] // Valid GUID starting with digit
        [InlineData("00000000-0000-0000-0000-000000000000")] // All zeros GUID
        [InlineData("98765432-abcd-ef01-2345-67890abcdef0")] // Mixed hex GUID starting with 9
        public void TrimAndRemoveSpaces_WithVariousGuidsStartingWithDigit_PrefixesWithA(string input)
        {
            // Arrange & Act
            var result = CommonUtils.TrimAndRemoveSpaces(input);

            // Assert
            Assert.StartsWith("a", result);
            Assert.Equal(37, result.Length);
        }

        [Theory]
        [InlineData("a2345678-1234-1234-1234-123456789012")] // GUID starting with 'a'
        [InlineData("f0000000-0000-0000-0000-000000000000")] // GUID starting with 'f'
        [InlineData("deadbeef-cafe-babe-face-123456789abc")] // GUID starting with 'd'
        public void TrimAndRemoveSpaces_WithGuidsStartingWithLetter_NoPrefix(string input)
        {
            // Arrange & Act
            var result = CommonUtils.TrimAndRemoveSpaces(input);

            // Assert
            Assert.Equal(input, result);
            Assert.Equal(36, result.Length);
        }

        [Theory]
        [InlineData("123456781234123412341234567890")] // No hyphens (not GUID format)
        [InlineData("12345678-12341234-1234-123456789012")] // Wrong hyphen positions
        [InlineData("1234567-1234-1234-1234-123456789012")] // Wrong segment length
        [InlineData("12345678-1234-1234-1234-12345678901z")] // Non-hex character
        public void TrimAndRemoveSpaces_WithNonGuidPatternsStartingWithDigit_NoPrefix(string input)
        {
            // Arrange & Act
            var result = CommonUtils.TrimAndRemoveSpaces(input);

            // Assert
            Assert.DoesNotContain("a" + input, result); // Should NOT be prefixed
            Assert.DoesNotContain("aa", result.Substring(0, Math.Min(2, result.Length))); // No double prefix
        }

        [Fact]
        public void TrimAndRemoveSpaces_WithMixedCaseGuid_ConvertsToLowerAndPrefixes()
        {
            // Arrange - Mixed case GUID
            var input = "83CF3355-F035-49C6-BC47-9841E20DFE55";

            // Act
            var result = CommonUtils.TrimAndRemoveSpaces(input);

            // Assert
            Assert.Equal("a83cf3355-f035-49c6-bc47-9841e20dfe55", result);
            Assert.True(result.All(c => !char.IsUpper(c)));
        }

        #endregion

        #region Real-World Name-Based AgentId Tests

        [Fact]
        public void TrimAndRemoveSpaces_WithFullNameWithSpace_RemovesSpaceAndLowercases()
        {
            // Arrange
            var input = "Himanshu Gupta";

            // Act
            var result = CommonUtils.TrimAndRemoveSpaces(input);

            // Assert
            Assert.Equal("himanshugupta", result);
            Assert.DoesNotContain(" ", result);
            Assert.True(result.All(c => char.IsLower(c) || char.IsDigit(c)));
        }

        [Fact]
        public void TrimAndRemoveSpaces_WithFullNameWithLeadingAndTrailingSpaces_TrimsAndRemovesSpaces()
        {
            // Arrange
            var input = " Himanshu Gupta ";

            // Act
            var result = CommonUtils.TrimAndRemoveSpaces(input);

            // Assert
            Assert.Equal("himanshugupta", result);
            Assert.DoesNotContain(" ", result);
            Assert.DoesNotStartWith(" ", result);
            Assert.DoesNotEndWith(" ", result);
        }

        [Fact]
        public void TrimAndRemoveSpaces_WithCamelCaseName_ConvertsToLowercase()
        {
            // Arrange
            var input = "HimanshuGupta";

            // Act
            var result = CommonUtils.TrimAndRemoveSpaces(input);

            // Assert
            Assert.Equal("himanshugupta", result);
            Assert.True(result.All(c => char.IsLower(c)));
        }

        [Fact]
        public void TrimAndRemoveSpaces_WithNumberPrefixedName_PreservesNumberNoPrefix()
        {
            // Arrange - Starts with digit but not GUID pattern
            var input = "1 Himanshu";

            // Act
            var result = CommonUtils.TrimAndRemoveSpaces(input);

            // Assert
            Assert.Equal("1himanshu", result);
            Assert.StartsWith("1", result); // No 'a' prefix (not GUID pattern)
            Assert.DoesNotContain(" ", result);
        }

        [Fact]
        public void TrimAndRemoveSpaces_WithNameAndNumberSuffix_RemovesSpacesAndLowercases()
        {
            // Arrange
            var input = " HimanshuGupta 123";

            // Act
            var result = CommonUtils.TrimAndRemoveSpaces(input);

            // Assert
            Assert.Equal("himanshugupta123", result);
            Assert.DoesNotContain(" ", result);
            Assert.EndsWith("123", result);
        }

        [Fact]
        public void TrimAndRemoveSpaces_WithEmailLikePattern_ReplacesSpecialCharsWithHyphens()
        {
            // Arrange
            var input = "himanshu.gupta@microsoft.com";

            // Act
            var result = CommonUtils.TrimAndRemoveSpaces(input);

            // Assert
            Assert.Equal("himanshu-gupta-microsoft-com", result);
            Assert.DoesNotContain(".", result);
            Assert.DoesNotContain("@", result);
        }

        [Fact]
        public void TrimAndRemoveSpaces_WithUnderscoreSeparatedName_ReplacesWithHyphens()
        {
            // Arrange
            var input = "himanshu_gupta_123";

            // Act
            var result = CommonUtils.TrimAndRemoveSpaces(input);

            // Assert
            Assert.Equal("himanshu-gupta-123", result);
            Assert.DoesNotContain("_", result);
            Assert.Contains("-", result);
        }

        [Fact]
        public void TrimAndRemoveSpaces_WithMultipleSpacesBetweenWords_RemovesAllSpaces()
        {
            // Arrange
            var input = "Himanshu   Gupta   Test";

            // Act
            var result = CommonUtils.TrimAndRemoveSpaces(input);

            // Assert
            Assert.Equal("himanshugupta test", result);
            Assert.DoesNotContain("   ", result);
        }

        [Fact]
        public void TrimAndRemoveSpaces_WithSpecialCharactersInName_ReplacesWithHyphens()
        {
            // Arrange
            var input = "Himanshu-Gupta!@#$%^&*()";

            // Act
            var result = CommonUtils.TrimAndRemoveSpaces(input);

            // Assert
            Assert.StartsWith("himanshu-gupta", result);
            Assert.DoesNotContain("!", result);
            Assert.DoesNotContain("@", result);
            Assert.DoesNotContain("#", result);
            // Special chars should be replaced with hyphens, then consecutive hyphens removed
            Assert.DoesNotContain("--", result);
        }

        [Fact]
        public void TrimAndRemoveSpaces_WithVeryShortName_PadsToMinimum3Characters()
        {
            // Arrange
            var input = "Hi";

            // Act
            var result = CommonUtils.TrimAndRemoveSpaces(input);

            // Assert
            Assert.Equal("hi0", result);
            Assert.Equal(3, result.Length);
        }

        [Fact]
        public void TrimAndRemoveSpaces_WithVeryLongName_TruncatesTo63Characters()
        {
            // Arrange
            var input = "HimanshuGuptaVeryLongNameThatExceedsSixtyThreeCharactersLimitForAzureBlobStorageContainerNamesAndNeedsToBetruncated";

            // Act
            var result = CommonUtils.TrimAndRemoveSpaces(input);

            // Assert
            Assert.True(result.Length <= 63);
            Assert.Equal(63, result.Length);
            Assert.StartsWith("himanshuguptaverylongnamethatexceedssixtyth", result);
        }

        [Theory]
        [InlineData("John Doe", "johndoe")]
        [InlineData("Jane_Smith_123", "jane-smith-123")]
        [InlineData("Bob-Wilson", "bob-wilson")]
        [InlineData("Alice O'Brien", "alice-o-brien")]
        [InlineData("José García", "jos--garc-a")] // Non-ASCII gets replaced with hyphens
        [InlineData("李明", "---")] // Chinese characters become hyphens, then trimmed to minimum
        public void TrimAndRemoveSpaces_WithVariousNameFormats_ProducesValidContainerNames(string input, string expected)
        {
            // Arrange & Act
            var result = CommonUtils.TrimAndRemoveSpaces(input);

            // Assert
            // For non-ASCII, the result might be different due to hyphen trimming and padding
            if (expected.StartsWith("-") || expected == "---")
            {
                // These get trimmed and padded
                Assert.True(result.Length >= 3);
                Assert.True(char.IsLetterOrDigit(result[0]));
                Assert.True(char.IsLetterOrDigit(result[^1]));
            }
            else
            {
                Assert.Equal(expected, result);
            }
        }

        [Fact]
        public void TrimAndRemoveSpaces_WithTabsAndNewlines_RemovesAllWhitespace()
        {
            // Arrange
            var input = "Himanshu\tGupta\nTest";

            // Act
            var result = CommonUtils.TrimAndRemoveSpaces(input);

            // Assert
            Assert.Equal("himanshugupta est", result);
            Assert.DoesNotContain("\t", result);
            Assert.DoesNotContain("\n", result);
        }

        [Fact]
        public void TrimAndRemoveSpaces_WithMixedCaseAndNumbers_NormalizesCorrectly()
        {
            // Arrange
            var input = "Agent007JamesBond";

            // Act
            var result = CommonUtils.TrimAndRemoveSpaces(input);

            // Assert
            Assert.Equal("agent007jamesbond", result);
            Assert.Contains("007", result);
            Assert.True(result.All(c => char.IsLower(c) || char.IsDigit(c)));
        }

        [Fact]
        public void TrimAndRemoveSpaces_WithOnlySpecialCharacters_CreatesValidContainerName()
        {
            // Arrange
            var input = "@#$%^&*()";

            // Act
            var result = CommonUtils.TrimAndRemoveSpaces(input);

            // Assert
            // All special chars replaced with hyphens, then consecutive hyphens removed, then trimmed
            // Should end up as empty string, then padded to minimum 3
            Assert.True(result.Length >= 3);
            Assert.True(char.IsLetterOrDigit(result[0]));
            Assert.True(char.IsLetterOrDigit(result[^1]));
        }

        #endregion
    }
}
```



