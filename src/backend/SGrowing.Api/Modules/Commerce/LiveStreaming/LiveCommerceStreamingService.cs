using System.Collections.Concurrent;

namespace SGrowing.Api.Modules.Commerce.LiveStreaming;

/// <summary>
/// Real-time live streaming management, WebRTC WHIP/WHEP handshake coordination,
/// flash product pinning, and synchronized flash stock reservation.
/// </summary>
public interface ILiveCommerceStreamingService
{
    Task<LiveStreamSession> CreateSessionAsync(
        string hostId,
        string title,
        string description,
        CancellationToken ct = default);

    Task<LiveStreamSession> StartSessionAsync(
        string sessionId,
        CancellationToken ct = default);

    Task<LiveStreamSession> PauseSessionAsync(
        string sessionId,
        CancellationToken ct = default);

    Task<LiveStreamSession> EndSessionAsync(
        string sessionId,
        CancellationToken ct = default);

    Task<PinnedProductRecord> PinProductAsync(
        string sessionId,
        string productId,
        string sku,
        string title,
        decimal originalPrice,
        decimal flashPrice,
        int stockLimit,
        TimeSpan duration,
        CancellationToken ct = default);

    Task<bool> UnpinProductAsync(
        string sessionId,
        CancellationToken ct = default);

    Task<StockReservationResult> ReserveFlashStockAsync(
        string sessionId,
        string productId,
        string customerId,
        int quantity,
        CancellationToken ct = default);

    Task<LiveStreamSession?> GetActiveSessionAsync(
        string sessionId,
        CancellationToken ct = default);

    Task<int> UpdateViewerCountAsync(
        string sessionId,
        int currentViewers,
        CancellationToken ct = default);

    Task<IReadOnlyList<LiveStreamSession>> GetAllSessionsAsync(
        CancellationToken ct = default);
}

/// <summary>
/// Thread-safe in-memory implementation of the Live Commerce streaming service.
/// </summary>
public class LiveCommerceStreamingService : ILiveCommerceStreamingService
{
    private readonly ConcurrentDictionary<string, LiveStreamSession> _sessions = new();
    private readonly Lock _reservationLock = new();

    public Task<LiveStreamSession> CreateSessionAsync(
        string hostId,
        string title,
        string description,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(hostId);
        ArgumentException.ThrowIfNullOrWhiteSpace(title);

        var sessionId = $"live_{Guid.NewGuid():N}";
        var streamKey = $"sk_{Guid.NewGuid():N}";

        var protocolInfo = new StreamingProtocolInfo(
            IngestUrl: $"https://stream.sgrowing.com/whip/{sessionId}",
            PlaybackUrl: $"https://stream.sgrowing.com/whep/{sessionId}",
            HlsFallbackUrl: $"https://stream.sgrowing.com/hls/{sessionId}/master.m3u8",
            ProtocolType: StreamingProtocolType.WebRTC_WHIP);

        var session = new LiveStreamSession
        {
            SessionId = sessionId,
            HostId = hostId,
            Title = title,
            Description = description ?? string.Empty,
            StreamKey = streamKey,
            Status = LiveStreamStatus.Scheduled,
            StartedAt = null,
            EndedAt = null,
            CurrentViewers = 0,
            PeakViewers = 0,
            TotalSalesAmount = 0m,
            PinnedProduct = null,
            StreamingProtocol = protocolInfo,
            CreatedAt = DateTime.UtcNow
        };

        _sessions[sessionId] = session;
        return Task.FromResult(session);
    }

    public Task<LiveStreamSession> StartSessionAsync(
        string sessionId,
        CancellationToken ct = default)
    {
        if (!_sessions.TryGetValue(sessionId, out var session))
        {
            throw new KeyNotFoundException($"Live stream session '{sessionId}' was not found.");
        }

        if (session.Status == LiveStreamStatus.Ended)
        {
            throw new InvalidOperationException($"Cannot start stream session '{sessionId}' because it has already ended.");
        }

        session.Status = LiveStreamStatus.Live;
        session.StartedAt ??= DateTime.UtcNow;

        return Task.FromResult(session);
    }

