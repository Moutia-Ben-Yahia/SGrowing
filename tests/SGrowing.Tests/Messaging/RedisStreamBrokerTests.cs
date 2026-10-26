using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using SGrowing.Api.Data;
using SGrowing.Api.Modules.Messaging;
using SGrowing.Core.Entities;
using SGrowing.Core.Messaging;
using SGrowing.Worker.Services;
using Xunit;

namespace SGrowing.Tests.Messaging;

public class RedisStreamBrokerTests
{
    private SGrowingDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<SGrowingDbContext>()
            .UseInMemoryDatabase(databaseName: $"StreamDb_{Guid.NewGuid()}")
            .Options;
        return new SGrowingDbContext(options);
    }

    [Fact]
    public async Task InMemoryStreamBroker_Should_Publish_And_Consume_With_Acks()
    {
        var broker = new InMemoryStreamBroker();
        var tenantId = Guid.NewGuid();
        string streamKey = "sgrowing:events:test";
        string groupName = "test-workers";

        await broker.EnsureConsumerGroupAsync(streamKey, groupName);

        // 1. Publish 3 events
        var msgId1 = await broker.PublishEventAsync(streamKey, "order.placed", "{\"orderId\":\"101\"}", tenantId);
        var msgId2 = await broker.PublishEventAsync(streamKey, "order.paid", "{\"orderId\":\"101\"}", tenantId);
        var msgId3 = await broker.PublishEventAsync(streamKey, "order.shipped", "{\"orderId\":\"101\"}", tenantId);

        Assert.NotEmpty(msgId1);
        Assert.NotEmpty(msgId2);
        Assert.NotEmpty(msgId3);

        // 2. Read first batch of 2
        var batch1 = await broker.ReadGroupMessagesAsync(streamKey, groupName, "consumer-1", count: 2);
        Assert.Equal(2, batch1.Count);
        Assert.Equal("order.placed", batch1[0].EventType);
        Assert.Equal("order.paid", batch1[1].EventType);

        // 3. Acknowledge first message
        await broker.AcknowledgeAsync(streamKey, groupName, batch1[0].MessageId);

        // 4. Read remaining unacknowledged messages
        var batch2 = await broker.ReadGroupMessagesAsync(streamKey, groupName, "consumer-1", count: 10);
        Assert.Equal(2, batch2.Count); // msgId2 (unacked) and msgId3 (not yet consumed)
        Assert.Equal(batch1[1].MessageId, batch2[0].MessageId);
        Assert.Equal(msgId3, batch2[1].MessageId);
    }

    [Fact]
    public async Task OutboxProcessor_And_RedisStreamConsumer_EndToEnd_Flow()
    {
        var db = CreateDbContext();
        var broker = new InMemoryStreamBroker();
        var tenantId = Guid.NewGuid();

        // 1. Seed Outbox with pending events
        var outboxMsg1 = OutboxMessage.Create(
            tenantId,
            "customer.registered",
            "{\"email\":\"client@sgrowing.io\"}");
        var outboxMsg2 = OutboxMessage.Create(
            tenantId,
            "lead.qualified",
            "{\"score\":95}");
        db.OutboxMessages.AddRange(outboxMsg1, outboxMsg2);
        await db.SaveChangesAsync();

        // 2. Setup Service Provider for Worker Daemons
        var services = new ServiceCollection();
        services.AddSingleton(db);
        services.AddSingleton<IRedisStreamBroker>(broker);
        var serviceProvider = services.BuildServiceProvider();

        // 3. Run OutboxProcessorDaemon Batch
        var outboxProcessor = new OutboxProcessorDaemon(serviceProvider, NullLogger<OutboxProcessorDaemon>.Instance);
        int processedCount = await outboxProcessor.ProcessBatchAsync();

        Assert.Equal(2, processedCount);

        // Verify outbox messages marked as processed
        var reloadedMsg1 = await db.OutboxMessages.FindAsync(outboxMsg1.Id);
        Assert.NotNull(reloadedMsg1?.ProcessedOnUtc);

        // 4. Run RedisStreamConsumerDaemon Batch
        var consumerDaemon = new RedisStreamConsumerDaemon(
            serviceProvider,
            NullLogger<RedisStreamConsumerDaemon>.Instance,
            streamKey: "sgrowing:stream:outbox",
            groupName: "sgrowing-worker-group",
            consumerName: "unit-test-worker");

        await broker.EnsureConsumerGroupAsync("sgrowing:stream:outbox", "sgrowing-worker-group");
        int consumedCount = await consumerDaemon.ProcessStreamBatchAsync(broker);

        Assert.Equal(2, consumedCount);

        // Verify all messages acknowledged
        var remainingMessages = await broker.ReadGroupMessagesAsync("sgrowing:stream:outbox", "sgrowing-worker-group", "unit-test-worker");
        Assert.Empty(remainingMessages);
    }
}
