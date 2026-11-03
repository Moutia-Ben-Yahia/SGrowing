namespace SGrowing.Core.Tenancy;

public interface ITenantContext
{
    Guid TenantId { get; }
    Guid? WorkspaceId { get; }
    bool HasTenant { get; }
    void SetTenant(Guid tenantId);
    void SetWorkspace(Guid? workspaceId);
}

public class TenantContext : ITenantContext
{
    public Guid TenantId { get; private set; }
    public Guid? WorkspaceId { get; private set; }
    public bool HasTenant => TenantId != Guid.Empty;

    public void SetTenant(Guid tenantId)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("TenantId cannot be empty.", nameof(tenantId));
        TenantId = tenantId;
    }

    public void SetWorkspace(Guid? workspaceId)
    {
        WorkspaceId = workspaceId;
    }
}
