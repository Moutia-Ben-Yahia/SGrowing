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
            .UseInMemoryDatabase(databaseName: $"MasterEnterpriseScaleDb_{Guid.NewGuid()}")
            .Options;
        return new SGrowingDbContext(options);
    }

    [Fact]
    public async Task Master_Enterprise_Scale_Commercial_Operations_E2E_Lifecycle_Should_Pass()
    {
        var db = CreateDbContext();
        var tenantId = Guid.NewGuid();
        var warehouseId = Guid.NewGuid();

        // 1. Initialize Services
        var affiliateService = new CreatorAffiliateService(db);
        var reverseExchangeService = new ReverseExchangeService(db);
        var crossBorderService = new CrossBorderTradeService(db);
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

        // 4. Configure Cross-Border Trade & Customs for Algeria
        await crossBorderService.ConfigureCountryAsync(tenantId, CountryTradeConfig.Create(tenantId, "DZ", "Algeria", "DZD", "+213", 9, 19.0m, 600.0m, true));
        await crossBorderService.SetFxRateAsync(tenantId, "EUR", "DZD", 145.0m);

        // Customer in Algiers places Order
        string normalizedPhone = crossBorderService.NormalizePhoneNumber("0550998877", "DZ");
        Assert.Equal("+213550998877", normalizedPhone);

        var order = CodOrder.Create(
            tenantId,
            "ORD-DZ-991",
            "Yacine Brahimi",
            normalizedPhone,
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

        // Generate Customs Commercial Invoice
        var customsInvoice = await crossBorderService.GenerateCustomsCommercialInvoiceAsync(
            tenantId,
            order.Id,
            "DZ",
            new List<CustomsInvoiceItem>
            {
                new CustomsInvoiceItem
                {
                    HsTariffCode = "6403.99",
                    Description = "Leather Sahara Boots",
                    Quantity = 1,
                    DeclaredUnitValue = 45.0m,
                    DeclaredTotalValue = 45.0m,
                    CountryOfOrigin = "TN"
                }
            },
            currency: "EUR");
        Assert.StartsWith("EXP-", customsInvoice.InvoiceNumber);

        // 5. Courier confirms Delivery in Algiers -> Escrow Clears
        order.Status = CodOrderStatus.Delivered;
        await affiliateService.ClearEscrowOnDeliveryAsync(tenantId, order.Id);

        var creatorSummary = await affiliateService.GetCreatorSummaryAsync(tenantId, creator.Id);
        Assert.Equal(60.0m, creatorSummary.TotalEarnedCommission);
        Assert.Equal(60.0m, creatorSummary.AvailableForPayoutBalance);

        // 6. Reverse Logistics: Customer requests Drop-and-Swap (Size 42 -> 43)
        var boot42 = new WarehouseInventory { Id = Guid.NewGuid(), TenantId = tenantId, WarehouseId = warehouseId, Sku = "BOOT-SAHARA-42", PhysicalStock = 0 };
        var boot43 = new WarehouseInventory { Id = Guid.NewGuid(), TenantId = tenantId, WarehouseId = warehouseId, Sku = "BOOT-SAHARA-43", PhysicalStock = 5 };
        db.WarehouseInventories.AddRange(boot42, boot43);
        await db.SaveChangesAsync();

        var exchangeReq = await reverseExchangeService.RequestExchangeAsync(
            tenantId,
            order.Id,
            "BOOT-SAHARA-42",
            "BOOT-SAHARA-43",
            "Sahara Leather Boot 43",
            "Tight fit around toes");

        var swapManifest = await reverseExchangeService.ApproveAndDispatchSwapManifestAsync(
            tenantId,
            exchangeReq.Id,
            Guid.NewGuid(),
            "Yalidine Swap Express",
            swapDeliveryFee: 12.0m);

        await reverseExchangeService.ConfirmDoorstepSwapAsync(tenantId, swapManifest.Id, true, true);
        await reverseExchangeService.ProcessWarehouseReturnIntakeAsync(tenantId, exchangeReq.Id, SwapInspectionResult.GradeA_RestockedSellable, "Good condition returned");

        Assert.Equal(1, boot42.PhysicalStock); // Returned 42 restocked
        Assert.Equal(4, boot43.PhysicalStock); // Dispatched 43 deducted

        // 7. Demand Forecasting & Automated Restock Matrix
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

        // 8. Call Center Agent Security & PII Protection
        var maskedOrder = await securityService.GetMaskedCustomerOrderAsync(tenantId, order.Id, UserSystemRole.CallCenterAgent);
        Assert.Contains("****", maskedOrder.DisplayPhoneNumber);
        Assert.StartsWith("DIAL-", maskedOrder.ClickToDialToken);

        // Audit Trail
        await securityService.RecordAuditLogAsync(tenantId, Guid.NewGuid(), "agent@callcenter.tn", "CallCenterAgent", "ClickToDial", "Order", order.Id.ToString(), "Agent dialed customer via token");
        var auditLogs = await securityService.ListAuditLogsAsync(tenantId);
        Assert.NotEmpty(auditLogs);

        // 9. Finance Payout Settlement for Creator Amira
        var payouts = await affiliateService.GeneratePayoutBatchAsync(tenantId);
        Assert.Single(payouts);
        var payout = payouts[0];
        Assert.Equal(60.0m, payout.Amount);

        await affiliateService.ExecutePayoutAsync(tenantId, payout.Id, "TXN-BANK-ALGERIE-001");
        var finalCreator = await affiliateService.GetCreatorSummaryAsync(tenantId, creator.Id);
        Assert.Equal(60.0m, finalCreator.PaidOutCommission);
        Assert.Equal(0m, finalCreator.AvailableForPayoutBalance);
    }
}
