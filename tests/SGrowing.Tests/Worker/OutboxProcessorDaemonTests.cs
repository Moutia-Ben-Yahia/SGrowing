using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using SGrowing.Api.Data;
using SGrowing.Core.Common;
using SGrowing.Core.Entities;
using SGrowing.Core.Messaging;
using SGrowing.Worker.Services;
using Xunit;

namespace SGrowing.Tests.Worker;

public class OutboxProcessorDaemonTests
{
    private (IServiceProvider Provider, string DbName) CreateServiceProvider(
        IRedisStreamBroker? broker = null,
        IConcurrencyLockService? lockService = null)
    {
        var services = new ServiceCollection();
        var dbName = Guid.NewGuid().ToString();

        services.AddDbContext<SGrowingDbContext>(options =>
            options.UseInMemoryDatabase(dbName));

        if (broker != null)
        {
            services.AddSingleton(broker);
        }

        if (lockService != null)
        {
            services.AddSingleton(lockService);
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
    public async Task ProcessBatchAsync_WhenNoPendingMessages_ReturnsZero()
    {
        var (provider, _) = CreateServiceProvider();
        var mockLogger = new Mock<ILogger<OutboxProcessorDaemon>>();
        var daemon = new OutboxProcessorDaemon(provider, mockLogger.Object);

        var result = await daemon.ProcessBatchAsync();

        result.Should().Be(0);
    }

    [Fact]
    public async Task ProcessBatchAsync_WhenPendingMessages_DispatchesViaBrokerAndMarksProcessed()
    {
        var mockBroker = new Mock<IRedisStreamBroker>();
        mockBroker
            .Setup(b => b.PublishEventAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Guid>()))
            .ReturnsAsync("stream-msg-001");

        var (provider, _) = CreateServiceProvider(broker: mockBroker.Object);
        var tenantId = Guid.NewGuid();

        var msg1 = OutboxMessage.Create(tenantId, "OrderPlaced", "{\"orderId\":\"101\"}");
        var msg2 = OutboxMessage.Create(tenantId, "PaymentReceived", "{\"amount\":150.0}");

        using (var seedDb = GetDbContext(provider))
        {
            seedDb.OutboxMessages.AddRange(msg1, msg2);
            await seedDb.SaveChangesAsync();
        }

        var mockLogger = new Mock<ILogger<OutboxProcessorDaemon>>();
        var daemon = new OutboxProcessorDaemon(provider, mockLogger.Object);

        var processedCount = await daemon.ProcessBatchAsync();

        processedCount.Should().Be(2);

        mockBroker.Verify(b => b.PublishEventAsync(
            "sgrowing:stream:outbox",
            "OrderPlaced",
            "{\"orderId\":\"101\"}",
            tenantId), Times.Once);

        mockBroker.Verify(b => b.PublishEventAsync(
            "sgrowing:stream:outbox",
            "PaymentReceived",
            "{\"amount\":150.0}",
            tenantId), Times.Once);

        using (var verifyDb = GetDbContext(provider))
        {
            var updated1 = await verifyDb.OutboxMessages.AsNoTracking().FirstOrDefaultAsync(m => m.Id == msg1.Id);
            updated1.Should().NotBeNull();
            updated1!.ProcessedOnUtc.Should().NotBeNull();
            updated1.RetryCount.Should().Be(0);
            updated1.Error.Should().BeNull();

            var updated2 = await verifyDb.OutboxMessages.AsNoTracking().FirstOrDefaultAsync(m => m.Id == msg2.Id);
            updated2.Should().NotBeNull();
            updated2!.ProcessedOnUtc.Should().NotBeNull();
        }
    }

    [Fact]
    public async Task ProcessBatchAsync_WhenBrokerThrowsException_MarksFailedAndIncrementsRetry()
    {
        var mockBroker = new Mock<IRedisStreamBroker>();
        mockBroker
            .Setup(b => b.PublishEventAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Guid>()))
            .ThrowsAsync(new TimeoutException("Redis cluster timeout"));

        var (provider, _) = CreateServiceProvider(broker: mockBroker.Object);
        var tenantId = Guid.NewGuid();

        var msg = OutboxMessage.Create(tenantId, "LeadCaptured", "{\"leadId\":\"999\"}");
        using (var seedDb = GetDbContext(provider))
        {
            seedDb.OutboxMessages.Add(msg);
            await seedDb.SaveChangesAsync();
        }

        var mockLogger = new Mock<ILogger<OutboxProcessorDaemon>>();
        var daemon = new OutboxProcessorDaemon(provider, mockLogger.Object);

        var processedCount = await daemon.ProcessBatchAsync();

        processedCount.Should().Be(1);

        using (var verifyDb = GetDbContext(provider))
        {
            var failedMsg = await verifyDb.OutboxMessages.AsNoTracking().FirstOrDefaultAsync(m => m.Id == msg.Id);
            failedMsg.Should().NotBeNull();
            failedMsg!.ProcessedOnUtc.Should().BeNull();
            failedMsg.RetryCount.Should().Be(1);
            failedMsg.Error.Should().Contain("Redis cluster timeout");
        }
    }

