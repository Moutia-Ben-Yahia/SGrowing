using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using SGrowing.Api.Common.Caching;
using Xunit;

namespace SGrowing.Tests.Caching;

public class HybridTenantSettingsCacheTests
{
    private record SampleTenantSetting(string TenantSlug, bool FeatureEnabled, int MaxSeats);

    [Fact]
    public async Task SetAsync_And_GetAsync_ShouldReturnCachedValue()
    {
        var cache = new HybridTenantSettingsCache();
        const string key = "tenant:123:branding";
        var setting = new SampleTenantSetting("AcmeCorp", true, 50);

        await cache.SetAsync(key, setting, TimeSpan.FromMinutes(5));

        var retrieved = await cache.GetAsync<SampleTenantSetting>(key);
        retrieved.Should().NotBeNull();
        retrieved!.TenantSlug.Should().Be("AcmeCorp");
        retrieved.FeatureEnabled.Should().BeTrue();
        retrieved.MaxSeats.Should().Be(50);
    }

    [Fact]
    public async Task GetOrCreateAsync_WhenCacheMiss_ShouldInvokeFactoryAndCacheResult()
    {
        var cache = new HybridTenantSettingsCache();
        const string key = "tenant:456:limits";
        int factoryInvocations = 0;

        var firstCall = await cache.GetOrCreateAsync(key, ct =>
        {
            Interlocked.Increment(ref factoryInvocations);
            return Task.FromResult(new SampleTenantSetting("BetaCorp", false, 10));
        }, TimeSpan.FromMinutes(10));

        firstCall.Should().NotBeNull();
        firstCall.TenantSlug.Should().Be("BetaCorp");
        factoryInvocations.Should().Be(1);

        // Second call should hit L1 cache and NOT invoke factory
        var secondCall = await cache.GetOrCreateAsync(key, ct =>
        {
            Interlocked.Increment(ref factoryInvocations);
            return Task.FromResult(new SampleTenantSetting("DifferentCorp", true, 999));
        }, TimeSpan.FromMinutes(10));

        secondCall.Should().NotBeNull();
        secondCall.TenantSlug.Should().Be("BetaCorp");
        factoryInvocations.Should().Be(1);
    }

    [Fact]
    public async Task RemoveAsync_ShouldEvictKey_AndSubsequentGetReturnsNull()
    {
        var cache = new HybridTenantSettingsCache();
        const string key = "tenant:789:domain";
        await cache.SetAsync(key, "subdomain.example.com", TimeSpan.FromMinutes(5));

        var beforeRemove = await cache.GetAsync<string>(key);
        beforeRemove.Should().Be("subdomain.example.com");

        await cache.RemoveAsync(key);

        var afterRemove = await cache.GetAsync<string>(key);
        afterRemove.Should().BeNull();
    }

    [Fact]
    public async Task Expiration_ShouldEvictKey_WhenTimeElapsed()
    {
        var cache = new HybridTenantSettingsCache();
        const string key = "tenant:temp:token";

        // Expire in 60 milliseconds
        await cache.SetAsync(key, "temp_token_abc", TimeSpan.FromMilliseconds(60));

        var immediate = await cache.GetAsync<string>(key);
        immediate.Should().Be("temp_token_abc");

        // Wait for expiration
        await Task.Delay(130);

        var expired = await cache.GetAsync<string>(key);
        expired.Should().BeNull();
    }

    [Fact]
    public async Task GetOrCreateAsync_UnderConcurrentAccess_ShouldBeThreadSafeAndInvokeFactoryOnce()
    {
        var cache = new HybridTenantSettingsCache();
        const string key = "tenant:concurrent:config";
        int factoryCallCount = 0;

        // Launch 25 concurrent tasks
        var tasks = Enumerable.Range(0, 25).Select(_ => Task.Run(async () =>
        {
            return await cache.GetOrCreateAsync(key, async ct =>
            {
                Interlocked.Increment(ref factoryCallCount);
                await Task.Delay(25, ct); // Simulate expensive DB/RPC fetch
                return new SampleTenantSetting("ConcurrentTenant", true, 100);
            }, TimeSpan.FromMinutes(5));
        })).ToList();

        var results = await Task.WhenAll(tasks);

        factoryCallCount.Should().Be(1, "stampede protection should guarantee factory is invoked exactly once");
        results.Should().HaveCount(25);
        results.All(r => r.TenantSlug == "ConcurrentTenant" && r.MaxSeats == 100).Should().BeTrue();
    }

    [Fact]
    public void AddGrowthOsCaching_ShouldRegisterServicesInServiceCollection()
    {
        var services = new ServiceCollection();
        services.AddGrowthOsCaching();

        using var provider = services.BuildServiceProvider();
        var cache = provider.GetService<ITenantSettingsCache>();

        cache.Should().NotBeNull();
        cache.Should().BeOfType<HybridTenantSettingsCache>();
    }
}
