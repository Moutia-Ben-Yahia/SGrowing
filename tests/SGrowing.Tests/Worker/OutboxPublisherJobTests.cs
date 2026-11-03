using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using SGrowing.Api.Data;
using SGrowing.Core.Entities;
using SGrowing.Core.Messaging;
using SGrowing.Worker.BackgroundJobs;
using Xunit;

namespace SGrowing.Tests.Worker;

public class OutboxPublisherJobTests
{
    private (IServiceProvider Provider, string DbName) CreateServiceProvider(
        IRedisStreamBroker? broker = null)
    {
        var services = new ServiceCollection();
        var dbName = Guid.NewGuid().ToString();

        services.AddDbContext<SGrowingDbContext>(options =>
            options.UseInMemoryDatabase(dbName));

        if (broker != null)
        {
            services.AddSingleton(broker);
        }

        services.AddLogging();
        var provider = services.BuildServiceProvider();

        return (provider, dbName);
    }

    private SGrowingDbContext GetDbContext(IServiceProvider provider)
    {
        var scope = provider.CreateScope();
        return scope.ServiceProvider.GetRequiredService<SGrowingDbContext>();
    }

    [Fact]
    public async Task ProcessPendingOutboxMessagesAsync_WhenNoMessages_ReturnsZero()
    {
        var (provider, _) = CreateServiceProvider();
        var mockLogger = new Mock<ILogger<OutboxPublisherJob>>();
        var job = new OutboxPublisherJob(provider, mockLogger.Object);

        var result = await job.ProcessPendingOutboxMessagesAsync();

        result.Should().Be(0);
    }

    [Fact]
    public async Task ProcessPendingOutboxMessagesAsync_WhenPendingMessages_DispatchesViaBrokerAndMarksProcessed()
    {
        var mockBroker = new Mock<IRedisStreamBroker>();
        mockBroker
            .Setup(b => b.PublishEventAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Guid>()))
            .ReturnsAsync("stream-id-999");

        var (provider, _) = CreateServiceProvider(broker: mockBroker.Object);
        var tenantId = Guid.NewGuid();

        var msg1 = OutboxMessage.Create(tenantId, "InvoiceGenerated", "{\"invoiceId\":\"INV-1\"}");
        var msg2 = OutboxMessage.Create(tenantId, "DealCreated", "{\"dealId\":\"DEAL-2\"}");

        using (var seedDb = GetDbContext(provider))
        {
            seedDb.OutboxMessages.AddRange(msg1, msg2);
            await seedDb.SaveChangesAsync();
        }

        var mockLogger = new Mock<ILogger<OutboxPublisherJob>>();
        var job = new OutboxPublisherJob(provider, mockLogger.Object);

        var count = await job.ProcessPendingOutboxMessagesAsync();

        count.Should().Be(2);

        mockBroker.Verify(b => b.PublishEventAsync(
            "sgrowing:stream:outbox",
            "InvoiceGenerated",
            "{\"invoiceId\":\"INV-1\"}",
            tenantId), Times.Once);

        mockBroker.Verify(b => b.PublishEventAsync(
            "sgrowing:stream:outbox",
            "DealCreated",
            "{\"dealId\":\"DEAL-2\"}",
            tenantId), Times.Once);

