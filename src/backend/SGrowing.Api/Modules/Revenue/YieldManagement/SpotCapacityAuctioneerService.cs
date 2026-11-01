using System.Collections.Concurrent;

namespace SGrowing.Api.Modules.Revenue.YieldManagement;

/// <summary>
/// High-velocity spot capacity auctioneer implementing Dutch descending auctions
/// to monetize perishable logistics and warehousing capacity before expiration.
/// </summary>
public sealed class SpotCapacityAuctioneerService
{
    private readonly ConcurrentDictionary<string, SpotCapacityAuctionLot> _lots = new(StringComparer.OrdinalIgnoreCase);
    private readonly System.Threading.Lock _lock = new();

    /// <summary>
    /// Creates a new descending Dutch auction lot for expiring spot capacity.
    /// </summary>
    public SpotCapacityAuctionLot CreateAuctionLot(CreateSpotAuctionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.StartingPriceDzd <= request.MinimumFloorPriceDzd)
        {
            throw new ArgumentException("Starting price must be strictly greater than minimum floor price.", nameof(request));
        }

        if (request.DecrementDzd <= 0m)
        {
            throw new ArgumentException("Price decrement must be positive.", nameof(request));
        }

        if (request.DecayIntervalSeconds <= 0)
        {
            throw new ArgumentException("Decay interval must be positive.", nameof(request));
        }

        var lotId = $"LOT-{Guid.NewGuid():N}"[..18].ToUpperInvariant();
        var now = DateTime.UtcNow;
        var expires = now.AddMinutes(request.DurationMinutes);

        var lot = new SpotCapacityAuctionLot(
            LotId: lotId,
            CapacityType: request.CapacityType,
            QuantityUnits: request.QuantityUnits,
            StartingReservePriceDzd: request.StartingPriceDzd,
            CurrentPriceDzd: request.StartingPriceDzd,
            MinimumFloorPriceDzd: request.MinimumFloorPriceDzd,
            DecayIntervalSeconds: request.DecayIntervalSeconds,
            PriceDecrementDzd: request.DecrementDzd,
            AuctionStartedAtUtc: now,
            AuctionExpiresAtUtc: expires,
            Status: AuctionLotStatus.Active
        );

        _lots[lotId] = lot;
        return lot;
    }

    /// <summary>
    /// Computes the current real-time decayed price of an active auction lot.
    /// </summary>
    public SpotCapacityAuctionLot GetCurrentLotState(string lotId, DateTime? asOfUtc = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(lotId);

        if (!_lots.TryGetValue(lotId, out var lot))
        {
            throw new KeyNotFoundException($"Auction lot '{lotId}' not found.");
        }

        if (lot.Status != AuctionLotStatus.Active)
        {
            return lot;
        }

        var now = asOfUtc ?? DateTime.UtcNow;

        if (now >= lot.AuctionExpiresAtUtc)
        {
            lock (_lock)
            {
                var expiredLot = lot with { Status = AuctionLotStatus.ExpiredFloorReached, CurrentPriceDzd = lot.MinimumFloorPriceDzd };
                _lots[lotId] = expiredLot;
                return expiredLot;
            }
        }

        var elapsedSeconds = (now - lot.AuctionStartedAtUtc).TotalSeconds;
        var steps = (int)Math.Max(0, elapsedSeconds / lot.DecayIntervalSeconds);
        var decayedPrice = lot.StartingReservePriceDzd - (steps * lot.PriceDecrementDzd);

        if (decayedPrice <= lot.MinimumFloorPriceDzd)
        {
            decayedPrice = lot.MinimumFloorPriceDzd;
        }

        var updatedLot = lot with { CurrentPriceDzd = decayedPrice };
        _lots[lotId] = updatedLot;
        return updatedLot;
    }

    /// <summary>
    /// First bidder to accept wins the lot instantly at current Dutch price.
    /// </summary>
    public SpotCapacityAuctionLot SubmitBid(SubmitAuctionBidRequest request, DateTime? asOfUtc = null)
    {
        ArgumentNullException.ThrowIfNull(request);

        var current = GetCurrentLotState(request.LotId, asOfUtc);
        if (current.Status != AuctionLotStatus.Active)
        {
            throw new InvalidOperationException($"Cannot bid on auction lot '{request.LotId}' with status '{current.Status}'.");
        }

        lock (_lock)
        {
            // Re-check after lock
            if (!_lots.TryGetValue(request.LotId, out var latest) || latest.Status != AuctionLotStatus.Active)
            {
                throw new InvalidOperationException($"Auction lot '{request.LotId}' is no longer active (already settled or expired).");
            }

            var settled = latest with
            {
                Status = AuctionLotStatus.Settled,
                WinningBidderId = request.BidderId,
                SettledAtUtc = asOfUtc ?? DateTime.UtcNow
            };

            _lots[request.LotId] = settled;
            return settled;
        }
    }

    /// <summary>
    /// Cancels an active auction lot.
    /// </summary>
    public bool CancelLot(string lotId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(lotId);

        lock (_lock)
        {
            if (_lots.TryGetValue(lotId, out var lot) && lot.Status == AuctionLotStatus.Active)
            {
                _lots[lotId] = lot with { Status = AuctionLotStatus.Canceled };
                return true;
            }
            return false;
        }
    }
}
