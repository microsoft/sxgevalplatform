using Azure.Core;
using Azure.Identity;

namespace SxgEvalPlatformApi.Utils
{
    public class TokenCredentailUtils
    {
        public static TokenCredential GetTokenCredential(string environment)
        {            
            var isDevelopment = environment.Equals("Development", StringComparison.OrdinalIgnoreCase);


            TokenCredential credential = isDevelopment
            ? new AzureCliCredential()
            : new DefaultAzureCredential();

            return credential;
        }
    }
}
