using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using SGrowing.Api.Modules.DataArchitecture.EventSourcing;
using Xunit;

namespace SGrowing.Tests.DataArchitecture;

public class EventSourcingCqrsTests
{
    private readonly EventStoreEngine _eventStore;
    private readonly CqrsProjectionEngine _projectionEngine;
    private readonly OutboxCdcStreamingService _cdcService;

    public EventSourcingCqrsTests()
    {
        _projectionEngine = new CqrsProjectionEngine();
        _cdcService = new OutboxCdcStreamingService(partitionCount: 4);
        _eventStore = new EventStoreEngine(_projectionEngine, _cdcService);
    }

    #region 1. Event Stream Appending & Sequence Versioning

    [Fact]
    public async Task AppendEventsAsync_WhenAppendedToNewStream_AssignsSequentialVersions()
    {
        var orderId = "ORD-" + Guid.NewGuid();
        var items = new List<OrderItemRecord> { new("SKU-1", "Product 1", 2, 50m) };

        var event1 = new OrderPlacedEvent(orderId, "CUST-1", 100m, items);
        var event2 = new OrderPaidEvent(orderId, "PAY-REF-1", 100m);

        await _eventStore.AppendEventsAsync(orderId, expectedVersion: 0, [event1, event2]);

        var stream = await _eventStore.GetEventStreamAsync(orderId);
        stream.Should().HaveCount(2);
        stream[0].Version.Should().Be(1);
        stream[0].AggregateId.Should().Be(orderId);
        stream[1].Version.Should().Be(2);
        stream[1].AggregateId.Should().Be(orderId);
        _eventStore.ActiveStreamsCount.Should().Be(1);
        _eventStore.TotalEventsCount.Should().Be(2);
    }

    [Fact]
    public async Task AppendEventsAsync_SuccessiveAppends_IncrementsStreamTipCorrectly()
    {
        var walletId = "WAL-" + Guid.NewGuid();

        await _eventStore.AppendEventsAsync(walletId, 0, [new WalletCreatedEvent(walletId, "USER-1", "USD", 50m)]);
        await _eventStore.AppendEventsAsync(walletId, 1, [new FundsCreditedEvent(walletId, 100m, "REF-1")]);
        await _eventStore.AppendEventsAsync(walletId, 2, [new FundsDebitedEvent(walletId, 30m, "REF-2")]);

        var stream = await _eventStore.GetEventStreamAsync(walletId);
        stream.Should().HaveCount(3);
        stream.Select(e => e.Version).Should().ContainInOrder(1, 2, 3);
    }

    [Fact]
    public async Task GetEventStreamAsync_WithFromVersionFilter_ReturnsOnlySubsequentEvents()
    {
        var orderId = "ORD-" + Guid.NewGuid();
        await _eventStore.AppendEventsAsync(orderId, 0, [
            new OrderPlacedEvent(orderId, "CUST-1", 200m, []),
            new OrderPaidEvent(orderId, "PAY-1", 200m),
            new OrderShippedEvent(orderId, "TRACK-1", "DHL"),
            new OrderCancelledEvent(orderId, "Mistake")
        ]);

        var streamFromV3 = await _eventStore.GetEventStreamAsync(orderId, fromVersion: 3);
        streamFromV3.Should().HaveCount(2);
        streamFromV3[0].Version.Should().Be(3);
        streamFromV3[1].Version.Should().Be(4);
    }

    [Fact]
    public async Task GetEventStreamAsync_WhenAggregateDoesNotExist_ReturnsEmptyList()
    {
        var stream = await _eventStore.GetEventStreamAsync("NON-EXISTENT-" + Guid.NewGuid());
        stream.Should().BeEmpty();
    }

    #endregion

    #region 2. Optimistic Concurrency Collision Enforcement

    [Fact]
    public async Task AppendEventsAsync_WhenExpectedVersionMismatches_ThrowsOptimisticConcurrencyException()
    {
        var orderId = "ORD-" + Guid.NewGuid();
        await _eventStore.AppendEventsAsync(orderId, 0, [new OrderPlacedEvent(orderId, "CUST-1", 100m, [])]);

        // Attempt append with expectedVersion 0 while stream is already at version 1
        var act = async () => await _eventStore.AppendEventsAsync(orderId, expectedVersion: 0, [
            new OrderPaidEvent(orderId, "PAY-REF", 100m)
        ]);

        var ex = await act.Should().ThrowAsync<OptimisticConcurrencyException>();
        ex.Which.AggregateId.Should().Be(orderId);
        ex.Which.ExpectedVersion.Should().Be(0);
        ex.Which.ActualVersion.Should().Be(1);
    }

