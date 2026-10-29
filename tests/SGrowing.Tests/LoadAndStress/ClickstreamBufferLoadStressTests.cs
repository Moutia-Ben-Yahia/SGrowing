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
        tenantContext.SetTenant(Guid.NewGuid());
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
}
