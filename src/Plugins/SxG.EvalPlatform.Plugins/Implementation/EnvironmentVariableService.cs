namespace SxG.EvalPlatform.Plugins.Common.Implementation
{
    using Microsoft.Xrm.Sdk;
    using Microsoft.Xrm.Sdk.Query;
    using Newtonsoft.Json.Linq;
    using SxG.EvalPlatform.Plugins.Common.Interfaces;
    using IEnvironmentVariableService = Interfaces.IEnvironmentVariableService;

    public class EnvironmentVariableService : IEnvironmentVariableService
    {
        private IOrganizationService _organizationService;
        private static IServiceCache<string, object> evCache = new ServiceCache<string, object>();

        public EnvironmentVariableService(IOrganizationService organizationService)
        {
            this._organizationService = organizationService;
        }

        /// <summary>
        /// Gets the environment variable value as a string
        /// </summary>
        /// <param name="schemaname"> The internal name for the environment variable </param>
        /// <returns> Returns schema name value as a string </returns>
        public string GetString(string schemaname)
        {
            return (string)GetValue(schemaname);
        }

        /// <summary>
        /// Gets the environment variable value as a boolean
        /// </summary>
        /// <param name="schemaname"> The internal name for the environment variable </param>
        /// <returns> Returns schema name value as a boolean </returns>
        public bool GetBool(string schemaname)
        {
            var configValue = this.GetString(schemaname);
            return configValue?.Trim()?.Equals("yes", System.StringComparison.OrdinalIgnoreCase) ?? false;
        }

        /// <summary>
        /// Gets the environment variable value as a decimal
        /// </summary>
        /// <param name="schemaname"> The internal name for the environment variable </param>
        /// <returns> Returns schema name value as a decimal </returns>
        public decimal GetDecimal(string schemaname)
        {
            return (decimal)GetValue(schemaname);
        }

        /// <summary>
        /// Gets the environment variable value as a JObject
        /// </summary>
        /// <param name="schemaname"> The internal name for the environment variable </param>
        /// <returns> Returns schema name value as a JObject </returns>
        public JObject GetJson(string schemaname)
        {
            return JObject.Parse(GetString(schemaname));
        }

        /// <summary>
        /// Gets the value of schema name of an environment variable.
        /// </summary>
        /// <param name="schemaname"> the schema name of the environment variable </param>
        /// <returns> Returns schema name value as an object </returns>
        public object GetValue(string schemaname)
        {
            object evValue;

            // Get the environment variable value from service cache if it exists
            // Otherwise, retrieve it from environmentvariabledefinition entity
            if (evCache.TryGetValue(schemaname, out evValue))
            {
                return evValue;
            }
            else
            {
                Entity entity = GetFetchValues(schemaname);

                string overrideKey = "overridevalue";
                if (entity.Contains(overrideKey))
                {
                    AliasedValue c = (AliasedValue)entity[overrideKey];
                    // Add environment variable to service cache
                    evCache.TryAdd(schemaname, c.Value);

                    return c.Value;
                }

                string defaultValueKey = "defaultvalue";
                if (entity.Contains(defaultValueKey))
                {
                    // Add environment variable to service cache
                    evCache.TryAdd(schemaname, entity[defaultValueKey]);

                    return entity[defaultValueKey];
                }

                throw new InvalidPluginExecutionException($"Missing values for enviroment variable {schemaname}");
            }
        }

        /// <summary>
        /// Clear the environment variable cache
        /// </summary>
        public void ClearEnvironmentVariableCache()
        {
            evCache.ClearCache();
        }

        /// <summary>
        /// Gets the value of an environment variable.
        /// </summary>
        /// <param name="schemaname">Schema Name</param>
        /// <returns>Value of an environment variable</returns>
        private Entity GetFetchValues(string schemaname)
        {
            var fetchXml = $@"
                <fetch top='2'>
                  <entity name='environmentvariabledefinition'>
                    <attribute name='displayname' />
                    <attribute name='defaultvalue' />
                    <filter>
                      <condition attribute='schemaname' operator='eq' value='{schemaname}'/>
                    </filter>
                    <link-entity name='environmentvariablevalue' from='environmentvariabledefinitionid' to='environmentvariabledefinitionid' link-type='outer'>
                      <attribute name='value' alias='overridevalue' />
                    </link-entity>
                  </entity>
                </fetch>";

            EntityCollection result = _organizationService.RetrieveMultiple(new FetchExpression(fetchXml));
            if (result.Entities.Count != 1)
            {
                throw new InvalidPluginExecutionException($"Missing enviroment variable of {schemaname}");
            }

            return result.Entities[0];
        }
    }
}
