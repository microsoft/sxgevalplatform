using Azure.Core;
using Azure.Identity;
using System.Linq;

namespace SXG.EvalPlatform.Common
{
    public class CommonUtils
    {

        public static TokenCredential GetTokenCredential(string environment)
        {
            var isDevelopment = environment.Equals("Development", StringComparison.OrdinalIgnoreCase);


            TokenCredential credential = isDevelopment
            ? new AzureCliCredential()
            : new DefaultAzureCredential();

            return credential;
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
            if (!char.IsLetterOrDigit(result[0]))
            {
                result = "a" + result.Substring(1);
            }
            if (!char.IsLetterOrDigit(result[result.Length - 1]))
            {
                result = result.Substring(0, result.Length - 1) + "z";
            }

            return result;
        }


    }
}
