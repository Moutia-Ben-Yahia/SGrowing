using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using SGrowing.Api.Common.Caching;
using SGrowing.Api.Common.Outbox;
using SGrowing.Api.Data;
using SGrowing.Core.Entities;
using Xunit;

namespace SGrowing.Tests.Integration;

public class MultiTenantConcurrencyStressTests
{
    private class MockDistributedCache : IDistributedCache
    {
        private readonly ConcurrentDictionary<string, byte[]> _store = new();
        public byte[]? Get(string key) => _store.TryGetValue(key, out var val) ? val : null;
        public Task<byte[]?> GetAsync(string key, System.Threading.CancellationToken token = default) => Task.FromResult(Get(key));
        public void Refresh(string key) { }
        public Task RefreshAsync(string key, System.Threading.CancellationToken token = default) => Task.CompletedTask;
        public void Remove(string key) => _store.TryRemove(key, out _);
        public Task RemoveAsync(string key, System.Threading.CancellationToken token = default) { Remove(key); return Task.CompletedTask; }
        public void Set(string key, byte[] value, DistributedCacheEntryOptions options) => _store[key] = value;
        public Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options, System.Threading.CancellationToken token = default) { Set(key, value, options); return Task.CompletedTask; }
    }

    [Fact]
    public async Task ConcurrencyStress_5Tenants_ParallelOperations_MaintainsStrictIsolation()
    {
        var dbOptions = new DbContextOptionsBuilder<SGrowingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var distributedCache = new MockDistributedCache();
        var hybridCache = new HybridTenantSettingsCache(memoryCache, distributedCache);

        var tenants = Enumerable.Range(1, 5).Select(_ => Guid.NewGuid()).ToList();

        // Run parallel tenant operations across 50 tasks
        var tasks = tenants.SelectMany(tenantId =>
            Enumerable.Range(1, 10).Select(async opIndex =>
            {
                using var db = new SGrowingDbContext(dbOptions);
                var outboxService = new OutboxService(db);

                // 1. Write tenant-specific setting
                var settingKey = $"tenant_{tenantId}:FeatureFlag_Module_{opIndex}";
                var settingVal = $"Enabled_For_Tenant_{tenantId}";
                await hybridCache.SetAsync(settingKey, settingVal);

                // 2. Read tenant-specific setting
                var retrievedVal = await hybridCache.GetAsync<string>(settingKey);
                retrievedVal.Should().Be(settingVal);

                // 3. Atomically enqueue outbox message
                await outboxService.EnqueueAsync(
                    tenantId: tenantId,
                    eventType: $"StressTest.Event_{opIndex}",
                    payload: new { Op = opIndex, Tenant = tenantId },
                    correlationId: $"corr_{tenantId}_{opIndex}");
            })
        ).ToList();

        await Task.WhenAll(tasks);

        // Verification: Ensure all 50 outbox messages exist with correct tenant isolation
        using var verifyDb = new SGrowingDbContext(dbOptions);
        var allMessages = await verifyDb.TransactionalOutboxMessages.ToListAsync();

        allMessages.Should().HaveCount(50);

        foreach (var tenantId in tenants)
        {
            var tenantMessages = allMessages.Where(m => m.TenantId == tenantId).ToList();
            tenantMessages.Should().HaveCount(10);
            tenantMessages.All(m => m.CorrelationId!.StartsWith($"corr_{tenantId}_")).Should().BeTrue();
        }
    }
}