    [Fact]
    public async Task AppendEventsAsync_WhenNewStreamGivenNonZeroExpectedVersion_ThrowsOptimisticConcurrencyException()
    {
        var orderId = "ORD-" + Guid.NewGuid();

        var act = async () => await _eventStore.AppendEventsAsync(orderId, expectedVersion: 5, [
            new OrderPlacedEvent(orderId, "CUST-1", 100m, [])
        ]);

        var ex = await act.Should().ThrowAsync<OptimisticConcurrencyException>();
        ex.Which.AggregateId.Should().Be(orderId);
        ex.Which.ExpectedVersion.Should().Be(5);
        ex.Which.ActualVersion.Should().Be(0);
    }

    #endregion

    #region 3. Order Aggregate Root Mutations & Domain Invariants

    [Fact]
    public void OrderAggregate_PlaceOrder_AppliesPlacedEventAndSetsPendingStatus()
    {
        var orderId = "ORD-" + Guid.NewGuid();
        var items = new List<OrderItemRecord> { new("SKU-A", "Widget", 1, 45m) };

        var order = OrderAggregate.PlaceOrder(orderId, "CUST-A", 45m, items);

        order.AggregateId.Should().Be(orderId);
        order.CustomerId.Should().Be("CUST-A");
        order.TotalAmount.Should().Be(45m);
        order.Status.Should().Be(OrderStatus.Pending);
        order.Version.Should().Be(1);
        order.UncommittedEvents.Should().HaveCount(1);
        order.UncommittedEvents[0].Should().BeOfType<OrderPlacedEvent>();
    }

    [Fact]
    public void OrderAggregate_MarkAsPaid_TransitionsStatusToPaid()
    {
        var order = OrderAggregate.PlaceOrder("ORD-1", "CUST-1", 100m, []);
        order.MarkEventsAsCommitted();

        order.MarkAsPaid("PAY-123", 100m);

        order.Status.Should().Be(OrderStatus.Paid);
        order.Version.Should().Be(2);
        order.UncommittedEvents.Should().HaveCount(1);
        order.UncommittedEvents[0].Should().BeOfType<OrderPaidEvent>();
    }

    [Fact]
    public void OrderAggregate_MarkAsPaid_WhenUnderpaid_ThrowsInvalidOperationException()
    {
        var order = OrderAggregate.PlaceOrder("ORD-1", "CUST-1", 100m, []);

        var act = () => order.MarkAsPaid("PAY-123", 99.99m);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Insufficient payment amount*");
    }

    [Fact]
    public void OrderAggregate_ShipOrder_TransitionsStatusToShipped()
    {
        var order = OrderAggregate.PlaceOrder("ORD-1", "CUST-1", 50m, []);
        order.MarkAsPaid("PAY-1", 50m);
        order.MarkEventsAsCommitted();

        order.ShipOrder("TRACK-999", "FedEx");

        order.Status.Should().Be(OrderStatus.Shipped);
        order.Version.Should().Be(3);
        order.UncommittedEvents.Should().HaveCount(1);
        order.UncommittedEvents[0].Should().BeOfType<OrderShippedEvent>();
    }

    [Fact]
    public void OrderAggregate_ShipOrder_WhenNotPaid_ThrowsInvalidOperationException()
    {
        var order = OrderAggregate.PlaceOrder("ORD-1", "CUST-1", 50m, []);

        var act = () => order.ShipOrder("TRACK-999", "FedEx");

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Only Paid orders can be shipped*");
    }

    [Fact]
    public void OrderAggregate_CancelOrder_WhenShipped_ThrowsInvalidOperationException()
    {
        var order = OrderAggregate.PlaceOrder("ORD-1", "CUST-1", 50m, []);
        order.MarkAsPaid("PAY-1", 50m);
        order.ShipOrder("TRK-1", "DHL");

        var act = () => order.CancelOrder("Customer changed mind");

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Cannot cancel an order that has already shipped*");
    }

