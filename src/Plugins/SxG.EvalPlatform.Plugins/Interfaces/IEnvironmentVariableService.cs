namespace SxG.EvalPlatform.Plugins.Common.Interfaces
{
    using Newtonsoft.Json.Linq;

    /// <summary>
    /// Interface for EnvironmentVariableService
    /// </summary>
    public interface IEnvironmentVariableService
    {
        /// <summary>
        /// Gets the environment variable value as a string
        /// </summary>
        /// <param name="schemaname"> The internal name for the environment variable </param>
        /// <returns> Returns schema name value as a string </returns>
        string GetString(string schemaname);

        /// <summary>
        /// Gets the environment variable value as a boolean
        /// </summary>
        /// <param name="schemaname"> The internal name for the environment variable </param>
        /// <returns> Returns schema name value as a boolean </returns>
        bool GetBool(string schemaname);

        /// <summary>
        /// Gets the environment variable value as a decimal
        /// </summary>
        /// <param name="schemaname"> The internal name for the environment variable </param>
        /// <returns> Returns schema name value as a decimal </returns>
        decimal GetDecimal(string schemaname);

        /// <summary>
        /// Gets the environment variable value as a JObject
        /// </summary>
        /// <param name="schemaname"> The internal name for the environment variable </param>
        /// <returns> Returns schema name value as a JObject </returns>
        JObject GetJson(string schemaname);

        /// <summary>
        /// Clear the environment variable cache
        /// </summary>
        void ClearEnvironmentVariableCache();
    }
}
