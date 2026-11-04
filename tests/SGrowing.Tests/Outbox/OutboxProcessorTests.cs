using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using SGrowing.Api.Data;
using SGrowing.Core.Entities;
using SGrowing.Worker.Services;
using Xunit;

namespace SGrowing.Tests.Outbox;

public class OutboxProcessorTests
{
    private SGrowingDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<SGrowingDbContext>()
            .UseInMemoryDatabase(databaseName: $"OutboxProcessorTestDb_{Guid.NewGuid()}")
            .Options;
        return new SGrowingDbContext(options);
    }

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

    [Fact]
    public void OutboxMessage_Create_ThrowsArgumentException_WhenTenantIdIsEmpty()
    {
        var act = () => OutboxMessage.Create(Guid.Empty, "InvoiceCreated", "{}");
        act.Should().Throw<ArgumentException>().WithParameterName("tenantId");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void OutboxMessage_Create_ThrowsArgumentException_WhenEventTypeIsWhitespaceOrNull(string? invalidEventType)
    {
        var act = () => OutboxMessage.Create(Guid.NewGuid(), invalidEventType!, "{}");
        act.Should().Throw<ArgumentException>().WithParameterName("eventType");
    }

    [Fact]
    public void OutboxMessage_MarkFailed_AccumulatesRetryCountAcrossMultipleFailures()
    {
        var message = OutboxMessage.Create(Guid.NewGuid(), "PaymentReceived", "{}");

        message.MarkFailed("Attempt 1: Connection refused");
        message.RetryCount.Should().Be(1);
        message.Error.Should().Be("Attempt 1: Connection refused");

        message.MarkFailed("Attempt 2: 504 Gateway Timeout");
        message.RetryCount.Should().Be(2);
        message.Error.Should().Be("Attempt 2: 504 Gateway Timeout");

        message.MarkFailed("Attempt 3: Host unreachable");
        message.RetryCount.Should().Be(3);
        message.Error.Should().Be("Attempt 3: Host unreachable");
    }

    [Fact]
    public async Task OutboxProcessorDaemon_ProcessBatchAsync_ProcessesPendingMessagesInAscendingOrder()
    {
        var dbName = $"OutboxProcessorTestDb_{Guid.NewGuid()}";
        var tenantId = Guid.NewGuid();

        var msg1 = OutboxMessage.Create(tenantId, "OrderCreated", "{\"orderId\":\"101\"}");
        var msg2 = OutboxMessage.Create(tenantId, "OrderShipped", "{\"orderId\":\"101\"}");

        var options = new DbContextOptionsBuilder<SGrowingDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .Options;

        await using (var seedDb = new SGrowingDbContext(options))
        {
            seedDb.OutboxMessages.AddRange(msg1, msg2);
            await seedDb.SaveChangesAsync();
        }

        var services = new ServiceCollection();
        services.AddDbContext<SGrowingDbContext>(opt => opt.UseInMemoryDatabase(dbName));
        var provider = services.BuildServiceProvider();

        var daemon = new OutboxProcessorDaemon(provider, NullLogger<OutboxProcessorDaemon>.Instance);
        var processedCount = await daemon.ProcessBatchAsync();

        processedCount.Should().Be(2);

        await using (var verifyDb = new SGrowingDbContext(options))
        {
            var refreshed1 = await verifyDb.OutboxMessages.FindAsync(msg1.Id);
            var refreshed2 = await verifyDb.OutboxMessages.FindAsync(msg2.Id);

            refreshed1!.ProcessedOnUtc.Should().NotBeNull();
            refreshed2!.ProcessedOnUtc.Should().NotBeNull();
            refreshed1.Error.Should().BeNull();
            refreshed2.Error.Should().BeNull();
        }
    }

    [Fact]
    public async Task OutboxProcessorDaemon_ProcessBatchAsync_IgnoresAlreadyProcessedMessages()
    {
        var dbName = $"OutboxProcessorTestDb_{Guid.NewGuid()}";
        var tenantId = Guid.NewGuid();

        var processedMsg = OutboxMessage.Create(tenantId, "ContactUpdated", "{}");
        processedMsg.MarkProcessed();

        var options = new DbContextOptionsBuilder<SGrowingDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .Options;

        await using (var seedDb = new SGrowingDbContext(options))
        {
            seedDb.OutboxMessages.Add(processedMsg);
            await seedDb.SaveChangesAsync();
        }

        var services = new ServiceCollection();
        services.AddDbContext<SGrowingDbContext>(opt => opt.UseInMemoryDatabase(dbName));
        var provider = services.BuildServiceProvider();

        var daemon = new OutboxProcessorDaemon(provider, NullLogger<OutboxProcessorDaemon>.Instance);
        var count = await daemon.ProcessBatchAsync();

        count.Should().Be(0);
    }

    [Fact]
    public async Task OutboxProcessorDaemon_ProcessBatchAsync_WhenNoMessages_ReturnsZero()
    {
        var dbName = $"OutboxProcessorTestDb_{Guid.NewGuid()}";
        var services = new ServiceCollection();
        services.AddDbContext<SGrowingDbContext>(opt => opt.UseInMemoryDatabase(dbName));
        var provider = services.BuildServiceProvider();

        var daemon = new OutboxProcessorDaemon(provider, NullLogger<OutboxProcessorDaemon>.Instance);
        var count = await daemon.ProcessBatchAsync();

        count.Should().Be(0);
    }
}