    [Fact]
    public void OrderAggregate_LoadFromHistory_ReconstructsStateWithoutUncommittedEvents()
    {
        var orderId = "ORD-" + Guid.NewGuid();
        var history = new List<DomainEvent>
        {
            new OrderPlacedEvent(orderId, "CUST-9", 150m, [new("SKU-X", "Item X", 3, 50m)], version: 1),
            new OrderPaidEvent(orderId, "PAY-9", 150m, version: 2),
            new OrderShippedEvent(orderId, "TRK-9", "UPS", version: 3)
        };

        var rehydrated = new OrderAggregate();
        rehydrated.LoadFromHistory(history);

        rehydrated.AggregateId.Should().Be(orderId);
        rehydrated.CustomerId.Should().Be("CUST-9");
        rehydrated.TotalAmount.Should().Be(150m);
        rehydrated.Status.Should().Be(OrderStatus.Shipped);
        rehydrated.Version.Should().Be(3);
        rehydrated.UncommittedEvents.Should().BeEmpty();
    }

    #endregion

    #region 4. Wallet Aggregate Root Mutations & Escrow Invariants

    [Fact]
    public void WalletAggregate_Create_InitializesBalancesAndOwner()
    {
        var walletId = "WAL-" + Guid.NewGuid();
        var wallet = WalletAggregate.Create(walletId, "OWNER-1", "EUR", 100m);

        wallet.AggregateId.Should().Be(walletId);
        wallet.OwnerId.Should().Be("OWNER-1");
        wallet.Currency.Should().Be("EUR");
        wallet.AvailableBalance.Should().Be(100m);
        wallet.EscrowLockedBalance.Should().Be(0m);
        wallet.Version.Should().Be(1);
    }

    [Fact]
    public void WalletAggregate_CreditFunds_IncreasesAvailableBalance()
    {
        var wallet = WalletAggregate.Create("WAL-1", "OWNER-1", "USD", 50m);
        wallet.MarkEventsAsCommitted();

        wallet.CreditFunds(150m, "DEPOSIT-1");

        wallet.AvailableBalance.Should().Be(200m);
        wallet.Version.Should().Be(2);
        wallet.UncommittedEvents.Should().HaveCount(1);
        wallet.UncommittedEvents[0].Should().BeOfType<FundsCreditedEvent>();
    }

    [Fact]
    public void WalletAggregate_DebitFunds_DecreasesAvailableBalance()
    {
        var wallet = WalletAggregate.Create("WAL-1", "OWNER-1", "USD", 100m);
        wallet.MarkEventsAsCommitted();

        wallet.DebitFunds(40m, "WITHDRAWAL-1");

        wallet.AvailableBalance.Should().Be(60m);
        wallet.Version.Should().Be(2);
        wallet.UncommittedEvents.Should().HaveCount(1);
        wallet.UncommittedEvents[0].Should().BeOfType<FundsDebitedEvent>();
    }

    [Fact]
    public void WalletAggregate_DebitFunds_WhenExceedsAvailableBalance_ThrowsInvalidOperationException()
    {
        var wallet = WalletAggregate.Create("WAL-1", "OWNER-1", "USD", 50m);

        var act = () => wallet.DebitFunds(50.01m, "OVERDRAFT");

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Insufficient available funds*");
    }

    [Fact]
    public void WalletAggregate_LockEscrow_MovesAmountFromAvailableToEscrowLocked()
    {
        var wallet = WalletAggregate.Create("WAL-1", "OWNER-1", "USD", 200m);
        wallet.MarkEventsAsCommitted();

        wallet.LockEscrow("ESC-1", 75m, "SELLER-1");

        wallet.AvailableBalance.Should().Be(125m);
        wallet.EscrowLockedBalance.Should().Be(75m);
        wallet.Version.Should().Be(2);
    }

    [Fact]
    public void WalletAggregate_LockEscrow_WhenInsufficientAvailable_ThrowsInvalidOperationException()
    {
        var wallet = WalletAggregate.Create("WAL-1", "OWNER-1", "USD", 50m);

        var act = () => wallet.LockEscrow("ESC-1", 100m, "SELLER-1");

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Insufficient available funds to lock escrow*");
    }

