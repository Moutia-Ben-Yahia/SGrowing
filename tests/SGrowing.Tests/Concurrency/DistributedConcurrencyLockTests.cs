using FluentAssertions;
using SGrowing.Api.Common.Concurrency;
using SGrowing.Core.Common;
using Xunit;

namespace SGrowing.Tests.Concurrency;

public class DistributedConcurrencyLockTests
{
    [Fact]
    public async Task TryAcquireLockAsync_WhenResourceNotLocked_AcquiresSuccessfully()
    {
        var lockService = new RedisConcurrencyLockService(); // Uses memory fallback when Redis is null
        var resourceKey = "warehouse:stock:TUNIS-01:SKU-99";

        await using var handle = await lockService.TryAcquireLockAsync(
            resourceKey,
            expiry: TimeSpan.FromSeconds(5),
            waitTimeout: TimeSpan.FromMilliseconds(100));

        handle.Should().NotBeNull();
        handle!.IsAcquired.Should().BeTrue();
        handle.ResourceKey.Should().Be(resourceKey);
        handle.LockToken.Should().NotBeNullOrWhiteSpace();

        var isLocked = await lockService.IsLockedAsync(resourceKey);
        isLocked.Should().BeTrue();
    }

    [Fact]
    public async Task TryAcquireLockAsync_WhenResourceAlreadyLocked_ReturnsNullAfterTimeout()
    {
        var lockService = new RedisConcurrencyLockService();
        var resourceKey = "order:lock:ORD-8821";

        await using var firstHandle = await lockService.TryAcquireLockAsync(
            resourceKey,
            expiry: TimeSpan.FromSeconds(5),
            waitTimeout: TimeSpan.FromMilliseconds(100));

        firstHandle.Should().NotBeNull();

        // Second acquisition attempt should fail and return null
        var secondHandle = await lockService.TryAcquireLockAsync(
            resourceKey,
            expiry: TimeSpan.FromSeconds(5),
            waitTimeout: TimeSpan.FromMilliseconds(50));

        secondHandle.Should().BeNull();
    }

    [Fact]
    public async Task DisposeAsync_ReleasesLock_AllowingSecondCallerToAcquire()
    {
        var lockService = new RedisConcurrencyLockService();
        var resourceKey = "sweeper:cart:inventory:TENANT-42";

        var firstHandle = await lockService.TryAcquireLockAsync(
            resourceKey,
            expiry: TimeSpan.FromSeconds(5),
            waitTimeout: TimeSpan.FromMilliseconds(100));

        firstHandle.Should().NotBeNull();

        // Release first lock via DisposeAsync
        await firstHandle!.DisposeAsync();

        var isLockedAfterRelease = await lockService.IsLockedAsync(resourceKey);
        isLockedAfterRelease.Should().BeFalse();

        // Now second acquisition should succeed
        await using var secondHandle = await lockService.TryAcquireLockAsync(
            resourceKey,
            expiry: TimeSpan.FromSeconds(5),
            waitTimeout: TimeSpan.FromMilliseconds(100));

        secondHandle.Should().NotBeNull();
        secondHandle!.IsAcquired.Should().BeTrue();
    }
}
