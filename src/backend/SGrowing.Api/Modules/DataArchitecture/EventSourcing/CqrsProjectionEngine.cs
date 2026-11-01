using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SGrowing.Api.Modules.DataArchitecture.EventSourcing;

public sealed record OrderSummaryReadModel(
    string OrderId,
    string Status,
    decimal TotalAmount,
    string CustomerId,
    int EventCount,
    DateTimeOffset LastUpdatedUtc
);

public sealed record WalletBalanceReadModel(
    string WalletId,
    decimal AvailableBalance,
    decimal EscrowLockedBalance,
    decimal TotalVolumeTransacted,
    DateTimeOffset LastUpdatedUtc
);

public interface ICqrsProjectionEngine
{
    Task ProjectEventAsync(DomainEvent @event, CancellationToken ct = default);
    Task<OrderSummaryReadModel?> GetOrderSummaryAsync(string orderId, CancellationToken ct = default);
    Task<WalletBalanceReadModel?> GetWalletBalanceAsync(string walletId, CancellationToken ct = default);
    Task<IReadOnlyList<OrderSummaryReadModel>> GetAllOrderSummariesAsync(CancellationToken ct = default);
    Task<IReadOnlyList<WalletBalanceReadModel>> GetAllWalletBalancesAsync(CancellationToken ct = default);
    Task ResetProjectionsAsync(CancellationToken ct = default);
    Task RebuildProjectionsAsync(IEnumerable<DomainEvent> events, CancellationToken ct = default);
    long ProcessedEventsCount { get; }
    int TrackedOrdersCount { get; }
    int TrackedWalletsCount { get; }
}

/// <summary>
/// Asynchronous in-memory CQRS projection engine with strict idempotency and cursor checkpoints,
/// updating read models for Orders and Wallets from event streams.
/// </summary>
public sealed class CqrsProjectionEngine : ICqrsProjectionEngine
{
    private readonly System.Threading.Lock _lock = new();

    // Checkpoint & idempotency tracking
    private readonly HashSet<Guid> _processedEventIds = [];
    private readonly Dictionary<string, long> _aggregateVersions = new(StringComparer.Ordinal);

    // Read Model stores
    private readonly Dictionary<string, OrderSummaryReadModel> _orderSummaries = new(StringComparer.Ordinal);
    private readonly Dictionary<string, WalletBalanceReadModel> _walletBalances = new(StringComparer.Ordinal);

    public long ProcessedEventsCount
    {
        get
        {
            lock (_lock)
            {
                return _processedEventIds.Count;
            }
        }
    }

    public int TrackedOrdersCount
    {
        get
        {
            lock (_lock)
            {
                return _orderSummaries.Count;
            }
        }
    }

    public int TrackedWalletsCount
    {
        get
        {
            lock (_lock)
            {
                return _walletBalances.Count;
            }
        }
    }

