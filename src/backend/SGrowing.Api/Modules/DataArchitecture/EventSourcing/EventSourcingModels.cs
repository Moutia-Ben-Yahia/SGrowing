using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace SGrowing.Api.Modules.DataArchitecture.EventSourcing;

#region Domain Events Base & Concrete Events

/// <summary>
/// Base record for all immutable domain events stored in the event journal.
/// </summary>
public abstract record DomainEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public string AggregateId { get; init; } = string.Empty;
    public string AggregateType { get; init; } = string.Empty;
    public long Version { get; init; }
    public DateTimeOffset TimestampUtc { get; init; } = DateTimeOffset.UtcNow;
    public IReadOnlyDictionary<string, string> Metadata { get; init; } = new Dictionary<string, string>();

    protected DomainEvent() { }

    protected DomainEvent(
        string aggregateId,
        string aggregateType,
        long version = 0,
        Guid? eventId = null,
        DateTimeOffset? timestampUtc = null,
        IReadOnlyDictionary<string, string>? metadata = null)
    {
        EventId = eventId ?? Guid.NewGuid();
        AggregateId = aggregateId;
        AggregateType = aggregateType;
        Version = version;
        TimestampUtc = timestampUtc ?? DateTimeOffset.UtcNow;
        Metadata = metadata ?? new Dictionary<string, string>();
    }
}

/// <summary>
/// Value object representing an individual line item within an order.
/// </summary>
public sealed record OrderItemRecord(string Sku, string Name, int Quantity, decimal UnitPrice);

// --- Order Domain Events ---

public sealed record OrderPlacedEvent : DomainEvent
{
    public string CustomerId { get; init; } = string.Empty;
    public decimal TotalAmount { get; init; }
    public IReadOnlyList<OrderItemRecord> Items { get; init; } = [];

    public OrderPlacedEvent() { AggregateType = "Order"; }

    public OrderPlacedEvent(
        string orderId,
        string customerId,
        decimal totalAmount,
        IReadOnlyList<OrderItemRecord> items,
        long version = 0,
        Guid? eventId = null,
        DateTimeOffset? timestampUtc = null,
        IReadOnlyDictionary<string, string>? metadata = null)
        : base(orderId, "Order", version, eventId, timestampUtc, metadata)
    {
        CustomerId = customerId;
        TotalAmount = totalAmount;
        Items = items ?? [];
    }
}

public sealed record OrderPaidEvent : DomainEvent
{
    public string PaymentReference { get; init; } = string.Empty;
    public decimal PaidAmount { get; init; }

    public OrderPaidEvent() { AggregateType = "Order"; }

    public OrderPaidEvent(
        string orderId,
        string paymentReference,
        decimal paidAmount,
        long version = 0,
        Guid? eventId = null,
        DateTimeOffset? timestampUtc = null,
        IReadOnlyDictionary<string, string>? metadata = null)
        : base(orderId, "Order", version, eventId, timestampUtc, metadata)
    {
        PaymentReference = paymentReference;
        PaidAmount = paidAmount;
    }
}

public sealed record OrderShippedEvent : DomainEvent
{
    public string TrackingNumber { get; init; } = string.Empty;
    public string Carrier { get; init; } = string.Empty;

    public OrderShippedEvent() { AggregateType = "Order"; }

    public OrderShippedEvent(
        string orderId,
        string trackingNumber,
        string carrier,
        long version = 0,
        Guid? eventId = null,
        DateTimeOffset? timestampUtc = null,
        IReadOnlyDictionary<string, string>? metadata = null)
        : base(orderId, "Order", version, eventId, timestampUtc, metadata)
    {
        TrackingNumber = trackingNumber;
        Carrier = carrier;
    }
}

public sealed record OrderCancelledEvent : DomainEvent
{
    public string Reason { get; init; } = string.Empty;
    public DateTimeOffset CancelledAtUtc { get; init; } = DateTimeOffset.UtcNow;

