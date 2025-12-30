using Azure.Core;
using Azure.Identity;

namespace SxgEvalPlatformApi.Utils
{
    public class TokenCredentialUtils
    {
        public static TokenCredential GetTokenCredential(string environment)
        {            
            var isDevelopment = environment.Equals("Development", StringComparison.OrdinalIgnoreCase);


            TokenCredential credential = isDevelopment
            ? new AzureCliCredential()
            : new DefaultAzureCredential(); // CodeQL[SM05137] justification - Not used in production

            return credential;
        }
    }
}