    public Task ProjectEventAsync(DomainEvent @event, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(@event);

        lock (_lock)
        {
            // 1. Idempotency Check: already processed by EventId?
            if (_processedEventIds.Contains(@event.EventId))
            {
                return Task.CompletedTask;
            }

            // 2. Cursor Checkpoint: already processed higher or equal version for aggregate?
            if (@event.Version > 0 &&
                _aggregateVersions.TryGetValue(@event.AggregateId, out var currentVersion) &&
                @event.Version <= currentVersion)
            {
                _processedEventIds.Add(@event.EventId);
                return Task.CompletedTask;
            }

            // 3. Mutate specific Read Model
            switch (@event)
            {
                case OrderPlacedEvent placed:
                    _orderSummaries[placed.AggregateId] = new OrderSummaryReadModel(
                        OrderId: placed.AggregateId,
                        Status: "Pending",
                        TotalAmount: placed.TotalAmount,
                        CustomerId: placed.CustomerId,
                        EventCount: 1,
                        LastUpdatedUtc: placed.TimestampUtc
                    );
                    break;

                case OrderPaidEvent paid:
                    if (_orderSummaries.TryGetValue(paid.AggregateId, out var existingOrderPaid))
                    {
                        _orderSummaries[paid.AggregateId] = existingOrderPaid with
                        {
                            Status = "Paid",
                            EventCount = existingOrderPaid.EventCount + 1,
                            LastUpdatedUtc = paid.TimestampUtc
                        };
                    }
                    break;

                case OrderShippedEvent shipped:
                    if (_orderSummaries.TryGetValue(shipped.AggregateId, out var existingOrderShipped))
                    {
                        _orderSummaries[shipped.AggregateId] = existingOrderShipped with
                        {
                            Status = "Shipped",
                            EventCount = existingOrderShipped.EventCount + 1,
                            LastUpdatedUtc = shipped.TimestampUtc
                        };
                    }
                    break;

                case OrderCancelledEvent cancelled:
                    if (_orderSummaries.TryGetValue(cancelled.AggregateId, out var existingOrderCancelled))
                    {
                        _orderSummaries[cancelled.AggregateId] = existingOrderCancelled with
                        {
                            Status = "Cancelled",
                            EventCount = existingOrderCancelled.EventCount + 1,
                            LastUpdatedUtc = cancelled.TimestampUtc
                        };
                    }
                    break;

                case WalletCreatedEvent walletCreated:
                    _walletBalances[walletCreated.AggregateId] = new WalletBalanceReadModel(
                        WalletId: walletCreated.AggregateId,
                        AvailableBalance: walletCreated.InitialBalance,
                        EscrowLockedBalance: 0m,
                        TotalVolumeTransacted: walletCreated.InitialBalance,
                        LastUpdatedUtc: walletCreated.TimestampUtc
                    );
                    break;

                case FundsCreditedEvent credited:
                    if (_walletBalances.TryGetValue(credited.AggregateId, out var existingWalletCredited))
                    {
                        _walletBalances[credited.AggregateId] = existingWalletCredited with
                        {
                            AvailableBalance = existingWalletCredited.AvailableBalance + credited.Amount,
                            TotalVolumeTransacted = existingWalletCredited.TotalVolumeTransacted + credited.Amount,
                            LastUpdatedUtc = credited.TimestampUtc
                        };
                    }
                    break;

                case FundsDebitedEvent debited:
                    if (_walletBalances.TryGetValue(debited.AggregateId, out var existingWalletDebited))
                    {
                        _walletBalances[debited.AggregateId] = existingWalletDebited with
                        {
                            AvailableBalance = existingWalletDebited.AvailableBalance - debited.Amount,
                            TotalVolumeTransacted = existingWalletDebited.TotalVolumeTransacted + debited.Amount,
                            LastUpdatedUtc = debited.TimestampUtc
                        };
                    }
                    break;

                case EscrowLockedEvent locked:
                    if (_walletBalances.TryGetValue(locked.AggregateId, out var existingWalletLocked))
                    {
                        _walletBalances[locked.AggregateId] = existingWalletLocked with
                        {
                            AvailableBalance = existingWalletLocked.AvailableBalance - locked.Amount,
                            EscrowLockedBalance = existingWalletLocked.EscrowLockedBalance + locked.Amount,
                            TotalVolumeTransacted = existingWalletLocked.TotalVolumeTransacted + locked.Amount,
                            LastUpdatedUtc = locked.TimestampUtc
                        };
                    }
                    break;

                case EscrowReleasedEvent released:
                    if (_walletBalances.TryGetValue(released.AggregateId, out var existingWalletReleased))
                    {
                        _walletBalances[released.AggregateId] = existingWalletReleased with
                        {
                            EscrowLockedBalance = existingWalletReleased.EscrowLockedBalance - released.Amount,
                            TotalVolumeTransacted = existingWalletReleased.TotalVolumeTransacted + released.Amount,
                            LastUpdatedUtc = released.TimestampUtc
                        };
                    }
                    break;
            }

            // 4. Update idempotency and version checkpoints
            _processedEventIds.Add(@event.EventId);
            if (@event.Version > 0)
            {
                _aggregateVersions[@event.AggregateId] = @event.Version;
            }
        }

        return Task.CompletedTask;
    }

    public Task<OrderSummaryReadModel?> GetOrderSummaryAsync(string orderId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(orderId);

        lock (_lock)
        {
            _orderSummaries.TryGetValue(orderId, out var model);
            return Task.FromResult(model);
        }
    }

    public Task<WalletBalanceReadModel?> GetWalletBalanceAsync(string walletId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(walletId);

        lock (_lock)
        {
            _walletBalances.TryGetValue(walletId, out var model);
            return Task.FromResult(model);
        }
    }

    public Task<IReadOnlyList<OrderSummaryReadModel>> GetAllOrderSummariesAsync(CancellationToken ct = default)
    {
        lock (_lock)
        {
            IReadOnlyList<OrderSummaryReadModel> list = [.. _orderSummaries.Values];
            return Task.FromResult(list);
        }
    }

    public Task<IReadOnlyList<WalletBalanceReadModel>> GetAllWalletBalancesAsync(CancellationToken ct = default)
    {
        lock (_lock)
        {
            IReadOnlyList<WalletBalanceReadModel> list = [.. _walletBalances.Values];
            return Task.FromResult(list);
        }
    }

    public Task ResetProjectionsAsync(CancellationToken ct = default)
    {
        lock (_lock)
        {
            _processedEventIds.Clear();
            _aggregateVersions.Clear();
            _orderSummaries.Clear();
            _walletBalances.Clear();
        }

        return Task.CompletedTask;
    }

    public async Task RebuildProjectionsAsync(IEnumerable<DomainEvent> events, CancellationToken ct = default)
    {
        await ResetProjectionsAsync(ct);

        foreach (var @event in events.OrderBy(e => e.TimestampUtc).ThenBy(e => e.Version))
        {
            await ProjectEventAsync(@event, ct);
        }
    }
}
