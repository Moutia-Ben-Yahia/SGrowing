using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SGrowing.Api.Data;
using SGrowing.Api.Modules.Analytics;
using SGrowing.Api.Modules.Telemetry;
using SGrowing.Core.Entities;
using Xunit;

namespace SGrowing.Tests.Telemetry;

public class ClickstreamAndPartitioningTests
{
    private readonly SGrowingDbContext _db;
    private readonly ClickstreamBufferService _bufferService;
    private readonly DatabasePartitioningService _partitionService;
    private readonly MultiTouchAttributionService _attributionService;

    public ClickstreamAndPartitioningTests()
    {
        var options = new DbContextOptionsBuilder<SGrowingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _db = new SGrowingDbContext(options);
        _bufferService = new ClickstreamBufferService(_db);
        _partitionService = new DatabasePartitioningService(_db);
        _attributionService = new MultiTouchAttributionService(_db);
    }

    [Fact]
    public async Task ClickstreamBuffer_EnqueuesAndBatchFlushesEventsToDatabase()
    {
        var tenantId = Guid.NewGuid();

        // Buffer 3 events
        var ev1 = TelemetryEvent.Create(tenantId, "page_view", "/pricing", "192.168.1.1", "Chrome");
        var ev2 = TelemetryEvent.Create(tenantId, "button_click", "btn_upgrade_enterprise", "192.168.1.1", "Chrome");
        var ev3 = TelemetryEvent.Create(tenantId, "checkout_started", "/checkout", "192.168.1.1", "Chrome");

        await _bufferService.BufferEventAsync(ev1);
        await _bufferService.BufferEventAsync(ev2);
        await _bufferService.BufferEventAsync(ev3);

        _bufferService.GetPendingBufferCount().Should().Be(3);

        // Flush batch of 2
        var flushedFirst = await _bufferService.FlushBufferBatchAsync(2);
        flushedFirst.Should().Be(2);
        _bufferService.GetPendingBufferCount().Should().Be(1);

        // Flush remaining
        var flushedSecond = await _bufferService.FlushBufferBatchAsync(10);
        flushedSecond.Should().Be(1);
        _bufferService.GetPendingBufferCount().Should().Be(0);

        // Confirm database contains the 3 flushed events
        var storedEvents = await _db.TelemetryEvents.Where(e => e.TenantId == tenantId).ToListAsync();
        storedEvents.Should().HaveCount(3);
    }

    [Fact]
    public void DatabasePartitioning_GeneratesValidMonthlyPostgreSqlDdl()
    {
        var month = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);

        var ddl = _partitionService.GenerateCreatePartitionDdl("TelemetryEvents", month);

        ddl.Should().Contain("CREATE TABLE IF NOT EXISTS \"TelemetryEvents_2026_10\" PARTITION OF \"TelemetryEvents_Partitioned\"");
        ddl.Should().Contain("FOR VALUES FROM ('2026-10-01 00:00:00+00') TO ('2026-11-01 00:00:00+00')");

        // Verify preconfigured partitions list
        var partitions = _partitionService.GetPreconfiguredPartitions("MessageLogs", month, 3);
        partitions.Should().HaveCount(4); // 3 monthly partitions + 1 default partition
        partitions.Should().Contain(p => p.PartitionName == "MessageLogs_2026_10");
        partitions.Should().Contain(p => p.PartitionName == "MessageLogs_2026_11");
        partitions.Should().Contain(p => p.PartitionName == "MessageLogs_2026_12");
        partitions.Should().Contain(p => p.IsDefault);
    }

    [Fact]
    public async Task MultiTouchAttribution_TimeDecayModel_WeightsRecentTouchpointsHigher()
    {
        var tenantId = Guid.NewGuid();
        var contact = Contact.Create(tenantId, "Tarek", "Bousetta", "tarek@bousetta.tn");
        _db.Contacts.Add(contact);

        var deal = Deal.Create(tenantId, contact.Id, "Tunis Retail Deal", 10000m, DealStage.Won);
        _db.Deals.Add(deal);
        await _db.SaveChangesAsync();

        var baseTime = DateTime.UtcNow;

        // 3 Touchpoints over 3 weeks:
        // Touchpoint 1: 14 days ago (Organic SEO)
        // Touchpoint 2: 7 days ago (Cold Email Spintax)
        // Touchpoint 3: 0 days ago (Closing Demo / Contract)
        var tp1 = AttributionTouchpoint.Create(tenantId, contact.Id, "Organic SEO", "Whitepaper", TouchpointStage.FirstDiscovery, deal.Id, 10m);
        tp1.OccurredAtUtc = baseTime.AddDays(-14);

        var tp2 = AttributionTouchpoint.Create(tenantId, contact.Id, "Cold Outreach", "Retail Blitz", TouchpointStage.LeadCreated, deal.Id, 5m);
        tp2.OccurredAtUtc = baseTime.AddDays(-7);

        var tp3 = AttributionTouchpoint.Create(tenantId, contact.Id, "Closing Call", "Executive Demo", TouchpointStage.OpportunityCreated, deal.Id, 50m);
        tp3.OccurredAtUtc = baseTime;

        _db.AttributionTouchpoints.AddRange(tp1, tp2, tp3);
        await _db.SaveChangesAsync();

        var results = await _attributionService.CalculateAttributionAsync(tenantId, deal.Id, AttributionModelType.TimeDecay);

        results.Should().HaveCount(3);

        var item1 = results.First(r => r.TouchpointId == tp1.Id);
        var item2 = results.First(r => r.TouchpointId == tp2.Id);
        var item3 = results.First(r => r.TouchpointId == tp3.Id);

        // Under Time-Decay, WeightPercent must monotonically increase with recency:
        // item3 (most recent) > item2 > item1 (oldest)
        item3.WeightPercent.Should().BeGreaterThan(item2.WeightPercent);
        item2.WeightPercent.Should().BeGreaterThan(item1.WeightPercent);

        // Sum of attributed revenue equals deal total (10,000)
        var totalAttributed = results.Sum(r => r.AttributedRevenue);
        totalAttributed.Should().BeApproximately(10000m, 1m);
    }
}
