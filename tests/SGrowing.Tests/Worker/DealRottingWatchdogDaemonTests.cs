using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SGrowing.Api.Data;
using SGrowing.Core.Entities;
using SGrowing.Worker.Services;
using Xunit;

namespace SGrowing.Tests.Worker;

public class DealRottingWatchdogDaemonTests
{
    private SGrowingDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<SGrowingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new SGrowingDbContext(options);
    }

    [Fact]
    public async Task ProcessRottenDealsAsync_WhenNoDeals_ReturnsZero()
    {
        using var db = CreateDbContext();

        var flagged = await DealRottingWatchdogDaemon.ProcessRottenDealsAsync(db, thresholdDays: 7);

        flagged.Should().Be(0);
    }

    [Fact]
    public async Task ProcessRottenDealsAsync_WhenDealOlderThanThreshold_FlagsWithRottenAlert()
    {
        using var db = CreateDbContext();
        var tenantId = Guid.NewGuid();
        var contactId = Guid.NewGuid();

        var staleDeal = Deal.Create(tenantId, contactId, "Enterprise Cloud Security Suite", 45000m, DealStage.Proposal);
        staleDeal.Notes = "Customer awaiting security compliance matrix.";
        db.Deals.Add(staleDeal);
        db.Entry(staleDeal).Property(d => d.CreatedAtUtc).CurrentValue = DateTime.UtcNow.AddDays(-14);
        await db.SaveChangesAsync();

        var flagged = await DealRottingWatchdogDaemon.ProcessRottenDealsAsync(db, thresholdDays: 7);

        flagged.Should().Be(1);

        var updated = await db.Deals.FirstAsync(d => d.Id == staleDeal.Id);
        updated.Notes.Should().StartWith("[ROTTEN DEAL ALERT]");
        updated.Notes.Should().Contain("stagnated in Proposal for over 7 days");
        updated.Notes.Should().Contain("Customer awaiting security compliance matrix.");
    }

    [Fact]
    public async Task ProcessRottenDealsAsync_WhenDealsWonOrLost_DoesNotFlag()
    {
        using var db = CreateDbContext();
        var tenantId = Guid.NewGuid();
        var contactId = Guid.NewGuid();

        var wonDeal = Deal.Create(tenantId, contactId, "Closed Won Enterprise Deal", 120000m, DealStage.Won);
        var lostDeal = Deal.Create(tenantId, contactId, "Closed Lost Deal", 15000m, DealStage.Lost);
        db.Deals.AddRange(wonDeal, lostDeal);
        db.Entry(wonDeal).Property(d => d.CreatedAtUtc).CurrentValue = DateTime.UtcNow.AddDays(-30);
        db.Entry(lostDeal).Property(d => d.CreatedAtUtc).CurrentValue = DateTime.UtcNow.AddDays(-30);
        await db.SaveChangesAsync();

        var flagged = await DealRottingWatchdogDaemon.ProcessRottenDealsAsync(db, thresholdDays: 7);

        flagged.Should().Be(0);

        var won = await db.Deals.FirstAsync(d => d.Id == wonDeal.Id);
        won.Notes.Should().BeNullOrEmpty();
    }

    [Fact]
    public async Task ProcessRottenDealsAsync_WhenAlreadyFlagged_DoesNotDuplicateNote()
    {
        using var db = CreateDbContext();
        var tenantId = Guid.NewGuid();
        var contactId = Guid.NewGuid();

        var existingNote = "[ROTTEN DEAL ALERT] Deal has stagnated in Qualified for over 7 days.\nFollow up immediately.";
        var deal = Deal.Create(tenantId, contactId, "Stagnated Qualified Deal", 25000m, DealStage.Qualified);
        deal.Notes = existingNote;
        db.Deals.Add(deal);
        db.Entry(deal).Property(d => d.CreatedAtUtc).CurrentValue = DateTime.UtcNow.AddDays(-10);
        await db.SaveChangesAsync();

        var flagged = await DealRottingWatchdogDaemon.ProcessRottenDealsAsync(db, thresholdDays: 7);

        flagged.Should().Be(0);

        var updated = await db.Deals.FirstAsync(d => d.Id == deal.Id);
        updated.Notes.Should().Be(existingNote);
    }

    [Fact]
    public async Task ProcessRottenDealsAsync_WhenWithinThreshold_DoesNotFlag()
    {
        using var db = CreateDbContext();
        var tenantId = Guid.NewGuid();
        var contactId = Guid.NewGuid();

        var freshDeal = Deal.Create(tenantId, contactId, "Fresh Inbound Deal", 30000m, DealStage.Qualified);
        freshDeal.Notes = "Intro call scheduled";
        db.Deals.Add(freshDeal);
        db.Entry(freshDeal).Property(d => d.CreatedAtUtc).CurrentValue = DateTime.UtcNow.AddDays(-2);
        await db.SaveChangesAsync();

        var flagged = await DealRottingWatchdogDaemon.ProcessRottenDealsAsync(db, thresholdDays: 7);

        flagged.Should().Be(0);

        var updated = await db.Deals.FirstAsync(d => d.Id == freshDeal.Id);
        updated.Notes.Should().Be("Intro call scheduled");
    }

    [Fact]
    public async Task ProcessRottenDealsAsync_WhenMultipleStagnantDeals_FlagsUpToBatchLimit()
    {
        using var db = CreateDbContext();
        var tenantId = Guid.NewGuid();
        var contactId = Guid.NewGuid();

        for (int i = 1; i <= 25; i++)
        {
            var deal = Deal.Create(tenantId, contactId, $"Enterprise Account {i}", 10000m * i, DealStage.Proposal);
            db.Deals.Add(deal);
            db.Entry(deal).Property(d => d.CreatedAtUtc).CurrentValue = DateTime.UtcNow.AddDays(-15);
        }
        await db.SaveChangesAsync();

        var flagged = await DealRottingWatchdogDaemon.ProcessRottenDealsAsync(db, thresholdDays: 7);

        flagged.Should().Be(20);

        var allDeals = await db.Deals.ToListAsync();
        var flaggedDeals = allDeals.Where(d => d.Notes != null && d.Notes.Contains("[ROTTEN DEAL ALERT]")).ToList();
        flaggedDeals.Should().HaveCount(20);
    }

    [Fact]
    public async Task ProcessRottenDealsAsync_WhenEmptyNotes_SetsRottenAlertCleanly()
    {
        using var db = CreateDbContext();
        var tenantId = Guid.NewGuid();
        var contactId = Guid.NewGuid();

        var dealWithNullNotes = Deal.Create(tenantId, contactId, "Null Notes Deal", 12000m, DealStage.Prospecting);
        dealWithNullNotes.Notes = null;
        db.Deals.Add(dealWithNullNotes);
        db.Entry(dealWithNullNotes).Property(d => d.CreatedAtUtc).CurrentValue = DateTime.UtcNow.AddDays(-10);
        await db.SaveChangesAsync();

        var flagged = await DealRottingWatchdogDaemon.ProcessRottenDealsAsync(db, thresholdDays: 7);

        flagged.Should().Be(1);

        var updated = await db.Deals.FirstAsync(d => d.Id == dealWithNullNotes.Id);
        updated.Notes.Should().Be("[ROTTEN DEAL ALERT] Deal has stagnated in Prospecting for over 7 days. Action required.");
    }

    [Fact]
    public async Task ProcessRottenDealsAsync_CustomThresholdDays_RespectsShorterCutoff()
    {
        using var db = CreateDbContext();
        var tenantId = Guid.NewGuid();
        var contactId = Guid.NewGuid();

        var deal4DaysOld = Deal.Create(tenantId, contactId, "Deal 4 Days Old", 18000m, DealStage.Proposal);
        var deal2DaysOld = Deal.Create(tenantId, contactId, "Deal 2 Days Old", 22000m, DealStage.Proposal);

        db.Deals.AddRange(deal4DaysOld, deal2DaysOld);
        db.Entry(deal4DaysOld).Property(d => d.CreatedAtUtc).CurrentValue = DateTime.UtcNow.AddDays(-4);
        db.Entry(deal2DaysOld).Property(d => d.CreatedAtUtc).CurrentValue = DateTime.UtcNow.AddDays(-2);
        await db.SaveChangesAsync();

        var flagged = await DealRottingWatchdogDaemon.ProcessRottenDealsAsync(db, thresholdDays: 3);

        flagged.Should().Be(1);

        var updated4 = await db.Deals.FirstAsync(d => d.Id == deal4DaysOld.Id);
        updated4.Notes.Should().StartWith("[ROTTEN DEAL ALERT]");

        var updated2 = await db.Deals.FirstAsync(d => d.Id == deal2DaysOld.Id);
        updated2.Notes.Should().BeNullOrEmpty();
    }
}
