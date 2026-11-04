using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SGrowing.Api.Data;
using SGrowing.Core.Entities;
using SGrowing.Worker.Services;
using Xunit;

namespace SGrowing.Tests.Worker;

public class P2PWarmupRescueDaemonTests
{
    private SGrowingDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<SGrowingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new SGrowingDbContext(options);
    }

    [Fact]
    public async Task ProcessWarmupAndRescueAsync_WhenNoActiveSenders_ReturnsZero()
    {
        using var db = CreateDbContext();

        var processed = await P2PWarmupRescueDaemon.ProcessWarmupAndRescueAsync(db);

        processed.Should().Be(0);
    }

    [Fact]
    public async Task ProcessWarmupAndRescueAsync_WhenSenderActiveAndWarmupEnabled_RecordsSendAndCreatesWarmupRecord()
    {
        using var db = CreateDbContext();
        var tenantId = Guid.NewGuid();

        var sender = new SenderAccount
        {
            TenantId = tenantId,
            Email = "outbound@startup.tn",
            DisplayName = "Outbound Rep",
            IsActive = true,
            WarmupEnabled = true,
            DailyQuota = 50,
            SentTodayCount = 10
        };
        db.SenderAccounts.Add(sender);
        await db.SaveChangesAsync();

        var processed = await P2PWarmupRescueDaemon.ProcessWarmupAndRescueAsync(db);

        processed.Should().Be(1);

        var updatedSender = await db.SenderAccounts.FindAsync(sender.Id);
        updatedSender.Should().NotBeNull();
        updatedSender!.SentTodayCount.Should().Be(11);

        var records = await db.WarmupRecords.Where(w => w.SenderAccountId == sender.Id).ToListAsync();
        records.Should().NotBeEmpty();
        records.Should().Contain(r => r.Direction == WarmupDirection.Sent && r.Status == WarmupStatus.Inbox);
    }

    [Fact]
    public async Task ProcessWarmupAndRescueAsync_WhenQuotaReached_ExcludesFromWarmup()
    {
        using var db = CreateDbContext();
        var tenantId = Guid.NewGuid();

        var sender = new SenderAccount
        {
            TenantId = tenantId,
            Email = "capped@startup.tn",
            DisplayName = "Capped Rep",
            IsActive = true,
            WarmupEnabled = true,
            DailyQuota = 30,
            SentTodayCount = 30
        };
        db.SenderAccounts.Add(sender);
        await db.SaveChangesAsync();

        var processed = await P2PWarmupRescueDaemon.ProcessWarmupAndRescueAsync(db);

        processed.Should().Be(0);
        var records = await db.WarmupRecords.ToListAsync();
        records.Should().BeEmpty();
    }

    [Fact]
    public async Task ProcessWarmupAndRescueAsync_WhenWarmupDisabled_SkipsSender()
    {
        using var db = CreateDbContext();
        var tenantId = Guid.NewGuid();

        var sender = new SenderAccount
        {
            TenantId = tenantId,
            Email = "disabled@startup.tn",
            DisplayName = "Disabled Rep",
            IsActive = true,
            WarmupEnabled = false,
            DailyQuota = 50,
            SentTodayCount = 0
        };
        db.SenderAccounts.Add(sender);
        await db.SaveChangesAsync();

        var processed = await P2PWarmupRescueDaemon.ProcessWarmupAndRescueAsync(db);

        processed.Should().Be(0);
    }

    [Fact]
    public async Task ProcessWarmupAndRescueAsync_WhenInactive_SkipsSender()
    {
        using var db = CreateDbContext();
        var tenantId = Guid.NewGuid();

        var sender = new SenderAccount
        {
            TenantId = tenantId,
            Email = "inactive@startup.tn",
            DisplayName = "Inactive Rep",
            IsActive = false,
            WarmupEnabled = true,
            DailyQuota = 50,
            SentTodayCount = 0
        };
        db.SenderAccounts.Add(sender);
        await db.SaveChangesAsync();

        var processed = await P2PWarmupRescueDaemon.ProcessWarmupAndRescueAsync(db);

        processed.Should().Be(0);
    }

    [Fact]
    public async Task ProcessWarmupAndRescueAsync_LimitsToBatchSizeOf20()
    {
        using var db = CreateDbContext();
        var tenantId = Guid.NewGuid();

        for (int i = 0; i < 25; i++)
        {
            db.SenderAccounts.Add(new SenderAccount
            {
                TenantId = tenantId,
                Email = $"rep{i}@startup.tn",
                DisplayName = $"Rep {i}",
                IsActive = true,
                WarmupEnabled = true,
                DailyQuota = 50,
                SentTodayCount = 0
            });
        }
        await db.SaveChangesAsync();

        var processed = await P2PWarmupRescueDaemon.ProcessWarmupAndRescueAsync(db);

        processed.Should().Be(20);
    }

    [Fact]
    public async Task ProcessWarmupAndRescueAsync_MultipleTenants_MaintainsTenantIdInWarmupRecords()
    {
        using var db = CreateDbContext();
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();

        var senderA = new SenderAccount
        {
            TenantId = tenantA,
            Email = "repA@companyA.tn",
            DisplayName = "Rep A",
            IsActive = true,
            WarmupEnabled = true,
            DailyQuota = 50,
            SentTodayCount = 2
        };
        var senderB = new SenderAccount
        {
            TenantId = tenantB,
            Email = "repB@companyB.tn",
            DisplayName = "Rep B",
            IsActive = true,
            WarmupEnabled = true,
            DailyQuota = 50,
            SentTodayCount = 5
        };

        db.SenderAccounts.AddRange(senderA, senderB);
        await db.SaveChangesAsync();

        var processed = await P2PWarmupRescueDaemon.ProcessWarmupAndRescueAsync(db);

        processed.Should().Be(2);

        var recordsA = await db.WarmupRecords.Where(w => w.TenantId == tenantA).ToListAsync();
        recordsA.Should().NotBeEmpty();
        recordsA.All(r => r.SenderAccountId == senderA.Id).Should().BeTrue();

        var recordsB = await db.WarmupRecords.Where(w => w.TenantId == tenantB).ToListAsync();
        recordsB.Should().NotBeEmpty();
        recordsB.All(r => r.SenderAccountId == senderB.Id).Should().BeTrue();
    }

    [Fact]
    public async Task ProcessWarmupAndRescueAsync_WhenCancelled_ThrowsOperationCanceledException()
    {
        using var db = CreateDbContext();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var act = async () => await P2PWarmupRescueDaemon.ProcessWarmupAndRescueAsync(db, cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }
}
