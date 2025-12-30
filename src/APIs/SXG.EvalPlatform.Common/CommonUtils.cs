using Azure.Core;
using Azure.Identity;

namespace SXG.EvalPlatform.Common
{
    public class CommonUtils
    {

        public static TokenCredential GetTokenCredential(string environment)
        {
            var isLocal = environment.Equals("Local", StringComparison.OrdinalIgnoreCase);

            if (isLocal)
            {
                return new AzureCliCredential();
            }

            #if DEBUG
            // For local development, use DefaultAzureCredential
            return new DefaultAzureCredential(); // CodeQL [SM05137] justification - Not used in production
            #else
            // For non-debug local builds, use Managed Identity                                    
            return new ManagedIdentityCredential();
            #endif
        }

        public static string TrimAndRemoveSpaces(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                throw new ArgumentException("Input cannot be null or empty", nameof(input));
            }

            // Step 1: Remove whitespace and convert to lowercase
            var result = new string(input.Where(c => !char.IsWhiteSpace(c)).ToArray()).ToLower();

            // Step 2: Replace underscores with hyphens and remove invalid characters
            // Azure Blob Storage container names can only contain lowercase letters, numbers, and hyphens
            result = new string(result.Select(c => 
            {
                if (char.IsLower(c) || char.IsDigit(c))
                    return c;
                else if (c == '_')
                    return '-';
                else
                    return '-'; // Replace other invalid characters with hyphens
            }).ToArray());

            // Step 3: Remove consecutive hyphens
            while (result.Contains("--"))
            {
                result = result.Replace("--", "-");
            }

            // Step 4: Ensure it starts and ends with alphanumeric characters
            result = result.Trim('-');

            // Step 5: Ensure minimum length of 3 characters
            if (result.Length < 3)
            {
                result = result.PadRight(3, '0'); // Pad with zeros if too short
            }

            // Step 6: Ensure maximum length of 63 characters
            if (result.Length > 63)
            {
                result = result.Substring(0, 63);
                // Ensure it still ends with alphanumeric after truncation
                result = result.TrimEnd('-');
                if (result.Length < 3)
                {
                    result = result.PadRight(3, '0');
                }
            }

            // Final validation: ensure it starts and ends with alphanumeric
            // Azure Blob Storage has edge cases with GUID-formatted container names starting with digits
            // Only prefix if it matches GUID pattern (8-4-4-4-12 with hyphens) and starts with digit
            if (char.IsDigit(result[0]) && LooksLikeGuid(result))
            {
                result = "a" + result;
                
                // Ensure still within max length after prefix
                if (result.Length > 63)
                {
                    result = result.Substring(0, 63);
                    result = result.TrimEnd('-');
                    
                    // After trimming, ensure we still have minimum length
                    if (result.Length < 3)
                    {
                        result = result.PadRight(3, '0');
                    }
                }
            }
            else if (!char.IsLetterOrDigit(result[0]))
            {
                // This should never happen after Trim('-'), but handle it defensively
                result = "a" + result.TrimStart('-');
                
                // Ensure still within max length
                if (result.Length > 63)
                {
                    result = result.Substring(0, 63).TrimEnd('-');
                }
                
                // Ensure minimum length
                if (result.Length < 3)
                {
                    result = result.PadRight(3, '0');
                }
            }
            
            // Ensure it ends with alphanumeric
            if (result.Length > 0 && !char.IsLetterOrDigit(result[result.Length - 1]))
            {
                result = result.Substring(0, result.Length - 1) + "z";
            }

            return result;
        }

        /// <summary>
        /// Check if a string matches the GUID pattern (8-4-4-4-12 format with hyphens)
        /// </summary>
        private static bool LooksLikeGuid(string input)
        {
            if (string.IsNullOrEmpty(input) || input.Length != 36)
                return false;

            // Check hyphen positions (8-4-4-4-12 pattern)
            if (input[8] != '-' || input[13] != '-' || input[18] != '-' || input[23] != '-')
                return false;

            // Check all other positions are hex characters
            for (int i = 0; i < input.Length; i++)
            {
                if (i == 8 || i == 13 || i == 18 || i == 23)
                    continue; // Skip hyphen positions

                char c = input[i];
                if (!((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f')))
                    return false;
            }

            return true;
        }

        public static string SanitizeForLog(string input)
        {
            if (string.IsNullOrEmpty(input))
                return string.Empty;
            return input.Replace("\r", "").Replace("\n", "");
        }

    }
}
