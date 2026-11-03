using FluentAssertions;
using SGrowing.Core.Common;
using Xunit;

namespace SGrowing.Tests.Concurrency;

public class DistributedMemoryLockServiceTests
{
    [Fact]
    public async Task TryAcquireLockAsync_ValidResourceKey_AcquiresSuccessfully()
    {
        var lockService = new DistributedMemoryLockService();
        var key = "order:lock:ORD-1001";

        await using var handle = await lockService.TryAcquireLockAsync(
            key,
            expiry: TimeSpan.FromSeconds(5),
            waitTimeout: TimeSpan.FromMilliseconds(100));

        handle.Should().NotBeNull();
        handle!.IsAcquired.Should().BeTrue();
        handle.ResourceKey.Should().Be(key);
        handle.LockToken.Should().NotBeNullOrWhiteSpace();
        handle.ExpiresAtUtc.Should().BeAfter(DateTime.UtcNow);

        var isLocked = await lockService.IsLockedAsync(key);
        isLocked.Should().BeTrue();
    }

    [Fact]
    public async Task TryAcquireLockAsync_EmptyResourceKey_ThrowsArgumentNullException()
    {
        var lockService = new DistributedMemoryLockService();

        var act = async () => await lockService.TryAcquireLockAsync("");

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task TryAcquireLockAsync_WhenAlreadyLockedByAnother_ReturnsNullAfterTimeout()
    {
        var lockService = new DistributedMemoryLockService();
        var key = "inventory:stock:TUNIS-01:SKU-99";

        await using var handle1 = await lockService.TryAcquireLockAsync(
            key,
            expiry: TimeSpan.FromSeconds(5),
            waitTimeout: TimeSpan.FromMilliseconds(50));

        handle1.Should().NotBeNull();

        // Second acquisition attempt should fail within 50ms wait timeout
        var handle2 = await lockService.TryAcquireLockAsync(
            key,
            expiry: TimeSpan.FromSeconds(5),
            waitTimeout: TimeSpan.FromMilliseconds(50));

        handle2.Should().BeNull();
    }

    [Fact]
    public async Task ReleaseLockAsync_WithCorrectToken_ReleasesLockAndAllowsReacquisition()
    {
        var lockService = new DistributedMemoryLockService();
        var key = "courier:driver:DRV-44";

        var handle1 = await lockService.TryAcquireLockAsync(key, expiry: TimeSpan.FromSeconds(5));
        handle1.Should().NotBeNull();

        var released = await lockService.ReleaseLockAsync(key, handle1!.LockToken);
        released.Should().BeTrue();

        var isLocked = await lockService.IsLockedAsync(key);
        isLocked.Should().BeFalse();

        // Should now be able to acquire again
        await using var handle2 = await lockService.TryAcquireLockAsync(key, expiry: TimeSpan.FromSeconds(5));
        handle2.Should().NotBeNull();
        handle2!.IsAcquired.Should().BeTrue();
    }

    [Fact]
    public async Task ReleaseLockAsync_WithWrongToken_FailsAndMaintainsLock()
    {
        var lockService = new DistributedMemoryLockService();
        var key = "payout:vendor:VEND-88";

        await using var handle = await lockService.TryAcquireLockAsync(key, expiry: TimeSpan.FromSeconds(5));
        handle.Should().NotBeNull();

        var released = await lockService.ReleaseLockAsync(key, "invalid_foreign_token_999");
        released.Should().BeFalse();

        var isLocked = await lockService.IsLockedAsync(key);
        isLocked.Should().BeTrue();
    }

    [Fact]
    public async Task DisposeAsync_OnHandle_AutomaticallyReleasesLock()
    {
        var lockService = new DistributedMemoryLockService();
        var key = "cart:reservation:CART-552";

        var handle = await lockService.TryAcquireLockAsync(key, expiry: TimeSpan.FromSeconds(5));
        handle.Should().NotBeNull();

        await handle!.DisposeAsync();

        var isLocked = await lockService.IsLockedAsync(key);
        isLocked.Should().BeFalse();
    }

    [Fact]
    public async Task TryAcquireLockAsync_AfterLeaseExpires_AllowsReacquisition()
    {
        var lockService = new DistributedMemoryLockService();
        var key = "transient:rate:limit:ip-127-0-0-1";

        // Acquire with very short lease (100ms)
        var handle1 = await lockService.TryAcquireLockAsync(key, expiry: TimeSpan.FromMilliseconds(100));
        handle1.Should().NotBeNull();

        // Wait for lease to expire
        await Task.Delay(150);

        // Even without explicit release, lock is expired and can be reacquired
        await using var handle2 = await lockService.TryAcquireLockAsync(
            key,
            expiry: TimeSpan.FromSeconds(5),
            waitTimeout: TimeSpan.FromMilliseconds(500));

        handle2.Should().NotBeNull();
        handle2!.IsAcquired.Should().BeTrue();
    }
}