    [Fact]
    public async Task ProcessBatchAsync_WhenBrokerIsNull_MarksProcessedWithoutError()
    {
        var (provider, _) = CreateServiceProvider(); // no broker registered
        var tenantId = Guid.NewGuid();

        var msg = OutboxMessage.Create(tenantId, "ContactUpdated", "{\"contactId\":\"333\"}");
        using (var seedDb = GetDbContext(provider))
        {
            seedDb.OutboxMessages.Add(msg);
            await seedDb.SaveChangesAsync();
        }

        var mockLogger = new Mock<ILogger<OutboxProcessorDaemon>>();
        var daemon = new OutboxProcessorDaemon(provider, mockLogger.Object);

        var processedCount = await daemon.ProcessBatchAsync();

        processedCount.Should().Be(1);

        using (var verifyDb = GetDbContext(provider))
        {
            var updated = await verifyDb.OutboxMessages.AsNoTracking().FirstOrDefaultAsync(m => m.Id == msg.Id);
            updated.Should().NotBeNull();
            updated!.ProcessedOnUtc.Should().NotBeNull();
            updated.Error.Should().BeNull();
        }
    }

    private class TestableOutboxProcessorDaemon : OutboxProcessorDaemon
    {
        public TestableOutboxProcessorDaemon(
            IServiceProvider serviceProvider,
            ILogger<OutboxProcessorDaemon> logger,
            TimeSpan? pollingInterval = null,
            IConcurrencyLockService? lockService = null)
            : base(serviceProvider, logger, pollingInterval, lockService)
        {
        }

        public Task InvokeExecuteAsync(CancellationToken token) => ExecuteAsync(token);
    }

