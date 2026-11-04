using FluentAssertions;
using SGrowing.Api.Data;
using SGrowing.Api.Modules.Outbound;
using SGrowing.Core.Entities;
using SGrowing.Core.Tenancy;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace SGrowing.Tests.Outbound;

public class MailboxWarmupTests
{
    private SGrowingDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<SGrowingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var tenantContext = new TenantContext();
        // tenantContext.SetTenant(Guid.NewGuid());
        return new SGrowingDbContext(options, tenantContext);
    }

    [Fact]
    public void CalculateMaxSafeQuota_ShouldRampUpSendingSafely()
    {
        var sender = SenderAccount.Create(
            tenantId: Guid.NewGuid(),
            email: "alex@coldoutbound.io",
            displayName: "Alex Hunter",
            provider: MailboxProvider.GoogleWorkspace,
            targetDailyQuota: 40,
            rampDailyIncrement: 3
        );

        // Day 1: starts at initial base of 5
        sender.CalculateMaxSafeQuota(daysSinceCreated: 0).Should().Be(5);

        // Day 5: 5 + (5 * 3) = 20
        sender.CalculateMaxSafeQuota(daysSinceCreated: 5).Should().Be(20);

        // Day 20: capped at targetDailyQuota 40
        sender.CalculateMaxSafeQuota(daysSinceCreated: 20).Should().Be(40);
    }

    [Fact]
    public async Task GetNextAvailableSender_ShouldRotateToMailboxWithLowestDailySends()
    {
        var db = CreateDbContext();
        var tenantId = Guid.NewGuid();

        var senderA = SenderAccount.Create(tenantId, "sales1@outreach.co", "Sales 1", MailboxProvider.GoogleWorkspace, 40);
        senderA.RecordSend();
        senderA.RecordSend(); // sent 2

        var senderB = SenderAccount.Create(tenantId, "sales2@outreach.co", "Sales 2", MailboxProvider.GoogleWorkspace, 40);
        senderB.RecordSend(); // sent 1

        db.SenderAccounts.AddRange(senderA, senderB);
        await db.SaveChangesAsync();

        var service = new MailboxService(db);
        var chosen = await service.GetNextAvailableSenderAsync(tenantId, new[] { senderA.Id, senderB.Id });

        chosen.Should().NotBeNull();
        chosen!.Email.Should().Be("sales2@outreach.co"); // senderB has sent fewer emails today
    }

    [Fact]
    public void CanSendToday_WhenSentCountReachesSafeQuota_ReturnsFalse()
    {
        var sender = SenderAccount.Create(
            tenantId: Guid.NewGuid(),
            email: "quota@test.com",
            displayName: "Quota Tester",
            provider: MailboxProvider.CustomSmtp,
            targetDailyQuota: 5
        );

        // Max safe quota on day 0 is 5
        for (int i = 0; i < 5; i++)
        {
            sender.CanSendToday().Should().BeTrue();
            sender.RecordSend();
        }

        sender.CanSendToday().Should().BeFalse();
    }

    [Fact]
    public void ResetDailyQuota_ResetsSentTodayCountToZero()
    {
        var sender = SenderAccount.Create(Guid.NewGuid(), "reset@test.com", "Reset", MailboxProvider.GoogleWorkspace, 20);
        sender.RecordSend();
        sender.RecordSend();
        sender.SentTodayCount.Should().Be(2);

        sender.ResetDailyQuota();

        sender.SentTodayCount.Should().Be(0);
        sender.CanSendToday().Should().BeTrue();
    }

    [Fact]
    public void QuarantineAndThaw_TransitionsStatusAndUpdatesHealthScore()
    {
        var sender = SenderAccount.Create(Guid.NewGuid(), "quarantine@test.com", "Quarantine Test", MailboxProvider.GoogleWorkspace, 20);
        sender.CanSendToday().Should().BeTrue();

        sender.Quarantine("Spam complaint received");

        sender.Status.Should().Be(InboxRotationStatus.Quarantined);
        sender.CanSendToday().Should().BeFalse();
        sender.QuarantineReason.Should().Be("Spam complaint received");
        sender.HealthScore.Should().BeLessThanOrEqualTo(40);

        sender.Thaw();

        sender.Status.Should().Be(InboxRotationStatus.Warmup);
        sender.QuarantineReason.Should().BeNull();
        sender.CanSendToday().Should().BeTrue();
    }

    [Fact]
    public async Task GetNextAvailableSender_WhenAllSendersExceededQuota_ReturnsNull()
    {
        var db = CreateDbContext();
        var tenantId = Guid.NewGuid();

        var sender = SenderAccount.Create(tenantId, "busy@outreach.co", "Busy", MailboxProvider.GoogleWorkspace, 5);
        for (int i = 0; i < 5; i++) sender.RecordSend();

        db.SenderAccounts.Add(sender);
        await db.SaveChangesAsync();

        var service = new MailboxService(db);
        var chosen = await service.GetNextAvailableSenderAsync(tenantId, new[] { sender.Id });

        chosen.Should().BeNull();
    }

    [Fact]
    public async Task RecordSendAsync_IncrementsSenderSentTodayCount()
    {
        var db = CreateDbContext();
        var tenantId = Guid.NewGuid();

        var sender = SenderAccount.Create(tenantId, "inc@outreach.co", "Inc", MailboxProvider.GoogleWorkspace, 50);
        db.SenderAccounts.Add(sender);
        await db.SaveChangesAsync();

        var service = new MailboxService(db);
        await service.RecordSendAsync(sender.Id);

        var refreshed = await db.SenderAccounts.FindAsync(sender.Id);
        refreshed!.SentTodayCount.Should().Be(1);
    }

    [Fact]
    public async Task SendEmailAsync_WhenSendersAvailable_SelectsAndIncrements()
    {
        var db = CreateDbContext();
        var tenantId = Guid.NewGuid();

        var sender = SenderAccount.Create(tenantId, "auto@outreach.co", "Auto", MailboxProvider.GoogleWorkspace, 50);
        db.SenderAccounts.Add(sender);
        await db.SaveChangesAsync();

        var service = new MailboxService(db);
        var result = await service.SendEmailAsync(tenantId, Guid.NewGuid(), "target@dest.com", "Test Subject", "Test Body");

        result.Should().BeTrue();
        var refreshed = await db.SenderAccounts.FindAsync(sender.Id);
        refreshed!.SentTodayCount.Should().Be(1);
    }
}
