using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using SGrowing.Api.Modules.Infrastructure.EventOutbox;
using Xunit;

namespace SGrowing.Tests.Unit;

/// <summary>
/// Unit tests for Enterprise Transactional Outbox & Resilient Event Streaming Mesh
/// (Domain K1 & PRD Section 8.2, 8.3).
/// </summary>
public class EventOutboxTests
{
    private readonly EventOutboxService _sut;
    private readonly Guid _tenantId = Guid.NewGuid();

    public EventOutboxTests()
    {
        _sut = new EventOutboxService();
        _sut.ResetState();
    }

    [Fact]
    public async Task EnqueueAsync_WithValidEvent_SuccessfullyStagesPendingEvent()
    {
        var request = new EnqueueOutboxEventRequest(
            TenantId: _tenantId,
            EventType: "OrderPaid",
            AggregateId: "ORD-101",
            Payload: new { Amount = 150.00, Currency = "USD" },
            CorrelationId: "corr-101"
        );

        var result = await _sut.EnqueueAsync(request);

        result.Should().NotBeNull();
        result.IsDuplicate.Should().BeFalse();
        result.Status.Should().Be("Enqueued");
        result.DeduplicationHash.Should().NotBeNullOrWhiteSpace();
        result.DeduplicationHash.Length.Should().Be(64);

        var stats = await _sut.GetMeshStatisticsAsync(_tenantId);
        stats.CurrentPendingCount.Should().Be(1);
        stats.TotalEnqueued.Should().Be(1);
    }

    [Fact]
    public async Task EnqueueAsync_WithDuplicateEventPayload_SuppressesDuplicateAndReturnsIsDuplicateTrue()
    {
        var request = new EnqueueOutboxEventRequest(
            TenantId: _tenantId,
            EventType: "DealWon",
            AggregateId: "DEAL-55",
            Payload: new { DealValue = 25000, Account = "Globex Corp" }
        );

        var firstResult = await _sut.EnqueueAsync(request);
        firstResult.IsDuplicate.Should().BeFalse();
        firstResult.Status.Should().Be("Enqueued");

        // Attempt duplicate enqueue with identical payload
        var secondResult = await _sut.EnqueueAsync(request);

        secondResult.IsDuplicate.Should().BeTrue();
        secondResult.Status.Should().Be("DuplicateSuppressed");
        secondResult.DeduplicationHash.Should().Be(firstResult.DeduplicationHash);
        secondResult.EventId.Should().Be(firstResult.EventId);

        var stats = await _sut.GetMeshStatisticsAsync(_tenantId);
        stats.CurrentPendingCount.Should().Be(1); // Queue did not grow
        stats.TotalDeduplicationHits.Should().Be(1);
    }

    [Fact]
    public async Task EnqueueStandardEvents_StagesAllSupportedEnterpriseTypes()
    {
        var orderResult = await _sut.EnqueueOrderPaidAsync(
            _tenantId,
            new OrderPaidPayload("ORD-200", 399.95m, "USD", "test@customer.com", "CreditCard", DateTime.UtcNow)
        );
        orderResult.Status.Should().Be("Enqueued");

        var dealResult = await _sut.EnqueueDealWonAsync(
            _tenantId,
            new DealWonPayload("DEAL-300", 120000m, "USD", "Wayne Enterprises", "usr_bruce", DateTime.UtcNow)
        );
        dealResult.Status.Should().Be("Enqueued");

        var leadResult = await _sut.EnqueueLeadQualifiedAsync(
            _tenantId,
            new LeadQualifiedPayload("LEAD-400", "TIER_A", "WebForm", "Clark Kent", "clark@dailyplanet.com", DateTime.UtcNow)
        );
        leadResult.Status.Should().Be("Enqueued");

        var ticketResult = await _sut.EnqueueTicketEscalatedAsync(
            _tenantId,
            new TicketEscalatedPayload("TCK-500", "HIGH", "SLA Exceeded", "agent_diana", 10, DateTime.UtcNow)
        );
        ticketResult.Status.Should().Be("Enqueued");

        var stats = await _sut.GetMeshStatisticsAsync(_tenantId);
        stats.CurrentPendingCount.Should().Be(4);
        stats.CountsByEventType.Should().ContainKey(EnterpriseEventTypes.OrderPaid);
        stats.CountsByEventType.Should().ContainKey(EnterpriseEventTypes.DealWon);
        stats.CountsByEventType.Should().ContainKey(EnterpriseEventTypes.LeadQualified);
        stats.CountsByEventType.Should().ContainKey(EnterpriseEventTypes.TicketEscalated);
    }

