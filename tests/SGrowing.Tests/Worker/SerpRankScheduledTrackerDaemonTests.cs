using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SGrowing.Api.Data;
using SGrowing.Core.Entities;
using SGrowing.Worker.Services;
using Xunit;

namespace SGrowing.Tests.Worker;

public class SerpRankScheduledTrackerDaemonTests
{
    private SGrowingDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<SGrowingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new SGrowingDbContext(options);
    }

    [Fact]
    public async Task AuditTrackedKeywordsAsync_WhenEmpty_ReturnsZero()
    {
        using var db = CreateDbContext();

        var count = await SerpRankScheduledTrackerDaemon.AuditTrackedKeywordsAsync(db);

        count.Should().Be(0);
    }

    [Fact]
    public async Task AuditTrackedKeywordsAsync_FiltersToPastTwentyFourHours()
    {
        using var db = CreateDbContext();
        var tenantId = Guid.NewGuid();

        // Fresh record (recent within 2 hours)
        var freshRecord = SerpRankHistoryRecord.Create(
            tenantId,
            "chaussures cuir homme tunisie",
            "https://store.sgrowing.io/men",
            position: 3,
            previousPosition: 5
        );
        freshRecord.RecordedDateUtc = DateTime.UtcNow.AddHours(-2);

        // Stale record (older than 24h, recorded 3 days ago)
        var staleRecord = SerpRankHistoryRecord.Create(
            tenantId,
            "montres automatiques",
            "https://store.sgrowing.io/watches",
            position: 8,
            previousPosition: 9
        );
        staleRecord.RecordedDateUtc = DateTime.UtcNow.AddDays(-3);

        db.SerpRankHistoryRecords.AddRange(freshRecord, staleRecord);
        await db.SaveChangesAsync();

        var auditedCount = await SerpRankScheduledTrackerDaemon.AuditTrackedKeywordsAsync(db);

        auditedCount.Should().Be(1);
    }
}