    [Fact]
    public void WalletAggregate_ReleaseEscrow_DeductsFromEscrowLockedBalance()
    {
        var wallet = WalletAggregate.Create("WAL-1", "OWNER-1", "USD", 200m);
        wallet.LockEscrow("ESC-1", 100m, "SELLER-1");
        wallet.MarkEventsAsCommitted();

        wallet.ReleaseEscrow("ESC-1", 60m, "SELLER-1");

        wallet.AvailableBalance.Should().Be(100m);
        wallet.EscrowLockedBalance.Should().Be(40m);
        wallet.Version.Should().Be(3);
    }

    [Fact]
    public void WalletAggregate_ReleaseEscrow_WhenExceedsLockedBalance_ThrowsInvalidOperationException()
    {
        var wallet = WalletAggregate.Create("WAL-1", "OWNER-1", "USD", 100m);
        wallet.LockEscrow("ESC-1", 30m, "SELLER-1");

        var act = () => wallet.ReleaseEscrow("ESC-1", 30.01m, "SELLER-1");

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Cannot release*locked escrow balance*");
    }

    #endregion

    #region 5. Snapshots & Fast-Forward Aggregate Hydration

    [Fact]
    public async Task Snapshots_SaveAndGetLatestSnapshot_PersistsSnapshotRecord()
    {
        var walletId = "WAL-" + Guid.NewGuid();
        var wallet = WalletAggregate.Create(walletId, "USER-7", "USD", 500m);
        var snapshot = wallet.CreateSnapshot();

        await _eventStore.SaveSnapshotAsync(snapshot);

        var fetched = await _eventStore.GetLatestSnapshotAsync(walletId);
        fetched.Should().NotBeNull();
        fetched!.AggregateId.Should().Be(walletId);
        fetched.AggregateType.Should().Be("Wallet");
        fetched.Version.Should().Be(1);
        _eventStore.TotalSnapshotsCount.Should().Be(1);
    }

    [Fact]
    public async Task LoadAggregateAsync_FastForwardsFromSnapshotPlusSubsequentEvents()
    {
        var orderId = "ORD-" + Guid.NewGuid();
        var order = OrderAggregate.PlaceOrder(orderId, "CUST-1", 300m, []);
        order.MarkAsPaid("PAY-1", 300m);

        // Append initial events to store
        await _eventStore.AppendEventsAsync(orderId, 0, order.UncommittedEvents);
        order.MarkEventsAsCommitted();

        // Take snapshot at version 2 (Paid)
        var snapshot = order.CreateSnapshot();
        await _eventStore.SaveSnapshotAsync(snapshot);

        // Subsequent event after snapshot (version 3: Shipped)
        var shipEvent = new OrderShippedEvent(orderId, "TRK-FAST", "DHL");
        await _eventStore.AppendEventsAsync(orderId, expectedVersion: 2, [shipEvent]);

        // Load aggregate using fast-forward
        var rehydrated = await _eventStore.LoadAggregateAsync<OrderAggregate>(orderId);

        rehydrated.Should().NotBeNull();
        rehydrated!.AggregateId.Should().Be(orderId);
        rehydrated.Status.Should().Be(OrderStatus.Shipped);
        rehydrated.Version.Should().Be(3);
        rehydrated.TotalAmount.Should().Be(300m);
    }

    [Fact]
    public async Task LoadAggregateAsync_WhenOnlyEventsExistWithoutSnapshot_ReconstructsAccurately()
    {
        var walletId = "WAL-" + Guid.NewGuid();
        await _eventStore.AppendEventsAsync(walletId, 0, [
            new WalletCreatedEvent(walletId, "U1", "USD", 100m),
            new FundsCreditedEvent(walletId, 50m, "DEP-1")
        ]);

        var wallet = await _eventStore.LoadAggregateAsync<WalletAggregate>(walletId);

        wallet.Should().NotBeNull();
        wallet!.AvailableBalance.Should().Be(150m);
        wallet.Version.Should().Be(2);
    }

    #endregion

    #region 6. Point-In-Time Time Travel Historical Reconstruction

