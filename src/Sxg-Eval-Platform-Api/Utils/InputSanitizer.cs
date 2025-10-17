using System.Text.RegularExpressions;

namespace SxgEvalPlatformApi.Utils;

/// <summary>
/// Utility class for input sanitization and validation
/// </summary>
public static class InputSanitizer
{
    // Regex patterns for validation
    private static readonly Regex AlphanumericPattern = new(@"^[a-zA-Z0-9\-_\.]+$", RegexOptions.Compiled);
    private static readonly Regex GuidPattern = new(@"^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$", RegexOptions.Compiled);
    private static readonly Regex SafeStringPattern = new(@"^[a-zA-Z0-9\s\-_\.@]+$", RegexOptions.Compiled);

    /// <summary>
    /// Sanitizes and validates agent ID
    /// </summary>
    /// <param name="agentId">Agent ID to validate</param>
    /// <returns>True if valid, false otherwise</returns>
    public static bool IsValidAgentId(string? agentId)
    {
        if (string.IsNullOrWhiteSpace(agentId))
            return false;

        // Agent ID should be alphanumeric with basic safe characters
        return agentId.Length <= 100 && AlphanumericPattern.IsMatch(agentId);
    }

    /// <summary>
    /// Sanitizes and validates dataset ID
    /// </summary>
    /// <param name="datasetId">Dataset ID to validate</param>
    /// <returns>True if valid, false otherwise</returns>
    public static bool IsValidDatasetId(string? datasetId)
    {
        if (string.IsNullOrWhiteSpace(datasetId))
            return false;

        // Dataset ID should be alphanumeric with basic safe characters
        return datasetId.Length <= 100 && AlphanumericPattern.IsMatch(datasetId);
    }

    /// <summary>
    /// Sanitizes and validates configuration ID
    /// </summary>
    /// <param name="configurationId">Configuration ID to validate</param>
    /// <returns>True if valid, false otherwise</returns>
    public static bool IsValidConfigurationId(string? configurationId)
    {
        if (string.IsNullOrWhiteSpace(configurationId))
            return false;

        // Configuration ID should be alphanumeric with basic safe characters
        return configurationId.Length <= 100 && AlphanumericPattern.IsMatch(configurationId);
    }

    /// <summary>
    /// Validates GUID format
    /// </summary>
    /// <param name="guid">GUID string to validate</param>
    /// <returns>True if valid GUID format, false otherwise</returns>
    public static bool IsValidGuid(string? guid)
    {
        if (string.IsNullOrWhiteSpace(guid))
            return false;

        return GuidPattern.IsMatch(guid);
    }

    /// <summary>
    /// Sanitizes general string input by removing potentially dangerous characters
    /// </summary>
    /// <param name="input">Input string to sanitize</param>
    /// <param name="maxLength">Maximum allowed length</param>
    /// <returns>Sanitized string or null if invalid</returns>
    public static string? SanitizeString(string? input, int maxLength = 200)
    {
        if (string.IsNullOrWhiteSpace(input))
            return null;

        // Trim whitespace
        input = input.Trim();

        // Check length
        if (input.Length > maxLength)
            return null;

        // Check for safe characters only
        if (!SafeStringPattern.IsMatch(input))
            return null;

        return input;
    }

    /// <summary>
    /// Validates environment name
    /// </summary>
    /// <param name="environmentName">Environment name to validate</param>
    /// <returns>True if valid, false otherwise</returns>
    public static bool IsValidEnvironmentName(string? environmentName)
    {
        if (string.IsNullOrWhiteSpace(environmentName))
            return true; // Optional parameter

        // Environment name should be alphanumeric
        return environmentName.Length <= 50 && AlphanumericPattern.IsMatch(environmentName);
    }

    /// <summary>
    /// Validates file name for dataset operations
    /// </summary>
    /// <param name="fileName">File name to validate</param>
    /// <returns>True if valid, false otherwise</returns>
    public static bool IsValidFileName(string? fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            return false;

        // File name should not contain path traversal characters or dangerous patterns
        if (fileName.Contains("..") || fileName.Contains("/") || fileName.Contains("\\"))
            return false;

        // Should be reasonable length and safe characters
        return fileName.Length <= 255 && AlphanumericPattern.IsMatch(Path.GetFileNameWithoutExtension(fileName));
    }
}