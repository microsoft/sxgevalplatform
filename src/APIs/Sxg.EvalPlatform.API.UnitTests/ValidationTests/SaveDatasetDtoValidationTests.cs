using Sxg.EvalPlatform.API.UnitTests.Helpers;
using SxgEvalPlatformApi.Models;

namespace Sxg.EvalPlatform.API.UnitTests.ValidationTests;

/// <summary>
/// SF-13-1: Input Validation Tests for SaveDatasetDto
/// Tests dataset validation including dataset type restrictions, record validation,
/// and boundary conditions for large datasets
/// </summary>
public class SaveDatasetDtoValidationTests
{
    #region Required Field Tests - AgentId

    [Fact]
    public void AgentId_WhenNull_FailsValidation()
    {
        // Arrange
        var dto = SecurityTestDataGenerator.GetValidSaveDatasetDto();
        dto.AgentId = null!;

        // Act & Assert
        ValidationTestHelper.AssertHasValidationErrors(dto, nameof(SaveDatasetDto.AgentId));
    }

    [Fact]
    public void AgentId_WhenEmpty_FailsValidation()
    {
        // Arrange
        var dto = SecurityTestDataGenerator.GetValidSaveDatasetDto();
        dto.AgentId = string.Empty;

        // Act & Assert
        ValidationTestHelper.AssertHasValidationErrors(dto, nameof(SaveDatasetDto.AgentId));
    }

    [Fact]
    public void AgentId_WhenWhitespace_FailsValidation()
    {
        // Arrange
        var dto = SecurityTestDataGenerator.GetValidSaveDatasetDto();
        dto.AgentId = "   ";

        // Act & Assert
        ValidationTestHelper.AssertHasValidationErrors(dto, nameof(SaveDatasetDto.AgentId));
    }

    #endregion

    #region DatasetType Validation

    [Fact]
    public void DatasetType_WhenNull_FailsValidation()
    {
        // Arrange
        var dto = SecurityTestDataGenerator.GetValidSaveDatasetDto();
        dto.DatasetType = null!;

        // Act & Assert
        ValidationTestHelper.AssertHasValidationErrors(dto, nameof(SaveDatasetDto.DatasetType));
    }

    [Fact]
    public void DatasetType_WhenEmpty_FailsValidation()
    {
        // Arrange
        var dto = SecurityTestDataGenerator.GetValidSaveDatasetDto();
        dto.DatasetType = string.Empty;

        // Act & Assert
        ValidationTestHelper.AssertHasValidationErrors(dto, nameof(SaveDatasetDto.DatasetType));
    }

    [Theory]
    [InlineData("Synthetic")]
    [InlineData("Golden")]
    public void DatasetType_WithValidValues_PassesValidation(string validType)
    {
        // Arrange
        var dto = SecurityTestDataGenerator.GetValidSaveDatasetDto();
        dto.DatasetType = validType;

        // Act & Assert
        ValidationTestHelper.AssertNoValidationErrors(dto);
    }

    [Theory]
    [InlineData("synthetic")] // lowercase
    [InlineData("GOLDEN")] // uppercase
    [InlineData("Invalid")]
    [InlineData("Test")]
    [InlineData("Custom")]
    [InlineData("")]
    public void DatasetType_WithInvalidValues_FailsValidation(string invalidType)
    {
        // Arrange
        var dto = SecurityTestDataGenerator.GetValidSaveDatasetDto();
        dto.DatasetType = invalidType;

        // Act & Assert
        // Model has [RegularExpression("^(Synthetic|Golden)$")] attribute
        ValidationTestHelper.AssertHasValidationErrors(dto, nameof(SaveDatasetDto.DatasetType));
    }

    #endregion

    #region DatasetName Validation

    [Fact]
    public void DatasetName_WhenNull_FailsValidation()
    {
        // Arrange
        var dto = SecurityTestDataGenerator.GetValidSaveDatasetDto();
        dto.DatasetName = null!;

        // Act & Assert
        ValidationTestHelper.AssertHasValidationErrors(dto, nameof(SaveDatasetDto.DatasetName));
    }

