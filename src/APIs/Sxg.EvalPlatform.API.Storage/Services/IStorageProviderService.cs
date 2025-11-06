namespace Sxg.EvalPlatform.API.Storage.Services
{
    public interface IStorageProviderService
    {
        /// <summary>
        /// Read content from a storage
        /// </summary>
        /// <param name="table">Table name</param>
        /// <param name="key">Key name</param>
        /// <returns> Data content as string</returns>
        Task<string?> ReadAsync(string table, string key);

        /// <summary>
        /// Write content to a storage
        /// </summary>
        /// <param name="table">Table name</param>
        /// <param name="key">Key name</param>
        /// <param name="content">Content to write</param>
        /// <returns>True if successful</returns>
        Task<bool> WriteAsync(string table, string key, string content);

        /// <summary>
        /// Check if a blob exists
        /// </summary>
        /// <param name="table">Table name</param>
        /// <param name="key">Key name</param>
        /// <returns>True if blob exists</returns>
        Task<bool> ExistsAsync(string table, string key);

        /// <summary>
        /// Delete a blob
        /// </summary>
        /// <param name="table">Table name</param>
        /// <param name="key">Key name</param>
        /// <returns>True if successful</returns>
        Task<bool> DeleteAsync(string table, string key);

        /// <summary>
        /// List blobs with a given prefix
        /// </summary>
        /// <param name="table">Table name</param>
        /// <param name="prefix">Key name prefix</param>
        /// <returns>List of key names</returns>
        Task<List<string>> ListAsync(string table, string prefix);
    }
}
