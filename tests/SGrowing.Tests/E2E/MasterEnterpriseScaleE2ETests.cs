using Microsoft.EntityFrameworkCore;
using SGrowing.Api.Data;
using SGrowing.Api.Modules.Commerce;
using SGrowing.Api.Modules.Marketing;
using SGrowing.Api.Modules.Security;
using SGrowing.Core.Entities;
using Xunit;

namespace SGrowing.Tests.E2E;

public class MasterEnterpriseScaleE2ETests
{
    private SGrowingDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<SGrowingDbContext>()
            .UseInMemoryDatabase(databaseName: $"MasterEnterpriseScaleE2EDb_{Guid.NewGuid()}")
            .Options;
        return new SGrowingDbContext(options);
    }

    [Fact]
    public async Task Master_Enterprise_Scale_Commercial_Operations_E2E_Lifecycle_Should_Pass()
    {
        var db = CreateDbContext();
        var tenantId = Guid.NewGuid();

        // 1. Initialize Services
        var affiliateService = new CreatorAffiliateService(db);
        var demandForecastingService = new DemandForecastingService(db);
        var securityService = new SecurityAuditAndRbacService(db);

        // 2. Setup RBAC and Roles
        await securityService.InitializeDefaultRolesAsync(tenantId);

        // 3. Register Influencer & Referral Code (Amira, 10% commission)
        var creator = CreatorProfile.Create(
            tenantId,
            fullName: "Amira Lifestyle",
            handle: "@amiralife",
            email: "amira@amiralife.tn",
            phone: "+21698776655",
            platform: CreatorPlatform.Instagram,
            commissionType: CommissionType.Percentage,
            commissionRate: 10.0m,
            minPayoutThreshold: 50.0m,
            paymentMethod: "BankTransfer",
            payoutDetails: "TN5910006035183492817342");
        await affiliateService.RegisterCreatorAsync(tenantId, creator);
        var refCode = await affiliateService.CreateReferralCodeAsync(tenantId, creator.Id, "AMIRA_VIP", "Percentage", 10.0m);
        Assert.Equal("AMIRA_VIP", refCode.Code);

        // Customer places Order
        var order = CodOrder.Create(
            tenantId,
            "ORD-DZ-991",
            "Yacine Brahimi",
            "+213550998877",
            "12 Rue Didouche Mourad",
            "Alger",
            "Sidi M'Hamed",
            600.0m,
            itemsJson: "[{\"sku\":\"BOOT-SAHARA-42\",\"title\":\"Sahara Leather Boot 42\",\"quantity\":1}]");
        order.Status = CodOrderStatus.Dispatched;
        db.CodOrders.Add(order);
        await db.SaveChangesAsync();

        // Attribute commission to Amira (Held in COD safety escrow)
        var escrowLedger = await affiliateService.AttributeOrderCommissionAsync(tenantId, "AMIRA_VIP", order.Id, "TRK-DZ-001", 600.0m);
        Assert.NotNull(escrowLedger);
        Assert.Equal(EscrowStatus.EscrowHold, escrowLedger.Status);
        Assert.Equal(60.0m, escrowLedger.CommissionAmount);

        // 4. Courier confirms Delivery in Algiers -> Escrow Clears
        order.Status = CodOrderStatus.Delivered;
        await affiliateService.ClearEscrowOnDeliveryAsync(tenantId, order.Id);

        var creatorSummary = await affiliateService.GetCreatorSummaryAsync(tenantId, creator.Id);
        Assert.Equal(60.0m, creatorSummary.TotalEarnedCommission);
        Assert.Equal(60.0m, creatorSummary.AvailableForPayoutBalance);

        // 5. Demand Forecasting & Automated Restock Matrix
        var velocity = await demandForecastingService.CalculateProductVelocityAsync(
            tenantId,
            Guid.NewGuid(),
            "BOOT-SAHARA-43",
            "Sahara Leather Boot 43",
            trailing7dUnits: 35m, // 5 units/day
            trailing30dUnits: 120m,
            adSpendMultiplier: 2.0m); // 10 units/day projected
        Assert.Equal(10.0m, velocity.WeightedProjectedDailyDemand);

        var alert = await demandForecastingService.EvaluateStockoutAlertAsync(
            tenantId,
            Guid.NewGuid(),
            "BOOT-SAHARA-43",
            currentPhysicalStock: 4,
            currentReservedStock: 0,
            supplierLeadTimeDays: 4,
            safetyStockDays: 2,
            targetCycleDays: 10);

        Assert.Equal(RestockPriority.CriticalStockoutImminent, alert.Priority);

        var supplier = SupplierScorecard.Create(tenantId, "Kairouan Leatherworks", "factory@kairouanleather.tn", "+21677112233");
        await demandForecastingService.RegisterSupplierScorecardAsync(tenantId, supplier);

        var restockOrder = await demandForecastingService.GenerateAutomatedRestockOrderAsync(tenantId, alert.Id, supplier.Id, 22.0m);
        var approvedRestock = await demandForecastingService.ApproveRestockOrderAsync(tenantId, restockOrder.Id);
        Assert.Equal(RestockOrderStatus.PlacedWithSupplier, approvedRestock.Status);

        // 6. Call Center Agent Security & PII Protection
        var maskedOrder = await securityService.GetMaskedCustomerOrderAsync(tenantId, order.Id, UserSystemRole.CallCenterAgent);
        Assert.Contains("****", maskedOrder.DisplayPhoneNumber);
        Assert.StartsWith("DIAL-", maskedOrder.ClickToDialToken);

        // Audit Trail
        await securityService.RecordAuditLogAsync(tenantId, Guid.NewGuid(), "agent@callcenter.tn", "CallCenterAgent", "ClickToDial", "Order", order.Id.ToString(), "Agent dialed customer via token");
        var auditLogs = await securityService.ListAuditLogsAsync(tenantId);
        Assert.NotEmpty(auditLogs);

        // 7. Finance Payout Settlement for Creator Amira
        var payouts = await affiliateService.GeneratePayoutBatchAsync(tenantId);
        Assert.Single(payouts);
        var payout = payouts[0];
        Assert.Equal(60.0m, payout.Amount);

        await affiliateService.ExecutePayoutAsync(tenantId, payout.Id, "TXN-BANK-ALGERIE-001");
        var finalCreator = await affiliateService.GetCreatorSummaryAsync(tenantId, creator.Id);
        Assert.Equal(60.0m, finalCreator.PaidOutCommission);
        Assert.Equal(0m, finalCreator.AvailableForPayoutBalance);
    }

    [Fact]
    public async Task Master_Enterprise_Scale_WhenCustomerReturnsOrder_VoidsAffiliateEscrowAndProtectsFinances()
    {
        var db = CreateDbContext();
        var tenantId = Guid.NewGuid();

        var affiliateService = new CreatorAffiliateService(db);
        var securityService = new SecurityAuditAndRbacService(db);
        var demandForecastingService = new DemandForecastingService(db);

        // 1. Setup Creator & Referral Code
        var creator = CreatorProfile.Create(
            tenantId,
            fullName: "Youssef Fashion",
            handle: "@youssefstyle",
            email: "youssef@fashion.tn",
            phone: "+21655112233",
            platform: CreatorPlatform.TikTok,
            commissionType: CommissionType.Percentage,
            commissionRate: 15.0m,
            minPayoutThreshold: 50.0m,
            paymentMethod: "BankTransfer",
            payoutDetails: "TN5910006035183492817342");
        await affiliateService.RegisterCreatorAsync(tenantId, creator);
        await affiliateService.CreateReferralCodeAsync(tenantId, creator.Id, "YOUSSEF15", "Percentage", 15.0m);

        // Order placed with creator code
        var order = CodOrder.Create(
            tenantId,
            "ORD-RET-001",
            "Sami Ben Ali",
            "+21622334455",
            "Avenue Habib Bourguiba",
            "Tunis",
            "Centre Ville",
            200.0m);
        order.Status = CodOrderStatus.Dispatched;
        db.CodOrders.Add(order);
        await db.SaveChangesAsync();

        // Commission placed in escrow hold
        var ledger = await affiliateService.AttributeOrderCommissionAsync(tenantId, "YOUSSEF15", order.Id, "TRK-RET-001", 200.0m);
        Assert.NotNull(ledger);
        Assert.Equal(EscrowStatus.EscrowHold, ledger.Status);
        Assert.Equal(30.0m, ledger.CommissionAmount);

        // 2. Customer returns / cancels order -> Escrow voided
        var voided = await affiliateService.VoidEscrowOnReturnOrCancelAsync(tenantId, order.Id, "Parcel refused at delivery door");
        Assert.NotNull(voided);
        Assert.Equal(EscrowStatus.VoidedReturn, voided.Status);

        var creatorSummary = await affiliateService.GetCreatorSummaryAsync(tenantId, creator.Id);
        Assert.Equal(0m, creatorSummary.AvailableForPayoutBalance);
        Assert.Equal(0m, creatorSummary.TotalEarnedCommission);

        // Payout batch generation yields 0 payouts
        var payouts = await affiliateService.GeneratePayoutBatchAsync(tenantId);
        Assert.Empty(payouts);

        // 3. Security Service: Querying masked order on non-existent order throws InvalidOperationException
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            securityService.GetMaskedCustomerOrderAsync(tenantId, Guid.NewGuid(), UserSystemRole.CallCenterAgent));

        // 4. Demand forecasting: Approving non-existent restock order throws InvalidOperationException
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            demandForecastingService.ApproveRestockOrderAsync(tenantId, Guid.NewGuid()));
    }

    [Fact]
    public async Task Master_Enterprise_Scale_PiiMasking_And_SettlementBatch_EdgeCases()
    {
        var db = CreateDbContext();
        var tenantId = Guid.NewGuid();

        var affiliateService = new CreatorAffiliateService(db);
        var securityService = new SecurityAuditAndRbacService(db);

        // 1. PII Masking: SuperAdmin sees raw PII, CallCenterAgent sees masked PII
        var order = CodOrder.Create(
            tenantId,
            "ORD-PII-TEST",
            "Kais Saidi",
            "+21698112233",
            "12 Rue Ibn Khaldoun",
            "Ariana",
            "Ariana Ville",
            120m);
        db.CodOrders.Add(order);
        await db.SaveChangesAsync();

        var adminView = await securityService.GetMaskedCustomerOrderAsync(tenantId, order.Id, UserSystemRole.SuperAdmin);
        Assert.Equal("+21698112233", adminView.DisplayPhoneNumber);
        Assert.Contains("12 Rue Ibn Khaldoun", adminView.DisplayAddress);

        var agentView = await securityService.GetMaskedCustomerOrderAsync(tenantId, order.Id, UserSystemRole.CallCenterAgent);
        Assert.Contains("*", agentView.DisplayPhoneNumber);
        Assert.Contains("*", agentView.DisplayAddress);

        // 2. Voiding escrow on non-existent order safely returns null
        var voidNonExistent = await affiliateService.VoidEscrowOnReturnOrCancelAsync(tenantId, Guid.NewGuid(), "Cancelled");
        Assert.Null(voidNonExistent);
    }

    [Fact]
    public async Task Master_Enterprise_Scale_SecurityAuditTrail_And_RolePermissionMatrix()
    {
        var db = CreateDbContext();
        var tenantId = Guid.NewGuid();
        var securityService = new SecurityAuditAndRbacService(db);

        // 1. Initialize default system roles
        var roles = await securityService.InitializeDefaultRolesAsync(tenantId);
        Assert.Equal(6, roles.Count);

        // 2. Evaluate RBAC permissions
        var canAdminExport = await securityService.EvaluatePermissionAsync(tenantId, UserSystemRole.SuperAdmin, "Finance", "Export");
        Assert.True(canAdminExport);

        var canPickerApprovePayout = await securityService.EvaluatePermissionAsync(tenantId, UserSystemRole.WarehousePicker, "Finance", "ExecutePayout");
        Assert.False(canPickerApprovePayout);

        // 3. Record security audit log entry
        var userId = Guid.NewGuid();
        var auditEntry = await securityService.RecordAuditLogAsync(
            tenantId,
            userId,
            "security.officer@sgrowing.io",
            "SuperAdmin",
            "ApiKeyRotated",
            "DeveloperWebhookCredentials",
            "key-9912",
            "Rotated production webhook HMAC secret key due to quarterly policy",
            "10.0.0.42"
        );
        Assert.NotNull(auditEntry);
        Assert.Equal("ApiKeyRotated", auditEntry.Action);

        // 4. List and verify audit trail
        var logs = await securityService.ListAuditLogsAsync(tenantId, limit: 10);
        Assert.Single(logs);
        Assert.Equal("DeveloperWebhookCredentials", logs[0].Resource);
        Assert.Equal("10.0.0.42", logs[0].IpAddress);
    }
}
