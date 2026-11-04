using System;
using System.Collections.Generic;
using System.Threading.Tasks;
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

    [Fact]
    public async Task InitializeDefaultRoles_Should_Be_Idempotent()
    {
        var db = CreateDbContext();
        var service = new SecurityAuditAndRbacService(db);
        var tenantId = Guid.NewGuid();

        var firstCall = await service.InitializeDefaultRolesAsync(tenantId);
        var secondCall = await service.InitializeDefaultRolesAsync(tenantId);

        Assert.Equal(6, firstCall.Count);
        Assert.Equal(6, secondCall.Count);
        Assert.Equal(6, await db.SystemRoles.CountAsync(r => r.TenantId == tenantId));
    }

    [Theory]
    [InlineData(null, true, "")]
    [InlineData("", false, "")]
    [InlineData("   ", false, "")]
    [InlineData("123", false, "****")]
    [InlineData("+21698123456", true, "+21698123456")]
    public void MaskPhoneNumber_HandlesVariousFormats(string? raw, bool canViewRaw, string expected)
    {
        var db = CreateDbContext();
        var service = new SecurityAuditAndRbacService(db);

        var result = service.MaskPhoneNumber(raw!, canViewRaw);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void MaskAddress_HandlesSingleAndMultiPartAddresses()
    {
        var db = CreateDbContext();
        var service = new SecurityAuditAndRbacService(db);

        // Multi part with comma
        var maskedMulti = service.MaskAddress("Avenue Habib Bourguiba, Tunis", false);
        Assert.Equal("***, Tunis", maskedMulti);

        // Single part without comma
        var maskedSingle = service.MaskAddress("Avenue Habib Bourguiba", false);
        Assert.Equal("*** [Protected Delivery Address]", maskedSingle);

        // Allowed raw view
        var unmasked = service.MaskAddress("Avenue Habib Bourguiba, Tunis", true);
        Assert.Equal("Avenue Habib Bourguiba, Tunis", unmasked);

        // Null / whitespace
        Assert.Equal(string.Empty, service.MaskAddress("", false));
    }

    [Fact]
    public async Task EvaluatePermissionAsync_TestsSpecializedRoles()
    {
        var db = CreateDbContext();
        var service = new SecurityAuditAndRbacService(db);
        var tenantId = Guid.NewGuid();
        await service.InitializeDefaultRolesAsync(tenantId);

        // WarehousePicker allowed for Inventory Read/Scan
        Assert.True(await service.EvaluatePermissionAsync(tenantId, UserSystemRole.WarehousePicker, "Inventory", "Scan"));
        Assert.False(await service.EvaluatePermissionAsync(tenantId, UserSystemRole.WarehousePicker, "Ledger", "Read"));

        // FinanceAuditor allowed for Treasury and Ledger
        Assert.True(await service.EvaluatePermissionAsync(tenantId, UserSystemRole.FinanceAuditor, "Ledger", "Read"));
        Assert.False(await service.EvaluatePermissionAsync(tenantId, UserSystemRole.FinanceAuditor, "Inventory", "Scan"));

        // OperationsManager allowed for Couriers and Exchanges
        Assert.True(await service.EvaluatePermissionAsync(tenantId, UserSystemRole.OperationsManager, "Couriers", "Read"));
    }

    [Fact]
    public async Task GetMaskedCustomerOrderAsync_ThrowsWhenOrderNotFound()
    {
        var db = CreateDbContext();
        var service = new SecurityAuditAndRbacService(db);
        var tenantId = Guid.NewGuid();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.GetMaskedCustomerOrderAsync(tenantId, Guid.NewGuid(), UserSystemRole.SuperAdmin));
    }

    [Fact]
    public async Task ListAuditLogsAsync_EnforcesTenantIsolationAndLimit()
    {
        var db = CreateDbContext();
        var service = new SecurityAuditAndRbacService(db);
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();

        for (int i = 0; i < 5; i++)
        {
            await service.RecordAuditLogAsync(tenantA, Guid.NewGuid(), "a@test.com", "Admin", $"Action_{i}", "Orders", $"ORD_{i}", "detail");
        }
        await service.RecordAuditLogAsync(tenantB, Guid.NewGuid(), "b@test.com", "Admin", "Action_B", "Orders", "ORD_B", "detail");

        var logsA = await service.ListAuditLogsAsync(tenantA, limit: 3);
        Assert.Equal(3, logsA.Count);
        Assert.All(logsA, l => Assert.Equal(tenantA, l.TenantId));

        var logsB = await service.ListAuditLogsAsync(tenantB);
        Assert.Single(logsB);
        Assert.Equal(tenantB, logsB[0].TenantId);
    }
}
