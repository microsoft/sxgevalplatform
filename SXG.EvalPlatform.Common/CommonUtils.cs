using Azure.Core;
using Azure.Identity;

namespace SXG.EvalPlatform.Common
{
    public class CommonUtils
    {

        public static TokenCredential GetTokenCredential(string environment)
        {
            var isDevelopment = environment.Equals("Development", StringComparison.OrdinalIgnoreCase);

            // Use DefaultAzureCredential for both environments for better fallback support
            // DefaultAzureCredential tries multiple credential types in this order:
            // 1. Environment variables (for service principals)
            // 2. Managed Identity (in Azure environments)
            // 3. Azure CLI (for local development)
            // 4. Azure PowerShell
            // 5. Interactive browser (as last resort)
            TokenCredential credential = new DefaultAzureCredential();

            return credential;
        }
        public static string TrimAndRemoveSpaces(string input)
        {
            return new string(input.Where(c => !char.IsWhiteSpace(c)).ToArray()).ToLower(); // Result: "Anotherstringwithspaces."
        }


    }
}