        using (var verifyDb = GetDbContext(provider))
        {
            var updated1 = await verifyDb.OutboxMessages.AsNoTracking().FirstOrDefaultAsync(m => m.Id == msg1.Id);
            updated1.Should().NotBeNull();
            updated1!.ProcessedOnUtc.Should().NotBeNull();
            updated1.Error.Should().BeNull();

            var updated2 = await verifyDb.OutboxMessages.AsNoTracking().FirstOrDefaultAsync(m => m.Id == msg2.Id);
            updated2.Should().NotBeNull();
            updated2!.ProcessedOnUtc.Should().NotBeNull();
        }
    }

    [Fact]
    public async Task ProcessPendingOutboxMessagesAsync_WhenRetryExceededMax_SkipsAndDoesNotPublish()
    {
        var mockBroker = new Mock<IRedisStreamBroker>();
        var (provider, _) = CreateServiceProvider(broker: mockBroker.Object);
        var tenantId = Guid.NewGuid();

        var msg = OutboxMessage.Create(tenantId, "PoisonPillEvent", "{}");
        // Mark failed 5 times to hit MaxRetries
        for (int i = 0; i < OutboxPublisherJob.MaxRetries; i++)
        {
            msg.MarkFailed("Persistent serialization failure");
        }

        using (var seedDb = GetDbContext(provider))
        {
            seedDb.OutboxMessages.Add(msg);
            await seedDb.SaveChangesAsync();
        }

        var mockLogger = new Mock<ILogger<OutboxPublisherJob>>();
        var job = new OutboxPublisherJob(provider, mockLogger.Object);

        var count = await job.ProcessPendingOutboxMessagesAsync();

        count.Should().Be(0);
        mockBroker.Verify(b => b.PublishEventAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Guid>()), Times.Never);

        using (var verifyDb = GetDbContext(provider))
        {
            var dbMsg = await verifyDb.OutboxMessages.AsNoTracking().FirstOrDefaultAsync(m => m.Id == msg.Id);
            dbMsg.Should().NotBeNull();
            dbMsg!.ProcessedOnUtc.Should().BeNull();
            dbMsg.RetryCount.Should().Be(OutboxPublisherJob.MaxRetries);
        }
    }

    [Fact]
    public async Task ProcessPendingOutboxMessagesAsync_WhenBrokerThrowsException_IncrementsRetryAndMarksFailed()
    {
        var mockBroker = new Mock<IRedisStreamBroker>();
        mockBroker
            .Setup(b => b.PublishEventAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Guid>()))
            .ThrowsAsync(new InvalidOperationException("Broker connection disconnected"));

        var (provider, _) = CreateServiceProvider(broker: mockBroker.Object);
        var tenantId = Guid.NewGuid();

        var msg = OutboxMessage.Create(tenantId, "WebhookFailed", "{}");
        using (var seedDb = GetDbContext(provider))
        {
            seedDb.OutboxMessages.Add(msg);
            await seedDb.SaveChangesAsync();
        }

        var mockLogger = new Mock<ILogger<OutboxPublisherJob>>();
        var job = new OutboxPublisherJob(provider, mockLogger.Object);

        var count = await job.ProcessPendingOutboxMessagesAsync();

        count.Should().Be(0);

        using (var verifyDb = GetDbContext(provider))
        {
            var failedMsg = await verifyDb.OutboxMessages.AsNoTracking().FirstOrDefaultAsync(m => m.Id == msg.Id);
            failedMsg.Should().NotBeNull();
            failedMsg!.ProcessedOnUtc.Should().BeNull();
            failedMsg.RetryCount.Should().Be(1);
            failedMsg.Error.Should().Contain("Broker connection disconnected");
        }
    }

    [Fact]
    public async Task ProcessPendingOutboxMessagesAsync_WhenBackoffDurationHasElapsed_AttemptsPublish()
    {
        var mockBroker = new Mock<IRedisStreamBroker>();
        mockBroker
            .Setup(b => b.PublishEventAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Guid>()))
            .ReturnsAsync("stream-ok");

        var (provider, _) = CreateServiceProvider(broker: mockBroker.Object);
        var tenantId = Guid.NewGuid();

        var msg = OutboxMessage.Create(tenantId, "RetriedEvent", "{}");
        msg.MarkFailed("First attempt failure"); // RetryCount = 1, delay = 2s

        using (var seedDb = GetDbContext(provider))
        {
            seedDb.OutboxMessages.Add(msg);
            await seedDb.SaveChangesAsync();
        }

        // Wait 2.1s so exponential backoff (2^1 = 2s) has elapsed
        await Task.Delay(2100);

        var mockLogger = new Mock<ILogger<OutboxPublisherJob>>();
        var job = new OutboxPublisherJob(provider, mockLogger.Object);

        var count = await job.ProcessPendingOutboxMessagesAsync();

        count.Should().Be(1);

        using (var verifyDb = GetDbContext(provider))
        {
            var updated = await verifyDb.OutboxMessages.AsNoTracking().FirstOrDefaultAsync(m => m.Id == msg.Id);
            updated!.ProcessedOnUtc.Should().NotBeNull();
        }
    }
}
