namespace SxG.EvalPlatform.Plugins.Models
{
    using System;
    using Microsoft.Xrm.Sdk;

    /// <summary>
    /// Entity model for cr890_evalrun Elastic table
    /// </summary>
    public class EvalRunEntity
    {
        /// <summary>
        /// Logical name of the entity
        /// </summary>
        public const string EntityLogicalName = "cr890_evalrun";

        /// <summary>
        /// Primary key field name (evalrunid)
        /// </summary>
        public const string PrimaryKey = "cr890_evalrunid";

        /// <summary>
        /// Primary name field (Id)
        /// </summary>
        public const string PrimaryName = "cr890_id";

        /// <summary>
        /// Entity field names
        /// </summary>
        public static class Fields
        {
            public const string EvalRunId = "cr890_evalrunid";
            public const string Id = "cr890_id";
            public const string AgentId = "cr890_agentid";
            public const string EnvironmentId = "cr890_environmentid";
            public const string AgentSchemaName = "cr890_agentschemaname";
            public const string Status = "cr890_status";
            public const string Dataset = "cr890_dataset";
            public const string CreatedOn = "createdon";
            public const string ModifiedOn = "modifiedon";
        }

        /// <summary>
        /// Status values as integers (Choice field values in Dataverse)
        /// </summary>
        public static class StatusValues
        {
            public const int New = 0;
            public const int Started = 1;
            public const int Updated = 2;
            public const int Completed = 3;
            public const int Failed = 4;
        }

        /// <summary>
        /// Status names as strings (for API responses)
        /// </summary>
        public static class StatusNames
        {
            public const string New = "New";
            public const string Started = "Started";
            public const string Updated = "Updated";
            public const string Completed = "Completed";
            public const string Failed = "Failed";
        }

        /// <summary>
        /// Unique identifier for the eval run (evalrunid) - Primary Key
        /// </summary>
        public Guid EvalRunId { get; set; }

        /// <summary>
        /// Primary name field (Id) - Same value as EvalRunId stored as string
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// Agent identifier (string)
        /// </summary>
        public string AgentId { get; set; }

        /// <summary>
        /// Environment identifier (GUID stored as string)
        /// </summary>
        public string EnvironmentId { get; set; }

        /// <summary>
        /// Agent schema name
        /// </summary>
        public string AgentSchemaName { get; set; }

        /// <summary>
        /// Status of the eval run (internal integer value for Dataverse Choice field)
        /// </summary>
        public int? Status { get; set; }

        /// <summary>
        /// Dataset JSON (Multi Line of Text) - Contains the complete dataset information
        /// </summary>
        public string Dataset { get; set; }

        /// <summary>
        /// Created date
        /// </summary>
        public DateTime? CreatedOn { get; set; }

        /// <summary>
        /// Modified date
        /// </summary>
        public DateTime? ModifiedOn { get; set; }

        /// <summary>
        /// Gets status as string for API responses
        /// </summary>
        /// <returns>Status name as string</returns>
        public string GetStatusName()
        {
            if (!Status.HasValue)
                return StatusNames.New;

            switch (Status.Value)
            {
                case StatusValues.New:
                    return StatusNames.New;
                case StatusValues.Started:
                    return StatusNames.Started;
                case StatusValues.Updated:
                    return StatusNames.Updated;
                case StatusValues.Completed:
                    return StatusNames.Completed;
                case StatusValues.Failed:
                    return StatusNames.Failed;
                default:
                    return StatusNames.New;
            }
        }

        /// <summary>
        /// Sets status from string name
        /// </summary>
        /// <param name="statusName">Status name as string</param>
        public void SetStatusFromName(string statusName)
        {
            if (string.IsNullOrWhiteSpace(statusName))
            {
                Status = StatusValues.New;
                return;
            }

            switch (statusName)
            {
                case StatusNames.New:
                    Status = StatusValues.New;
                    break;
                case StatusNames.Started:
                    Status = StatusValues.Started;
                    break;
                case StatusNames.Updated:
                    Status = StatusValues.Updated;
                    break;
                case StatusNames.Completed:
                    Status = StatusValues.Completed;
                    break;
                case StatusNames.Failed:
                    Status = StatusValues.Failed;
                    break;
                default:
                    Status = StatusValues.New;
                    break;
            }
        }

