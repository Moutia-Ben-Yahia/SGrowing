using FluentAssertions;
using SGrowing.Core.Entities;
using Xunit;

namespace SGrowing.Tests.Outbox;

public class OutboxProcessorTests
{
    [Fact]
    public void OutboxMessage_WhenCreated_ShouldHavePendingStatus()
    {
        var tenantId = Guid.NewGuid();
        var message = OutboxMessage.Create(tenantId, "ContactCreated", "{\"email\":\"alice@example.com\"}");

        message.Id.Should().NotBeEmpty();
        message.TenantId.Should().Be(tenantId);
        message.EventType.Should().Be("ContactCreated");
        message.ProcessedOnUtc.Should().BeNull();
        message.RetryCount.Should().Be(0);
        message.Error.Should().BeNull();
    }

    [Fact]
    public void OutboxMessage_WhenMarkedProcessed_ShouldRecordTimestamp()
    {
        var message = OutboxMessage.Create(Guid.NewGuid(), "InboundMessageReceived", "{}");

        message.MarkProcessed();

        message.ProcessedOnUtc.Should().NotBeNull();
        message.Error.Should().BeNull();
    }

    [Fact]
    public void OutboxMessage_WhenFailed_ShouldIncrementRetryCountAndRecordError()
    {
        var message = OutboxMessage.Create(Guid.NewGuid(), "InboundMessageReceived", "{}");

        message.MarkFailed("Network timeout connecting to webhook");

        message.ProcessedOnUtc.Should().BeNull();
        message.RetryCount.Should().Be(1);
        message.Error.Should().Be("Network timeout connecting to webhook");
    }
}