    [Fact]
    public async Task ReconstructAggregateAtTimeAsync_ReconstructsStateAtExactIntermediateTimestamp()
    {
        var orderId = "ORD-" + Guid.NewGuid();
        var t0 = DateTimeOffset.UtcNow.AddMinutes(-30);
        var t1 = DateTimeOffset.UtcNow.AddMinutes(-20);
        var t2 = DateTimeOffset.UtcNow.AddMinutes(-10);

        var placed = new OrderPlacedEvent(orderId, "CUST-TIME", 250m, [], timestampUtc: t0);
        var paid = new OrderPaidEvent(orderId, "PAY-TIME", 250m, timestampUtc: t1);
        var shipped = new OrderShippedEvent(orderId, "TRK-TIME", "FedEx", timestampUtc: t2);

        await _eventStore.AppendEventsAsync(orderId, 0, [placed, paid, shipped]);

        // 1. Before t0 -> should be null
        var atBeforeT0 = await _eventStore.ReconstructAggregateAtTimeAsync<OrderAggregate>(orderId, t0.AddMinutes(-5));
        atBeforeT0.Should().BeNull();

        // 2. Between t0 and t1 -> should be Pending
        var atT0 = await _eventStore.ReconstructAggregateAtTimeAsync<OrderAggregate>(orderId, t0.AddMinutes(5));
        atT0.Should().NotBeNull();
        atT0!.Status.Should().Be(OrderStatus.Pending);
        atT0.Version.Should().Be(1);

        // 3. Between t1 and t2 -> should be Paid
        var atT1 = await _eventStore.ReconstructAggregateAtTimeAsync<OrderAggregate>(orderId, t1.AddMinutes(5));
        atT1.Should().NotBeNull();
        atT1!.Status.Should().Be(OrderStatus.Paid);
        atT1.Version.Should().Be(2);

        // 4. After t2 -> should be Shipped
        var atAfterT2 = await _eventStore.ReconstructAggregateAtTimeAsync<OrderAggregate>(orderId, t2.AddMinutes(5));
        atAfterT2.Should().NotBeNull();
        atAfterT2!.Status.Should().Be(OrderStatus.Shipped);
        atAfterT2.Version.Should().Be(3);
    }

    [Fact]
    public async Task ReconstructAggregateAtTimeAsync_ForWallet_ReflectsHistoricalBalance()
    {
        var walletId = "WAL-" + Guid.NewGuid();
        var t0 = DateTimeOffset.UtcNow.AddHours(-2);
        var t1 = DateTimeOffset.UtcNow.AddHours(-1);

        await _eventStore.AppendEventsAsync(walletId, 0, [
            new WalletCreatedEvent(walletId, "OWNER-TT", "USD", 1000m, timestampUtc: t0),
            new FundsDebitedEvent(walletId, 400m, "DEB-1", timestampUtc: t1)
        ]);

        var historicalWallet = await _eventStore.ReconstructAggregateAtTimeAsync<WalletAggregate>(walletId, t0.AddMinutes(10));
        historicalWallet.Should().NotBeNull();
        historicalWallet!.AvailableBalance.Should().Be(1000m);
        historicalWallet.Version.Should().Be(1);
    }

    #endregion

    #region 7. CQRS Projections & Idempotency Checkpoints

    [Fact]
    public async Task CqrsProjectionEngine_ProjectEvent_UpdatesOrderSummaryReadModel()
    {
        var orderId = "ORD-" + Guid.NewGuid();
        var placed = new OrderPlacedEvent(orderId, "CUST-PQ", 120m, [new("SKU", "Prod", 1, 120m)], version: 1);
        var paid = new OrderPaidEvent(orderId, "PAY-REF", 120m, version: 2);

        await _projectionEngine.ProjectEventAsync(placed);
        await _projectionEngine.ProjectEventAsync(paid);

        var model = await _projectionEngine.GetOrderSummaryAsync(orderId);
        model.Should().NotBeNull();
        model!.OrderId.Should().Be(orderId);
        model.CustomerId.Should().Be("CUST-PQ");
        model.TotalAmount.Should().Be(120m);
        model.Status.Should().Be("Paid");
        model.EventCount.Should().Be(2);
    }

