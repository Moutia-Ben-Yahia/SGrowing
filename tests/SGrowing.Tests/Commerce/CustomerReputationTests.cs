using Microsoft.EntityFrameworkCore;
using SGrowing.Api.Data;
using SGrowing.Api.Modules.Commerce;
using SGrowing.Core.Entities;
using Xunit;

namespace SGrowing.Tests.Commerce;

public class CustomerReputationTests
{
    private SGrowingDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<SGrowingDbContext>()
            .UseInMemoryDatabase(databaseName: $"ReputationTestDb_{Guid.NewGuid()}")
            .Options;
        return new SGrowingDbContext(options);
    }

    [Fact]
    public async Task CustomerReputation_Should_Identify_High_Risk_And_Require_Deposit()
    {
        var db = CreateDbContext();
        var service = new CustomerReputationService(db);
        var tenantId = Guid.NewGuid();
        var customerPhone = "+21622334455";

        // Seed 3 historical orders with 1 return (refusal) and 2 deliveries
        var order1 = CodOrder.Create(tenantId, "ORD-OLD-01", "Foued Tounsi", customerPhone, "Address", "Sousse", "Sousse", 70.0m);
        order1.Status = CodOrderStatus.Delivered;
        db.CodOrders.Add(order1);

        var order2 = CodOrder.Create(tenantId, "ORD-OLD-02", "Foued Tounsi", customerPhone, "Address", "Sousse", "Sousse", 80.0m);
        order2.Status = CodOrderStatus.Delivered;
        db.CodOrders.Add(order2);

        var order3 = CodOrder.Create(tenantId, "ORD-OLD-03", "Foued Tounsi", customerPhone, "Address", "Sousse", "Sousse", 60.0m);
        order3.Status = CodOrderStatus.Returned; // Refused at door
        db.CodOrders.Add(order3);

        await db.SaveChangesAsync();

        // Evaluate reputation
        var evaluation = await service.EvaluateCustomerReputationAsync(tenantId, customerPhone);

        Assert.Equal(customerPhone, evaluation.CustomerPhone);
        Assert.Equal(1, evaluation.RefusedReturnsCount);
        Assert.True(evaluation.RequiresMicroDeposit);
        Assert.Equal(ReputationTier.HighRisk, evaluation.Tier);
        Assert.Contains("micro-deposit", evaluation.RecommendedAction);
    }

    [Fact]
    public async Task MicroDeposit_Settlement_Should_Confirm_Order_And_Deduct_From_Remaining_Cod()
    {
        var db = CreateDbContext();
        var service = new CustomerReputationService(db);
        var tenantId = Guid.NewGuid();
        var warehouseId = Guid.NewGuid();

        // 1. Seed Defensive WMS stock
        var inv = new WarehouseInventory
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            WarehouseId = warehouseId,
            Sku = "PACK-PRESTIGE-DUO",
            PhysicalStock = 10,
            ReservedStock = 1,
            CommittedStock = 0
        };
        db.WarehouseInventories.Add(inv);

        // 2. Seed pending order with 80 TND total amount
        var order = CodOrder.Create(tenantId, "ORD-NEW-55", "Sami Khemir", "+21699887766", "Nabeul", "Nabeul", "Hammamet", 80.0m);
        order.Status = CodOrderStatus.PendingConfirmation;
        db.CodOrders.Add(order);
        await db.SaveChangesAsync();

        // 3. Generate 10 TND micro-deposit link
        var link = await service.GenerateMicroDepositLinkAsync(tenantId, order.Id, "+21699887766", depositAmount: 10.0m, provider: "Flouci");
        Assert.NotNull(link);
        Assert.Equal(MicroDepositStatus.Pending, link.Status);
        Assert.Contains("https://pay.sgrowing.com/deposit/", link.PaymentUrl);

        // 4. Customer pays micro-deposit via Flouci
        var confirmedOrder = await service.RecordMicroDepositSettlementAsync(tenantId, link.Id);

        Assert.Equal(CodOrderStatus.Confirmed, confirmedOrder.Status);
        Assert.Equal(70.0m, confirmedOrder.TotalAmount); // 80 - 10 deposit = 70 TND remaining COD
        Assert.Contains("Deposit of 10.00 TND settled", confirmedOrder.Notes);

        // 5. Verify Defensive WMS promoted stock from Reserved to Committed
        var updatedInv = await db.WarehouseInventories.FindAsync(inv.Id);
        Assert.Equal(0, updatedInv!.ReservedStock);
        Assert.Equal(1, updatedInv.CommittedStock);
    }

    [Fact]
    public async Task EvaluateCustomerReputationAsync_WithNoHistoricalOrders_ReturnsBaselineStandardTier()
    {
        var db = CreateDbContext();
        var service = new CustomerReputationService(db);
        var tenantId = Guid.NewGuid();
        var phone = "+21620000000";

        var evaluation = await service.EvaluateCustomerReputationAsync(tenantId, phone);

        Assert.Equal(phone, evaluation.CustomerPhone);
        Assert.Equal(70, evaluation.ReputationScore);
        Assert.Equal(ReputationTier.Standard, evaluation.Tier);
        Assert.False(evaluation.RequiresMicroDeposit);
        Assert.Contains("Standard Customer", evaluation.RecommendedAction);
    }

    [Fact]
    public async Task EvaluateCustomerReputationAsync_WithMultipleDeliveredOrdersAndZeroReturns_ReturnsTrustedTier()
    {
        var db = CreateDbContext();
        var service = new CustomerReputationService(db);
        var tenantId = Guid.NewGuid();
        var phone = "+21699000111";

        // Seed 5 delivered orders with substantial spend ($500)
        for (int i = 0; i < 5; i++)
        {
            var ord = CodOrder.Create(tenantId, $"ORD-VIP-{i}", "VIP Buyer", phone, "Street", "Tunis", "Tunis", 100m);
            ord.Status = CodOrderStatus.Delivered;
            db.CodOrders.Add(ord);
        }
        await db.SaveChangesAsync();

        var evaluation = await service.EvaluateCustomerReputationAsync(tenantId, phone);

        Assert.True(evaluation.ReputationScore >= 80);
        Assert.Equal(ReputationTier.Trusted, evaluation.Tier);
        Assert.False(evaluation.RequiresMicroDeposit);
        Assert.Equal(100.0m, evaluation.DeliverySuccessRate);
        Assert.Contains("Trusted Customer", evaluation.RecommendedAction);
    }

    [Fact]
    public async Task EvaluateCustomerReputationAsync_WithExcessiveReturns_ReturnsBlacklistedTier()
    {
        var db = CreateDbContext();
        var service = new CustomerReputationService(db);
        var tenantId = Guid.NewGuid();
        var phone = "+21655667788";

        // Seed 4 orders where all 4 were returned/refused
        for (int i = 0; i < 4; i++)
        {
            var ord = CodOrder.Create(tenantId, $"ORD-BAD-{i}", "Serial Rejector", phone, "Street", "Bizerte", "Bizerte", 50m);
            ord.Status = CodOrderStatus.Returned;
            db.CodOrders.Add(ord);
        }
        await db.SaveChangesAsync();

        var evaluation = await service.EvaluateCustomerReputationAsync(tenantId, phone);

        Assert.True(evaluation.ReputationScore < 20);
        Assert.Equal(ReputationTier.Blacklisted, evaluation.Tier);
        Assert.True(evaluation.RequiresMicroDeposit);
        Assert.Equal(0m, evaluation.DeliverySuccessRate);
        Assert.Contains("Blacklisted", evaluation.RecommendedAction);
    }

    [Fact]
    public async Task GenerateMicroDepositLinkAsync_WithUnknownOrderId_ThrowsInvalidOperationException()
    {
        var db = CreateDbContext();
        var service = new CustomerReputationService(db);
        var tenantId = Guid.NewGuid();
        var missingOrderId = Guid.NewGuid();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.GenerateMicroDepositLinkAsync(tenantId, missingOrderId, "+216000"));

        Assert.Contains("not found", ex.Message);
    }

    [Fact]
    public async Task RecordMicroDepositSettlementAsync_WithUnknownLinkId_ThrowsInvalidOperationException()
    {
        var db = CreateDbContext();
        var service = new CustomerReputationService(db);
        var tenantId = Guid.NewGuid();
        var missingLinkId = Guid.NewGuid();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.RecordMicroDepositSettlementAsync(tenantId, missingLinkId));

        Assert.Contains("not found", ex.Message);
    }
}
