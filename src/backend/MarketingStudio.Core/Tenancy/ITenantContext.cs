namespace MarketingStudio.Core.Tenancy;

public interface ITenantContext
{
    Guid TenantId { get; }
    bool HasTenant { get; }
    void SetTenant(Guid tenantId);
}

public class TenantContext : ITenantContext
{
    public Guid TenantId { get; private set; }
    public bool HasTenant => TenantId != Guid.Empty;

    public void SetTenant(Guid tenantId)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("TenantId cannot be empty.", nameof(tenantId));
        TenantId = tenantId;
    }
}