    [Fact]
    public async Task ProcessPendingOutboxBatchAsync_WhenDispatchSucceeds_MarksEventsPublished()
    {
        await _sut.EnqueueOrderPaidAsync(
            _tenantId,
            new OrderPaidPayload("ORD-301", 100m, "USD", "user1@example.com", "Stripe", DateTime.UtcNow)
        );
        await _sut.EnqueueOrderPaidAsync(
            _tenantId,
            new OrderPaidPayload("ORD-302", 200m, "USD", "user2@example.com", "Stripe", DateTime.UtcNow)
        );

        var batchResult = await _sut.ProcessPendingOutboxBatchAsync(10, _tenantId);

        batchResult.ProcessedCount.Should().Be(2);
        batchResult.SucceededCount.Should().Be(2);
        batchResult.FailedCount.Should().Be(0);
        batchResult.QuarantinedCount.Should().Be(0);
        batchResult.DispatchedEvents.All(d => d.Status == "Published").Should().BeTrue();

        var stats = await _sut.GetMeshStatisticsAsync(_tenantId);
        stats.CurrentPendingCount.Should().Be(0);
        stats.TotalPublished.Should().Be(2);
    }

    [Fact]
    public async Task ProcessPendingOutboxBatchAsync_RespectsBatchSizeLimit()
    {
        for (int i = 0; i < 5; i++)
        {
            await _sut.EnqueueOrderPaidAsync(
                _tenantId,
                new OrderPaidPayload($"ORD-BATCH-{i}", 50m * i, "USD", $"user{i}@example.com", "Stripe", DateTime.UtcNow)
            );
        }

        var batchResult = await _sut.ProcessPendingOutboxBatchAsync(batchSize: 2, tenantId: _tenantId);

        batchResult.ProcessedCount.Should().Be(2);
        batchResult.SucceededCount.Should().Be(2);

        var stats = await _sut.GetMeshStatisticsAsync(_tenantId);
        stats.CurrentPendingCount.Should().Be(3); // 3 remaining pending
    }

    [Fact]
    public async Task ProcessPendingOutboxBatchAsync_WhenFailureOccurs_IncrementsRetryCountAndAppliesExponentialBackoff()
    {
        var enqueueResult = await _sut.EnqueueOrderPaidAsync(
            _tenantId,
            new OrderPaidPayload("ORD-FAIL-1", 100m, "USD", "fail@example.com", "Stripe", DateTime.UtcNow)
        );

        // Process with custom dispatcher that fails
        var batchResult = await _sut.ProcessPendingOutboxBatchAsync(
            batchSize: 10,
            tenantId: _tenantId,
            customDispatcher: evt =>
            {
                evt.LastError = "Kafka connection timeout";
                return Task.FromResult(false);
            }
        );

        batchResult.ProcessedCount.Should().Be(1);
        batchResult.FailedCount.Should().Be(1);
        batchResult.QuarantinedCount.Should().Be(0);

        var dispatched = batchResult.DispatchedEvents.First();
        dispatched.Status.Should().Be("FailedRetryScheduled");
        dispatched.RetryCount.Should().Be(1);
        dispatched.Error.Should().Be("Kafka connection timeout");
        dispatched.NextAttemptAtUtc.Should().NotBeNull();
        dispatched.NextAttemptAtUtc.Should().BeAfter(DateTime.UtcNow.AddSeconds(1));
    }