    [Fact]
    public async Task ExecuteAsync_WhenLockNotAcquired_SkipsProcessing()
    {
        var mockLockService = new Mock<IConcurrencyLockService>();
        mockLockService
            .Setup(l => l.TryAcquireLockAsync(
                "sweeper:OutboxProcessor",
                It.IsAny<TimeSpan?>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((IConcurrencyLockHandle?)null);

        var (provider, _) = CreateServiceProvider(lockService: mockLockService.Object);
        var tenantId = Guid.NewGuid();
        var msg = OutboxMessage.Create(tenantId, "EventX", "{}");

        using (var seedDb = GetDbContext(provider))
        {
            seedDb.OutboxMessages.Add(msg);
            await seedDb.SaveChangesAsync();
        }

        var mockLogger = new Mock<ILogger<OutboxProcessorDaemon>>();
        var daemon = new TestableOutboxProcessorDaemon(
            provider,
            mockLogger.Object,
            pollingInterval: TimeSpan.FromMilliseconds(20),
            lockService: mockLockService.Object);

        using var cts = new CancellationTokenSource();
        cts.CancelAfter(50);

        try
        {
            await daemon.InvokeExecuteAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            // Expected
        }

        // Message should NOT have been processed because lock was not acquired
        using (var verifyDb = GetDbContext(provider))
        {
            var pendingMsg = await verifyDb.OutboxMessages.AsNoTracking().FirstOrDefaultAsync(m => m.Id == msg.Id);
            pendingMsg!.ProcessedOnUtc.Should().BeNull();
        }
    }

    [Fact]
    public async Task ProcessBatchAsync_WhenBatchExceeds50_ProcessesFirst50OrderedByOccurredOnUtc()
    {
        var (provider, _) = CreateServiceProvider();
        var tenantId = Guid.NewGuid();
        var baseTime = DateTime.UtcNow.AddHours(-10);

        using (var seedDb = GetDbContext(provider))
        {
            for (int i = 0; i < 60; i++)
            {
                var msg = OutboxMessage.Create(tenantId, $"EventBatch_{i:D2}", $"{{\"index\":{i}}}");
                seedDb.OutboxMessages.Add(msg);
                seedDb.Entry(msg).Property(m => m.OccurredOnUtc).CurrentValue = baseTime.AddMinutes(i);
            }
            await seedDb.SaveChangesAsync();
        }

        var mockLogger = new Mock<ILogger<OutboxProcessorDaemon>>();
        var daemon = new OutboxProcessorDaemon(provider, mockLogger.Object);

        var processedCount = await daemon.ProcessBatchAsync();

        processedCount.Should().Be(50);

        using (var verifyDb = GetDbContext(provider))
        {
            var processedMessages = await verifyDb.OutboxMessages.AsNoTracking().Where(m => m.ProcessedOnUtc != null).ToListAsync();
            var remainingPending = await verifyDb.OutboxMessages.AsNoTracking().Where(m => m.ProcessedOnUtc == null).ToListAsync();

            processedMessages.Should().HaveCount(50);
            remainingPending.Should().HaveCount(10);

            // Verify earliest 50 were processed
            processedMessages.Select(m => m.EventType).Should().Contain("EventBatch_00");
            processedMessages.Select(m => m.EventType).Should().Contain("EventBatch_49");
            remainingPending.Select(m => m.EventType).Should().Contain("EventBatch_50");
            remainingPending.Select(m => m.EventType).Should().Contain("EventBatch_59");
        }
    }

    [Fact]
    public async Task ProcessBatchAsync_MixedSuccessAndFailure_ProperlyPartitionsProcessedAndFailedStates()
    {
        var tenantId = Guid.NewGuid();
        var mockBroker = new Mock<IRedisStreamBroker>();
        mockBroker
            .Setup(b => b.PublishEventAsync(It.IsAny<string>(), "FailEvent", It.IsAny<string>(), It.IsAny<Guid>()))
            .ThrowsAsync(new InvalidOperationException("Broker partition unreachable"));
        mockBroker
            .Setup(b => b.PublishEventAsync(It.IsAny<string>(), "SuccessEvent", It.IsAny<string>(), It.IsAny<Guid>()))
            .ReturnsAsync("stream-ok-123");

        var (provider, _) = CreateServiceProvider(broker: mockBroker.Object);

        var msgSuccess = OutboxMessage.Create(tenantId, "SuccessEvent", "{\"status\":\"ok\"}");
        var msgFail = OutboxMessage.Create(tenantId, "FailEvent", "{\"status\":\"fail\"}");

        using (var seedDb = GetDbContext(provider))
        {
            seedDb.OutboxMessages.AddRange(msgSuccess, msgFail);
            await seedDb.SaveChangesAsync();
        }

        var mockLogger = new Mock<ILogger<OutboxProcessorDaemon>>();
        var daemon = new OutboxProcessorDaemon(provider, mockLogger.Object);

        var count = await daemon.ProcessBatchAsync();
        count.Should().Be(2);

        using (var verifyDb = GetDbContext(provider))
        {
            var success = await verifyDb.OutboxMessages.AsNoTracking().FirstAsync(m => m.Id == msgSuccess.Id);
            var failed = await verifyDb.OutboxMessages.AsNoTracking().FirstAsync(m => m.Id == msgFail.Id);

            success.ProcessedOnUtc.Should().NotBeNull();
            success.RetryCount.Should().Be(0);
            success.Error.Should().BeNull();

            failed.ProcessedOnUtc.Should().BeNull();
            failed.RetryCount.Should().Be(1);
            failed.Error.Should().Contain("Broker partition unreachable");
        }
    }

    [Fact]
    public async Task ProcessBatchAsync_WhenCancellationTokenCancelled_AbortsProcessing()
    {
        var (provider, _) = CreateServiceProvider();
        var tenantId = Guid.NewGuid();

        using (var seedDb = GetDbContext(provider))
        {
            seedDb.OutboxMessages.Add(OutboxMessage.Create(tenantId, "CanceledEvent", "{}"));
            await seedDb.SaveChangesAsync();
        }

        var mockLogger = new Mock<ILogger<OutboxProcessorDaemon>>();
        var daemon = new OutboxProcessorDaemon(provider, mockLogger.Object);

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        Func<Task> act = async () => await daemon.ProcessBatchAsync(cts.Token);
        await act.Should().ThrowAsync<OperationCanceledException>();
    }
}
