namespace SxG.EvalPlatform.Plugins.Models.DTO
{
    using System;

    /// <summary>
    /// Data Transfer Object for EvalRun API responses
    /// Provides a clean separation between database entities and API contracts
    /// </summary>
    public class EvalRunDto
    {
        /// <summary>
        /// Unique identifier for the eval run (Primary Key)
        /// </summary>
        public Guid EvalRunId { get; set; }

        /// <summary>
        /// Primary name field (same value as EvalRunId stored as string)
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// Dataset identifier
        /// </summary>
        public string DatasetId { get; set; }

        /// <summary>
        /// Agent identifier
        /// </summary>
        public string AgentId { get; set; }

        /// <summary>
        /// Environment identifier
        /// </summary>
        public string EnvironmentId { get; set; }

        /// <summary>
        /// Agent schema name
        /// </summary>
        public string AgentSchemaName { get; set; }

        /// <summary>
        /// Status of the eval run (integer value)
        /// </summary>
        public int? Status { get; set; }

        /// <summary>
        /// Dataset JSON content
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
        /// Status values constants
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
        /// Status names for API responses
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
        /// Gets status as string for API responses
        /// </summary>
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
    }
}
