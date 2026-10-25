using FluentAssertions;
using MarketingStudio.Api.Data;
using MarketingStudio.Api.Modules.Outbound;
using MarketingStudio.Core.Entities;
using MarketingStudio.Core.Tenancy;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace MarketingStudio.Tests.Outbound;

public class MailboxWarmupTests
{
    private MarketingStudioDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<MarketingStudioDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var tenantContext = new TenantContext();
        tenantContext.SetTenant(Guid.NewGuid());
        return new MarketingStudioDbContext(options, tenantContext);
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
}