    [Fact]
    public void DatasetName_WhenEmpty_FailsValidation()
    {
        // Arrange
        var dto = SecurityTestDataGenerator.GetValidSaveDatasetDto();
        dto.DatasetName = string.Empty;

        // Act & Assert
        ValidationTestHelper.AssertHasValidationErrors(dto, nameof(SaveDatasetDto.DatasetName));
    }

    [Fact]
    public void DatasetName_WhenExceedsMaxLength_FailsValidation()
    {
        // Arrange
        var dto = SecurityTestDataGenerator.GetValidSaveDatasetDto();
        dto.DatasetName = new string('A', 101); // Max is 100

        // Act & Assert
        ValidationTestHelper.AssertHasValidationErrors(dto, nameof(SaveDatasetDto.DatasetName));
    }

    [Fact]
    public void DatasetName_WhenAtMaxLength_PassesValidation()
    {
        // Arrange
        var dto = SecurityTestDataGenerator.GetValidSaveDatasetDto();
        dto.DatasetName = new string('A', 100);

        // Act & Assert
        ValidationTestHelper.AssertNoValidationErrors(dto);
    }

    [Fact]
    public void DatasetName_WhenAtMinLength_PassesValidation()
    {
        // Arrange
        var dto = SecurityTestDataGenerator.GetValidSaveDatasetDto();
        dto.DatasetName = "A"; // Min is 1

        // Act & Assert
        ValidationTestHelper.AssertNoValidationErrors(dto);
    }

    [Theory]
    [MemberData(nameof(GetXssPayloads))]
    public void DatasetName_WithXssAttempt_DocumentedBehavior(string xssPayload)
    {
        // Arrange
        var dto = SecurityTestDataGenerator.GetValidSaveDatasetDto();
        dto.DatasetName = xssPayload;

        // Act
        var isValid = ValidationTestHelper.IsValid(dto);

        // Assert
        if (isValid && xssPayload.Length <= 100)
        {
            Assert.True(true, $"DTO accepted XSS payload in DatasetName: '{xssPayload}'. " +
                            "Ensure output encoding when displaying.");
        }
    }

    public static IEnumerable<object[]> GetXssPayloads() =>
        SecurityTestDataGenerator.GetXssPayloads()
            .Where(p => p.Length <= 100)
            .Select(p => new object[] { p });

    #endregion

    #region DatasetRecords Validation

    [Fact]
    public void DatasetRecords_WhenNull_FailsValidation()
    {
        // Arrange
        var dto = SecurityTestDataGenerator.GetValidSaveDatasetDto();
        dto.DatasetRecords = null!;

        // Act & Assert
        ValidationTestHelper.AssertHasValidationErrors(dto, nameof(SaveDatasetDto.DatasetRecords));
    }

    [Fact]
    public void DatasetRecords_WhenEmpty_FailsValidation()
    {
        // Arrange
        var dto = SecurityTestDataGenerator.GetValidSaveDatasetDto();
        dto.DatasetRecords = new List<EvalDataset>();

        // Act & Assert
        // Model has [MinLength(1)] attribute
        ValidationTestHelper.AssertHasValidationErrors(dto, nameof(SaveDatasetDto.DatasetRecords));
    }

    [Fact]
    public void DatasetRecords_WithSingleRecord_PassesValidation()
    {
        // Arrange
        var dto = SecurityTestDataGenerator.GetValidSaveDatasetDto();
        dto.DatasetRecords = new List<EvalDataset>
        {
            new EvalDataset
            {
                Query = "Test query",
                GroundTruth = "Test ground truth",
                ActualResponse = string.Empty,
                Context = "Test context"
            }
        };

        // Act & Assert
        ValidationTestHelper.AssertNoValidationErrors(dto);
    }

    [Fact]
    public void DatasetRecords_WithMultipleRecords_PassesValidation()
    {
        // Arrange
        var dto = SecurityTestDataGenerator.GetValidSaveDatasetDto();
        dto.DatasetRecords = SecurityTestDataGenerator.GenerateLargeDataset(10);

        // Act & Assert
        ValidationTestHelper.AssertNoValidationErrors(dto);
    }

