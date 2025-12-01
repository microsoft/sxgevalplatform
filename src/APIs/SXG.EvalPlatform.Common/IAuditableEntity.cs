namespace SXG.EvalPlatform.Common
{
    /// <summary>
    /// Interface for entities that support audit trail (CreatedBy, CreatedOn, LastUpdatedBy, LastUpdatedOn)
    /// </summary>
    public interface IAuditableEntity
    {
        /// <summary>
        /// User or application name that created this entity
        /// </summary>
        string? CreatedBy { get; set; }

        /// <summary>
        /// Timestamp when this entity was created
        /// </summary>
        DateTime? CreatedOn { get; set; }

        /// <summary>
        /// User or application name that last updated this entity
        /// </summary>
        string? LastUpdatedBy { get; set; }

        /// <summary>
        /// Timestamp when this entity was last updated
        /// </summary>
        DateTime? LastUpdatedOn { get; set; }
    }
}
