using System.ComponentModel.DataAnnotations;
using FluentAssertions;

namespace Sxg.EvalPlatform.API.UnitTests.Helpers;

/// <summary>
/// Helper methods for validating DTO validation attributes and assertions
/// Supports SF-13-1 input validation testing
/// </summary>
public static class ValidationTestHelper
{
    /// <summary>
    /// Validates a DTO object and returns validation results
    /// </summary>
    public static List<ValidationResult> ValidateObject<T>(T obj) where T : class
    {
        var validationContext = new ValidationContext(obj);
        var validationResults = new List<ValidationResult>();
        Validator.TryValidateObject(obj, validationContext, validationResults, validateAllProperties: true);
        return validationResults;
    }

    /// <summary>
    /// Asserts that an object has validation errors
    /// </summary>
    public static void AssertHasValidationErrors<T>(T obj, string? expectedPropertyName = null) where T : class
    {
        var validationResults = ValidateObject(obj);
        
        validationResults.Should().NotBeEmpty("Expected validation to fail but it passed");

        if (!string.IsNullOrEmpty(expectedPropertyName))
        {
            validationResults.Should().Contain(
                vr => vr.MemberNames.Contains(expectedPropertyName),
                $"Expected validation error for property '{expectedPropertyName}'"
            );
        }
    }

    /// <summary>
    /// Asserts that an object has NO validation errors
    /// </summary>
    public static void AssertNoValidationErrors<T>(T obj) where T : class
    {
        var validationResults = ValidateObject(obj);
        
        if (validationResults.Any())
        {
            var errorMessages = string.Join("; ", validationResults.Select(vr => 
                $"{string.Join(", ", vr.MemberNames)}: {vr.ErrorMessage}"));
            
            validationResults.Should().BeEmpty($"Expected no validation errors but found: {errorMessages}");
        }
    }

    /// <summary>
    /// Asserts that validation error contains expected message
    /// </summary>
    public static void AssertValidationErrorContains<T>(T obj, string propertyName, string expectedMessageFragment) 
        where T : class
    {
        var validationResults = ValidateObject(obj);
        
        validationResults.Should().NotBeEmpty("Expected validation errors but found none");
        
        var propertyErrors = validationResults.Where(vr => vr.MemberNames.Contains(propertyName)).ToList();
        
        propertyErrors.Should().NotBeEmpty($"Expected validation error for property '{propertyName}'");
        
        var errorMessages = propertyErrors.Select(e => e.ErrorMessage ?? string.Empty);
        errorMessages.Should().Contain(msg => 
            msg.Contains(expectedMessageFragment, StringComparison.OrdinalIgnoreCase),
            $"Expected error message to contain '{expectedMessageFragment}'"
        );
    }

    /// <summary>
    /// Validates that a specific property has a Required attribute
    /// </summary>
    public static void AssertPropertyHasRequiredAttribute<T>(string propertyName) where T : class
    {
        var property = typeof(T).GetProperty(propertyName);
        property.Should().NotBeNull($"Property '{propertyName}' does not exist on type {typeof(T).Name}");

        var hasRequiredAttribute = property!.GetCustomAttributes(typeof(RequiredAttribute), inherit: true).Any();
        hasRequiredAttribute.Should().BeTrue($"Property '{propertyName}' should have [Required] attribute");
    }

    /// <summary>
    /// Validates that a specific property has a StringLength attribute with expected values
    /// </summary>
    public static void AssertPropertyHasStringLengthAttribute<T>(
        string propertyName, 
        int? expectedMaxLength = null, 
        int? expectedMinLength = null) where T : class
    {
        var property = typeof(T).GetProperty(propertyName);
        property.Should().NotBeNull($"Property '{propertyName}' does not exist on type {typeof(T).Name}");

        var stringLengthAttr = property!.GetCustomAttributes(typeof(StringLengthAttribute), inherit: true)
            .FirstOrDefault() as StringLengthAttribute;

        stringLengthAttr.Should().NotBeNull($"Property '{propertyName}' should have [StringLength] attribute");

        if (expectedMaxLength.HasValue)
        {
            stringLengthAttr!.MaximumLength.Should().Be(expectedMaxLength.Value,
                $"Property '{propertyName}' should have MaximumLength={expectedMaxLength}");
        }

        if (expectedMinLength.HasValue)
        {
            stringLengthAttr!.MinimumLength.Should().Be(expectedMinLength.Value,
                $"Property '{propertyName}' should have MinimumLength={expectedMinLength}");
        }
    }

    /// <summary>
    /// Gets all validation error messages for a DTO
    /// </summary>
    public static string GetValidationErrorMessages<T>(T obj) where T : class
    {
        var validationResults = ValidateObject(obj);
        return string.Join("; ", validationResults.Select(vr =>
            $"{string.Join(", ", vr.MemberNames)}: {vr.ErrorMessage}"));
    }

    /// <summary>
    /// Counts validation errors for a specific property
    /// </summary>
    public static int CountValidationErrors<T>(T obj, string propertyName) where T : class
    {
        var validationResults = ValidateObject(obj);
        return validationResults.Count(vr => vr.MemberNames.Contains(propertyName));
    }

    /// <summary>
    /// Validates that an object passes validation (no errors)
    /// </summary>
    public static bool IsValid<T>(T obj) where T : class
    {
        var validationContext = new ValidationContext(obj);
        return Validator.TryValidateObject(obj, validationContext, null, validateAllProperties: true);
    }

    /// <summary>
    /// Gets validation errors grouped by property name
    /// </summary>
    public static Dictionary<string, List<string>> GetValidationErrorsByProperty<T>(T obj) where T : class
    {
        var validationResults = ValidateObject(obj);
        var errorsByProperty = new Dictionary<string, List<string>>();

        foreach (var result in validationResults)
        {
            foreach (var memberName in result.MemberNames)
            {
                if (!errorsByProperty.ContainsKey(memberName))
                {
                    errorsByProperty[memberName] = new List<string>();
                }
                errorsByProperty[memberName].Add(result.ErrorMessage ?? "Unknown error");
            }
        }

        return errorsByProperty;
    }
}