    [Fact]
    public async Task ProcessPendingOutboxBatchAsync_DoesNotProcessFailedEventsBeforeNextAttemptAtUtc()
    {
        await _sut.EnqueueOrderPaidAsync(
            _tenantId,
            new OrderPaidPayload("ORD-BACKOFF-1", 100m, "USD", "fail@example.com", "Stripe", DateTime.UtcNow)
        );

        // Fail once -> scheduled for future (e.g., +2 seconds)
        await _sut.ProcessPendingOutboxBatchAsync(
            batchSize: 10,
            tenantId: _tenantId,
            customDispatcher: _ => Task.FromResult(false)
        );

        // Immediately try to process again: should find 0 candidate events because NextAttemptAtUtc is in the future
        var secondRun = await _sut.ProcessPendingOutboxBatchAsync(batchSize: 10, tenantId: _tenantId);
        secondRun.ProcessedCount.Should().Be(0);
    }

    [Fact]
    public async Task ProcessPendingOutboxBatchAsync_WhenExceedingMaxRetries_QuarantinesToDeadLetterQueue()
    {
        var request = new EnqueueOutboxEventRequest(
            TenantId: _tenantId,
            EventType: "TicketEscalated",
            AggregateId: "TCK-POISON-1",
            Payload: new { Reason = "Poison payload" },
            MaxRetries: 1
        );
        var enqueued = await _sut.EnqueueAsync(request);

        // 1st failure: retryCount reaches 1 == MaxRetries -> Quarantined!
        var batchResult = await _sut.ProcessPendingOutboxBatchAsync(10, _tenantId, _ => Task.FromResult(false));

        batchResult.QuarantinedCount.Should().Be(1);
        var quarantinedEvt = batchResult.DispatchedEvents.First();
        quarantinedEvt.Status.Should().Be("QuarantinedDeadLetter");
        quarantinedEvt.RetryCount.Should().Be(1);

        var dlqMessages = await _sut.GetDeadLetterMessagesAsync(new DeadLetterQueryParameters(TenantId: _tenantId));
        dlqMessages.Should().HaveCount(1);
        dlqMessages.First().EventId.Should().Be(enqueued.EventId);
        dlqMessages.First().DeadLetterReason.Should().Contain("Exceeded max retry threshold");
    }

    [Fact]
    public async Task GetDeadLetterMessagesAsync_SupportsFilteringAndSearch()
    {
        var req1 = new EnqueueOutboxEventRequest(_tenantId, "OrderPaid", "ORD-SEARCH-TARGET", MaxRetries: 1);
        var req2 = new EnqueueOutboxEventRequest(_tenantId, "DealWon", "DEAL-OTHER", MaxRetries: 1);

        await _sut.EnqueueAsync(req1);
        await _sut.EnqueueAsync(req2);

        // Force fail both into DLQ
        await _sut.ProcessPendingOutboxBatchAsync(10, _tenantId, _ => Task.FromResult(false));

        // 1. Search by aggregate ID
        var searchResult = await _sut.GetDeadLetterMessagesAsync(new DeadLetterQueryParameters(_tenantId, SearchTerm: "SEARCH-TARGET"));
        searchResult.Should().HaveCount(1);
        searchResult.First().AggregateId.Should().Be("ORD-SEARCH-TARGET");

        // 2. Filter by event type
        var typeResult = await _sut.GetDeadLetterMessagesAsync(new DeadLetterQueryParameters(_tenantId, EventType: "DealWon"));
        typeResult.Should().HaveCount(1);
        typeResult.First().EventType.Should().Be("DealWon");
    }

