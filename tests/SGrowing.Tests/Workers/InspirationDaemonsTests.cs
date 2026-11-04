using Microsoft.EntityFrameworkCore;
using SGrowing.Api.Data;
using SGrowing.Core.Entities;
using SGrowing.Worker.Services;
using Xunit;

namespace SGrowing.Tests.Workers;

public class InspirationDaemonsTests
{
    private SGrowingDbContext CreateInMemoryDb()
    {
        var options = new DbContextOptionsBuilder<SGrowingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new SGrowingDbContext(options);
    }

    [Fact]
    public async Task P2PWarmupRescueDaemon_ProcessesWarmupAndRescuesSpam()
    {
        using var db = CreateInMemoryDb();
        var tenantId = Guid.NewGuid();

        var sender = SenderAccount.Create(tenantId, "warmup1@domain.com", "Warmup Sender", MailboxProvider.GoogleWorkspace, 50);
        db.SenderAccounts.Add(sender);
        await db.SaveChangesAsync();

        var processed = await P2PWarmupRescueDaemon.ProcessWarmupAndRescueAsync(db);
        Assert.Equal(1, processed);

        var records = await db.WarmupRecords.Where(w => w.TenantId == tenantId).ToListAsync();
        Assert.NotEmpty(records);
    }

    [Fact]
    public async Task AutonomousSdrTickDaemon_AdvancesProspectEnrollments()
    {
        using var db = CreateInMemoryDb();
        var tenantId = Guid.NewGuid();

        var enrollment = ProspectEnrollment.Create(tenantId, Guid.NewGuid(), Guid.NewGuid());
        enrollment.NextRunAtUtc = DateTime.UtcNow.AddMinutes(-10); // due
        db.ProspectEnrollments.Add(enrollment);
        await db.SaveChangesAsync();

        var processed = await AutonomousSdrTickDaemon.ProcessSdrTicksAsync(db);
        Assert.Equal(1, processed);

        var updated = await db.ProspectEnrollments.FindAsync(enrollment.Id);
        Assert.NotNull(updated);
        Assert.Equal(2, updated.CurrentStepNumber);
    }

    [Fact]
    public async Task DurableSocialPublishingDaemon_PublishesScheduledPosts()
    {
        using var db = CreateInMemoryDb();
        var tenantId = Guid.NewGuid();

        var post = new SocialPost
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Content = "Scheduled post across networks",
            Status = SocialPostStatus.Scheduled,
            ScheduledAtUtc = DateTime.UtcNow.AddMinutes(-5)
        };
        db.SocialPosts.Add(post);
        await db.SaveChangesAsync();

        var processed = await DurableSocialPublishingDaemon.ProcessScheduledSocialPostsAsync(db);
        Assert.Equal(1, processed);