    public OrderCancelledEvent() { AggregateType = "Order"; }

    public OrderCancelledEvent(
        string orderId,
        string reason,
        DateTimeOffset? cancelledAtUtc = null,
        long version = 0,
        Guid? eventId = null,
        DateTimeOffset? timestampUtc = null,
        IReadOnlyDictionary<string, string>? metadata = null)
        : base(orderId, "Order", version, eventId, timestampUtc, metadata)
    {
        Reason = reason;
        CancelledAtUtc = cancelledAtUtc ?? DateTimeOffset.UtcNow;
    }
}

// --- Wallet Domain Events ---

public sealed record WalletCreatedEvent : DomainEvent
{
    public string OwnerId { get; init; } = string.Empty;
    public string Currency { get; init; } = "USD";
    public decimal InitialBalance { get; init; }

    public WalletCreatedEvent() { AggregateType = "Wallet"; }

    public WalletCreatedEvent(
        string walletId,
        string ownerId,
        string currency,
        decimal initialBalance = 0m,
        long version = 0,
        Guid? eventId = null,
        DateTimeOffset? timestampUtc = null,
        IReadOnlyDictionary<string, string>? metadata = null)
        : base(walletId, "Wallet", version, eventId, timestampUtc, metadata)
    {
        OwnerId = ownerId;
        Currency = currency;
        InitialBalance = initialBalance;
    }
}

public sealed record FundsCreditedEvent : DomainEvent
{
    public decimal Amount { get; init; }
    public string SourceReference { get; init; } = string.Empty;

    public FundsCreditedEvent() { AggregateType = "Wallet"; }

    public FundsCreditedEvent(
        string walletId,
        decimal amount,
        string sourceReference,
        long version = 0,
        Guid? eventId = null,
        DateTimeOffset? timestampUtc = null,
        IReadOnlyDictionary<string, string>? metadata = null)
        : base(walletId, "Wallet", version, eventId, timestampUtc, metadata)
    {
        Amount = amount;
        SourceReference = sourceReference;
    }
}

public sealed record FundsDebitedEvent : DomainEvent
{
    public decimal Amount { get; init; }
    public string DestinationReference { get; init; } = string.Empty;

    public FundsDebitedEvent() { AggregateType = "Wallet"; }

    public FundsDebitedEvent(
        string walletId,
        decimal amount,
        string destinationReference,
        long version = 0,
        Guid? eventId = null,
        DateTimeOffset? timestampUtc = null,
        IReadOnlyDictionary<string, string>? metadata = null)
        : base(walletId, "Wallet", version, eventId, timestampUtc, metadata)
    {
        Amount = amount;
        DestinationReference = destinationReference;
    }
}

public sealed record EscrowLockedEvent : DomainEvent
{
    public string EscrowId { get; init; } = string.Empty;
    public decimal Amount { get; init; }
    public string BeneficiaryId { get; init; } = string.Empty;

    public EscrowLockedEvent() { AggregateType = "Wallet"; }

    public EscrowLockedEvent(
        string walletId,
        string escrowId,
        decimal amount,
        string beneficiaryId,
        long version = 0,
        Guid? eventId = null,
        DateTimeOffset? timestampUtc = null,
        IReadOnlyDictionary<string, string>? metadata = null)
        : base(walletId, "Wallet", version, eventId, timestampUtc, metadata)
    {
        EscrowId = escrowId;
        Amount = amount;
        BeneficiaryId = beneficiaryId;
    }
}

public sealed record EscrowReleasedEvent : DomainEvent
{
    public string EscrowId { get; init; } = string.Empty;
    public decimal Amount { get; init; }
    public string BeneficiaryId { get; init; } = string.Empty;

    public EscrowReleasedEvent() { AggregateType = "Wallet"; }

