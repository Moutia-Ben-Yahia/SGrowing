using Microsoft.EntityFrameworkCore;
using SGrowing.Api.Data;
using SGrowing.Api.Modules.Voice;
using SGrowing.Core.Entities;
using Xunit;

namespace SGrowing.Tests.Voice;

public class AutomatedVoiceDialerTests
{
    private SGrowingDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<SGrowingDbContext>()
            .UseInMemoryDatabase(databaseName: $"VoiceDialerTestDb_{Guid.NewGuid()}")
            .Options;
        return new SGrowingDbContext(options);
    }

    [Fact]
    public async Task VoiceDialer_Should_Queue_Attempt_And_Confirm_Order_On_Spoken_Yes()
    {
        var db = CreateDbContext();
        var dialer = new AutomatedVoiceDialerService(db);
        var tenantId = Guid.NewGuid();
        var warehouseId = Guid.NewGuid();

        // 1. Seed Defensive WMS Inventory and Order
        var inv = new WarehouseInventory
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            WarehouseId = warehouseId,
            Sku = "PERF-OUD-100ML",
            PhysicalStock = 10,
            ReservedStock = 1,
            CommittedStock = 0
        };
        db.WarehouseInventories.Add(inv);

        var order = CodOrder.Create(
            tenantId: tenantId,
            orderNumber: "ORD-TN-9901",
            customerName: "Mohamed Ali",
            phoneNumber: "+21620123456",
            shippingAddress: "Avenue Habib Bourguiba",
            governorateOrWilaya: "Tunis",
            delegationOrCommune: "Tunis Centre",
            totalAmount: 120.0m);
        db.CodOrders.Add(order);
        await db.SaveChangesAsync();

        // 2. Queue order for automated dialout
        var attempt = await dialer.QueueOrderForVoiceConfirmationAsync(tenantId, order.Id);
        Assert.NotNull(attempt);
        Assert.Equal(IvrCallState.Queued, attempt.State);
        Assert.Equal(1, attempt.AttemptNumber);

        // 3. Verify IVR Script generation in Tunisian Derja
        var script = dialer.GenerateIvrScript(order, "ar-TN");
        Assert.Contains("Mohamed Ali", script);
        Assert.Contains("ORD-TN-9901", script);
        Assert.Contains("120.00", script);
        Assert.Contains("Tunis", script);

        // 4. Simulate Spoken Confirmation ("نعم نأكد")
        var processedAttempt = await dialer.ProcessIvrCustomerResponseAsync(tenantId, attempt.Id, "نعم نأكد الطلب", null);

        Assert.Equal(IvrCallState.Confirmed, processedAttempt.State);
        Assert.Equal(IvrSpokenIntent.Confirmed, processedAttempt.SpokenIntent);

        // Verify Order updated
        var updatedOrder = await db.CodOrders.FindAsync(order.Id);
        Assert.NotNull(updatedOrder);
        Assert.Equal(CodOrderStatus.Confirmed, updatedOrder.Status);
        Assert.Equal(ConfirmationChannel.PhoneCall, updatedOrder.ConfirmationChannel);
        Assert.NotNull(updatedOrder.ConfirmedAtUtc);

        // Verify Defensive WMS promoted stock from Reserved to Committed
        var updatedInv = await db.WarehouseInventories.FindAsync(inv.Id);
        Assert.NotNull(updatedInv);
        Assert.Equal(0, updatedInv.ReservedStock);
        Assert.Equal(1, updatedInv.CommittedStock);
    }

    [Fact]
    public async Task VoiceDialer_Should_Cancel_Order_And_Release_Stock_On_Dtmf_Three()
    {
        var db = CreateDbContext();
        var dialer = new AutomatedVoiceDialerService(db);
        var tenantId = Guid.NewGuid();

        var inv = new WarehouseInventory
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            WarehouseId = Guid.NewGuid(),
            Sku = "ROBE-SOIE-ROUGE",
            PhysicalStock = 5,
            ReservedStock = 1,
            CommittedStock = 0
        };
        db.WarehouseInventories.Add(inv);

        var order = CodOrder.Create(
            tenantId: tenantId,
            orderNumber: "ORD-TN-9902",
            customerName: "Sonia Mansour",
            phoneNumber: "+21650987654",
            shippingAddress: "Route de Skanes",
            governorateOrWilaya: "Monastir",
            delegationOrCommune: "Skanes",
            totalAmount: 85.0m);
        db.CodOrders.Add(order);
        await db.SaveChangesAsync();

        var attempt = await dialer.QueueOrderForVoiceConfirmationAsync(tenantId, order.Id);

        // Simulate customer pressing key '3' (Cancel)
        var processedAttempt = await dialer.ProcessIvrCustomerResponseAsync(tenantId, attempt.Id, null, "3");

        Assert.Equal(IvrCallState.Cancelled, processedAttempt.State);
        Assert.Equal(IvrSpokenIntent.Cancelled, processedAttempt.SpokenIntent);

        var updatedOrder = await db.CodOrders.FindAsync(order.Id);
        Assert.NotNull(updatedOrder);
        Assert.Equal(CodOrderStatus.Cancelled, updatedOrder.Status);

        // Verify Defensive WMS released reserved stock back
        var updatedInv = await db.WarehouseInventories.FindAsync(inv.Id);
        Assert.NotNull(updatedInv);
        Assert.Equal(0, updatedInv.ReservedStock);
    }
}
