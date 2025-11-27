using Azure.Core;
using Azure.Identity;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EvalPurgeJob
{
    internal class BlobStorageHelper
    {
        private readonly BlobContainerClient _containerClient;
        public BlobStorageHelper(string blobServiceUrl, string containerName, string? managedIdentityClientId = null)
        {
            TokenCredential credential;

            // If a user-assigned managed identity client ID is provided
            if (!string.IsNullOrEmpty(managedIdentityClientId))
            {
                credential = new ManagedIdentityCredential(managedIdentityClientId);
            }
            else
            {
                // Uses System-assigned MI automatically
                credential = new DefaultAzureCredential();
            }

            var blobServiceClient = new BlobServiceClient(new Uri(blobServiceUrl), credential);
            _containerClient = blobServiceClient.GetBlobContainerClient(containerName);
        }

        public async Task DeleteBlobsNewerThanAsync(DateTimeOffset cutoffDate)
        {
            await foreach (BlobItem blobItem in _containerClient.GetBlobsAsync(prefix:"evaluation-results")) // to do make this configurable
            {
                if ((!string.IsNullOrEmpty(blobItem.Name) && blobItem.Name.Contains("_dataset.json")) && blobItem.Properties.LastModified.HasValue &&
                    blobItem.Properties.LastModified.Value > cutoffDate)
                {
                    await _containerClient.DeleteBlobIfExistsAsync(blobItem.Name);
                }
            }
        }
    }
}
