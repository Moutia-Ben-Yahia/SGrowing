using System;
using System.Threading.Tasks;
using FluentAssertions;
using SGrowing.Api.Modules.Messaging;
using Xunit;

namespace SGrowing.Tests.Messaging;

public class EventDeduplicationTests
{
    [Fact]
    public async Task TryAcquireAsync_FirstAttempt_Succeeds()
    {
        // Arrange
        var service = new EventDeduplicationService();
        var tenantId = Guid.NewGuid();
        string eventId = $"evt-{Guid.NewGuid():N}";

        // Act
        bool acquired = await service.TryAcquireAsync(tenantId, eventId, "{\"orderId\":\"123\"}", TimeSpan.FromHours(1));

        // Assert
        acquired.Should().BeTrue();
    }

    [Fact]
    public async Task TryAcquireAsync_DuplicateEvent_ReturnsFalse()
    {
        // Arrange
        var service = new EventDeduplicationService();
        var tenantId = Guid.NewGuid();
        string eventId = $"evt-dup-{Guid.NewGuid():N}";

        // Act
        bool first = await service.TryAcquireAsync(tenantId, eventId, "{\"status\":\"delivered\"}", TimeSpan.FromHours(1));
        bool duplicate = await service.TryAcquireAsync(tenantId, eventId, "{\"status\":\"delivered\"}", TimeSpan.FromHours(1));

        // Assert
        first.Should().BeTrue();
        duplicate.Should().BeFalse("duplicate webhook within 1 hour sliding TTL must be rejected");
    }

    [Fact]
    public async Task IsProcessedAsync_AfterMarkProcessed_ReturnsTrue()
    {
        // Arrange
        var service = new EventDeduplicationService();
        var tenantId = Guid.NewGuid();
        string eventId = $"evt-proc-{Guid.NewGuid():N}";

        // Act
        await service.TryAcquireAsync(tenantId, eventId, null, TimeSpan.FromHours(1));
        bool beforeMark = await service.IsProcessedAsync(tenantId, eventId);

        await service.MarkProcessedAsync(tenantId, eventId);
        bool afterMark = await service.IsProcessedAsync(tenantId, eventId);

        // Assert
        beforeMark.Should().BeFalse();
        afterMark.Should().BeTrue();
    }

    [Fact]
    public async Task TryAcquireAsync_DifferentTenants_CanAcquireSameEventIdIndependently()
    {
        // Arrange
        var service = new EventDeduplicationService();
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        string sharedEventId = $"webhook-order-{Guid.NewGuid():N}";

        // Act
        bool acquiredA = await service.TryAcquireAsync(tenantA, sharedEventId, "{}", TimeSpan.FromHours(1));
        bool acquiredB = await service.TryAcquireAsync(tenantB, sharedEventId, "{}", TimeSpan.FromHours(1));

        // Assert
        acquiredA.Should().BeTrue();
        acquiredB.Should().BeTrue("event IDs must be partitioned strictly per tenant");
    }

    [Fact]
    public async Task TryAcquireAsync_DifferentEventIds_SameTenant_BothSucceed()
    {
        // Arrange
        var service = new EventDeduplicationService();
        var tenantId = Guid.NewGuid();
        string evt1 = $"evt-seq-1-{Guid.NewGuid():N}";
        string evt2 = $"evt-seq-2-{Guid.NewGuid():N}";

        // Act
        bool acq1 = await service.TryAcquireAsync(tenantId, evt1, "payload1", TimeSpan.FromMinutes(30));
        bool acq2 = await service.TryAcquireAsync(tenantId, evt2, "payload2", TimeSpan.FromMinutes(30));

        // Assert
        acq1.Should().BeTrue();
        acq2.Should().BeTrue();
    }

    [Fact]
    public async Task IsProcessedAsync_UnacquiredEvent_ReturnsFalse()
    {
        // Arrange
        var service = new EventDeduplicationService();
        var tenantId = Guid.NewGuid();
        string unacquiredId = $"evt-never-seen-{Guid.NewGuid():N}";

        // Act
        bool isProc = await service.IsProcessedAsync(tenantId, unacquiredId);

        // Assert
        isProc.Should().BeFalse();
    }

    [Fact]
    public async Task TryAcquireAsync_ExpiredTtl_CanReacquire()
    {
        // Arrange
        var service = new EventDeduplicationService();
        var tenantId = Guid.NewGuid();
        string eventId = $"evt-exp-{Guid.NewGuid():N}";

        // Act: Acquire with negative TTL so it is already expired
        bool first = await service.TryAcquireAsync(tenantId, eventId, "initial", TimeSpan.FromMilliseconds(-10));
        first.Should().BeTrue();

        // Second acquisition after expiration should succeed
        bool reacquired = await service.TryAcquireAsync(tenantId, eventId, "retry", TimeSpan.FromMinutes(10));

        // Assert
        reacquired.Should().BeTrue("expired deduplication locks must be cleared to allow retries");
    }
}
