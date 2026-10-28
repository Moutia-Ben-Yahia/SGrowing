namespace SGrowing.Core.Common;

/// <summary>
/// Unified base entity for tenant-isolated data models in SGrowing SaaS.
/// Provides consistent primary key, tenant isolation, auditing timestamps,
/// soft deletion tracking, and concurrency control.
/// </summary>
public abstract class TenantScopedEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    
    public Guid TenantId { get; set; }
    
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    
    public DateTime? UpdatedAtUtc { get; set; }
    
    public bool IsDeleted { get; set; } = false;
    
    /// <summary>
    /// Concurrency token used for optimistic locking during high-concurrency writes.
    /// Incremented on each update to detect and prevent race-condition state corruptions.
    /// </summary>
    public uint RowVersion { get; set; } = 1;
}