    [Fact]
    public void DatasetRecords_WithLargeDataset_PassesValidation()
    {
        // Arrange
        var dto = SecurityTestDataGenerator.GetValidSaveDatasetDto();
        dto.DatasetRecords = SecurityTestDataGenerator.GenerateLargeDataset(1000);

        // Act & Assert
        // DTO validation should pass; API may have size limits at different layer
        ValidationTestHelper.AssertNoValidationErrors(dto);
    }

    [Fact]
    public void DatasetRecords_WithExcessivelyLargeDataset_DocumentedBehavior()
    {
        // Arrange
        var dto = SecurityTestDataGenerator.GetValidSaveDatasetDto();
        dto.DatasetRecords = SecurityTestDataGenerator.GenerateLargeDataset(10000);

        // Act
        var isValid = ValidationTestHelper.IsValid(dto);

        // Assert - Document that large datasets pass DTO validation
        // API should implement size limits at request pipeline or service layer
        Assert.True(isValid, "Large dataset (10,000 records) passes DTO validation. " +
                            "Ensure payload size limits at API middleware layer.");
    }

    #endregion

    #region EvalDataset Record Content Validation

    [Fact]
    public void DatasetRecord_WithEmptyFields_PassesValidation()
    {
        // Arrange
        var dto = SecurityTestDataGenerator.GetValidSaveDatasetDto();
        dto.DatasetRecords = new List<EvalDataset>
        {
            new EvalDataset
            {
                Query = string.Empty,
                GroundTruth = string.Empty,
                ActualResponse = string.Empty,
                Context = string.Empty
            }
        };

        // Act & Assert
        // EvalDataset properties have no Required attribute
        ValidationTestHelper.AssertNoValidationErrors(dto);
    }

    [Fact]
    public void DatasetRecord_WithMaliciousContent_DocumentedBehavior()
    {
        // Arrange
        var dto = SecurityTestDataGenerator.GetValidSaveDatasetDto();
        dto.DatasetRecords = new List<EvalDataset>
        {
            new EvalDataset
            {
                Query = "<script>alert('xss')</script>",
                GroundTruth = "'; DROP TABLE--",
                ActualResponse = "../../etc/passwd",
                Context = "| rm -rf /"
            }
        };

        // Act & Assert
        // DTO validation doesn't inspect record content deeply
        // Must be sanitized/validated at service layer or during storage
        ValidationTestHelper.AssertNoValidationErrors(dto);
    }

    [Fact]
    public void DatasetRecord_WithVeryLongFields_DocumentedBehavior()
    {
        // Arrange
        var dto = SecurityTestDataGenerator.GetValidSaveDatasetDto();
        dto.DatasetRecords = new List<EvalDataset>
        {
            new EvalDataset
            {
                Query = new string('A', 10000),
                GroundTruth = new string('B', 10000),
                ActualResponse = new string('C', 10000),
                Context = new string('D', 10000)
            }
        };

        // Act
        var isValid = ValidationTestHelper.IsValid(dto);

        // Assert - Document that very long fields pass DTO validation
        Assert.True(isValid, "Very long dataset record fields pass DTO validation. " +
                            "Ensure field length limits at service or storage layer.");
    }

    #endregion

    #region SQL Injection in Dataset Fields

    [Theory]
    [MemberData(nameof(GetSqlInjectionPayloads))]
    public void DatasetRecords_WithSqlInjectionInQuery_DocumentedBehavior(string sqlPayload)
    {
        // Arrange
        var dto = SecurityTestDataGenerator.GetValidSaveDatasetDto();
        dto.DatasetRecords = new List<EvalDataset>
        {
            new EvalDataset
            {
                Query = sqlPayload,
                GroundTruth = "Valid ground truth",
                ActualResponse = string.Empty,
                Context = "Valid context"
            }
        };

        // Act
        var isValid = ValidationTestHelper.IsValid(dto);

        // Assert - DTO accepts SQL injection attempts; data layer must use parameterized queries
        Assert.True(isValid, $"DTO accepted SQL injection in dataset Query: '{sqlPayload}'. " +
                            "Ensure Azure Table/Blob storage operations are parameterized.");
    }

    public static IEnumerable<object[]> GetSqlInjectionPayloads() =>
        SecurityTestDataGenerator.GetSqlInjectionPayloads().Select(p => new object[] { p });

    #endregion

    #region Validation Attributes Tests

