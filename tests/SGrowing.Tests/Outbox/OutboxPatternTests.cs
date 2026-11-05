using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using SGrowing.Api.Common.Outbox;
using SGrowing.Api.Data;
using SGrowing.Core.Entities;
using SGrowing.Worker.Workers;
using Xunit;

namespace SGrowing.Tests.Outbox;

public class OutboxPatternTests
{
    private sealed record OrderCreatedEvent(Guid OrderId, decimal Amount, string CustomerEmail);

    private static (SGrowingDbContext Db, IServiceProvider Sp) CreateInMemoryContext()
    {
        var dbName = $"OutboxTestDb_{Guid.NewGuid():N}";
        var services = new ServiceCollection();
        services.AddDbContext<SGrowingDbContext>(opt => opt.UseInMemoryDatabase(dbName));
        services.AddScoped<IOutboxService, OutboxService>();
        var sp = services.BuildServiceProvider();

        var db = sp.GetRequiredService<SGrowingDbContext>();
        return (db, sp);
    }

    [Fact]
    public async Task Enqueue_PersistsMessageWithPendingStatus()
    {
        // Arrange
        var (db, sp) = CreateInMemoryContext();
        var outbox = sp.GetRequiredService<IOutboxService>();
        var tenantId = Guid.NewGuid();
        var payload = new OrderCreatedEvent(Guid.NewGuid(), 299.99m, "buyer@domain.com");

        // Act
        var message = await outbox.EnqueueAsync(
            tenantId: tenantId,
            eventType: "Order.Created",
            payload: payload,
            correlationId: "corr-12345",
            destinationTopic: "orders-topic"
        );

        // Assert
        message.Should().NotBeNull();
        message.TenantId.Should().Be(tenantId);
        message.EventType.Should().Be("Order.Created");
        message.DestinationTopic.Should().Be("orders-topic");
        message.CorrelationId.Should().Be("corr-12345");
        message.Status.Should().Be(OutboxMessageStatus.Pending);
        message.RetryCount.Should().Be(0);
        message.MaxRetries.Should().Be(5);
        message.PayloadJson.Should().Contain("299.99");
        message.PayloadJson.Should().Contain("buyer@domain.com");

        var dbRecord = await db.TransactionalOutboxMessages.FirstOrDefaultAsync(m => m.Id == message.Id);
        dbRecord.Should().NotBeNull();
        dbRecord!.Status.Should().Be(OutboxMessageStatus.Pending);
        dbRecord.CorrelationId.Should().Be("corr-12345");
    }

    [Fact]
    public async Task GetPendingMessages_ReturnsOnlyDueMessages()
    {
        // Arrange
        var (db, sp) = CreateInMemoryContext();
        var outbox = sp.GetRequiredService<IOutboxService>();
        var tenantId = Guid.NewGuid();

        // 1. Due pending message
        var dueMsg = await outbox.EnqueueAsync(tenantId, "Due.Event", new { Value = 1 });

        // 2. Future message (not yet due)
        var futureMsg = await outbox.EnqueueAsync(tenantId, "Future.Event", new { Value = 2 });
        futureMsg.NextAttemptAtUtc = DateTime.UtcNow.AddHours(2);
        await db.SaveChangesAsync();

        // 3. Already published message
        var pubMsg = await outbox.EnqueueAsync(tenantId, "Pub.Event", new { Value = 3 });
        pubMsg.Status = OutboxMessageStatus.Published;
        pubMsg.ProcessedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync();

        // Act
        var pending = await outbox.GetPendingMessagesAsync(batchSize: 10);

        // Assert
        pending.Should().HaveCount(1);
        pending.First().Id.Should().Be(dueMsg.Id);
        pending.First().Status.Should().Be(OutboxMessageStatus.Processing);

        // Verify status in DB is now Processing
        var updatedInDb = await db.TransactionalOutboxMessages.FindAsync(dueMsg.Id);
        updatedInDb!.Status.Should().Be(OutboxMessageStatus.Processing);
    }

