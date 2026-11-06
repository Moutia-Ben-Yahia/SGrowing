using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SGrowing.Api.Modules.Analytics;

namespace SGrowing.Api.Common.Streaming;

public record LoadTestRunResult(
    int TotalEventsInjected,
    int PartitionsUtilized,
    double EventsPerSecond,
    long TotalDurationMs,
    bool ZeroMessageLoss,
    decimal TotalProjectedRevenue,
    DateTime FinishedAtUtc
);

public interface ISyntheticTrafficLoadGeneratorService
{
    Task<LoadTestRunResult> ExecuteLoadBurstAsync(
        IEventStreamingMesh mesh,
        IClickHouseAnalyticsProjectionService olap,
        int totalEvents = 1000,
        int concurrency = 8,
        CancellationToken ct = default);
}

/// <summary>
/// SRE High-concurrency synthetic traffic load generator.
/// Injects high-volume partitioned events into IEventStreamingMesh and validates
/// real-time ClickHouse OLAP projections under sustained load (PRD Domain E & SRE Benchmarking).
/// </summary>
public class SyntheticTrafficLoadGeneratorService : ISyntheticTrafficLoadGeneratorService
{
    private readonly ILogger<SyntheticTrafficLoadGeneratorService>? _logger;

    public SyntheticTrafficLoadGeneratorService(ILogger<SyntheticTrafficLoadGeneratorService>? logger = null)
    {
        _logger = logger;
    }

    public async Task<LoadTestRunResult> ExecuteLoadBurstAsync(
        IEventStreamingMesh mesh,
        IClickHouseAnalyticsProjectionService olap,
        int totalEvents = 1000,
        int concurrency = 8,
        CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        var topic = "clickstream.events";
        var consumerGroup = "sre-load-projection-worker";

        var uniqueTenants = new[]
        {
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            Guid.Parse("22222222-2222-2222-2222-222222222222"),
            Guid.Parse("33333333-3333-3333-3333-333333333333"),
            Guid.Parse("44444444-4444-4444-4444-444444444444")
        };

        var utilizedPartitions = new ConcurrentDictionary<int, bool>();
        decimal accumulatedRevenue = 0m;
        var revenueLock = new System.Threading.Lock();

        // 1. Concurrently publish events
        var eventIndexes = Enumerable.Range(0, totalEvents).ToList();
        var parallelOptions = new ParallelOptions
        {
            MaxDegreeOfParallelism = Math.Max(1, concurrency),
            CancellationToken = ct
        };

        await Parallel.ForEachAsync(eventIndexes, parallelOptions, async (idx, token) =>
        {
            var tenantId = uniqueTenants[idx % uniqueTenants.Length];
            var partitionKey = $"contact_{tenantId:N}_{idx % 50}";
            var partitionId = mesh.ComputePartition(topic, partitionKey);
            utilizedPartitions.TryAdd(partitionId, true);

            var isPurchase = (idx % 10) == 0;
            var cost = isPurchase ? 0m : 0.75m;
            var revenue = isPurchase ? 65.00m : 0m;

            if (revenue > 0)
            {
                lock (revenueLock)
                {
                    accumulatedRevenue += revenue;
                }
            }

            var evt = new ClickHouseEventRecord(
                EventId: Guid.NewGuid(),
                TenantId: tenantId,
                EventType: isPurchase ? "purchase" : "ad.click",
                TimestampUtc: DateTime.UtcNow,
                Channel: idx % 2 == 0 ? "meta" : "google",
                CampaignId: $"camp_{idx % 10}",
                ContactId: Guid.NewGuid(),
                Cost: cost,
                Revenue: revenue,
                MetadataJson: "{}"
            );

            await mesh.PublishEventAsync(topic, partitionKey, evt, token);
        });

        // 2. Project from streaming mesh into ClickHouse until all partitions are drained
        var totalProjectedCount = 0;
        while (true)
        {
            var batchCount = await olap.ProjectFromStreamingMeshAsync(mesh, topic, consumerGroup, maxBatchSize: 1000, ct: ct);
            if (batchCount == 0)
            {
                break;
            }
            totalProjectedCount += batchCount;
        }

        sw.Stop();
        var elapsedMs = Math.Max(1, sw.ElapsedMilliseconds);
        var eps = Math.Round((double)totalEvents / (elapsedMs / 1000.0), 2);

        _logger?.LogInformation(
            "Load test burst completed: {Events} events injected across {Partitions} partitions in {Elapsed}ms ({EPS} events/sec)",
            totalEvents,
            utilizedPartitions.Count,
            elapsedMs,
            eps);

        return new LoadTestRunResult(
            TotalEventsInjected: totalEvents,
            PartitionsUtilized: utilizedPartitions.Count,
            EventsPerSecond: eps,
            TotalDurationMs: elapsedMs,
            ZeroMessageLoss: totalProjectedCount == totalEvents,
            TotalProjectedRevenue: accumulatedRevenue,
            FinishedAtUtc: DateTime.UtcNow
        );
    }
}
