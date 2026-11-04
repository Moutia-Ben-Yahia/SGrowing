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

    [Fact]
    public async Task IsLockedAsync_WhenResourceNotLocked_ReturnsFalse()
    {
        var lockService = new RedisConcurrencyLockService();
        var resourceKey = $"unlocked:resource:{Guid.NewGuid():N}";

        var isLocked = await lockService.IsLockedAsync(resourceKey);
        isLocked.Should().BeFalse();
    }

    [Fact]
    public async Task TryAcquireLockAsync_MultipleDistinctKeys_CanBeHeldConcurrently()
    {
        var lockService = new RedisConcurrencyLockService();
        var key1 = $"resource:alpha:{Guid.NewGuid():N}";
        var key2 = $"resource:beta:{Guid.NewGuid():N}";

        await using var handle1 = await lockService.TryAcquireLockAsync(key1, TimeSpan.FromSeconds(5), TimeSpan.FromMilliseconds(50));
        await using var handle2 = await lockService.TryAcquireLockAsync(key2, TimeSpan.FromSeconds(5), TimeSpan.FromMilliseconds(50));

        handle1.Should().NotBeNull();
        handle1!.IsAcquired.Should().BeTrue();
        handle2.Should().NotBeNull();
        handle2!.IsAcquired.Should().BeTrue();

        (await lockService.IsLockedAsync(key1)).Should().BeTrue();
        (await lockService.IsLockedAsync(key2)).Should().BeTrue();
    }

    [Fact]
    public async Task TryAcquireLockAsync_ZeroWaitTimeout_WhenLocked_FailsImmediately()
    {
        var lockService = new RedisConcurrencyLockService();
        var resourceKey = $"fast:lock:{Guid.NewGuid():N}";

        await using var primary = await lockService.TryAcquireLockAsync(resourceKey, TimeSpan.FromSeconds(10), TimeSpan.FromMilliseconds(50));
        primary.Should().NotBeNull();

        await using var contender = await lockService.TryAcquireLockAsync(resourceKey, TimeSpan.FromSeconds(10), TimeSpan.Zero);
        contender.Should().BeNull();
    }
}