    [Fact]
    public async Task ReplayDeadLetterMessagesAsync_ResetsQuarantinedEventToPendingAndClearsError()
    {
        var req = new EnqueueOutboxEventRequest(_tenantId, "TicketEscalated", "TCK-REPLAY-1", MaxRetries: 1);
        var enqueued = await _sut.EnqueueAsync(req);

        // Fail into DLQ
        await _sut.ProcessPendingOutboxBatchAsync(10, _tenantId, _ => Task.FromResult(false));

        var dlqBefore = await _sut.GetDeadLetterMessagesAsync(new DeadLetterQueryParameters(_tenantId));
        dlqBefore.Should().HaveCount(1);

        // Replay
        var replayResult = await _sut.ReplayDeadLetterMessagesAsync(new ReplayDeadLetterRequest(
            EventIds: [enqueued.EventId],
            ResetRetryCount: true,
            Reason: "Broker issue resolved"
        ));

        replayResult.ReplayedCount.Should().Be(1);
        replayResult.ReplayedEventIds.Should().Contain(enqueued.EventId);
        replayResult.Errors.Should().BeEmpty();

        // DLQ should now be empty
        var dlqAfter = await _sut.GetDeadLetterMessagesAsync(new DeadLetterQueryParameters(_tenantId));
        dlqAfter.Should().BeEmpty();

        // Should be pending in active mesh
        var stats = await _sut.GetMeshStatisticsAsync(_tenantId);
        stats.CurrentPendingCount.Should().Be(1);

        // Next dispatch should succeed
        var dispatchAfterReplay = await _sut.ProcessPendingOutboxBatchAsync(10, _tenantId);
        dispatchAfterReplay.SucceededCount.Should().Be(1);
    }

    [Fact]
    public async Task PurgeDeadLetterMessageAsync_PermanentlyRemovesEvent()
    {
        var req = new EnqueueOutboxEventRequest(_tenantId, "OrderPaid", "ORD-PURGE", MaxRetries: 1);
        var enqueued = await _sut.EnqueueAsync(req);

        await _sut.ProcessPendingOutboxBatchAsync(10, _tenantId, _ => Task.FromResult(false));

        var purged = await _sut.PurgeDeadLetterMessageAsync(enqueued.EventId);
        purged.Should().BeTrue();

        var detail = await _sut.GetDeadLetterMessageByIdAsync(enqueued.EventId);
        detail.Should().BeNull();

        // Purging again returns false
        var secondPurge = await _sut.PurgeDeadLetterMessageAsync(enqueued.EventId);
        secondPurge.Should().BeFalse();
    }

    [Fact]
    public void ComputeDeduplicationHash_IsDeterministicAndCaseNormalized()
    {
        var hash1 = _sut.ComputeDeduplicationHash(_tenantId, "OrderPaid", "AGG-123", "{\"val\":1}");
        var hash2 = _sut.ComputeDeduplicationHash(_tenantId, "orderpaid", "agg-123", "{\"val\":1}");
        var hashDiff = _sut.ComputeDeduplicationHash(_tenantId, "OrderPaid", "AGG-123", "{\"val\":2}");

        hash1.Should().Be(hash2);
        hash1.Should().NotBe(hashDiff);
        hash1.Length.Should().Be(64);
    }

    [Fact]
    public async Task GetMeshStatisticsAsync_AccuratelyCalculatesCountsAndSuccessRate()
    {
        // 1 success, 1 DLQ
        var successReq = new EnqueueOutboxEventRequest(_tenantId, "OrderPaid", "ORD-STAT-OK", MaxRetries: 3);
        var failReq = new EnqueueOutboxEventRequest(_tenantId, "OrderPaid", "ORD-STAT-FAIL", MaxRetries: 1);

        await _sut.EnqueueAsync(successReq);
        await _sut.EnqueueAsync(failReq);

        // Process: failReq will fail into DLQ, successReq will succeed
        await _sut.ProcessPendingOutboxBatchAsync(10, _tenantId, evt =>
        {
            var isFail = evt.AggregateId == "ORD-STAT-FAIL";
            return Task.FromResult(!isFail);
        });

        var stats = await _sut.GetMeshStatisticsAsync(_tenantId);
        stats.TotalEnqueued.Should().Be(2);
        stats.TotalPublished.Should().Be(1);
        stats.TotalQuarantinedDeadLetter.Should().Be(1);
        stats.CurrentDeadLetterDepth.Should().Be(1);
        stats.CurrentPendingCount.Should().Be(0);
        stats.SuccessRatePercent.Should().Be(50.0);
    }
}