        /// <summary>
        /// Gets status integer value from string name
        /// </summary>
        /// <param name="statusName">Status name as string</param>
        /// <returns>Status integer value</returns>
        public static int GetStatusValueFromName(string statusName)
        {
            if (string.IsNullOrWhiteSpace(statusName))
                return StatusValues.New;

            switch (statusName)
            {
                case StatusNames.New:
                    return StatusValues.New;
                case StatusNames.Started:
                    return StatusValues.Started;
                case StatusNames.Updated:
                    return StatusValues.Updated;
                case StatusNames.Completed:
                    return StatusValues.Completed;
                case StatusNames.Failed:
                    return StatusValues.Failed;
                default:
                    return StatusValues.New;
            }
        }

        /// <summary>
        /// Converts EvalRunEntity to Dataverse Entity
        /// </summary>
        /// <returns>Dataverse Entity object</returns>
        public Entity ToEntity()
        {
            var entity = new Entity(EntityLogicalName);
            
            // Set the primary key (evalrunid)
            if (this.EvalRunId != Guid.Empty)
                entity.Id = this.EvalRunId;
            
            // Set the primary name field (Id) - same value as EvalRunId
            if (!string.IsNullOrEmpty(this.Id))
                entity[Fields.Id] = this.Id;
            
            if (!string.IsNullOrEmpty(this.AgentId))
                entity[Fields.AgentId] = this.AgentId;
            
            if (!string.IsNullOrEmpty(this.EnvironmentId))
                entity[Fields.EnvironmentId] = this.EnvironmentId;
            
            if (!string.IsNullOrEmpty(this.AgentSchemaName))
                entity[Fields.AgentSchemaName] = this.AgentSchemaName;
            
            // Set status as OptionSetValue (Choice field in Dataverse)
            if (this.Status.HasValue)
                entity[Fields.Status] = new OptionSetValue(this.Status.Value);

            if (!string.IsNullOrEmpty(this.Dataset))
                entity[Fields.Dataset] = this.Dataset;

            return entity;
        }

        /// <summary>
        /// Creates EvalRunEntity from Dataverse Entity
        /// </summary>
        /// <param name="entity">Dataverse Entity</param>
        /// <returns>EvalRunEntity object</returns>
        public static EvalRunEntity FromEntity(Entity entity)
        {
            if (entity == null)
                return null;

            var evalRun = new EvalRunEntity();
            evalRun.EvalRunId = entity.Id;
            evalRun.Id = GetStringValue(entity, Fields.Id);
            evalRun.AgentId = GetStringValue(entity, Fields.AgentId);
            evalRun.EnvironmentId = GetStringValue(entity, Fields.EnvironmentId);
            evalRun.AgentSchemaName = GetStringValue(entity, Fields.AgentSchemaName);
            evalRun.Status = GetOptionSetValue(entity, Fields.Status);
            evalRun.Dataset = GetStringValue(entity, Fields.Dataset);
            evalRun.CreatedOn = GetDateTimeValue(entity, Fields.CreatedOn);
            evalRun.ModifiedOn = GetDateTimeValue(entity, Fields.ModifiedOn);

            return evalRun;
        }

        /// <summary>
        /// Helper method to safely get string attributes
        /// </summary>
        private static string GetStringValue(Entity entity, string attributeName)
        {
            if (entity.Contains(attributeName) && entity[attributeName] != null)
                return entity[attributeName].ToString();
            return string.Empty;
        }

        /// <summary>
        /// Helper method to safely get OptionSet (Choice) values
        /// </summary>
        private static int? GetOptionSetValue(Entity entity, string attributeName)
        {
            if (entity.Contains(attributeName) && entity[attributeName] != null)
            {
                if (entity[attributeName] is OptionSetValue optionSet)
                    return optionSet.Value;
            }
            return StatusValues.New; // Default to New if not found
        }

        /// <summary>
        /// Helper method to safely get DateTime attributes
        /// </summary>
        private static DateTime? GetDateTimeValue(Entity entity, string attributeName)
        {
            if (entity.Contains(attributeName) && entity[attributeName] != null)
                if (entity[attributeName] is DateTime dateTime) return dateTime;
            return null;
        }
    }
}