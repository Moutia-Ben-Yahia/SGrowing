using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SGrowing.Api.Data;
using SGrowing.Api.Modules.Channels.WhatsApp;
using SGrowing.Core.Entities;
using Xunit;

namespace SGrowing.Tests.Channels;

public class WhatsAppPairingAndFlowTests
{
    private SGrowingDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<SGrowingDbContext>()
            .UseInMemoryDatabase(databaseName: $"WhatsAppPairingDb_{Guid.NewGuid()}")
            .Options;
        return new SGrowingDbContext(options);
    }

    [Fact]
    public void PhoneNumberValidation_Should_Accept_Valid_E164_And_Normalize_Formats()
    {
        // Valid international formats
        Assert.Equal("+21698123456", WhatsAppPairingProtocolService.NormalizeAndValidatePhoneNumber("+21698123456"));
        Assert.Equal("+201012345678", WhatsAppPairingProtocolService.NormalizeAndValidatePhoneNumber("+201012345678"));
        Assert.Equal("+212612345678", WhatsAppPairingProtocolService.NormalizeAndValidatePhoneNumber("+212612345678"));

        // Normalization of spaces, dashes, parentheses and 00 prefixes
        Assert.Equal("+21698123456", WhatsAppPairingProtocolService.NormalizeAndValidatePhoneNumber("+216 98 123 456"));
        Assert.Equal("+21698123456", WhatsAppPairingProtocolService.NormalizeAndValidatePhoneNumber("0021698123456"));
        Assert.Equal("+21698123456", WhatsAppPairingProtocolService.NormalizeAndValidatePhoneNumber("21698123456"));
        Assert.Equal("+201012345678", WhatsAppPairingProtocolService.NormalizeAndValidatePhoneNumber("+20 (10) 1234-5678"));

        // Invalid cases must throw ArgumentException
        Assert.Throws<ArgumentException>(() => WhatsAppPairingProtocolService.NormalizeAndValidatePhoneNumber(null));
        Assert.Throws<ArgumentException>(() => WhatsAppPairingProtocolService.NormalizeAndValidatePhoneNumber("   "));
        Assert.Throws<ArgumentException>(() => WhatsAppPairingProtocolService.NormalizeAndValidatePhoneNumber("invalid-phone"));
        Assert.Throws<ArgumentException>(() => WhatsAppPairingProtocolService.NormalizeAndValidatePhoneNumber("+1234")); // Too short
        Assert.Throws<ArgumentException>(() => WhatsAppPairingProtocolService.NormalizeAndValidatePhoneNumber("+12345678901234567")); // Too long
    }

    [Fact]
    public void PairingCodeGeneration_Should_Generate_Cryptographically_Random_Human_Readable_8Char_Code()
    {
        var codes = new HashSet<string>();
        for (int i = 0; i < 50; i++)
        {
            var code = WhatsAppPairingProtocolService.GenerateCryptographicPairingCode();
            Assert.Equal(9, code.Length); // 4 chars + '-' + 4 chars
            Assert.Equal('-', code[4]);

            var part1 = code[..4];
            var part2 = code[5..];
            Assert.Matches("^[2-9A-HJ-NP-Z]{4}$", part1);
            Assert.Matches("^[2-9A-HJ-NP-Z]{4}$", part2);

            codes.Add(code);
        }

        // Entropy verification: all 50 codes must be unique
        Assert.Equal(50, codes.Count);
    }

    [Fact]
    public async Task RequestPairingCodeAsync_Should_Store_Ephemeral_Session_With_10Min_TTL_And_Pending_Status()
    {
        var db = CreateDbContext();
        var service = new WhatsAppPairingProtocolService(db, NullLogger<WhatsAppPairingProtocolService>.Instance);
        var tenantId = Guid.NewGuid();

        var res = await service.RequestPairingCodeAsync(tenantId, "+21698123456", "sales-tunis");

        Assert.NotNull(res.PairingCode);
        Assert.Equal("PendingUserEntry", res.Status);
        Assert.Equal(600, res.ExpiresInSeconds);
        Assert.Equal("+21698123456", res.PhoneNumber);
        Assert.Equal("sales-tunis", res.InstanceName);
        Assert.Contains(res.PairingCode, res.StepInstructions);

        // Verify stored in DB
        var session = await db.WhatsAppPairingSessions.FirstOrDefaultAsync(s => s.TenantId == tenantId && s.InstanceName == "sales-tunis");
        Assert.NotNull(session);
        Assert.Equal(res.PairingCode, session.PairingCode);
        Assert.Equal("PendingUserEntry", session.Status);
        Assert.True(session.ExpiresAtUtc > DateTime.UtcNow.AddMinutes(9));

        // Verify WhatsAppInstanceRecord updated
        var instance = await db.WhatsAppInstances.FirstOrDefaultAsync(w => w.TenantId == tenantId && w.InstanceName == "sales-tunis");
        Assert.NotNull(instance);
        Assert.Equal("Pairing", instance.Status);

        // Verify Outbox message enqueued
        var outbox = await db.OutboxMessages.FirstOrDefaultAsync(m => m.TenantId == tenantId && m.EventType == "WhatsAppPairingCodeRequested");
        Assert.NotNull(outbox);
    }

    [Fact]
    public async Task SimulatePairingHandshakeAsync_Should_Connect_Device_And_Persist_Companion_Credentials()
    {
        var db = CreateDbContext();
        var service = new WhatsAppPairingProtocolService(db, NullLogger<WhatsAppPairingProtocolService>.Instance);
        var tenantId = Guid.NewGuid();

        // 1. Request pairing code
        var reqResult = await service.RequestPairingCodeAsync(tenantId, "+21698123456", "pos-sousse");

        // 2. Simulate handshake with code (tolerating missing dash or lowercase)
        var rawCode = reqResult.PairingCode.Replace("-", "").ToLowerInvariant();
        var handshake = await service.SimulatePairingHandshakeAsync(tenantId, rawCode);

        Assert.True(handshake.Success);
        Assert.Equal("Connected", handshake.Status);
        Assert.Equal("Chrome/Linux", handshake.CompanionOs);
        Assert.Equal("SGrowing-MultiDevice", handshake.CompanionPlatform);

        // Verify session status updated
        var session = await db.WhatsAppPairingSessions.FirstOrDefaultAsync(s => s.TenantId == tenantId && s.InstanceName == "pos-sousse");
        Assert.NotNull(session);
        Assert.Equal("Connected", session.Status);
        Assert.NotNull(session.ConnectedAtUtc);

        // Verify instance connected
        var instance = await db.WhatsAppInstances.FirstOrDefaultAsync(w => w.TenantId == tenantId && w.InstanceName == "pos-sousse");
        Assert.NotNull(instance);
        Assert.Equal("Connected", instance.Status);
        Assert.Null(instance.PairingCode);

        // Verify ChannelCredentialVault record created
        var cred = await db.ChannelCredentials.FirstOrDefaultAsync(c => c.TenantId == tenantId && c.ChannelType == "WhatsAppCompanion");
        Assert.NotNull(cred);
        Assert.True(cred.IsActive);
        Assert.Contains("Chrome/Linux", cred.EncryptedAppSecret);
        Assert.Contains("SGrowing-MultiDevice", cred.EncryptedAppSecret);

        // Verify Outbox message
        var outbox = await db.OutboxMessages.FirstOrDefaultAsync(m => m.TenantId == tenantId && m.EventType == "WhatsAppCompanionDeviceConnected");
        Assert.NotNull(outbox);
    }

    [Fact]
    public async Task SimulatePairingHandshakeAsync_Should_Fail_When_Code_Is_Expired()
    {
        var db = CreateDbContext();
        var service = new WhatsAppPairingProtocolService(db, NullLogger<WhatsAppPairingProtocolService>.Instance);
        var tenantId = Guid.NewGuid();

        var reqResult = await service.RequestPairingCodeAsync(tenantId, "+21698123456", "default");

        // Expire the session in database
        var session = await db.WhatsAppPairingSessions.FirstAsync(s => s.TenantId == tenantId);
        session.ExpiresAtUtc = DateTime.UtcNow.AddMinutes(-5);
        await db.SaveChangesAsync();

        // Handshake attempt must throw InvalidOperationException
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.SimulatePairingHandshakeAsync(tenantId, reqResult.PairingCode));

        Assert.Contains("expired", ex.Message, StringComparison.OrdinalIgnoreCase);

        // Verify status marked Expired in DB
        await db.Entry(session).ReloadAsync();
        Assert.Equal("Expired", session.Status);
    }

    [Fact]
    public async Task SimulatePairingHandshakeAsync_Should_Throw_When_Code_Not_Found()
    {
        var db = CreateDbContext();
        var service = new WhatsAppPairingProtocolService(db, NullLogger<WhatsAppPairingProtocolService>.Instance);
        var tenantId = Guid.NewGuid();

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            service.SimulatePairingHandshakeAsync(tenantId, "ZZZZ-9999"));
    }

    [Fact]
    public async Task DispatchInteractiveCodConfirmationAsync_Should_Construct_Message_With_3_Buttons()
    {
        var db = CreateDbContext();
        var service = new WhatsAppPairingProtocolService(db, NullLogger<WhatsAppPairingProtocolService>.Instance);
        var tenantId = Guid.NewGuid();

        var order = CodOrder.Create(tenantId, "ORD-7711", "Mehdi Mansour", "+21698765432", "Ennasr 2", "Ariana", "Ennasr", 145.0m);
        db.CodOrders.Add(order);
        await db.SaveChangesAsync();

        var dispatch = await service.DispatchInteractiveCodConfirmationAsync(
            tenantId,
            order.Id,
            "+216 98 765 432",
            145.0m,
            "TND"
        );

        Assert.Equal("Dispatched", dispatch.Status);
        Assert.StartsWith("WAM-COD-", dispatch.MessageId);
        Assert.Equal(3, dispatch.Buttons.Count);
        Assert.Equal("btn_confirm", dispatch.Buttons[0].ButtonId);
        Assert.Equal("Confirm Order (Ship Now)", dispatch.Buttons[0].ButtonText);
        Assert.Equal("btn_edit_address", dispatch.Buttons[1].ButtonId);
        Assert.Equal("btn_cancel", dispatch.Buttons[2].ButtonId);

        // Verify CodOrder updated
        Assert.NotNull(order.InteractiveConfirmationSentAtUtc);
        Assert.Contains("Interactive confirmation dispatched", order.Notes);

        // Verify confirmation record stored
        var record = await db.WhatsAppInteractiveOrderConfirmations.FirstOrDefaultAsync(c => c.TenantId == tenantId && c.OrderId == order.Id);
        Assert.NotNull(record);
        Assert.Equal("Dispatched", record.Status);
        Assert.Equal(dispatch.MessageId, record.MessageId);

        // Verify Outbox message
        var outbox = await db.OutboxMessages.FirstOrDefaultAsync(m => m.TenantId == tenantId && m.EventType == "WhatsAppInteractiveOrderConfirmationDispatched");
        Assert.NotNull(outbox);
    }

    [Fact]
    public async Task HandleInteractiveButtonResponse_Confirm_Should_Transition_Order_To_Confirmed_And_Release_Call_Center_Locks()
    {
        var db = CreateDbContext();
        var service = new WhatsAppPairingProtocolService(db, NullLogger<WhatsAppPairingProtocolService>.Instance);
        var tenantId = Guid.NewGuid();

        // 1. Create order
        var order = CodOrder.Create(tenantId, "ORD-9901", "Anis Gharbi", "+21698111222", "Centre Urbain Nord", "Tunis", "Tunis", 220.0m);
        order.Status = CodOrderStatus.PendingConfirmation;
        db.CodOrders.Add(order);

        // 2. Create active Call Center lock & Collision lock
        var callLock = CallCenterOrderLockRecord.Create(tenantId, order.Id, order.OrderNumber, Guid.NewGuid(), "Agent Salma");
        db.CallCenterOrderLocks.Add(callLock);

        var collisionLock = new OrderCollisionLock
        {
            TenantId = tenantId,
            OrderId = order.Id,
            AgentId = "agent-salma",
            AgentName = "Salma",
            IsActive = true,
            ExpiresAtUtc = DateTime.UtcNow.AddMinutes(5)
        };
        db.OrderCollisionLocks.Add(collisionLock);

        await db.SaveChangesAsync();

        // 3. Dispatch confirmation
        await service.DispatchInteractiveCodConfirmationAsync(tenantId, order.Id, order.PhoneNumber, order.TotalAmount);

        // 4. Customer taps "Confirm Order" (btn_confirm)
        var result = await service.HandleInteractiveButtonResponseAsync(tenantId, order.Id, "btn_confirm");

        Assert.True(result.Success);
        Assert.Equal("OrderConfirmedInWms", result.ExecutedAction);

        // Order state validations
        Assert.Equal(CodOrderStatus.Confirmed, order.Status);
        Assert.Equal(ConfirmationChannel.WhatsAppInteractive, order.ConfirmationChannel);
        Assert.Equal("WhatsAppInteractive", order.ConfirmedVia);
        Assert.NotNull(order.ConfirmedAtUtc);
        Assert.False(order.RequiresPhoneCall);

        // Call center locks released
        Assert.False(callLock.IsActive);
        Assert.False(collisionLock.IsActive);

        // Outbox notification
        var outbox = await db.OutboxMessages.FirstOrDefaultAsync(m => m.TenantId == tenantId && m.EventType == "WhatsAppInteractiveButtonProcessed");
        Assert.NotNull(outbox);
    }

    [Fact]
    public async Task HandleInteractiveButtonResponse_EditAddress_Should_Append_Correction_Note()
    {
        var db = CreateDbContext();
        var service = new WhatsAppPairingProtocolService(db, NullLogger<WhatsAppPairingProtocolService>.Instance);
        var tenantId = Guid.NewGuid();

        var order = CodOrder.Create(tenantId, "ORD-9902", "Sarra Karray", "+21698333444", "Route de Teniour", "Sfax", "Sfax", 110.0m);
        db.CodOrders.Add(order);
        await db.SaveChangesAsync();

        await service.DispatchInteractiveCodConfirmationAsync(tenantId, order.Id, order.PhoneNumber, order.TotalAmount);

        var result = await service.HandleInteractiveButtonResponseAsync(
            tenantId,
            order.Id,
            "btn_edit_address",
            updatedText: "A côté de la pharmacie, livrer après 16h SVP"
        );

        Assert.True(result.Success);
        Assert.Equal("AddressCorrectionAppended", result.ExecutedAction);
        Assert.Contains("pharmacie", order.Notes);
        Assert.Contains("pharmacie", order.ShippingAddress);

        var record = await db.WhatsAppInteractiveOrderConfirmations.FirstOrDefaultAsync(c => c.TenantId == tenantId && c.OrderId == order.Id);
        Assert.NotNull(record);
        Assert.Equal("AddressUpdated", record.Status);
        Assert.Equal("A côté de la pharmacie, livrer après 16h SVP", record.AddressUpdateNote);
    }

    [Fact]
    public async Task HandleInteractiveButtonResponse_Cancel_Should_Cancel_Order_And_Release_Inventory_Reservation()
    {
        var db = CreateDbContext();
        var service = new WhatsAppPairingProtocolService(db, NullLogger<WhatsAppPairingProtocolService>.Instance);
        var tenantId = Guid.NewGuid();

        var order = CodOrder.Create(tenantId, "ORD-9903", "Bilel Trabelsi", "+21698555666", "Hammam Sousse", "Sousse", "Sousse", 340.0m);
        db.CodOrders.Add(order);

        // Active Cart Reservation Lock for WMS stock protection
        var reservation = new CartReservationLockRecord
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            OrderId = order.Id,
            WarehouseId = Guid.NewGuid(),
            Sku = "SKU-PREMIUM-JACKET",
            Quantity = 2,
            Status = "Active",
            ExpiresAtUtc = DateTime.UtcNow.AddHours(2)
        };
        db.CartReservationLocks.Add(reservation);
        await db.SaveChangesAsync();

        await service.DispatchInteractiveCodConfirmationAsync(tenantId, order.Id, order.PhoneNumber, order.TotalAmount);

        var result = await service.HandleInteractiveButtonResponseAsync(tenantId, order.Id, "btn_cancel");

        Assert.True(result.Success);
        Assert.Equal("OrderCancelledAndStockReleased", result.ExecutedAction);

        // Order cancelled
        Assert.Equal(CodOrderStatus.Cancelled, order.Status);

        // Inventory reservation released back to active stock
        Assert.Equal("Released", reservation.Status);
        Assert.NotNull(reservation.ReleasedAtUtc);

        var record = await db.WhatsAppInteractiveOrderConfirmations.FirstOrDefaultAsync(c => c.TenantId == tenantId && c.OrderId == order.Id);
        Assert.NotNull(record);
        Assert.Equal("Cancelled", record.Status);
    }

    [Fact]
    public async Task CheckConfirmationTimeoutAndFallbackAsync_Should_Escalate_Unconfirmed_Orders_To_Outbound_Dialer()
    {
        var db = CreateDbContext();
        var service = new WhatsAppPairingProtocolService(db, NullLogger<WhatsAppPairingProtocolService>.Instance);
        var tenantId = Guid.NewGuid();

        // Order 1: Sent 35 minutes ago, unconfirmed -> MUST ESCALATE
        var order1 = CodOrder.Create(tenantId, "ORD-FALLBACK-1", "Hedi Ben Ali", "+21698111001", "Bizerte", "Bizerte", "Bizerte", 90.0m);
        order1.InteractiveConfirmationSentAtUtc = DateTime.UtcNow.AddMinutes(-35);
        order1.Status = CodOrderStatus.PendingConfirmation;
        order1.RequiresPhoneCall = false;
        db.CodOrders.Add(order1);

        var conf1 = new WhatsAppInteractiveOrderConfirmationRecord
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            OrderId = order1.Id,
            OrderNumber = order1.OrderNumber,
            CustomerPhone = order1.PhoneNumber,
            TotalAmount = order1.TotalAmount,
            Status = "Dispatched",
            DispatchedAtUtc = DateTime.UtcNow.AddMinutes(-35),
            FallbackToDialerTriggered = false
        };
        db.WhatsAppInteractiveOrderConfirmations.Add(conf1);

        // Order 2: Sent 10 minutes ago, unconfirmed -> MUST NOT ESCALATE YET
        var order2 = CodOrder.Create(tenantId, "ORD-FALLBACK-2", "Leila Chahed", "+21698111002", "Nabeul", "Nabeul", "Nabeul", 120.0m);
        order2.InteractiveConfirmationSentAtUtc = DateTime.UtcNow.AddMinutes(-10);
        order2.Status = CodOrderStatus.PendingConfirmation;
        order2.RequiresPhoneCall = false;
        db.CodOrders.Add(order2);

        var conf2 = new WhatsAppInteractiveOrderConfirmationRecord
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            OrderId = order2.Id,
            OrderNumber = order2.OrderNumber,
            CustomerPhone = order2.PhoneNumber,
            TotalAmount = order2.TotalAmount,
            Status = "Dispatched",
            DispatchedAtUtc = DateTime.UtcNow.AddMinutes(-10),
            FallbackToDialerTriggered = false
        };
        db.WhatsAppInteractiveOrderConfirmations.Add(conf2);

        // Order 3: Sent 45 minutes ago, but ALREADY CONFIRMED -> MUST NOT ESCALATE
        var order3 = CodOrder.Create(tenantId, "ORD-FALLBACK-3", "Tarek Jlassi", "+21698111003", "Monastir", "Monastir", "Monastir", 210.0m);
        order3.InteractiveConfirmationSentAtUtc = DateTime.UtcNow.AddMinutes(-45);
        order3.Status = CodOrderStatus.Confirmed;
        order3.ConfirmedAtUtc = DateTime.UtcNow.AddMinutes(-40);
        order3.RequiresPhoneCall = false;
        db.CodOrders.Add(order3);

        await db.SaveChangesAsync();

        // Execute Sweep with 30-minute threshold
        var sweepResult = await service.CheckConfirmationTimeoutAndFallbackAsync(tenantId, TimeSpan.FromMinutes(30));

        Assert.Equal(1, sweepResult.EscalatedCount);
        Assert.Contains(order1.Id, sweepResult.EscalatedOrderIds);
        Assert.DoesNotContain(order2.Id, sweepResult.EscalatedOrderIds);
        Assert.DoesNotContain(order3.Id, sweepResult.EscalatedOrderIds);

        // Verify Order 1 flagged for outbound dialer call
        Assert.True(order1.RequiresPhoneCall);
        Assert.Contains("Auto-Fallback", order1.Notes);
        Assert.Equal("TimedOut", conf1.Status);
        Assert.True(conf1.FallbackToDialerTriggered);

        // Verify OutboundCallAttempt created in DB for the automated dialer
        var callAttempt = await db.OutboundCallAttempts.FirstOrDefaultAsync(c => c.TenantId == tenantId && c.OrderId == order1.Id);
        Assert.NotNull(callAttempt);
        Assert.Equal(IvrCallState.Queued, callAttempt.State);
        Assert.Equal(order1.PhoneNumber, callAttempt.CustomerPhone);

        // Verify Order 2 still waiting on WhatsApp
        Assert.False(order2.RequiresPhoneCall);
        Assert.Equal("Dispatched", conf2.Status);
        Assert.False(conf2.FallbackToDialerTriggered);

        // Verify Outbox message enqueued
        var outbox = await db.OutboxMessages.FirstOrDefaultAsync(m => m.TenantId == tenantId && m.EventType == "CodOrderEscalatedToCallCenter");
        Assert.NotNull(outbox);
    }

    [Fact]
    public async Task GetPairingStatusAsync_Should_Return_Accurate_State()
    {
        var db = CreateDbContext();
        var service = new WhatsAppPairingProtocolService(db, NullLogger<WhatsAppPairingProtocolService>.Instance);
        var tenantId = Guid.NewGuid();

        // Before request: Disconnected
        var status1 = await service.GetPairingStatusAsync(tenantId, "default");
        Assert.Equal("Disconnected", status1.Status);
        Assert.False(status1.IsConnected);

        // After request: PendingUserEntry with active code
        var req = await service.RequestPairingCodeAsync(tenantId, "+21698123456", "default");
        var status2 = await service.GetPairingStatusAsync(tenantId, "default");
        Assert.Equal("PendingUserEntry", status2.Status);
        Assert.False(status2.IsConnected);
        Assert.Equal(req.PairingCode, status2.ActivePairingCode);
        Assert.NotNull(status2.RemainingSeconds);

        // After handshake: Connected
        await service.SimulatePairingHandshakeAsync(tenantId, req.PairingCode);
        var status3 = await service.GetPairingStatusAsync(tenantId, "default");
        Assert.Equal("Connected", status3.Status);
        Assert.True(status3.IsConnected);
        Assert.Null(status3.ActivePairingCode);
    }
}
