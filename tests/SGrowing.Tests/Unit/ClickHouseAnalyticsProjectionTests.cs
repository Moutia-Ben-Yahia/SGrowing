using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using SGrowing.Api.Common.Streaming;
using SGrowing.Api.Modules.Analytics;
using Xunit;

namespace SGrowing.Tests.Unit;

public class ClickHouseAnalyticsProjectionTests
{
    private readonly ClickHouseAnalyticsProjectionService _service = new();
    private readonly Guid _tenantId = Guid.NewGuid();

    [Fact]
    public async Task IngestBatchAndFlush_ShouldStoreRecordsCorrectly()
    {
        var records = new List<ClickHouseEventRecord>
        {
            new(Guid.NewGuid(), _tenantId, "ad.click", DateTime.UtcNow, "facebook", "camp-1", Guid.NewGuid(), 1.50m, 0m, "{}"),
            new(Guid.NewGuid(), _tenantId, "ad.conversion", DateTime.UtcNow, "facebook", "camp-1", Guid.NewGuid(), 0m, 45.00m, "{}")
        };

        await _service.IngestBatchAsync(records);
        Assert.Equal(2, _service.GetBufferedRecordCount());

        var flushed = await _service.FlushPendingBatchesAsync();
        Assert.Equal(2, flushed);
        Assert.Equal(0, _service.GetBufferedRecordCount());
    }

    [Fact]
    public async Task GetRealtimeRoasAsync_ShouldComputeAccurateRoasAndCpa()
    {
        var now = DateTime.UtcNow;
        var records = new List<ClickHouseEventRecord>
        {
            new(Guid.NewGuid(), _tenantId, "ad.click", now.AddMinutes(-5), "google", "camp-search", Guid.NewGuid(), 10.00m, 0m, "{}"),
            new(Guid.NewGuid(), _tenantId, "ad.click", now.AddMinutes(-4), "google", "camp-search", Guid.NewGuid(), 10.00m, 0m, "{}"),
            new(Guid.NewGuid(), _tenantId, "ad.conversion", now.AddMinutes(-3), "google", "camp-search", Guid.NewGuid(), 0m, 100.00m, "{}")
        };

        await _service.IngestBatchAsync(records);

        var metrics = await _service.GetRealtimeRoasAsync(_tenantId, now.AddHours(-1), now.AddHours(1), "google");

        Assert.Equal(_tenantId, metrics.TenantId);
        Assert.Equal("google", metrics.Channel);
        Assert.Equal(20.00m, metrics.TotalSpend);
        Assert.Equal(100.00m, metrics.TotalRevenue);
        Assert.Equal(5.0000m, metrics.Roas); // 100 / 20 = 5.0
        Assert.Equal(2, metrics.TotalClicks);
        Assert.Equal(1, metrics.TotalConversions);
        Assert.Equal(20.00m, metrics.CostPerAcquisition);
    }

    [Fact]
    public async Task GetFunnelConversionAsync_ShouldCalculateStepDropoffs()
    {
        var now = DateTime.UtcNow;
        var c1 = Guid.NewGuid();
        var c2 = Guid.NewGuid();
        var c3 = Guid.NewGuid();

        var records = new List<ClickHouseEventRecord>
        {
            // Step 1: visit (3 contacts)
            new(Guid.NewGuid(), _tenantId, "page_view", now, "web", null, c1, 0, 0, "{}"),
            new(Guid.NewGuid(), _tenantId, "page_view", now, "web", null, c2, 0, 0, "{}"),
            new(Guid.NewGuid(), _tenantId, "page_view", now, "web", null, c3, 0, 0, "{}"),

            // Step 2: add_to_cart (2 contacts)
            new(Guid.NewGuid(), _tenantId, "add_to_cart", now, "web", null, c1, 0, 0, "{}"),
            new(Guid.NewGuid(), _tenantId, "add_to_cart", now, "web", null, c2, 0, 0, "{}"),

            // Step 3: purchase (1 contact)
            new(Guid.NewGuid(), _tenantId, "purchase", now, "web", null, c1, 0, 80m, "{}")
        };

        await _service.IngestBatchAsync(records);

        var report = await _service.GetFunnelConversionAsync(
            _tenantId,
            now.AddHours(-1),
            now.AddHours(1),
            new[] { "page_view", "add_to_cart", "purchase" });

        Assert.Equal(3, report.Steps.Count);
        Assert.Equal(3, report.Steps[0].UniqueCount);
        Assert.Equal(1.0, report.Steps[0].ConversionRateFromPrevious);

        Assert.Equal(2, report.Steps[1].UniqueCount);
        Assert.Equal(0.6667, report.Steps[1].ConversionRateFromPrevious, 4);

        Assert.Equal(1, report.Steps[2].UniqueCount);
        Assert.Equal(0.5, report.Steps[2].ConversionRateFromPrevious, 4);
        Assert.Equal(0.3333, report.OverallConversionRate, 4);
    }