    public EscrowReleasedEvent(
        string walletId,
        string escrowId,
        decimal amount,
        string beneficiaryId,
        long version = 0,
        Guid? eventId = null,
        DateTimeOffset? timestampUtc = null,
        IReadOnlyDictionary<string, string>? metadata = null)
        : base(walletId, "Wallet", version, eventId, timestampUtc, metadata)
    {
        EscrowId = escrowId;
        Amount = amount;
        BeneficiaryId = beneficiaryId;
    }
}

#endregion

#region Concurrency Exception & Snapshots

/// <summary>
/// Thrown when an optimistic concurrency collision occurs while appending events to an aggregate stream.
/// </summary>
public sealed class OptimisticConcurrencyException : Exception
{
    public string AggregateId { get; }
    public long ExpectedVersion { get; }
    public long ActualVersion { get; }

    public OptimisticConcurrencyException(string aggregateId, long expectedVersion, long actualVersion)
        : base($"Optimistic concurrency collision on aggregate '{aggregateId}'. Expected version {expectedVersion}, but actual current stream version is {actualVersion}.")
    {
        AggregateId = aggregateId;
        ExpectedVersion = expectedVersion;
        ActualVersion = actualVersion;
    }
}

/// <summary>
/// Represents a point-in-time state snapshot of an aggregate root for fast hydration.
/// </summary>
public sealed record SnapshotRecord(
    string AggregateId,
    string AggregateType,
    long Version,
    string PayloadJson,
    DateTimeOffset CreatedAtUtc
);

#endregion

#region Aggregate Root Base & Implementations

/// <summary>
/// Base class for all Event-Sourced Aggregate Roots.
/// </summary>
public abstract class AggregateRoot
{
    public string AggregateId { get; protected set; } = string.Empty;
    public long Version { get; protected set; }

    private readonly List<DomainEvent> _uncommittedEvents = [];
    public IReadOnlyList<DomainEvent> UncommittedEvents => _uncommittedEvents.AsReadOnly();

    public void MarkEventsAsCommitted()
    {
        _uncommittedEvents.Clear();
    }

    public void ApplyChange(DomainEvent @event)
    {
        ApplyChange(@event, isNew: true);
    }

    protected void ApplyChange(DomainEvent @event, bool isNew)
    {
        When(@event);
        if (isNew)
        {
            Version++;
            var stampedEvent = @event with { Version = Version, AggregateId = AggregateId.Length > 0 ? AggregateId : @event.AggregateId };
            _uncommittedEvents.Add(stampedEvent);
        }
        else
        {
            Version = @event.Version;
        }
    }

    public void LoadFromHistory(IEnumerable<DomainEvent> history)
    {
        foreach (var @event in history.OrderBy(e => e.Version))
        {
            ApplyChange(@event, isNew: false);
        }
    }

    public abstract void When(DomainEvent @event);

    public virtual SnapshotRecord CreateSnapshot()
    {
        throw new NotSupportedException($"Snapshots are not configured for aggregate type {GetType().Name}.");
    }

    public virtual void RestoreFromSnapshot(SnapshotRecord snapshot)
    {
        throw new NotSupportedException($"Snapshots are not configured for aggregate type {GetType().Name}.");
    }
}

public enum OrderStatus
{
    Pending,
    Paid,
    Shipped,
    Cancelled
}

/// <summary>
/// Event-sourced Order aggregate root.
/// </summary>
public sealed class OrderAggregate : AggregateRoot
{
    public OrderStatus Status { get; private set; } = OrderStatus.Pending;
    public decimal TotalAmount { get; private set; }
    public string CustomerId { get; private set; } = string.Empty;
    public IReadOnlyList<OrderItemRecord> Items { get; private set; } = [];

    public OrderAggregate() { }

    public static OrderAggregate PlaceOrder(string orderId, string customerId, decimal totalAmount, IReadOnlyList<OrderItemRecord> items)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(orderId);
        ArgumentException.ThrowIfNullOrWhiteSpace(customerId);
        if (totalAmount < 0) throw new ArgumentOutOfRangeException(nameof(totalAmount), "Total amount cannot be negative.");

