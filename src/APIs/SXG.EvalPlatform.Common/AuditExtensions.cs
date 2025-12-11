namespace SXG.EvalPlatform.Common
{
    /// <summary>
    /// Extension methods for setting audit trail properties on IAuditableEntity implementations
    /// </summary>
    public static class AuditExtensions
    {
        /// <summary>
        /// Sets audit properties for a newly created entity
        /// Sets: CreatedBy, CreatedOn, LastUpdatedBy, LastUpdatedOn
        /// </summary>
        /// <param name="entity">Entity to set audit properties on</param>
        /// <param name="auditUser">User or application name performing the creation</param>
        public static void SetCreationAudit(this IAuditableEntity entity, string auditUser)
        {
            var now = DateTime.UtcNow;
            entity.CreatedBy = auditUser;
            entity.CreatedOn = now;
            entity.LastUpdatedBy = auditUser;
            entity.LastUpdatedOn = now;
        }

        /// <summary>
        /// Sets audit properties for an updated entity
        /// Sets: LastUpdatedBy, LastUpdatedOn
        /// Does NOT modify: CreatedBy, CreatedOn (preserves original values)
        /// </summary>
        /// <param name="entity">Entity to set audit properties on</param>
        /// <param name="auditUser">User or application name performing the update</param>
        public static void SetUpdateAudit(this IAuditableEntity entity, string auditUser)
        {
            entity.LastUpdatedBy = auditUser;
            entity.LastUpdatedOn = DateTime.UtcNow;
        }

        /// <summary>
        /// Sets audit properties based on whether this is a create or update operation
        /// Uses CreatedOn to determine if entity is new (CreatedOn == null or default)
        /// </summary>
        /// <param name="entity">Entity to set audit properties on</param>
        /// <param name="auditUser">User or application name performing the operation</param>
        public static void SetAudit(this IAuditableEntity entity, string auditUser)
        {
            bool isNewEntity = !entity.CreatedOn.HasValue || entity.CreatedOn == default;

            if (isNewEntity)
            {
                entity.SetCreationAudit(auditUser);
            }
            else
            {
                entity.SetUpdateAudit(auditUser);
            }
        }
    }
}
