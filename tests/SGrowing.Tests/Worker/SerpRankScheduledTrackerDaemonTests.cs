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

    [Fact]
    public void SerpRankHistoryRecord_Create_ComputesDeltaAndNormalizesFields()
    {
        var tenantId = Guid.NewGuid();
        // Moved from pos 10 to pos 4 => delta = 10 - 4 = +6
        var record = SerpRankHistoryRecord.Create(
            tenantId,
            "  agence marketing tunis  ",
            " https://sgrowing.io/marketing ",
            position: 4,
            previousPosition: 10,
            deviceType: "Mobile",
            countryCode: "tn",
            serpFeatures: new List<string> { "FeaturedSnippet", "LocalPack" }
        );

        record.Should().NotBeNull();
        record.Keyword.Should().Be("agence marketing tunis");
        record.TargetUrl.Should().Be("https://sgrowing.io/marketing");
        record.Position.Should().Be(4);
        record.PreviousPosition.Should().Be(10);
        record.DeltaPosition.Should().Be(6); // 10 - 4 = 6
        record.CountryCode.Should().Be("TN");
        record.SerpFeaturesJson.Should().Contain("FeaturedSnippet");
    }

    [Fact]
    public async Task AuditTrackedKeywordsAsync_WithMultipleKeywordsInWindow_ReturnsExactCount()
    {
        using var db = CreateDbContext();
        var tenant1 = Guid.NewGuid();
        var tenant2 = Guid.NewGuid();

        var r1 = SerpRankHistoryRecord.Create(tenant1, "k1", "https://t1.com", 2, 3);
        var r2 = SerpRankHistoryRecord.Create(tenant1, "k2", "https://t1.com/p", 5, 5);
        var r3 = SerpRankHistoryRecord.Create(tenant2, "k3", "https://t2.com", 1, 2);

        db.SerpRankHistoryRecords.AddRange(r1, r2, r3);
        await db.SaveChangesAsync();

        var count = await SerpRankScheduledTrackerDaemon.AuditTrackedKeywordsAsync(db);

        count.Should().Be(3);
    }
}
