using System.Diagnostics;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SGrowing.Api.Data;
using SGrowing.Api.Modules.Telemetry;
using SGrowing.Core.Entities;
using SGrowing.Core.Tenancy;
using Xunit;

namespace SGrowing.Tests.LoadAndStress;

[Collection("ClickstreamBuffer")]
public class ClickstreamBufferLoadStressTests
{
    private SGrowingDbContext CreateDbContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<SGrowingDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;
        var tenantContext = new TenantContext();
        return new SGrowingDbContext(options, tenantContext);
    }

    [Fact]
    public async Task HighConcurrency_ClickstreamBuffer_ShouldQueueAndFlushWithoutLoss()
    {
        var dbName = Guid.NewGuid().ToString();
        var tenantId = Guid.NewGuid();
        const int concurrentWorkers = 20;
        const int eventsPerWorker = 50;
        const int totalExpectedEvents = concurrentWorkers * eventsPerWorker;

        // Drain any lingering static queue items before starting
        using (var cleanDb = CreateDbContext(dbName))
        {
            var cleanService = new ClickstreamBufferService(cleanDb);
            await cleanService.FlushBufferBatchAsync(100000);
        }

        var stopwatch = Stopwatch.StartNew();

        // 1. High-concurrency parallel event ingestion burst
        var tasks = Enumerable.Range(0, concurrentWorkers).Select(workerIndex => Task.Run(async () =>
        {
            using var db = CreateDbContext(dbName);
            var bufferService = new ClickstreamBufferService(db);

            for (int i = 0; i < eventsPerWorker; i++)
            {
                var ev = TelemetryEvent.Create(
                    tenantId,
                    sessionId: $"sess_{workerIndex}",
                    visitorId: $"vis_{workerIndex}_{i}",
                    eventName: "pageview",
                    url: $"/products/item-{i}",
                    deviceType: "Mobile",
                    browser: "Safari"
                );

                var queued = await bufferService.BufferEventAsync(ev);
                queued.Should().BeTrue();
            }
        })).ToArray();

        await Task.WhenAll(tasks);
        stopwatch.Stop();

        // 2. Verify all events are present in the pending buffer
        using (var verifyDb = CreateDbContext(dbName))
        {
            var bufferService = new ClickstreamBufferService(verifyDb);
            var pendingCount = bufferService.GetPendingBufferCount();
            pendingCount.Should().BeGreaterThanOrEqualTo(totalExpectedEvents);

            // 3. High-throughput chunked flush simulation under burst
            int totalFlushed = 0;
            while (bufferService.GetPendingBufferCount() > 0)
            {
                var flushed = await bufferService.FlushBufferBatchAsync(150);
                if (flushed == 0) break;
                totalFlushed += flushed;
            }

            totalFlushed.Should().BeGreaterThanOrEqualTo(totalExpectedEvents);

            // 4. Verify database persistence
            var savedCount = await verifyDb.TelemetryEvents
                .Where(e => e.TenantId == tenantId)
                .CountAsync();
            savedCount.Should().Be(totalExpectedEvents);
        }
    }

    [Fact]
    public async Task FlushBufferBatch_WhenBufferEmpty_ShouldReturnZeroWithoutError()
    {
        var dbName = Guid.NewGuid().ToString();
        using var db = CreateDbContext(dbName);
        var bufferService = new ClickstreamBufferService(db);

        // Ensure buffer empty
        await bufferService.FlushBufferBatchAsync(10000);

        var flushed = await bufferService.FlushBufferBatchAsync(50);
        flushed.Should().Be(0);
        bufferService.GetPendingBufferCount().Should().Be(0);
    }

    [Fact]
    public async Task BufferEventAsync_WithNullEvent_ShouldReturnFalse()
    {
        var dbName = Guid.NewGuid().ToString();
        using var db = CreateDbContext(dbName);
        var bufferService = new ClickstreamBufferService(db);

        var result = await bufferService.BufferEventAsync(null!);
        result.Should().BeFalse();
    }

    [Fact]
    public async Task ResetBuffer_ShouldEmptyAllQueuedEvents()
    {
        var dbName = Guid.NewGuid().ToString();
        using var db = CreateDbContext(dbName);
        var bufferService = new ClickstreamBufferService(db);

        var tenantId = Guid.NewGuid();
        for (int i = 0; i < 5; i++)
        {
            await bufferService.BufferEventAsync(TelemetryEvent.Create(tenantId, "sess", "vis", "click", "/cart", "Desktop", "Chrome"));
        }

        bufferService.GetPendingBufferCount().Should().BeGreaterThanOrEqualTo(5);

        ClickstreamBufferService.ResetBuffer();

        bufferService.GetPendingBufferCount().Should().Be(0);
    }

    [Fact]
    public async Task FlushBufferBatch_ClampsBatchSizeToMinimumOne()
    {
        var dbName = Guid.NewGuid().ToString();
        using var db = CreateDbContext(dbName);
        var bufferService = new ClickstreamBufferService(db);

        ClickstreamBufferService.ResetBuffer();

        var tenantId = Guid.NewGuid();
        var ev = TelemetryEvent.Create(tenantId, "sess", "vis", "view", "/checkout", "Tablet", "Firefox");
        await bufferService.BufferEventAsync(ev);

        // Pass 0 or negative batch size -> Math.Max(1, batchSize) clamps to 1
        var flushed = await bufferService.FlushBufferBatchAsync(-10);
        flushed.Should().Be(1);

        var count = await db.TelemetryEvents.CountAsync(e => e.TenantId == tenantId);
        count.Should().Be(1);
    }

    [Fact]
    public async Task GetPendingBufferCount_ShouldAccuratelyReflectEnqueuedEvents()
    {
        var dbName = Guid.NewGuid().ToString();
        using var db = CreateDbContext(dbName);
        var bufferService = new ClickstreamBufferService(db);

        ClickstreamBufferService.ResetBuffer();
        bufferService.GetPendingBufferCount().Should().Be(0);

        var tenantId = Guid.NewGuid();
        await bufferService.BufferEventAsync(TelemetryEvent.Create(tenantId, "s1", "v1", "e1", "/", "Mobile", "Edge"));
        bufferService.GetPendingBufferCount().Should().Be(1);

        await bufferService.BufferEventAsync(TelemetryEvent.Create(tenantId, "s2", "v2", "e2", "/", "Mobile", "Edge"));
        bufferService.GetPendingBufferCount().Should().Be(2);

        await bufferService.FlushBufferBatchAsync(1);
        bufferService.GetPendingBufferCount().Should().Be(1);

        await bufferService.FlushBufferBatchAsync(1);
        bufferService.GetPendingBufferCount().Should().Be(0);
    }

    [Fact]
    public async Task FlushBufferBatchAsync_WhenMultiTenantEvents_PersistsAcrossTenants()
    {
        var dbName = Guid.NewGuid().ToString();
        using var db = CreateDbContext(dbName);
        var bufferService = new ClickstreamBufferService(db);

        ClickstreamBufferService.ResetBuffer();

        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();

        await bufferService.BufferEventAsync(TelemetryEvent.Create(tenantA, "sA1", "vA1", "pageview", "/a", "Desktop", "Chrome"));
        await bufferService.BufferEventAsync(TelemetryEvent.Create(tenantB, "sB1", "vB1", "pageview", "/b", "Mobile", "Safari"));

        var flushed = await bufferService.FlushBufferBatchAsync(10);
        flushed.Should().Be(2);

        var countA = await db.TelemetryEvents.CountAsync(e => e.TenantId == tenantA);
        var countB = await db.TelemetryEvents.CountAsync(e => e.TenantId == tenantB);

        countA.Should().Be(1);
        countB.Should().Be(1);
    }

    [Fact]
    public async Task BufferEventAsync_HighThroughputMultiThreadedIngest_HandlesMixedDeviceTypes()
    {
        var dbName = Guid.NewGuid().ToString();
        using var db = CreateDbContext(dbName);
        var bufferService = new ClickstreamBufferService(db);

        ClickstreamBufferService.ResetBuffer();
        var tenantId = Guid.NewGuid();
        var devices = new[] { "Desktop", "Mobile", "Tablet", "SmartTV" };

        var tasks = Enumerable.Range(0, 16).Select(i => Task.Run(async () =>
        {
            var device = devices[i % devices.Length];
            var ev = TelemetryEvent.Create(tenantId, $"sess_{i}", $"vis_{i}", "interaction", $"/feature/{i}", device, "Browser");
            await bufferService.BufferEventAsync(ev);
        })).ToArray();

        await Task.WhenAll(tasks);

        bufferService.GetPendingBufferCount().Should().Be(16);

        var flushed = await bufferService.FlushBufferBatchAsync(50);
        flushed.Should().Be(16);

        var count = await db.TelemetryEvents.CountAsync(e => e.TenantId == tenantId);
        count.Should().Be(16);
    }
}
