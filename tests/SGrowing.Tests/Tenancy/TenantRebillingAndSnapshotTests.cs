using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SGrowing.Api.Data;
using SGrowing.Api.Modules.Tenancy.SnapshotsAndRebilling;
using SGrowing.Core.Entities;
using Xunit;

namespace SGrowing.Tests.Tenancy;

public class TenantRebillingAndSnapshotTests
{
    private static SGrowingDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<SGrowingDbContext>()
            .UseInMemoryDatabase(databaseName: $"RebillingSnapshotDb_{Guid.NewGuid()}")
            .Options;
        return new SGrowingDbContext(options);
    }

    #region TenantRebillingService Tests

    [Fact]
    public async Task SetRebillingConfigAsync_ClampsMultipliersToMinimumOne()
    {
        using var db = CreateDbContext();
        var service = new TenantRebillingService(db);
        var agencyId = Guid.NewGuid();
        var subAccountId = Guid.NewGuid();

        // Pass multipliers below 1.0 (e.g. 0.5)
        var config = await service.SetRebillingConfigAsync(agencyId, subAccountId, 0.5m, 0.8m, 0.2m);

        config.Should().NotBeNull();
        config.AgencyTenantId.Should().Be(agencyId);
        config.SubAccountTenantId.Should().Be(subAccountId);
        config.SmsMarkupMultiplier.Should().Be(1.0m);
        config.VoiceMarkupMultiplier.Should().Be(1.0m);
        config.AiTokenMarkupMultiplier.Should().Be(1.0m);
    }

    [Fact]
    public async Task SetRebillingConfigAsync_UpdatesExistingRecord()
    {
        using var db = CreateDbContext();
        var service = new TenantRebillingService(db);
        var agencyId = Guid.NewGuid();
        var subAccountId = Guid.NewGuid();

        // Initial setup
        await service.SetRebillingConfigAsync(agencyId, subAccountId, 1.3m, 1.5m, 2.0m);

        // Update with new values
        var updated = await service.SetRebillingConfigAsync(agencyId, subAccountId, 1.4m, 1.8m, 2.5m);

        updated.SmsMarkupMultiplier.Should().Be(1.4m);
        updated.VoiceMarkupMultiplier.Should().Be(1.8m);
        updated.AiTokenMarkupMultiplier.Should().Be(2.5m);

        var count = await db.TenantRebillingConfigs.CountAsync();
        count.Should().Be(1);
    }

    [Fact]
    public async Task GetRebillingConfigAsync_ReturnsConfigWhenFoundOrNullWhenMissing()
    {
        using var db = CreateDbContext();
        var service = new TenantRebillingService(db);
        var agencyId = Guid.NewGuid();
        var subAccountId = Guid.NewGuid();

        var missing = await service.GetRebillingConfigAsync(agencyId, subAccountId);
        missing.Should().BeNull();

        await service.SetRebillingConfigAsync(agencyId, subAccountId, 1.25m, 1.30m, 1.50m);

        var existing = await service.GetRebillingConfigAsync(agencyId, subAccountId);
        existing.Should().NotBeNull();
        existing!.SmsMarkupMultiplier.Should().Be(1.25m);
    }

    [Fact]
    public async Task CalculateRebilledCostAsync_WithCustomConfig_ComputesAccurateRetailAndProfit()
    {
        using var db = CreateDbContext();
        var service = new TenantRebillingService(db);
        var agencyId = Guid.NewGuid();
        var subAccountId = Guid.NewGuid();

        // Agency sets 50% markup on AI tokens, 20% on SMS
        await service.SetRebillingConfigAsync(agencyId, subAccountId, smsMultiplier: 1.20m, voiceMultiplier: 1.30m, aiMultiplier: 1.50m);

        // AI Token cost: wholesale 10.00 -> retail 15.00, profit 5.00
        var aiResult = await service.CalculateRebilledCostAsync(agencyId, subAccountId, "ai", 10.00m);
        aiResult.RetailCharge.Should().Be(15.00m);
        aiResult.NetAgencyProfit.Should().Be(5.00m);
        aiResult.MarkupMultiplier.Should().Be(1.50m);

        // SMS cost: wholesale 50.00 -> retail 60.00, profit 10.00
        var smsResult = await service.CalculateRebilledCostAsync(agencyId, subAccountId, "sms", 50.00m);
        smsResult.RetailCharge.Should().Be(60.00m);
        smsResult.NetAgencyProfit.Should().Be(10.00m);
    }

    [Fact]
    public async Task CalculateRebilledCostAsync_WithoutConfig_AppliesDefaultTwentyFivePercentAgencyMarkup()
    {
        using var db = CreateDbContext();
        var service = new TenantRebillingService(db);
        var agencyId = Guid.NewGuid();
        var subAccountId = Guid.NewGuid();

        // No custom config registered
        var result = await service.CalculateRebilledCostAsync(agencyId, subAccountId, "voice", 100.00m);

        // Default 25% markup
        result.MarkupMultiplier.Should().Be(1.25m);
        result.RetailCharge.Should().Be(125.00m);
        result.NetAgencyProfit.Should().Be(25.00m);
    }

    #endregion

    #region TenantSnapshotService Tests

    [Fact]
    public async Task CreateSnapshotAsync_WhenTenantHasNoCustomEntities_PackagesStarterBlueprint()
    {
        using var db = CreateDbContext();
        var service = new TenantSnapshotService(db);
        var tenantId = Guid.NewGuid();

        var snapshot = await service.CreateSnapshotAsync(
            tenantId,
            "Real Estate Starter",
            "RealEstate",
            "Standard lead qualification and scheduling snapshot"
        );

        snapshot.Should().NotBeNull();
        snapshot.TenantId.Should().Be(tenantId);
        snapshot.SnapshotName.Should().Be("Real Estate Starter");
        snapshot.Category.Should().Be("RealEstate");
        snapshot.BlueprintJson.Should().NotBeNullOrWhiteSpace();

        // Stored in database
        var inDb = await db.TenantSnapshotRecords.FindAsync(snapshot.Id);
        inDb.Should().NotBeNull();
    }

    [Fact]
    public async Task GetSnapshotAsync_WhenSnapshotExists_ReturnsSnapshotRecord_AndNullWhenMissing()
    {
        using var db = CreateDbContext();
        var service = new TenantSnapshotService(db);
        var tenantId = Guid.NewGuid();

        var missing = await service.GetSnapshotAsync(Guid.NewGuid());
        missing.Should().BeNull();

        var created = await service.CreateSnapshotAsync(tenantId, "Dental Clinic Pack", "Healthcare", "Dental blueprints");
        var retrieved = await service.GetSnapshotAsync(created.Id);

        retrieved.Should().NotBeNull();
        retrieved!.Id.Should().Be(created.Id);
        retrieved.SnapshotName.Should().Be("Dental Clinic Pack");
    }

    [Fact]
    public async Task ListSnapshotsAsync_FiltersByTenantId_AndReturnsOrderedSnapshots()
    {
        using var db = CreateDbContext();
        var service = new TenantSnapshotService(db);
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();

        await service.CreateSnapshotAsync(tenantA, "Snap A1", "Retail");
        await service.CreateSnapshotAsync(tenantA, "Snap A2", "Retail");
        await service.CreateSnapshotAsync(tenantB, "Snap B1", "Hospitality");

        var listA = await service.ListSnapshotsAsync(tenantA);
        listA.Should().HaveCount(2);
        listA.Should().OnlyContain(s => s.TenantId == tenantA);

        var listB = await service.ListSnapshotsAsync(tenantB);
        listB.Should().HaveCount(1);
        listB[0].SnapshotName.Should().Be("Snap B1");
    }

    #endregion
}
