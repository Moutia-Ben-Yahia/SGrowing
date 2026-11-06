using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using FluentAssertions;
using SGrowing.Api.Common.Streaming;
using SGrowing.Api.Modules.Analytics;
using Xunit;

namespace SGrowing.Tests.Performance;

public class HighThroughputSoakTests
{
    [Fact]
    public async Task HighThroughputBurst_10000Events_AchievesTargetThroughputAndZeroLoss()
    {
        // Arrange
        var mesh = new EventStreamingMeshService();
        var olap = new ClickHouseAnalyticsProjectionService();
        var generator = new SyntheticTrafficLoadGeneratorService();
        mesh.ConfigurePartitionCount("clickstream.events", 16);

        const int totalEvents = 10_000;
        const int concurrency = 16;

        // Act
        var result = await generator.ExecuteLoadBurstAsync(mesh, olap, totalEvents, concurrency);

        // Assert SRE Benchmarks
        result.TotalEventsInjected.Should().Be(totalEvents);
        result.ZeroMessageLoss.Should().BeTrue("All 10,000 streaming events must be projected to OLAP with zero message loss");
        result.PartitionsUtilized.Should().Be(16, "All 16 streaming mesh partitions must be actively distributed");
        result.EventsPerSecond.Should().BeGreaterThan(3_000, "In-memory throughput must exceed 3,000 events/sec");
        result.TotalProjectedRevenue.Should().BeGreaterThan(0, "Revenue events must be accumulated accurately");

        // Verify OLAP vector query capabilities on the projected dataset
        var tenant1 = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var now = DateTime.UtcNow;
        var start = now.AddDays(-1);
        var end = now.AddDays(1);

        var roas = await olap.GetRealtimeRoasAsync(tenant1, start, end);
        roas.TenantId.Should().Be(tenant1);
        roas.TotalClicks.Should().BeGreaterThan(0);
        roas.TotalConversions.Should().BeGreaterThan(0);
        roas.TotalRevenue.Should().BeGreaterThan(0);
        roas.Roas.Should().BeGreaterThan(0);

        var funnel = await olap.GetFunnelConversionAsync(tenant1, start, end, new[] { "ad.click", "purchase" });
        funnel.TenantId.Should().Be(tenant1);
        funnel.Steps.Should().HaveCount(2);
        funnel.Steps[0].StepName.Should().Be("ad.click");
        funnel.Steps[0].ConversionRateFromInitial.Should().Be(1.0);
        funnel.Steps[1].StepName.Should().Be("purchase");
        funnel.Steps[1].ConversionRateFromInitial.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task MultiTenant_ConcurrentBurst_StrictDataIsolationUnderLoad()
    {
        // Arrange
        var mesh = new EventStreamingMeshService();
        var olap = new ClickHouseAnalyticsProjectionService();
        var generator = new SyntheticTrafficLoadGeneratorService();
        mesh.ConfigurePartitionCount("clickstream.events", 16);

        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();

        const string topic = "clickstream.events";
        const string group = "tenant-isolation-worker";

        // Ingest 1,000 events for Tenant A and 1,000 events for Tenant B concurrently
        var publishTasks = new List<Task>();
        for (int i = 0; i < 1_000; i++)
        {
            var idx = i;
            publishTasks.Add(mesh.PublishEventAsync(
                topic,
                $"tA_{idx}",
                new ClickHouseEventRecord(
                    EventId: Guid.NewGuid(),
                    TenantId: tenantA,
                    EventType: "ad.click",
                    TimestampUtc: DateTime.UtcNow,
                    Channel: "meta",
                    CampaignId: "campA",
                    ContactId: Guid.NewGuid(),
                    Cost: 1.00m,
                    Revenue: 0m,
                    MetadataJson: "{}"
                )));

            publishTasks.Add(mesh.PublishEventAsync(
                topic,
                $"tB_{idx}",
                new ClickHouseEventRecord(
                    EventId: Guid.NewGuid(),
                    TenantId: tenantB,
                    EventType: "purchase",
                    TimestampUtc: DateTime.UtcNow,
                    Channel: "google",
                    CampaignId: "campB",
                    ContactId: Guid.NewGuid(),
                    Cost: 0m,
                    Revenue: 50.00m,
                    MetadataJson: "{}"
                )));
        }

        await Task.WhenAll(publishTasks);

        // Drain streaming mesh into ClickHouse
        while (true)
        {
            var projected = await olap.ProjectFromStreamingMeshAsync(mesh, topic, group, maxBatchSize: 500);
            if (projected == 0) break;
        }

        // Assert Tenant A queries strictly see Tenant A events (all clicks, 0 revenue)
        var now = DateTime.UtcNow;
        var start = now.AddDays(-1);
        var end = now.AddDays(1);

        var roasA = await olap.GetRealtimeRoasAsync(tenantA, start, end);
        roasA.TenantId.Should().Be(tenantA);
        roasA.TotalClicks.Should().Be(1_000);
        roasA.TotalRevenue.Should().Be(0m);
        roasA.TotalConversions.Should().Be(0);

        // Assert Tenant B queries strictly see Tenant B events (0 clicks, revenue from 1,000 purchases)
        var roasB = await olap.GetRealtimeRoasAsync(tenantB, start, end);
        roasB.TenantId.Should().Be(tenantB);
        roasB.TotalClicks.Should().Be(0);
        roasB.TotalRevenue.Should().Be(50_000m);
        roasB.TotalConversions.Should().Be(1_000);
    }
}