        var aggregate = new OrderAggregate();
        aggregate.ApplyChange(new OrderPlacedEvent(orderId, customerId, totalAmount, items));
        return aggregate;
    }

    public void MarkAsPaid(string paymentReference, decimal paidAmount)
    {
        if (Status == OrderStatus.Cancelled)
            throw new InvalidOperationException("Cannot pay a cancelled order.");
        if (Status == OrderStatus.Paid || Status == OrderStatus.Shipped)
            throw new InvalidOperationException($"Order is already in '{Status}' state.");
        if (paidAmount < TotalAmount)
            throw new InvalidOperationException($"Insufficient payment amount: expected {TotalAmount}, got {paidAmount}.");

        ApplyChange(new OrderPaidEvent(AggregateId, paymentReference, paidAmount));
    }

    public void ShipOrder(string trackingNumber, string carrier)
    {
        if (Status != OrderStatus.Paid)
            throw new InvalidOperationException($"Cannot ship order with status '{Status}'. Only Paid orders can be shipped.");

        ApplyChange(new OrderShippedEvent(AggregateId, trackingNumber, carrier));
    }

    public void CancelOrder(string reason)
    {
        if (Status == OrderStatus.Shipped)
            throw new InvalidOperationException("Cannot cancel an order that has already shipped.");
        if (Status == OrderStatus.Cancelled)
            return; // Idempotent cancellation

        ApplyChange(new OrderCancelledEvent(AggregateId, reason, DateTimeOffset.UtcNow));
    }

    public override void When(DomainEvent @event)
    {
        switch (@event)
        {
            case OrderPlacedEvent placed:
                AggregateId = placed.AggregateId;
                CustomerId = placed.CustomerId;
                TotalAmount = placed.TotalAmount;
                Items = placed.Items;
                Status = OrderStatus.Pending;
                break;
            case OrderPaidEvent:
                Status = OrderStatus.Paid;
                break;
            case OrderShippedEvent:
                Status = OrderStatus.Shipped;
                break;
            case OrderCancelledEvent:
                Status = OrderStatus.Cancelled;
                break;
        }
    }

    public override SnapshotRecord CreateSnapshot()
    {
        var state = new OrderSnapshotState(AggregateId, CustomerId, TotalAmount, Status.ToString(), Items);
        var json = JsonSerializer.Serialize(state);
        return new SnapshotRecord(AggregateId, "Order", Version, json, DateTimeOffset.UtcNow);
    }

    public override void RestoreFromSnapshot(SnapshotRecord snapshot)
    {
        var state = JsonSerializer.Deserialize<OrderSnapshotState>(snapshot.PayloadJson)
            ?? throw new InvalidOperationException("Failed to deserialize Order snapshot state.");

        AggregateId = state.OrderId;
        CustomerId = state.CustomerId;
        TotalAmount = state.TotalAmount;
        Status = Enum.Parse<OrderStatus>(state.Status);
        Items = state.Items ?? [];
        Version = snapshot.Version;
    }

    private sealed record OrderSnapshotState(
        string OrderId,
        string CustomerId,
        decimal TotalAmount,
        string Status,
        IReadOnlyList<OrderItemRecord> Items
    );
}

/// <summary>
/// Event-sourced Wallet aggregate root supporting balances, debits, credits, and multi-party escrow holds.
/// </summary>
public sealed class WalletAggregate : AggregateRoot
{
    public decimal AvailableBalance { get; private set; }
    public decimal EscrowLockedBalance { get; private set; }
    public string Currency { get; private set; } = "USD";
    public string OwnerId { get; private set; } = string.Empty;

    public WalletAggregate() { }

