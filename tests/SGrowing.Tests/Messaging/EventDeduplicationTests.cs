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
}
