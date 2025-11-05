using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sxg.EvalPlatform.API.Storage.Services
{
    public interface IAzureQueueStorageService
    {
        Task<bool> SendMessageAsync(string queueName, string messageContent);
    }
}