    [Fact]
    public async Task CqrsProjectionEngine_ProjectEvent_IsStrictlyIdempotentOnDuplicateEvents()
    {
        var orderId = "ORD-" + Guid.NewGuid();
        var placed = new OrderPlacedEvent(orderId, "CUST-IDEM", 500m, [], version: 1);

        // Project the same event 3 times
        await _projectionEngine.ProjectEventAsync(placed);
        await _projectionEngine.ProjectEventAsync(placed);
        await _projectionEngine.ProjectEventAsync(placed);

        var model = await _projectionEngine.GetOrderSummaryAsync(orderId);
        model.Should().NotBeNull();
        model!.EventCount.Should().Be(1);
        _projectionEngine.ProcessedEventsCount.Should().Be(1);
    }

    [Fact]
    public async Task CqrsProjectionEngine_WalletProjection_CalculatesAvailableEscrowAndVolume()
    {
        var walletId = "WAL-" + Guid.NewGuid();
        var evCreated = new WalletCreatedEvent(walletId, "USER-W", "USD", 200m, version: 1);
        var evCredit = new FundsCreditedEvent(walletId, 300m, "REF-CR", version: 2);
        var evDebit = new FundsDebitedEvent(walletId, 100m, "REF-DB", version: 3);
        var evLock = new EscrowLockedEvent(walletId, "ESC-1", 150m, "BENEF-1", version: 4);
        var evRelease = new EscrowReleasedEvent(walletId, "ESC-1", 50m, "BENEF-1", version: 5);

        await _projectionEngine.ProjectEventAsync(evCreated);
        await _projectionEngine.ProjectEventAsync(evCredit);
        await _projectionEngine.ProjectEventAsync(evDebit);
        await _projectionEngine.ProjectEventAsync(evLock);
        await _projectionEngine.ProjectEventAsync(evRelease);

        var model = await _projectionEngine.GetWalletBalanceAsync(walletId);
        model.Should().NotBeNull();
        // Calculation:
        // Available: 200 + 300 - 100 - 150 = 250
        // EscrowLocked: 150 - 50 = 100
        // TotalVolume: 200 + 300 + 100 + 150 + 50 = 800
        model!.AvailableBalance.Should().Be(250m);
        model.EscrowLockedBalance.Should().Be(100m);
        model.TotalVolumeTransacted.Should().Be(800m);
    }

    [Fact]
    public async Task CqrsProjectionEngine_RebuildProjectionsAsync_ResetsAndRestoresAllModels()
    {
        var orderId = "ORD-" + Guid.NewGuid();
        var events = new List<DomainEvent>
        {
            new OrderPlacedEvent(orderId, "CUST-REBUILD", 80m, [], version: 1),
            new OrderPaidEvent(orderId, "P-REB", 80m, version: 2)
        };

        await _projectionEngine.RebuildProjectionsAsync(events);

        var model = await _projectionEngine.GetOrderSummaryAsync(orderId);
        model.Should().NotBeNull();
        model!.Status.Should().Be("Paid");
        model.EventCount.Should().Be(2);
    }

    [Fact]
    public async Task CqrsProjectionEngine_GetAllSummariesAndBalances_ReturnsAllEntities()
    {
        await _projectionEngine.ResetProjectionsAsync();

        var ord1 = "ORD-A";
        var ord2 = "ORD-B";
        var wal1 = "WAL-A";

        await _projectionEngine.ProjectEventAsync(new OrderPlacedEvent(ord1, "C1", 10m, [], version: 1));
        await _projectionEngine.ProjectEventAsync(new OrderPlacedEvent(ord2, "C2", 20m, [], version: 1));
        await _projectionEngine.ProjectEventAsync(new WalletCreatedEvent(wal1, "U1", "USD", 50m, version: 1));

        var orders = await _projectionEngine.GetAllOrderSummariesAsync();
        var wallets = await _projectionEngine.GetAllWalletBalancesAsync();

        orders.Should().HaveCount(2);
        wallets.Should().HaveCount(1);
    }

    #endregion

    #region 8. Kafka/CDC Outbox Streamer, Partitions & Lag Metrics

    [Fact]
    public async Task CdcStreamingService_PublishEvent_AssignsPartitionAndIncrementalOffset()
    {
        var orderId = "ORD-" + Guid.NewGuid();
        var ev1 = new OrderPlacedEvent(orderId, "CUST-CDC", 99m, []);
        var ev2 = new OrderPaidEvent(orderId, "PAY-CDC", 99m);

        var msg1 = await _cdcService.PublishEventAsync(ev1);
        var msg2 = await _cdcService.PublishEventAsync(ev2);

        msg1.Partition.Should().Be(msg2.Partition); // same aggregate routes to same partition
        msg1.Offset.Should().BeGreaterThan(0);
        msg2.Offset.Should().Be(msg1.Offset + 1);
        msg1.Topic.Should().Be("sgrowing.events.order");
    }

