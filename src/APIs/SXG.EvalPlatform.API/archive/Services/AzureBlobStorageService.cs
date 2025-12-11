//using Azure;
//using Azure.Core;
//using Azure.Identity;
//using Azure.Storage.Blobs;
//using Azure.Storage.Blobs.Models;
//using SXG.EvalPlatform.Common;
//using System.Text;

//namespace SxgEvalPlatformApi.Services;

///// <summary>
///// Service for Azure Blob Storage operations
///// [DEPRECATED] - Use Sxg.EvalPlatform.API.Storage.Services.AzureBlobStorageService instead
///// This service is kept for backward compatibility and will be removed in future versions
///// </summary>
//[Obsolete("Use Sxg.EvalPlatform.API.Storage.Services.AzureBlobStorageService instead")]
//public class AzureBlobStorageService : IAzureBlobStorageService
//{
//    private readonly BlobServiceClient _blobServiceClient;
//    private readonly ILogger<AzureBlobStorageService> _logger;

//    public AzureBlobStorageService(IConfiguration configuration, ILogger<AzureBlobStorageService> logger)
//    {
//        _logger = logger;
        
//        var accountName = configuration["AzureStorage:AccountName"];
        
//        if (string.IsNullOrEmpty(accountName))
//        {
//            throw new ArgumentException("Azure Storage account name is not configured");
//        }

//        var blobUri = $"https://{accountName}.blob.core.windows.net";

//        var environment = configuration.GetValue<string>("ASPNETCORE_ENVIRONMENT") ?? "Production";
//        TokenCredential credential = CommonUtils.GetTokenCredential(environment);

//        // Use DefaultAzureCredential for managed identity
//        _blobServiceClient = new BlobServiceClient(new Uri(blobUri), credential);
//        _logger.LogInformation("Azure Blob Storage service initialized with managed identity for account: {AccountName}", accountName);
//    }

//    /// <inheritdoc />
//    public async Task<string?> ReadBlobContentAsync(string containerName, string blobName)
//    {
//        try
//        {
//            _logger.LogInformation("Reading blob content from container: {ContainerName}, blob: {BlobName}", 
//                containerName, blobName);

//            var containerClient = _blobServiceClient.GetBlobContainerClient(containerName);
//            var blobClient = containerClient.GetBlobClient(blobName);

//            if (!await blobClient.ExistsAsync())
//            {
//                _logger.LogWarning("Blob not found: {ContainerName}/{BlobName}", containerName, blobName);
//                return null;
//            }

//            var response = await blobClient.DownloadContentAsync();
//            var content = response.Value.Content.ToString();
            
//            _logger.LogInformation("Successfully read blob content from {ContainerName}/{BlobName}", 
//                containerName, blobName);
            
//            return content;
//        }
//        catch (Exception ex)
//        {
//            _logger.LogError(ex, "Failed to read blob content from {ContainerName}/{BlobName}", 
//                containerName, blobName);
//            throw;
//        }
//    }

//    /// <inheritdoc />
//    public async Task<bool> WriteBlobContentAsync(string containerName, string blobName, string content)
//    {
//        try
//        {
//            _logger.LogInformation("Writing blob content to container: {ContainerName}, blob: {BlobName}", 
//                containerName, blobName);

//            var containerClient = _blobServiceClient.GetBlobContainerClient(containerName);
            
//            // Create container if it doesn't exist
//            await containerClient.CreateIfNotExistsAsync(PublicAccessType.None);
            
//            var blobClient = containerClient.GetBlobClient(blobName);
            
//            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(content));
//            await blobClient.UploadAsync(stream, overwrite: true);
            
//            _logger.LogInformation("Successfully wrote blob content to {ContainerName}/{BlobName}", 
//                containerName, blobName);
            
//            return true;
//        }
//        catch (Exception ex)
//        {
//            _logger.LogError(ex, "Failed to write blob content to {ContainerName}/{BlobName}", 
//                containerName, blobName);
//            return false;
//        }
//    }

//    /// <inheritdoc />
//    public async Task<bool> BlobExistsAsync(string containerName, string blobName)
//    {
//        try
//        {
//            var containerClient = _blobServiceClient.GetBlobContainerClient(containerName);
//            var blobClient = containerClient.GetBlobClient(blobName);
            
//            var response = await blobClient.ExistsAsync();
//            return response.Value;
//        }
//        catch (Exception ex)
//        {
//            _logger.LogError(ex, "Failed to check blob existence for {ContainerName}/{BlobName}", 
//                containerName, blobName);
//            return false;
//        }
//    }

//    /// <inheritdoc />
//    public async Task<bool> DeleteBlobAsync(string containerName, string blobName)
//    {
//        try
//        {
//            _logger.LogInformation("Deleting blob from container: {ContainerName}, blob: {BlobName}", 
//                containerName, blobName);

//            var containerClient = _blobServiceClient.GetBlobContainerClient(containerName);
//            var blobClient = containerClient.GetBlobClient(blobName);
            
//            var response = await blobClient.DeleteIfExistsAsync();
            
//            _logger.LogInformation("Successfully deleted blob {ContainerName}/{BlobName}", 
//                containerName, blobName);
            
//            return response.Value;
//        }
//        catch (Exception ex)
//        {
//            _logger.LogError(ex, "Failed to delete blob {ContainerName}/{BlobName}", 
//                containerName, blobName);
//            return false;
//        }
//    }
//}