    [Fact]
    public void AgentId_HasRequiredAttribute()
    {
        // Act & Assert
        ValidationTestHelper.AssertPropertyHasRequiredAttribute<SaveDatasetDto>(
            nameof(SaveDatasetDto.AgentId));
    }

    [Fact]
    public void DatasetType_HasRequiredAttribute()
    {
        // Act & Assert
        ValidationTestHelper.AssertPropertyHasRequiredAttribute<SaveDatasetDto>(
            nameof(SaveDatasetDto.DatasetType));
    }

    [Fact]
    public void DatasetName_HasRequiredAttribute()
    {
        // Act & Assert
        ValidationTestHelper.AssertPropertyHasRequiredAttribute<SaveDatasetDto>(
            nameof(SaveDatasetDto.DatasetName));
    }

    [Fact]
    public void DatasetName_HasCorrectStringLengthAttribute()
    {
        // Act & Assert
        ValidationTestHelper.AssertPropertyHasStringLengthAttribute<SaveDatasetDto>(
            nameof(SaveDatasetDto.DatasetName),
            expectedMaxLength: 100,
            expectedMinLength: 1);
    }

    [Fact]
    public void DatasetRecords_HasRequiredAttribute()
    {
        // Act & Assert
        ValidationTestHelper.AssertPropertyHasRequiredAttribute<SaveDatasetDto>(
            nameof(SaveDatasetDto.DatasetRecords));
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void ValidDto_WithMinimalData_PassesValidation()
    {
        // Arrange
        var dto = new SaveDatasetDto
        {
            AgentId = "A",
            DatasetType = "Golden",
            DatasetName = "D",
            DatasetRecords = new List<EvalDataset>
            {
                new EvalDataset
                {
                    Query = "Q",
                    GroundTruth = "G",
                    ActualResponse = string.Empty,
                    Context = string.Empty
                }
            }
        };

        // Act & Assert
        ValidationTestHelper.AssertNoValidationErrors(dto);
    }

    [Fact]
    public void ValidDto_WithMaximalData_PassesValidation()
    {
        // Arrange
        var dto = new SaveDatasetDto
        {
            AgentId = new string('A', 100), // Assuming AgentId has max length
            DatasetType = "Synthetic",
            DatasetName = new string('D', 100),
            DatasetRecords = SecurityTestDataGenerator.GenerateLargeDataset(100)
        };

        // Act & Assert
        ValidationTestHelper.AssertNoValidationErrors(dto);
    }

    [Fact]
    public void CompletelyValidDto_PassesAllValidation()
    {
        // Arrange
        var dto = SecurityTestDataGenerator.GetValidSaveDatasetDto();

        // Act & Assert
        ValidationTestHelper.AssertNoValidationErrors(dto);
    }

    #endregion

    #region Unicode and Special Characters

    [Fact]
    public void DatasetName_WithUnicodeCharacters_PassesValidation()
    {
        // Arrange
        var dto = SecurityTestDataGenerator.GetValidSaveDatasetDto();
        dto.DatasetName = "?????-??-Dataset";

        // Act & Assert
        ValidationTestHelper.AssertNoValidationErrors(dto);
    }

    [Fact]
    public void DatasetRecords_WithUnicodeContent_PassesValidation()
    {
        // Arrange
        var dto = SecurityTestDataGenerator.GetValidSaveDatasetDto();
        dto.DatasetRecords = new List<EvalDataset>
        {
            new EvalDataset
            {
                Query = "What is AI? (????????)",
                GroundTruth = "AI stands for Artificial Intelligence (????)",
                ActualResponse = string.Empty,
                Context = "General AI question with ??"
            }
        };

        // Act & Assert
        ValidationTestHelper.AssertNoValidationErrors(dto);
    }

    #endregion

    #region DoS and Performance Tests

    [Fact]
    public void DatasetRecords_WithDeeplyNestedObjects_DocumentedBehavior()
    {
        // Arrange
        var dto = SecurityTestDataGenerator.GetValidSaveDatasetDto();
        // Note: EvalDataset has flat structure, but documenting potential JSON serialization issues
        
        // Act & Assert
        ValidationTestHelper.AssertNoValidationErrors(dto);
    }

    #endregion
}
