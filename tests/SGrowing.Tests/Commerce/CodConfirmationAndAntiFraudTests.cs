using Microsoft.EntityFrameworkCore;
using SGrowing.Api.Data;
using SGrowing.Api.Modules.Commerce;
using SGrowing.Core.Entities;
using Xunit;

namespace SGrowing.Tests.Commerce;

public class CodConfirmationAndAntiFraudTests
{
    private SGrowingDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<SGrowingDbContext>()
            .UseInMemoryDatabase(databaseName: $"CodTestDb_{Guid.NewGuid()}")
            .Options;
        return new SGrowingDbContext(options);
    }

    [Fact]
    public async Task AcquireLock_Should_Prevent_Collision_When_Another_Agent_Holds_Active_Lock()
    {
        var db = CreateDbContext();
        var service = new CodConfirmationService(db);
        var tenantId = Guid.NewGuid();
        var orderId = Guid.NewGuid();

        // Agent Sarah acquires lock for 3 minutes
        var lock1 = await service.AcquireLockAsync(tenantId, orderId, "agent_1", "Sarah", leaseMinutes: 3);
        Assert.True(lock1.LockAcquired);
        Assert.Equal("Sarah", lock1.CurrentHolderAgentName);

        // Agent Ahmed tries to acquire lock on same order
        var lock2 = await service.AcquireLockAsync(tenantId, orderId, "agent_2", "Ahmed", leaseMinutes: 3);
        Assert.False(lock2.LockAcquired);
        Assert.Contains("Sarah", lock2.Message);

        // Sarah renews lease
        var lock3 = await service.AcquireLockAsync(tenantId, orderId, "agent_1", "Sarah", leaseMinutes: 5);
        Assert.True(lock3.LockAcquired);

        // Sarah releases lock
        var released = await service.ReleaseLockAsync(tenantId, orderId, "agent_1");
        Assert.True(released);

        // Ahmed can now acquire lock
        var lock4 = await service.AcquireLockAsync(tenantId, orderId, "agent_2", "Ahmed", leaseMinutes: 3);
        Assert.True(lock4.LockAcquired);
        Assert.Equal("Ahmed", lock4.CurrentHolderAgentName);
    }

    [Fact]
    public async Task EvaluateRisk_Should_Detect_Invalid_Phone_Blacklist_And_Duplicate_Velocity()
    {
        var db = CreateDbContext();
        var service = new CodConfirmationService(db);
        var tenantId = Guid.NewGuid();

        // Add blacklisted number
        db.BlacklistEntries.Add(new BlacklistEntry
        {
            TenantId = tenantId,
            Type = "Phone",
            Value = "+21622000000",
            Reason = "Known fake order bot"
        });
        await db.SaveChangesAsync();

        // 1. Valid order
        var validOrder = CodOrder.Create(tenantId, "ORD-101", "Ali Ben Amor", "+21698123456", "Avenue Habib Bourguiba", "Tunis", "Bab Bhar", 120m, countryCode: "TN");
        db.CodOrders.Add(validOrder);
        await db.SaveChangesAsync();

        var risk1 = await service.EvaluateRiskAsync(tenantId, validOrder);
        Assert.Equal(FraudRiskLevel.Low, risk1.RiskLevel);
        Assert.True(risk1.IsAllowedToShip);

        // 2. Blacklisted order
        var blacklistedOrder = CodOrder.Create(tenantId, "ORD-102", "Bad Bot", "+21622000000", "Unknown Street", "Ariana", "Menzah", 250m, countryCode: "TN");
        db.CodOrders.Add(blacklistedOrder);
        await db.SaveChangesAsync();

        var risk2 = await service.EvaluateRiskAsync(tenantId, blacklistedOrder);
        Assert.Contains("BLACKLISTED_PHONE_NUMBER", risk2.Flags);
        Assert.True(risk2.Score >= 60);

        // 3. Duplicate order velocity within 15 minutes
        var duplicateOrder = CodOrder.Create(tenantId, "ORD-103", "Ali Fast", "+21698123456", "Avenue Habib Bourguiba", "Tunis", "Bab Bhar", 120m, countryCode: "TN");
        db.CodOrders.Add(duplicateOrder);
        await db.SaveChangesAsync();

        var risk3 = await service.EvaluateRiskAsync(tenantId, duplicateOrder);
        Assert.Contains(risk3.Flags, f => f.StartsWith("DUPLICATE_ORDER_VELOCITY"));
    }

    [Fact]
    public async Task RecordCallOutcome_And_InteractiveWhatsApp_Should_Transition_Order_State()
    {
        var db = CreateDbContext();
        var service = new CodConfirmationService(db);
        var tenantId = Guid.NewGuid();

        var order = CodOrder.Create(tenantId, "ORD-201", "Mariem Mansour", "+21650987654", "Route de Teniour", "Sfax", "Sfax Ville", 185m);
        db.CodOrders.Add(order);
        await db.SaveChangesAsync();

        // Acquire lock
        await service.AcquireLockAsync(tenantId, order.Id, "agent_1", "Sarah");

        // Record Confirmed call outcome
        var confirmedOrder = await service.RecordCallOutcomeAsync(tenantId, order.Id, "agent_1", "Sarah", CallOutcome.Confirmed, "Confirmed delivery for tomorrow afternoon", null);
        Assert.Equal(CodOrderStatus.Confirmed, confirmedOrder.Status);
        Assert.Equal(ConfirmationChannel.PhoneCall, confirmedOrder.ConfirmationChannel);
        Assert.NotNull(confirmedOrder.ConfirmedAtUtc);

        // Lock should be released automatically
        var lockStatus = await db.OrderCollisionLocks.AnyAsync(l => l.OrderId == order.Id && l.IsActive);
        Assert.False(lockStatus);

        // Customer Interactive WhatsApp Response
        var order2 = CodOrder.Create(tenantId, "ORD-202", "Hichem Sassi", "+21655112233", "Rue de Paris", "Sousse", "Kantaoui", 90m);
        db.CodOrders.Add(order2);
        await db.SaveChangesAsync();

        var waPayload = await service.GenerateInteractiveWhatsAppPayloadAsync(tenantId, order2.Id);
        Assert.Contains("Hichem Sassi", waPayload);
        Assert.Contains("CONFIRM_", waPayload);

        var autoConfirmed = await service.HandleCustomerInteractiveResponseAsync(tenantId, order2.Id, $"CONFIRM_{order2.Id}");
        Assert.Equal(CodOrderStatus.Confirmed, autoConfirmed.Status);
        Assert.Equal(ConfirmationChannel.WhatsAppInteractive, autoConfirmed.ConfirmationChannel);
    }
}
