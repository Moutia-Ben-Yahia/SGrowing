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

    [Fact]
    public async Task QueueOrderForVoiceConfirmationAsync_WithUnknownOrder_ThrowsInvalidOperationException()
    {
        var db = CreateDbContext();
        var dialer = new AutomatedVoiceDialerService(db);
        var tenantId = Guid.NewGuid();
        var nonExistentOrderId = Guid.NewGuid();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            dialer.QueueOrderForVoiceConfirmationAsync(tenantId, nonExistentOrderId));

        Assert.Contains("not found", ex.Message);
    }

    [Fact]
    public void GenerateIvrScript_WithFrenchAndEnglishLanguages_ShouldGenerateLocalizedScripts()
    {
        var db = CreateDbContext();
        var dialer = new AutomatedVoiceDialerService(db);
        var tenantId = Guid.NewGuid();

        var order = CodOrder.Create(
            tenantId: tenantId,
            orderNumber: "ORD-INTL-101",
            customerName: "Claire Dupont",
            phoneNumber: "+33612345678",
            shippingAddress: "15 Rue de la Paix",
            governorateOrWilaya: "Paris",
            delegationOrCommune: "75001",
            totalAmount: 150.0m);

        var scriptFr = dialer.GenerateIvrScript(order, "fr-FR");
        Assert.Contains("Bonjour Claire Dupont", scriptFr);
        Assert.Contains("ORD-INTL-101", scriptFr);
        Assert.Contains("tapez 1", scriptFr);

        var scriptEn = dialer.GenerateIvrScript(order, "en-US");
        Assert.Contains("Hello Claire Dupont", scriptEn);
        Assert.Contains("press 1 to confirm", scriptEn);
    }

    [Fact]
    public async Task ProcessIvrCustomerResponseAsync_WhenCustomerPostpones_ShouldSetPostponedStateAndNote()
    {
        var db = CreateDbContext();
        var dialer = new AutomatedVoiceDialerService(db);
        var tenantId = Guid.NewGuid();

        var order = CodOrder.Create(
            tenantId: tenantId,
            orderNumber: "ORD-POSTPONE-01",
            customerName: "Yassine Trabelsi",
            phoneNumber: "+21698765432",
            shippingAddress: "Ennasr 2",
            governorateOrWilaya: "Ariana",
            delegationOrCommune: "Ariana Ville",
            totalAmount: 95.0m);
        db.CodOrders.Add(order);
        await db.SaveChangesAsync();

        var attempt = await dialer.QueueOrderForVoiceConfirmationAsync(tenantId, order.Id);

        // Customer responds in French "reporter svp"
        var processed = await dialer.ProcessIvrCustomerResponseAsync(tenantId, attempt.Id, "reporter svp", null);

        Assert.Equal(IvrCallState.Postponed, processed.State);
        Assert.Equal(IvrSpokenIntent.Postponed, processed.SpokenIntent);

        var updatedOrder = await db.CodOrders.FindAsync(order.Id);
        Assert.NotNull(updatedOrder);
        Assert.Equal(CodOrderStatus.Postponed, updatedOrder.Status);
        Assert.Contains("Postponed via Voice AI Call", updatedOrder.Notes);
    }

    [Fact]
    public async Task ProcessIvrCustomerResponseAsync_WhenUnrecognizedOnFirstAttempt_ShouldScheduleRetryInOneHour()
    {
        var db = CreateDbContext();
        var dialer = new AutomatedVoiceDialerService(db);
        var tenantId = Guid.NewGuid();

        var order = CodOrder.Create(
            tenantId: tenantId,
            orderNumber: "ORD-RETRY-01",
            customerName: "Karim Ben Salah",
            phoneNumber: "+21622334455",
            shippingAddress: "Les Berges du Lac",
            governorateOrWilaya: "Tunis",
            delegationOrCommune: "Lac 1",
            totalAmount: 200.0m);
        db.CodOrders.Add(order);
        await db.SaveChangesAsync();

        var attempt = await dialer.QueueOrderForVoiceConfirmationAsync(tenantId, order.Id);

        // Attempt 1 with static noise/unrecognized input
        var processed = await dialer.ProcessIvrCustomerResponseAsync(tenantId, attempt.Id, "allo allo qui parle", null);

        Assert.Equal(IvrCallState.NoAnswer, processed.State);
        Assert.NotNull(processed.ScheduledRetryUtc);
        Assert.True(processed.ScheduledRetryUtc > DateTime.UtcNow);

        var updatedOrder = await db.CodOrders.FindAsync(order.Id);
        Assert.Contains("Retry at", updatedOrder!.Notes);
    }

    [Fact]
    public async Task ProcessIvrCustomerResponseAsync_WhenUnrecognizedOnThirdAttempt_ShouldSetFailedState()
    {
        var db = CreateDbContext();
        var dialer = new AutomatedVoiceDialerService(db);
        var tenantId = Guid.NewGuid();

        var order = CodOrder.Create(
            tenantId: tenantId,
            orderNumber: "ORD-FAIL-03",
            customerName: "Salma Kallel",
            phoneNumber: "+21699887766",
            shippingAddress: "Centre Ville",
            governorateOrWilaya: "Sousse",
            delegationOrCommune: "Sousse",
            totalAmount: 110.0m);
        db.CodOrders.Add(order);

        // Simulate 2 previous attempts
        db.OutboundCallAttempts.Add(OutboundCallAttempt.Create(tenantId, order.Id, order.PhoneNumber, order.CustomerName, null, 1));
        db.OutboundCallAttempts.Add(OutboundCallAttempt.Create(tenantId, order.Id, order.PhoneNumber, order.CustomerName, null, 2));
        await db.SaveChangesAsync();

        // Queue 3rd attempt
        var attempt3 = await dialer.QueueOrderForVoiceConfirmationAsync(tenantId, order.Id);
        Assert.Equal(3, attempt3.AttemptNumber);

        // Process 3rd attempt with unrecognized input
        var processed = await dialer.ProcessIvrCustomerResponseAsync(tenantId, attempt3.Id, "...", null);

        Assert.Equal(IvrCallState.Failed, processed.State);

        var updatedOrder = await db.CodOrders.FindAsync(order.Id);
        Assert.Contains("Fallback to WhatsApp", updatedOrder!.Notes);
    }
}
