using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using SGrowing.Api.Data;
using SGrowing.Core.Entities;

namespace SGrowing.Api.Modules.Security;

public record MaskedCustomerOrderView(
    Guid OrderId,
    string OrderNumber,
    string CustomerName,
    string DisplayPhoneNumber,
    string DisplayAddress,
    string ClickToDialToken,
    decimal TotalAmount,
    string Status
);

public interface ISecurityAuditAndRbacService
{
    Task<List<SystemRole>> InitializeDefaultRolesAsync(Guid tenantId);
    Task<bool> EvaluatePermissionAsync(Guid tenantId, UserSystemRole role, string resource, string action);
    string MaskPhoneNumber(string rawPhone, bool canViewRawPii);
    string MaskAddress(string rawAddress, bool canViewRawPii);
    string GenerateClickToDialToken(Guid tenantId, Guid orderId, string rawPhone);
    Task<SecurityAuditLogEntry> RecordAuditLogAsync(
        Guid tenantId,
        Guid userId,
        string email,
        string role,
        string action,
        string resource,
        string resourceId,
        string details,
        string ipAddress = "127.0.0.1");
    Task<List<SecurityAuditLogEntry>> ListAuditLogsAsync(Guid tenantId, string? resource = null, int limit = 50);
    Task<MaskedCustomerOrderView> GetMaskedCustomerOrderAsync(Guid tenantId, Guid orderId, UserSystemRole userRole);
}

public class SecurityAuditAndRbacService : ISecurityAuditAndRbacService
{
    private readonly SGrowingDbContext _db;

    public SecurityAuditAndRbacService(SGrowingDbContext db)
    {
        _db = db;
    }

    public async Task<List<SystemRole>> InitializeDefaultRolesAsync(Guid tenantId)
    {
        var existing = await _db.SystemRoles.Where(r => r.TenantId == tenantId).ToListAsync();
        if (existing.Count > 0) return existing;

        var roles = new List<SystemRole>
        {
            SystemRole.CreateDefault(tenantId, UserSystemRole.SuperAdmin),
            SystemRole.CreateDefault(tenantId, UserSystemRole.OperationsManager),
            SystemRole.CreateDefault(tenantId, UserSystemRole.CallCenterAgent),
            SystemRole.CreateDefault(tenantId, UserSystemRole.WarehousePicker),
            SystemRole.CreateDefault(tenantId, UserSystemRole.FinanceAuditor),
            SystemRole.CreateDefault(tenantId, UserSystemRole.AffiliateCreator)
        };

        _db.SystemRoles.AddRange(roles);
        await _db.SaveChangesAsync();
        return roles;
    }

    public async Task<bool> EvaluatePermissionAsync(Guid tenantId, UserSystemRole role, string resource, string action)
    {
        // SuperAdmin has full unrestricted access
        if (role == UserSystemRole.SuperAdmin) return true;

        var roleEntity = await _db.SystemRoles
            .FirstOrDefaultAsync(r => r.TenantId == tenantId && r.RoleName == role.ToString());

        // Check high-level role properties
        if (action == "ViewRawPii") return roleEntity?.CanViewRawPii ?? false;
        if (action == "ExportData") return roleEntity?.CanExportData ?? false;
        if (action == "OverridePricing") return roleEntity?.CanOverridePricing ?? false;
        if (action == "ApproveDisbursements") return roleEntity?.CanApproveDisbursements ?? false;

        // Resource-specific matrix
        return role switch
        {
            UserSystemRole.OperationsManager => resource is "Orders" or "Inventory" or "Couriers" or "Exchanges",
            UserSystemRole.CallCenterAgent => (resource is "Orders" or "Exchanges") && action is "Read" or "Confirm",
            UserSystemRole.WarehousePicker => resource is "Inventory" or "Barcodes" or "Exchanges" && action is "Read" or "Scan",
            UserSystemRole.FinanceAuditor => resource is "Ledger" or "Payouts" or "Treasury" or "Couriers",
            UserSystemRole.AffiliateCreator => resource is "CreatorEscrow" && action is "Read",
            _ => false
        };
    }

    public string MaskPhoneNumber(string rawPhone, bool canViewRawPii)
    {
        if (string.IsNullOrWhiteSpace(rawPhone)) return string.Empty;
        if (canViewRawPii) return rawPhone;

        string digitsOnly = Regex.Replace(rawPhone, @"\D", "");
        if (digitsOnly.Length <= 4) return "****";

        // Keep prefix (first 5 digits including country code) and suffix (last 2 digits), mask middle
        string prefix = digitsOnly[..Math.Min(5, digitsOnly.Length - 2)];
        string suffix = digitsOnly[^2..];
        return $"+{prefix}****{suffix}";
    }

    public string MaskAddress(string rawAddress, bool canViewRawPii)
    {
        if (string.IsNullOrWhiteSpace(rawAddress)) return string.Empty;
        if (canViewRawPii) return rawAddress;

        // Mask exact street and house number for agents to prevent customer harassment/theft
        var parts = rawAddress.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length > 1)
        {
            return $"***, {parts[^1]}";
        }
        return "*** [Protected Delivery Address]";
    }

    public string GenerateClickToDialToken(Guid tenantId, Guid orderId, string rawPhone)
    {
        var raw = $"{tenantId}_{orderId}_{rawPhone}_{DateTime.UtcNow.Ticks}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
        return $"DIAL-{Convert.ToHexString(hash)[..12]}";
    }

    public async Task<SecurityAuditLogEntry> RecordAuditLogAsync(
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
        var log = SecurityAuditLogEntry.Record(
            tenantId,
            userId,
            email,
            role,
            action,
            resource,
            resourceId,
            details,
            ipAddress);

        _db.SecurityAuditLogEntries.Add(log);
        await _db.SaveChangesAsync();
        return log;
    }

    public async Task<List<SecurityAuditLogEntry>> ListAuditLogsAsync(Guid tenantId, string? resource = null, int limit = 50)
    {
        var query = _db.SecurityAuditLogEntries.Where(l => l.TenantId == tenantId);
        if (!string.IsNullOrWhiteSpace(resource))
        {
            query = query.Where(l => l.Resource == resource);
        }
        return await query
            .OrderByDescending(l => l.TimestampUtc)
            .Take(limit)
            .ToListAsync();
    }

    public async Task<MaskedCustomerOrderView> GetMaskedCustomerOrderAsync(Guid tenantId, Guid orderId, UserSystemRole userRole)
    {
        var order = await _db.CodOrders
            .FirstOrDefaultAsync(o => o.TenantId == tenantId && o.Id == orderId);

        if (order == null)
            throw new InvalidOperationException($"Order {orderId} not found.");

        bool canViewPii = await EvaluatePermissionAsync(tenantId, userRole, "Orders", "ViewRawPii");

        string displayPhone = MaskPhoneNumber(order.PhoneNumber, canViewPii);
        string displayAddress = MaskAddress($"{order.ShippingAddress}, {order.GovernorateOrWilaya}", canViewPii);
        string dialToken = GenerateClickToDialToken(tenantId, orderId, order.PhoneNumber);

        return new MaskedCustomerOrderView(
            order.Id,
            order.OrderNumber,
            order.CustomerName,
            displayPhone,
            displayAddress,
            dialToken,
            order.TotalAmount,
            order.Status.ToString()
        );
    }
}
