using Azure.Core;
using Azure.Identity;

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
            return new string(input.Where(c => !char.IsWhiteSpace(c)).ToArray()).ToLower(); // Result: "Anotherstringwithspaces."
        }


    }
}
