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
}