    [Fact]
    public async Task CdcStreamingService_FetchBatch_ReturnsOnlyUncommittedMessagesForConsumerGroup()
    {
        var group = "analytics-consumer-group";
        await _cdcService.RegisterConsumerGroupAsync(group);

        var orderId = "ORD-" + Guid.NewGuid();
        var partition = _cdcService.GetPartitionForAggregate(orderId);

        var msg1 = await _cdcService.PublishEventAsync(new OrderPlacedEvent(orderId, "CUST-1", 10m, []));
        var msg2 = await _cdcService.PublishEventAsync(new OrderPaidEvent(orderId, "P-1", 10m));

        var batch1 = await _cdcService.FetchBatchAsync(group, partition, maxBatchSize: 10);
        batch1.Should().Contain(m => m.Offset == msg1.Offset);
        batch1.Should().Contain(m => m.Offset == msg2.Offset);

        // Commit first message
        await _cdcService.CommitOffsetAsync(group, partition, msg1.Offset);

        // Fetch again, should only return msg2
        var batch2 = await _cdcService.FetchBatchAsync(group, partition, maxBatchSize: 10);
        batch2.Should().ContainSingle(m => m.Offset == msg2.Offset);
    }

    [Fact]
    public async Task CdcStreamingService_GetMetrics_CalculatesLagCorrectly()
    {
        var group = "reconciliation-worker";
        await _cdcService.RegisterConsumerGroupAsync(group);

        var orderId = "ORD-" + Guid.NewGuid();
        var partition = _cdcService.GetPartitionForAggregate(orderId);

        var msg1 = await _cdcService.PublishEventAsync(new OrderPlacedEvent(orderId, "CUST-1", 10m, []));
        var msg2 = await _cdcService.PublishEventAsync(new OrderPaidEvent(orderId, "P-1", 10m));

        var metricsBefore = await _cdcService.GetMetricsAsync();
        var lagBefore = metricsBefore.ConsumerGroupLags.First(l => l.ConsumerGroup == group && l.Partition == partition);
        lagBefore.Lag.Should().BeGreaterThanOrEqualTo(2);

        // Commit up to msg2
        await _cdcService.CommitOffsetAsync(group, partition, msg2.Offset);

        var metricsAfter = await _cdcService.GetMetricsAsync();
        var lagAfter = metricsAfter.ConsumerGroupLags.First(l => l.ConsumerGroup == group && l.Partition == partition);
        lagAfter.Lag.Should().Be(0);
    }

    #endregion

    #region 9. CDC Dead Letter Queue (DLQ) & Consumer Resilience