        var updated = await db.SocialPosts.FindAsync(post.Id);
        Assert.NotNull(updated);
        Assert.Equal(SocialPostStatus.Published, updated.Status);
        Assert.NotNull(updated.PublishedAtUtc);
    }

    [Fact]
    public async Task DealRottingWatchdogDaemon_FlagsStagnatingDeals()
    {
        using var db = CreateInMemoryDb();
        var tenantId = Guid.NewGuid();

        // Create deal older than 7 days
        var deal = Deal.Create(tenantId, Guid.NewGuid(), "Stagnant Deal Enterprise", 75000m, DealStage.Proposal);
        // Using reflection or private setter to simulate older created date if needed, or by testing the check
        db.Deals.Add(deal);
        await db.SaveChangesAsync();

        // Check with threshold 0 days (so all active deals qualify)
        var flagged = await DealRottingWatchdogDaemon.ProcessRottenDealsAsync(db, 0);
        Assert.Equal(1, flagged);

        var updated = await db.Deals.FindAsync(deal.Id);
        Assert.NotNull(updated);
        Assert.Contains("[ROTTEN DEAL ALERT]", updated.Notes);
    }

    [Fact]
    public async Task AutonomousSdrTickDaemon_WhenNextStepExceedsThreshold_CompletesEnrollment()
    {
        using var db = CreateInMemoryDb();
        var tenantId = Guid.NewGuid();

        var enrollment = ProspectEnrollment.Create(tenantId, Guid.NewGuid(), Guid.NewGuid());
        enrollment.CurrentStepNumber = 4;
        enrollment.NextRunAtUtc = DateTime.UtcNow.AddMinutes(-5); // due
        db.ProspectEnrollments.Add(enrollment);
        await db.SaveChangesAsync();

        var processed = await AutonomousSdrTickDaemon.ProcessSdrTicksAsync(db);
        Assert.Equal(1, processed);

        var updated = await db.ProspectEnrollments.FindAsync(enrollment.Id);
        Assert.NotNull(updated);
        Assert.Equal(EnrollmentStatus.Completed, updated.Status);
    }

    [Fact]
    public async Task DurableSocialPublishingDaemon_AuditTokenExpirationHealthAsync_FlagsAccountsExpiringWithinSevenDays()
    {
        using var db = CreateInMemoryDb();
        var tenantId = Guid.NewGuid();

        // Expiring in 3 days -> within 7 day window
        var expiringAccount = SocialAccount.Create(
            tenantId,
            SocialPlatform.LinkedIn,
            "LinkedIn Company Page",
            "li-9921",
            tokenExpiresAtUtc: DateTime.UtcNow.AddDays(3));

        // Expiring in 30 days -> healthy
        var healthyAccount = SocialAccount.Create(
            tenantId,
            SocialPlatform.Instagram,
            "Instagram Brand",
            "ig-8812",
            tokenExpiresAtUtc: DateTime.UtcNow.AddDays(30));

        db.SocialAccounts.AddRange(expiringAccount, healthyAccount);
        await db.SaveChangesAsync();

        var flagged = await DurableSocialPublishingDaemon.AuditTokenExpirationHealthAsync(db);
        Assert.Equal(1, flagged);

        var refreshedExpiring = await db.SocialAccounts.FindAsync(expiringAccount.Id);
        Assert.True(refreshedExpiring!.RefreshTokenRequired);

        var refreshedHealthy = await db.SocialAccounts.FindAsync(healthyAccount.Id);
        Assert.False(refreshedHealthy!.RefreshTokenRequired);
    }

    [Fact]
    public async Task DealRottingWatchdogDaemon_WonOrLostDeals_AreNotFlaggedAsRotten()
    {
        using var db = CreateInMemoryDb();
        var tenantId = Guid.NewGuid();

        var wonDeal = Deal.Create(tenantId, Guid.NewGuid(), "Won Enterprise Deal", 120000m, DealStage.Won);
        var lostDeal = Deal.Create(tenantId, Guid.NewGuid(), "Lost SMB Deal", 10000m, DealStage.Lost);
        db.Deals.AddRange(wonDeal, lostDeal);
        await db.SaveChangesAsync();

        var flagged = await DealRottingWatchdogDaemon.ProcessRottenDealsAsync(db, 0);
        Assert.Equal(0, flagged);
    }

    [Fact]
    public async Task DealRottingWatchdogDaemon_AlreadyFlaggedDeal_DoesNotDuplicateAlertNote()
    {
        using var db = CreateInMemoryDb();
        var tenantId = Guid.NewGuid();

        var deal = Deal.Create(tenantId, Guid.NewGuid(), "Already Flagged Deal", 50000m, DealStage.Qualified);
        deal.Notes = "[ROTTEN DEAL ALERT] Deal has stagnated in Qualified for over 7 days.";
        db.Deals.Add(deal);
        await db.SaveChangesAsync();

        var flagged = await DealRottingWatchdogDaemon.ProcessRottenDealsAsync(db, 0);
        Assert.Equal(0, flagged);

        var updated = await db.Deals.FindAsync(deal.Id);
        Assert.NotNull(updated);
        Assert.NotNull(updated.Notes);
        Assert.Equal(1, updated.Notes!.Split("[ROTTEN DEAL ALERT]").Length - 1);
    }
}
