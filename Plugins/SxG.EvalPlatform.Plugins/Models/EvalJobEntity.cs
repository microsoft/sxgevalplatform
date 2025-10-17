namespace SxG.EvalPlatform.Plugins.Models
{
    using System;
    using Microsoft.Xrm.Sdk;

    /// <summary>
    /// Entity model for cr890_evaljob Elastic table
    /// </summary>
    public class EvalJobEntity
    {
        /// <summary>
        /// Logical name of the entity
        /// </summary>
        public const string EntityLogicalName = "cr890_evaljob";

        /// <summary>
        /// Primary key field name (evaljobid)
        /// </summary>
        public const string PrimaryKey = "cr890_evaljobid";

        /// <summary>
        /// Primary name field (Id)
        /// </summary>
        public const string PrimaryName = "cr890_id";

        /// <summary>
        /// Entity field names
        /// </summary>
        public static class Fields
        {
            public const string EvalJobId = "cr890_evaljobid";
            public const string Id = "cr890_id";
            public const string AgentId = "cr890_agentid";
            public const string EnvironmentId = "cr890_environmentid";
            public const string SchemaName = "cr890_schemaname";
            public const string Status = "cr890_status";
            public const string Input = "cr890_input";
            public const string Output = "cr890_output";
            public const string CreatedOn = "createdon";
            public const string ModifiedOn = "modifiedon";
        }

        /// <summary>
        /// Status option set values
        /// </summary>
        public static class StatusValues
        {
            public const int New = 0;
            public const int Active = 1;
            public const int Success = 2;
            public const int Failed = 3;
        }

        /// <summary>
        /// Unique identifier for the eval job (evaljobid)
        /// </summary>
        public Guid EvalJobId { get; set; }

        /// <summary>
        /// Primary name field (Id) - Same value as EvalJobId stored as string
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// Agent identifier (GUID stored as string)
        /// </summary>
        public string AgentId { get; set; }

        /// <summary>
        /// Environment identifier (GUID stored as string)
        /// </summary>
        public string EnvironmentId { get; set; }

        /// <summary>
        /// Schema name
        /// </summary>
        public string SchemaName { get; set; }

        /// <summary>
        /// Status of the eval job (Choice field)
        /// </summary>
        public int Status { get; set; }

        /// <summary>
        /// Input JSON (Multi Line of Text)
        /// </summary>
        public string Input { get; set; }

        /// <summary>
        /// Output JSON (Multi Line of Text)
        /// </summary>
        public string Output { get; set; }

        /// <summary>
        /// Created date
        /// </summary>
        public DateTime? CreatedOn { get; set; }

        /// <summary>
        /// Modified date
        /// </summary>
        public DateTime? ModifiedOn { get; set; }

        /// <summary>
        /// Converts EvalJobEntity to Dataverse Entity
        /// </summary>
        /// <returns>Dataverse Entity object</returns>
        public Entity ToEntity()
        {
            var entity = new Entity(EntityLogicalName);
            
            // Set the primary key (evaljobid)
            if (EvalJobId != Guid.Empty)
                entity.Id = EvalJobId;
            
            // Set the primary name field (Id) - same value as EvalJobId
            if (!string.IsNullOrEmpty(Id))
                entity[Fields.Id] = Id;
            
            if (!string.IsNullOrEmpty(AgentId))
                entity[Fields.AgentId] = AgentId;
            
            if (!string.IsNullOrEmpty(EnvironmentId))
                entity[Fields.EnvironmentId] = EnvironmentId;
            
            if (!string.IsNullOrEmpty(SchemaName))
                entity[Fields.SchemaName] = SchemaName;
            
            // Set status as OptionSetValue
            entity[Fields.Status] = new OptionSetValue(Status);

            if (!string.IsNullOrEmpty(Input))
                entity[Fields.Input] = Input;

            if (!string.IsNullOrEmpty(Output))
                entity[Fields.Output] = Output;

            return entity;
        }

        /// <summary>
        /// Creates EvalJobEntity from Dataverse Entity
        /// </summary>
        /// <param name="entity">Dataverse Entity</param>
        /// <returns>EvalJobEntity object</returns>
        public static EvalJobEntity FromEntity(Entity entity)
        {
            if (entity == null)
                return null;

            return new EvalJobEntity
            {
                EvalJobId = entity.Id,
                Id = GetStringAttribute(entity, Fields.Id),
                AgentId = GetStringAttribute(entity, Fields.AgentId),
                EnvironmentId = GetStringAttribute(entity, Fields.EnvironmentId),
                SchemaName = GetStringAttribute(entity, Fields.SchemaName),
                Status = GetOptionSetValue(entity, Fields.Status),
                Input = GetStringAttribute(entity, Fields.Input),
                Output = GetStringAttribute(entity, Fields.Output),
                CreatedOn = GetDateTimeAttribute(entity, Fields.CreatedOn),
                ModifiedOn = GetDateTimeAttribute(entity, Fields.ModifiedOn)
            };
        }

        /// <summary>
        /// Helper method to safely get string attributes
        /// </summary>
        private static string GetStringAttribute(Entity entity, string attributeName)
        {
            if (entity.Contains(attributeName) && entity[attributeName] != null)
            {
                return entity[attributeName].ToString();
            }
            return string.Empty;
        }

        /// <summary>
        /// Helper method to safely get DateTime attributes
        /// </summary>
        private static DateTime? GetDateTimeAttribute(Entity entity, string attributeName)
        {
            if (entity.Contains(attributeName) && entity[attributeName] != null)
            {
                if (entity[attributeName] is DateTime dateTime)
                    return dateTime;
            }
            return null;
        }

        /// <summary>
        /// Helper method to safely get OptionSet values
        /// </summary>
        private static int GetOptionSetValue(Entity entity, string attributeName)
        {
            if (entity.Contains(attributeName) && entity[attributeName] != null)
            {
                if (entity[attributeName] is OptionSetValue optionSetValue)
                    return optionSetValue.Value;
            }
            return StatusValues.New; // Default to New
        }
    }
}