    [Fact]
    public async Task MarkAsPublished_SetsPublishedStatusAndTimestamp()
    {
        // Arrange
        var (db, sp) = CreateInMemoryContext();
        var outbox = sp.GetRequiredService<IOutboxService>();
        var message = await outbox.EnqueueAsync(Guid.NewGuid(), "Payment.Received", new { Amount = 50.0m });

        // Act
        await outbox.MarkAsPublishedAsync(message.Id);

        // Assert
        var updated = await db.TransactionalOutboxMessages.FindAsync(message.Id);
        updated.Should().NotBeNull();
        updated!.Status.Should().Be(OutboxMessageStatus.Published);
        updated.ProcessedAtUtc.Should().NotBeNull();
        updated.ProcessedAtUtc.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task MarkAsFailed_AppliesExponentialBackoffAndRetries()
    {
        // Arrange
        var (db, sp) = CreateInMemoryContext();
        var outbox = sp.GetRequiredService<IOutboxService>();
        var message = await outbox.EnqueueAsync(Guid.NewGuid(), "Notification.Send", new { Text = "Hello" });

        var beforeFirstFail = DateTime.UtcNow;

        // Act 1: First failure
        await outbox.MarkAsFailedAsync(message.Id, "Downstream network timeout");

        // Assert 1: RetryCount = 1, Status = Pending, NextAttempt = now + 2^1 * 5 = 10s
        var firstFail = await db.TransactionalOutboxMessages.FindAsync(message.Id);
        firstFail.Should().NotBeNull();
        firstFail!.RetryCount.Should().Be(1);
        firstFail.Status.Should().Be(OutboxMessageStatus.Pending);
        firstFail.LastError.Should().Be("Downstream network timeout");
        firstFail.NextAttemptAtUtc.Should().NotBeNull();
        firstFail.NextAttemptAtUtc!.Value.Should().BeOnOrAfter(beforeFirstFail.AddSeconds(9));

        // Act 2: Second failure
        var beforeSecondFail = DateTime.UtcNow;
        await outbox.MarkAsFailedAsync(message.Id, "503 Service Unavailable");

        // Assert 2: RetryCount = 2, Status = Pending, NextAttempt = now + 2^2 * 5 = 20s
        var secondFail = await db.TransactionalOutboxMessages.FindAsync(message.Id);
        secondFail!.RetryCount.Should().Be(2);
        secondFail.Status.Should().Be(OutboxMessageStatus.Pending);
        secondFail.LastError.Should().Be("503 Service Unavailable");
        secondFail.NextAttemptAtUtc!.Value.Should().BeOnOrAfter(beforeSecondFail.AddSeconds(19));
    }

    [Fact]
    public async Task MarkAsFailed_WhenExceedingMaxRetries_TransitionsToFailedStatus()
    {
        // Arrange
        var (db, sp) = CreateInMemoryContext();
        var outbox = sp.GetRequiredService<IOutboxService>();
        var message = await outbox.EnqueueAsync(Guid.NewGuid(), "Webhook.Deliver", new { Url = "https://dead.endpoint" });

        // Act: Fail MaxRetries times (5 times)
        for (int i = 1; i <= 5; i++)
        {
            await outbox.MarkAsFailedAsync(message.Id, $"Attempt {i} failed");
        }

        // Assert
        var finalState = await db.TransactionalOutboxMessages.FindAsync(message.Id);
        finalState.Should().NotBeNull();
        finalState!.RetryCount.Should().Be(5);
        finalState.Status.Should().Be(OutboxMessageStatus.Failed);
        finalState.LastError.Should().Be("Attempt 5 failed");

        // Should no longer be returned as pending message
        var pending = await outbox.GetPendingMessagesAsync(batchSize: 50);
        pending.Should().NotContain(m => m.Id == message.Id);
    }

    [Fact]
    public async Task OutboxProcessingWorker_ExecutesBatchAndPublishesSuccessfully()
    {
        // Arrange
        var (db, sp) = CreateInMemoryContext();
        var outbox = sp.GetRequiredService<IOutboxService>();
        var scopeFactory = sp.GetRequiredService<IServiceScopeFactory>();

        var tenantId = Guid.NewGuid();
        var msg1 = await outbox.EnqueueAsync(tenantId, "Campaign.Triggered", new { CampaignId = "c-1" });
        var msg2 = await outbox.EnqueueAsync(tenantId, "Campaign.Triggered", new { CampaignId = "c-2" });

        var dispatchedMessageIds = new List<Guid>();

        var worker = new OutboxProcessingWorker(
            scopeFactory,
            NullLogger<OutboxProcessingWorker>.Instance,
            TimeSpan.FromSeconds(5),
            dispatchHandler: (msg, ct) =>
            {
                dispatchedMessageIds.Add(msg.Id);
                return Task.CompletedTask;
            }
        );

        // Act
        var processedCount = await worker.ProcessBatchAsync(CancellationToken.None);

        // Assert
        processedCount.Should().Be(2);
        dispatchedMessageIds.Should().Contain(new[] { msg1.Id, msg2.Id });

        var postDb1 = await db.TransactionalOutboxMessages.AsNoTracking().FirstOrDefaultAsync(m => m.Id == msg1.Id);
        var postDb2 = await db.TransactionalOutboxMessages.AsNoTracking().FirstOrDefaultAsync(m => m.Id == msg2.Id);

        postDb1!.Status.Should().Be(OutboxMessageStatus.Published);
        postDb1.ProcessedAtUtc.Should().NotBeNull();
        postDb2!.Status.Should().Be(OutboxMessageStatus.Published);
        postDb2.ProcessedAtUtc.Should().NotBeNull();
    }

    [Fact]
    public async Task PurgeProcessedMessages_RemovesEligiblePublishedAndFailedMessages()
    {
        // Arrange
        var (db, sp) = CreateInMemoryContext();
        var outbox = sp.GetRequiredService<IOutboxService>();
        var tenantId = Guid.NewGuid();

        var oldPublished = await outbox.EnqueueAsync(tenantId, "Old.Event", new { });
        await outbox.MarkAsPublishedAsync(oldPublished.Id);
        oldPublished.ProcessedAtUtc = DateTime.UtcNow.AddDays(-10);
        await db.SaveChangesAsync();

        var recentPublished = await outbox.EnqueueAsync(tenantId, "Recent.Event", new { });
        await outbox.MarkAsPublishedAsync(recentPublished.Id);

        // Act
        var purgedCount = await outbox.PurgeProcessedMessagesAsync(TimeSpan.FromDays(7));

        // Assert
        purgedCount.Should().Be(1);
        var remaining = await db.TransactionalOutboxMessages.ToListAsync();
        remaining.Should().HaveCount(1);
        remaining.First().Id.Should().Be(recentPublished.Id);
    }
}
