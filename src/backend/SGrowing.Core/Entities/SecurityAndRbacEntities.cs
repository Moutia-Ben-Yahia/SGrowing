namespace SGrowing.Core.Entities;

public enum UserSystemRole
{
    SuperAdmin,
    OperationsManager,
    CallCenterAgent,
    WarehousePicker,
    FinanceAuditor,
    AffiliateCreator
}

public class SystemRole
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public string RoleName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool CanViewRawPii { get; set; } = false;
    public bool CanExportData { get; set; } = false;
    public bool CanOverridePricing { get; set; } = false;
    public bool CanApproveDisbursements { get; set; } = false;
    public bool IsSystemDefault { get; set; } = false;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public static SystemRole CreateDefault(Guid tenantId, UserSystemRole role)
    {
        return role switch
        {
            UserSystemRole.SuperAdmin => new SystemRole
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                RoleName = "SuperAdmin",
                Description = "Full administrative system access with unmasked PII and financial approval",
                CanViewRawPii = true,
                CanExportData = true,
                CanOverridePricing = true,
                CanApproveDisbursements = true,
                IsSystemDefault = true
            },
            UserSystemRole.OperationsManager => new SystemRole
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                RoleName = "OperationsManager",
                Description = "Oversees warehouse dispatch, courier routing and returns",
                CanViewRawPii = true,
                CanExportData = true,
                CanOverridePricing = true,
                CanApproveDisbursements = false,
                IsSystemDefault = true
            },
            UserSystemRole.CallCenterAgent => new SystemRole
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                RoleName = "CallCenterAgent",
                Description = "Customer confirmation agent; phone and address PII strictly masked with click-to-dial",
                CanViewRawPii = false,
                CanExportData = false,
                CanOverridePricing = false,
                CanApproveDisbursements = false,
                IsSystemDefault = true
            },
            UserSystemRole.WarehousePicker => new SystemRole
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                RoleName = "WarehousePicker",
                Description = "Barcoding, picking, and packing without customer PII or financial access",
                CanViewRawPii = false,
                CanExportData = false,
                CanOverridePricing = false,
                CanApproveDisbursements = false,
                IsSystemDefault = true
            },
            UserSystemRole.FinanceAuditor => new SystemRole
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                RoleName = "FinanceAuditor",
                Description = "Courier float reconciliation, affiliate payouts, and financial reporting",
                CanViewRawPii = false,
                CanExportData = true,
                CanOverridePricing = false,
                CanApproveDisbursements = true,
                IsSystemDefault = true
            },
            UserSystemRole.AffiliateCreator => new SystemRole
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                RoleName = "AffiliateCreator",
                Description = "External creator portal; view referral links, clicks, and cleared commissions",
                CanViewRawPii = false,
                CanExportData = false,
                CanOverridePricing = false,
                CanApproveDisbursements = false,
                IsSystemDefault = true
            },
            _ => throw new ArgumentOutOfRangeException(nameof(role))
        };
    }
}

public class RolePermissionClaim
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid RoleId { get; set; }
    public string Resource { get; set; } = string.Empty; // "Orders", "Customers", "Payouts", "Inventory"
    public string Action { get; set; } = string.Empty;   // "Read", "Write", "Export", "Approve"
    public bool IsAllowed { get; set; } = true;
}

public class SecurityAuditLogEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid UserId { get; set; }
    public string UserEmail { get; set; } = string.Empty;
    public string UserRole { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty; // "ExportCsv", "OverridePrice", "ReleaseEscrowPayout", "RevealPiiPhone"
    public string Resource { get; set; } = string.Empty; // "Order", "AffiliatePayout", "Customer"
    public string ResourceId { get; set; } = string.Empty;
    public string Details { get; set; } = string.Empty;
    public string IpAddress { get; set; } = "127.0.0.1";
    public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;

    public static SecurityAuditLogEntry Record(
        Guid tenantId,
        Guid userId,
        string email,
        string role,
        string action,
        string resource,
        string resourceId,
        string details,
        string ipAddress = "127.0.0.1")
    {
        return new SecurityAuditLogEntry
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            UserId = userId,
            UserEmail = email.Trim(),
            UserRole = role.Trim(),
            Action = action.Trim(),
            Resource = resource.Trim(),
            ResourceId = resourceId.Trim(),
            Details = details.Trim(),
            IpAddress = ipAddress.Trim(),
            TimestampUtc = DateTime.UtcNow
        };
    }
}
