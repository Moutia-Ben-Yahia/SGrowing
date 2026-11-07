namespace SGrowing.Core.Common;

/// <summary>
/// Defines a contract for entities that are scoped to a specific tenant boundary.
/// </summary>
public interface ITenantScoped
{
    /// <summary>
    /// Gets the identifier of the tenant that owns this entity.
    /// </summary>
    Guid TenantId { get; }
}
