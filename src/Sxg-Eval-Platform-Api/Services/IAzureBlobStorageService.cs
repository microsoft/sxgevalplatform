using Azure.Storage.Blobs;
using Azure.Identity;
using System.Text;

namespace SxgEvalPlatformApi.Services;

/// <summary>
/// Interface for Azure Blob Storage operations
/// </summary>
public interface IAzureBlobStorageService
{
    /// <summary>
    /// Read content from a blob
    /// </summary>
    /// <param name="containerName">Container name</param>
    /// <param name="blobName">Blob name</param>
    /// <returns>Blob content as string</returns>
    Task<string?> ReadBlobContentAsync(string containerName, string blobName);

    /// <summary>
    /// Write content to a blob
    /// </summary>
    /// <param name="containerName">Container name</param>
    /// <param name="blobName">Blob name</param>
    /// <param name="content">Content to write</param>
    /// <returns>True if successful</returns>
    Task<bool> WriteBlobContentAsync(string containerName, string blobName, string content);

    /// <summary>
    /// Check if a blob exists
    /// </summary>
    /// <param name="containerName">Container name</param>
    /// <param name="blobName">Blob name</param>
    /// <returns>True if blob exists</returns>
    Task<bool> BlobExistsAsync(string containerName, string blobName);

    /// <summary>
    /// Delete a blob
    /// </summary>
    /// <param name="containerName">Container name</param>
    /// <param name="blobName">Blob name</param>
    /// <returns>True if successful</returns>
    Task<bool> DeleteBlobAsync(string containerName, string blobName);
}