    public Task<LiveStreamSession> PauseSessionAsync(
        string sessionId,
        CancellationToken ct = default)
    {
        if (!_sessions.TryGetValue(sessionId, out var session))
        {
            throw new KeyNotFoundException($"Live stream session '{sessionId}' was not found.");
        }

        if (session.Status != LiveStreamStatus.Live)
        {
            throw new InvalidOperationException($"Cannot pause session '{sessionId}' because its status is {session.Status}, not Live.");
        }

        session.Status = LiveStreamStatus.Paused;
        return Task.FromResult(session);
    }

    public Task<LiveStreamSession> EndSessionAsync(
        string sessionId,
        CancellationToken ct = default)
    {
        if (!_sessions.TryGetValue(sessionId, out var session))
        {
            throw new KeyNotFoundException($"Live stream session '{sessionId}' was not found.");
        }

        session.Status = LiveStreamStatus.Ended;
        session.EndedAt = DateTime.UtcNow;

        // Deactivate any currently active pinned product
        if (session.PinnedProduct is { IsActive: true })
        {
            session.PinnedProduct = session.PinnedProduct with { IsActive = false };
        }

        return Task.FromResult(session);
    }

    public Task<PinnedProductRecord> PinProductAsync(
        string sessionId,
        string productId,
        string sku,
        string title,
        decimal originalPrice,
        decimal flashPrice,
        int stockLimit,
        TimeSpan duration,
        CancellationToken ct = default)
    {
        if (!_sessions.TryGetValue(sessionId, out var session))
        {
            throw new KeyNotFoundException($"Live stream session '{sessionId}' was not found.");
        }

        if (session.Status == LiveStreamStatus.Ended)
        {
            throw new InvalidOperationException($"Cannot pin a product to an ended live stream session '{sessionId}'.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(productId);
        ArgumentException.ThrowIfNullOrWhiteSpace(sku);
        ArgumentException.ThrowIfNullOrWhiteSpace(title);

        if (originalPrice <= 0m)
        {
            throw new ArgumentException("Original price must be greater than zero.", nameof(originalPrice));
        }

        if (flashPrice <= 0m)
        {
            throw new ArgumentException("Flash sale price must be greater than zero.", nameof(flashPrice));
        }

        if (flashPrice >= originalPrice)
        {
            throw new ArgumentException(
                $"Flash sale price ({flashPrice:C}) must be strictly lower than original catalog price ({originalPrice:C}).",
                nameof(flashPrice));
        }

        if (stockLimit <= 0)
        {
            throw new ArgumentException("Stock limit must be greater than zero.", nameof(stockLimit));
        }

        if (duration <= TimeSpan.Zero)
        {
            throw new ArgumentException("Flash deal duration must be a positive duration.", nameof(duration));
        }

        var now = DateTime.UtcNow;
        var pinned = new PinnedProductRecord(
            ProductId: productId,
            Sku: sku,
            Title: title,
            OriginalPrice: originalPrice,
            FlashSalePrice: flashPrice,
            AvailableStock: stockLimit,
            ReservedStock: 0,
            PinnedAt: now,
            ExpirationTime: now.Add(duration),
            IsActive: true);

        session.PinnedProduct = pinned;
        return Task.FromResult(pinned);
    }

    public Task<bool> UnpinProductAsync(
        string sessionId,
        CancellationToken ct = default)
    {
        if (!_sessions.TryGetValue(sessionId, out var session))
        {
            throw new KeyNotFoundException($"Live stream session '{sessionId}' was not found.");
        }

        if (session.PinnedProduct is null || !session.PinnedProduct.IsActive)
        {
            return Task.FromResult(false);
        }

        session.PinnedProduct = session.PinnedProduct with { IsActive = false };
        return Task.FromResult(true);
    }

    public Task<StockReservationResult> ReserveFlashStockAsync(
        string sessionId,
        string productId,
        string customerId,
        int quantity,
        CancellationToken ct = default)
    {
        if (!_sessions.TryGetValue(sessionId, out var session))
        {
            throw new KeyNotFoundException($"Live stream session '{sessionId}' was not found.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(productId);
        ArgumentException.ThrowIfNullOrWhiteSpace(customerId);

        if (quantity <= 0)
        {
            return Task.FromResult(new StockReservationResult(
                Success: false,
                ReservationId: null,
                SessionId: sessionId,
                ProductId: productId,
                Quantity: quantity,
                UnitPrice: 0m,
                TotalAmount: 0m,
                RemainingStock: 0,
                Message: "Reservation quantity must be at least 1 unit."));
        }

        lock (_reservationLock)
        {
            var pinned = session.PinnedProduct;
            if (pinned is null || !pinned.IsActive)
            {
                return Task.FromResult(new StockReservationResult(
                    Success: false,
                    ReservationId: null,
                    SessionId: sessionId,
                    ProductId: productId,
                    Quantity: quantity,
                    UnitPrice: 0m,
                    TotalAmount: 0m,
                    RemainingStock: 0,
                    Message: "No active flash product is currently pinned to this live stream."));
            }

            if (!string.Equals(pinned.ProductId, productId, StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult(new StockReservationResult(
                    Success: false,
                    ReservationId: null,
                    SessionId: sessionId,
                    ProductId: productId,
                    Quantity: quantity,
                    UnitPrice: pinned.FlashSalePrice,
                    TotalAmount: 0m,
                    RemainingStock: pinned.RemainingStock,
                    Message: $"Product '{productId}' does not match the active pinned flash deal ('{pinned.ProductId}')."));
            }

            if (pinned.IsExpired)
            {
                session.PinnedProduct = pinned with { IsActive = false };
                return Task.FromResult(new StockReservationResult(
                    Success: false,
                    ReservationId: null,
                    SessionId: sessionId,
                    ProductId: productId,
                    Quantity: quantity,
                    UnitPrice: pinned.FlashSalePrice,
                    TotalAmount: 0m,
                    RemainingStock: 0,
                    Message: "The flash deal countdown has expired."));
            }

            int available = pinned.RemainingStock;
            if (available < quantity)
            {
                return Task.FromResult(new StockReservationResult(
                    Success: false,
                    ReservationId: null,
                    SessionId: sessionId,
                    ProductId: productId,
                    Quantity: quantity,
                    UnitPrice: pinned.FlashSalePrice,
                    TotalAmount: 0m,
                    RemainingStock: available,
                    Message: $"Insufficient flash inventory. Only {available} units remaining."));
            }

            var updatedPinned = pinned with
            {
                ReservedStock = pinned.ReservedStock + quantity
            };

            session.PinnedProduct = updatedPinned;

            var reservationAmount = quantity * updatedPinned.FlashSalePrice;
            session.TotalSalesAmount += reservationAmount;

            var reservationId = $"res_{Guid.NewGuid():N}";

            return Task.FromResult(new StockReservationResult(
                Success: true,
                ReservationId: reservationId,
                SessionId: sessionId,
                ProductId: productId,
                Quantity: quantity,
                UnitPrice: updatedPinned.FlashSalePrice,
                TotalAmount: reservationAmount,
                RemainingStock: updatedPinned.RemainingStock,
                Message: "Flash deal stock locked successfully. Complete checkout within 10 minutes."));
        }
    }

    public Task<LiveStreamSession?> GetActiveSessionAsync(
        string sessionId,
        CancellationToken ct = default)
    {
        _sessions.TryGetValue(sessionId, out var session);
        return Task.FromResult(session);
    }

    public Task<int> UpdateViewerCountAsync(
        string sessionId,
        int currentViewers,
        CancellationToken ct = default)
    {
        if (!_sessions.TryGetValue(sessionId, out var session))
        {
            throw new KeyNotFoundException($"Live stream session '{sessionId}' was not found.");
        }

        session.CurrentViewers = Math.Max(0, currentViewers);
        if (session.CurrentViewers > session.PeakViewers)
        {
            session.PeakViewers = session.CurrentViewers;
        }

        return Task.FromResult(session.PeakViewers);
    }

    public Task<IReadOnlyList<LiveStreamSession>> GetAllSessionsAsync(
        CancellationToken ct = default)
    {
        IReadOnlyList<LiveStreamSession> list = [.. _sessions.Values];
        return Task.FromResult(list);
    }
}