    [Fact]
    public async Task GetAgentExecutionTelemetryAggregationAsync_ShouldAggregatePersonaMetrics()
    {
        var now = DateTime.UtcNow;
        var records = new List<ClickHouseEventRecord>
        {
            new(Guid.NewGuid(), _tenantId, "agent.mission_complete", now, "Copywriter", "mission-1", null, 0.04m, 0m, "{\"tokens\": 850, \"durationMs\": 1200, \"success\": true}"),
            new(Guid.NewGuid(), _tenantId, "agent.mission_complete", now, "Copywriter", "mission-2", null, 0.06m, 0m, "{\"tokens\": 1150, \"durationMs\": 1800, \"success\": true}"),
            new(Guid.NewGuid(), _tenantId, "agent.mission_failed", now, "AdOptimizer", "mission-3", null, 0.02m, 0m, "{\"tokens\": 400, \"durationMs\": 900, \"success\": false}")
        };

        await _service.IngestBatchAsync(records);

        var summaries = await _service.GetAgentExecutionTelemetryAggregationAsync(_tenantId, now.AddHours(-1), now.AddHours(1));

        Assert.Equal(2, summaries.Count);

        var copywriter = summaries.First(s => s.AgentPersona == "Copywriter");
        Assert.Equal(2, copywriter.TotalMissions);
        Assert.Equal(2000, copywriter.TotalTokens);
        Assert.Equal(1500, copywriter.AvgDurationMs);
        Assert.Equal(0.10m, copywriter.TotalCost);
        Assert.Equal(1.0, copywriter.SuccessRate);

        var adOpt = summaries.First(s => s.AgentPersona == "AdOptimizer");
        Assert.Equal(1, adOpt.TotalMissions);
        Assert.Equal(400, adOpt.TotalTokens);
        Assert.Equal(0.0, adOpt.SuccessRate);
    }

    [Fact]
    public async Task ProjectFromStreamingMeshAsync_ShouldIngestAndCommitOffsets()
    {
        var mesh = new EventStreamingMeshService();
        var topic = "clickstream.events";
        var consumerGroup = "clickhouse-olap-worker";

        // Publish events across partitions
        await mesh.PublishEventAsync(topic, "tenant-a", new ClickHouseEventRecord(
            Guid.NewGuid(), _tenantId, "page_view", DateTime.UtcNow, "organic", null, null, 0, 0, "{}"));
        await mesh.PublishEventAsync(topic, "tenant-b", new ClickHouseEventRecord(
            Guid.NewGuid(), _tenantId, "ad.click", DateTime.UtcNow, "meta", "c1", null, 1.2m, 0, "{}"));

        var projected = await _service.ProjectFromStreamingMeshAsync(mesh, topic, consumerGroup);

        Assert.Equal(2, projected);

        // Subsequent projection without new events should yield 0
        var projectedAgain = await _service.ProjectFromStreamingMeshAsync(mesh, topic, consumerGroup);
        Assert.Equal(0, projectedAgain);
    }
}
