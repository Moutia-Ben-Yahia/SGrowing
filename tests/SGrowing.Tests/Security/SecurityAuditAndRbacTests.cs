using Microsoft.EntityFrameworkCore;
using SGrowing.Api.Data;
using SGrowing.Api.Modules.Security;
using SGrowing.Core.Entities;
using Xunit;

namespace SGrowing.Tests.Security;

public class SecurityAuditAndRbacTests
{
    private SGrowingDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<SGrowingDbContext>()
            .UseInMemoryDatabase(databaseName: $"SecurityAuditRbacDb_{Guid.NewGuid()}")
            .Options;
        return new SGrowingDbContext(options);
    }

    [Fact]
    public async Task Rbac_PiiMasking_ClickToDial_And_AuditLogging_Should_Succeed()
    {
        var db = CreateDbContext();
        var service = new SecurityAuditAndRbacService(db);
        var tenantId = Guid.NewGuid();

        // 1. Initialize Default Roles
        var roles = await service.InitializeDefaultRolesAsync(tenantId);
        Assert.Equal(6, roles.Count);

        // 2. Permission Evaluation: CallCenterAgent cannot Export or ViewRawPii
        bool agentCanViewPii = await service.EvaluatePermissionAsync(tenantId, UserSystemRole.CallCenterAgent, "Orders", "ViewRawPii");
        bool agentCanExport = await service.EvaluatePermissionAsync(tenantId, UserSystemRole.CallCenterAgent, "Orders", "ExportData");
        bool superAdminCanExport = await service.EvaluatePermissionAsync(tenantId, UserSystemRole.SuperAdmin, "Orders", "ExportData");

        Assert.False(agentCanViewPii);
        Assert.False(agentCanExport);
        Assert.True(superAdminCanExport);

        // 3. Create a Customer Order with real PII
        var order = CodOrder.Create(
            tenantId,
            "ORD-TUNIS-771",
            "Mouna Trabelsi",
            "+21698123456",
            "14 Rue Ibn Khaldoun, Appt 4B",
            "Tunis",
            "Centre Ville",
            145.0m);
        db.CodOrders.Add(order);
        await db.SaveChangesAsync();

        // 4. Call Center Agent View -> Phone and Address are Masked, Click-to-dial token is present
        var agentView = await service.GetMaskedCustomerOrderAsync(tenantId, order.Id, UserSystemRole.CallCenterAgent);
        Assert.Contains("****", agentView.DisplayPhoneNumber);
        Assert.DoesNotContain("1234", agentView.DisplayPhoneNumber);
        Assert.Contains("***, Tunis", agentView.DisplayAddress);
        Assert.StartsWith("DIAL-", agentView.ClickToDialToken);

        // 5. SuperAdmin View -> Raw PII visible
        var adminView = await service.GetMaskedCustomerOrderAsync(tenantId, order.Id, UserSystemRole.SuperAdmin);
        Assert.Equal("+21698123456", adminView.DisplayPhoneNumber);
        Assert.Contains("14 Rue Ibn Khaldoun", adminView.DisplayAddress);

        // 6. Security Audit Trail Logging
        var audit = await service.RecordAuditLogAsync(
            tenantId,
            userId: Guid.NewGuid(),
            email: "admin@sgrowing.tn",
            role: "SuperAdmin",
            action: "ExportCustomerCsv",
            resource: "CustomerOrders",
            resourceId: order.Id.ToString(),
            details: "Full CSV customer export requested for marketing sync",
            ipAddress: "197.1.2.3");

        Assert.NotNull(audit);
        Assert.Equal("ExportCustomerCsv", audit.Action);

        var logs = await service.ListAuditLogsAsync(tenantId, resource: "CustomerOrders");
        Assert.Single(logs);
        Assert.Equal("197.1.2.3", logs[0].IpAddress);
    }
}