    public static WalletAggregate Create(string walletId, string ownerId, string currency, decimal initialBalance = 0m)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(walletId);
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerId);
        if (initialBalance < 0) throw new ArgumentOutOfRangeException(nameof(initialBalance), "Initial balance cannot be negative.");

        var wallet = new WalletAggregate();
        wallet.ApplyChange(new WalletCreatedEvent(walletId, ownerId, currency, initialBalance));
        return wallet;
    }

    public void CreditFunds(decimal amount, string sourceReference)
    {
        if (amount <= 0) throw new ArgumentOutOfRangeException(nameof(amount), "Credit amount must be positive.");
        ApplyChange(new FundsCreditedEvent(AggregateId, amount, sourceReference));
    }

    public void DebitFunds(decimal amount, string destinationReference)
    {
        if (amount <= 0) throw new ArgumentOutOfRangeException(nameof(amount), "Debit amount must be positive.");
        if (AvailableBalance < amount)
            throw new InvalidOperationException($"Insufficient available funds: have {AvailableBalance}, requested debit of {amount}.");

        ApplyChange(new FundsDebitedEvent(AggregateId, amount, destinationReference));
    }

    public void LockEscrow(string escrowId, decimal amount, string beneficiaryId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(escrowId);
        if (amount <= 0) throw new ArgumentOutOfRangeException(nameof(amount), "Escrow amount must be positive.");
        if (AvailableBalance < amount)
            throw new InvalidOperationException($"Insufficient available funds to lock escrow: have {AvailableBalance}, escrow hold is {amount}.");

        ApplyChange(new EscrowLockedEvent(AggregateId, escrowId, amount, beneficiaryId));
    }

    public void ReleaseEscrow(string escrowId, decimal amount, string beneficiaryId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(escrowId);
        if (amount <= 0) throw new ArgumentOutOfRangeException(nameof(amount), "Escrow release amount must be positive.");
        if (EscrowLockedBalance < amount)
            throw new InvalidOperationException($"Cannot release {amount} from escrow: locked escrow balance is {EscrowLockedBalance}.");

        ApplyChange(new EscrowReleasedEvent(AggregateId, escrowId, amount, beneficiaryId));
    }

    public override void When(DomainEvent @event)
    {
        switch (@event)
        {
            case WalletCreatedEvent created:
                AggregateId = created.AggregateId;
                OwnerId = created.OwnerId;
                Currency = created.Currency;
                AvailableBalance = created.InitialBalance;
                EscrowLockedBalance = 0m;
                break;
            case FundsCreditedEvent credited:
                AvailableBalance += credited.Amount;
                break;
            case FundsDebitedEvent debited:
                AvailableBalance -= debited.Amount;
                break;
            case EscrowLockedEvent locked:
                AvailableBalance -= locked.Amount;
                EscrowLockedBalance += locked.Amount;
                break;
            case EscrowReleasedEvent released:
                EscrowLockedBalance -= released.Amount;
                break;
        }
    }

    public override SnapshotRecord CreateSnapshot()
    {
        var state = new WalletSnapshotState(AggregateId, OwnerId, Currency, AvailableBalance, EscrowLockedBalance);
        var json = JsonSerializer.Serialize(state);
        return new SnapshotRecord(AggregateId, "Wallet", Version, json, DateTimeOffset.UtcNow);
    }

    public override void RestoreFromSnapshot(SnapshotRecord snapshot)
    {
        var state = JsonSerializer.Deserialize<WalletSnapshotState>(snapshot.PayloadJson)
            ?? throw new InvalidOperationException("Failed to deserialize Wallet snapshot state.");

        AggregateId = state.WalletId;
        OwnerId = state.OwnerId;
        Currency = state.Currency;
        AvailableBalance = state.AvailableBalance;
        EscrowLockedBalance = state.EscrowLockedBalance;
        Version = snapshot.Version;
    }

    private sealed record WalletSnapshotState(
        string WalletId,
        string OwnerId,
        string Currency,
        decimal AvailableBalance,
        decimal EscrowLockedBalance
    );
}

#endregion