    [Fact]
    public async Task CdcStreamingService_ConsumeAndProcessBatch_RoutesFailingMessageToDlq()
    {
        var group = "failing-consumer-group";
        await _cdcService.RegisterConsumerGroupAsync(group);

        var orderId = "ORD-" + Guid.NewGuid();
        var partition = _cdcService.GetPartitionForAggregate(orderId);

        var ev = new OrderPlacedEvent(orderId, "CUST-FAIL", 66m, []);
        var message = await _cdcService.PublishEventAsync(ev);

        // Handler throws simulating downstream failure
        var processed = await _cdcService.ConsumeAndProcessBatchAsync(
            group,
            partition,
            maxBatchSize: 1,
            handler: _ => throw new TimeoutException("Downstream webhook timeout"),
            maxRetries: 2
        );

        processed.Should().Be(0);

        var dlqMessages = await _cdcService.GetDlqMessagesAsync(group);
        dlqMessages.Should().ContainSingle();
        dlqMessages[0].Message.Offset.Should().Be(message.Offset);
        dlqMessages[0].ErrorReason.Should().Contain("Downstream webhook timeout");
        dlqMessages[0].RetryCount.Should().Be(2);

        var metrics = await _cdcService.GetMetricsAsync();
        metrics.TotalMessagesInDlq.Should().BeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public async Task CdcStreamingService_ReplayDlqMessage_WhenHandlerSucceeds_RemovesFromDlq()
    {
        var group = "replay-consumer-group";
        var orderId = "ORD-" + Guid.NewGuid();
        var message = await _cdcService.PublishEventAsync(new OrderPlacedEvent(orderId, "CUST-R", 50m, []));

        await _cdcService.RouteToDlqAsync(group, message, "Transient failure", retryCount: 1);
        var dlqBefore = await _cdcService.GetDlqMessagesAsync(group);
        var targetDlqId = dlqBefore[0].DlqId;

        // Replay with successful handler
        var replayed = await _cdcService.ReplayDlqMessageAsync(targetDlqId, _ => Task.FromResult(true));
        replayed.Should().BeTrue();

        var dlqAfter = await _cdcService.GetDlqMessagesAsync(group);
        dlqAfter.Should().NotContain(d => d.DlqId == targetDlqId);
    }

    [Fact]
    public async Task CdcStreamingService_ReplayDlqMessage_WhenHandlerFails_PreservesInDlq()
    {
        var group = "replay-fail-group";
        var orderId = "ORD-" + Guid.NewGuid();
        var message = await _cdcService.PublishEventAsync(new OrderPlacedEvent(orderId, "CUST-RF", 50m, []));

        await _cdcService.RouteToDlqAsync(group, message, "Network failure");
        var dlq = await _cdcService.GetDlqMessagesAsync(group);
        var targetDlqId = dlq[0].DlqId;

        var replayed = await _cdcService.ReplayDlqMessageAsync(targetDlqId, _ => Task.FromResult(false));
        replayed.Should().BeFalse();

        var dlqStillExists = await _cdcService.GetDlqMessagesAsync(group);
        dlqStillExists.Should().ContainSingle(d => d.DlqId == targetDlqId);
    }

    #endregion

    #region 10. Thread Safety & High-Throughput Concurrent Appends

    [Fact]
    public async Task EventStoreEngine_ConcurrentAppendsAcrossAggregates_MaintainsIntegrity()
    {
        const int aggregateCount = 20;
        const int eventsPerAggregate = 5;

        var tasks = Enumerable.Range(0, aggregateCount).Select(async i =>
        {
            var id = $"AGG-CONCUR-{i}";
            var events = Enumerable.Range(1, eventsPerAggregate)
                .Select(j => (DomainEvent)new OrderPlacedEvent(id, $"CUST-{i}", j * 10m, []))
                .ToList();

            await _eventStore.AppendEventsAsync(id, expectedVersion: 0, events);
        });

        await Task.WhenAll(tasks);

        _eventStore.ActiveStreamsCount.Should().Be(aggregateCount);
        _eventStore.TotalEventsCount.Should().Be(aggregateCount * eventsPerAggregate);

        for (var i = 0; i < aggregateCount; i++)
        {
            var stream = await _eventStore.GetEventStreamAsync($"AGG-CONCUR-{i}");
            stream.Should().HaveCount(eventsPerAggregate);
            stream.Select(e => e.Version).Should().ContainInOrder(1, 2, 3, 4, 5);
        }
    }

    #endregion

    #region 11. Dependency Injection Service Descriptor Assertions

    [Fact]
    public void AddEventSourcingCqrsServices_RegistersAllRequiredServicesAsSingletons()
    {
        // Must NOT call BuildServiceProvider() per strict prompt requirements
        var services = new ServiceCollection();
        services.AddEventSourcingCqrsServices();

        services.Should().Contain(d =>
            d.ServiceType == typeof(IEventStoreEngine) &&
            d.ImplementationType == typeof(EventStoreEngine) &&
            d.Lifetime == ServiceLifetime.Singleton);

        services.Should().Contain(d =>
            d.ServiceType == typeof(ICqrsProjectionEngine) &&
            d.ImplementationType == typeof(CqrsProjectionEngine) &&
            d.Lifetime == ServiceLifetime.Singleton);

        services.Should().Contain(d =>
            d.ServiceType == typeof(IOutboxCdcStreamingService) &&
            d.ImplementationType == typeof(OutboxCdcStreamingService) &&
            d.Lifetime == ServiceLifetime.Singleton);
    }

    #endregion
}
