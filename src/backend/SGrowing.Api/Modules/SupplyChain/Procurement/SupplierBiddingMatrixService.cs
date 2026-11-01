namespace SGrowing.Api.Modules.SupplyChain.Procurement;

/// <summary>
/// Interface for the Supplier Bidding Matrix Engine and Auction Management Service.
/// </summary>
public interface ISupplierBiddingMatrixService
{
    BiddingAuction CreateAuction(CreateAuctionRequest request);
    BiddingAuction? GetAuction(Guid auctionId);
    IReadOnlyList<BiddingAuction> GetAllAuctions();
    SupplierBid SubmitBid(Guid auctionId, SubmitBidRequest request);
    IReadOnlyList<ScoredSupplierBid> EvaluateAndRankBids(Guid auctionId, BiddingMatrixWeights? weights = null);
    double CalculateUtilityScore(SupplierBid bid, decimal minPrice, decimal maxPrice, int maxLeadTime, BiddingMatrixWeights? weights = null);
    bool UpdateBid(Guid auctionId, SupplierBid updatedBid);
    bool UpdateAuctionStatus(Guid auctionId, AuctionStatus status);
}

/// <summary>
/// Implements the multi-criteria supplier utility evaluation matrix, out-of-spec bid filtering,
/// and thread-safe auction repository using .NET 10 System.Threading.Lock.
/// </summary>
public class SupplierBiddingMatrixService : ISupplierBiddingMatrixService
{
    private readonly Lock _lock = new();
    private readonly Dictionary<Guid, BiddingAuction> _auctions = [];

    /// <summary>
    /// Creates a new reverse bidding auction.
    /// </summary>
    public BiddingAuction CreateAuction(CreateAuctionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.Sku))
        {
            throw new ArgumentException("SKU is required.", nameof(request));
        }

        if (request.RequiredQuantity <= 0)
        {
            throw new ArgumentException("Required quantity must be positive.", nameof(request));
        }

        if (request.TargetUnitPrice <= 0m || request.ReserveUnitPrice < request.TargetUnitPrice)
        {
            throw new ArgumentException("Reserve unit price must be greater than or equal to target unit price and positive.", nameof(request));
        }

        var now = DateTime.UtcNow;
        var expiresAt = request.ExpiresAtUtc ?? now.AddDays(7);

        var auction = new BiddingAuction(
            AuctionId: Guid.NewGuid(),
            Sku: request.Sku.Trim().ToUpperInvariant(),
            RequiredQuantity: request.RequiredQuantity,
            TargetUnitPrice: request.TargetUnitPrice,
            ReserveUnitPrice: request.ReserveUnitPrice,
            DeliveryDeadlineUtc: request.DeliveryDeadlineUtc,
            Status: AuctionStatus.OpenForBids,
            CreatedAtUtc: now,
            ExpiresAtUtc: expiresAt,
            Bids: [],
            Type: request.Type,
            Description: request.Description);

        lock (_lock)
        {
            _auctions[auction.AuctionId] = auction;
        }

        return auction;
    }

    /// <summary>
    /// Retrieves an auction by ID.
    /// </summary>
    public BiddingAuction? GetAuction(Guid auctionId)
    {
        lock (_lock)
        {
            return _auctions.GetValueOrDefault(auctionId);
        }
    }

    /// <summary>
    /// Retrieves all auctions.
    /// </summary>
    public IReadOnlyList<BiddingAuction> GetAllAuctions()
    {
        lock (_lock)
        {
            return [.. _auctions.Values];
        }
    }

    /// <summary>
    /// Submits a supplier bid for an auction.
    /// </summary>
    public SupplierBid SubmitBid(Guid auctionId, SubmitBidRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.SupplierId))
        {
            throw new ArgumentException("Supplier ID is required.", nameof(request));
        }

        if (request.UnitPrice <= 0m)
        {
            throw new ArgumentException("Unit price must be positive.", nameof(request));
        }

        if (request.LeadTimeDays < 0)
        {
            throw new ArgumentException("Lead time cannot be negative.", nameof(request));
        }

        if (request.SlaReliabilityScore is < 0.0 or > 1.0)
        {
            throw new ArgumentOutOfRangeException(nameof(request), "SLA reliability score must be between 0.0 and 1.0.");
        }

        lock (_lock)
        {
            if (!_auctions.TryGetValue(auctionId, out var auction))
            {
                throw new KeyNotFoundException($"Auction '{auctionId}' was not found.");
            }

            if (auction.Status != AuctionStatus.OpenForBids && auction.Status != AuctionStatus.UnderNegotiation)
            {
                throw new InvalidOperationException($"Cannot submit bid. Auction is in '{auction.Status}' state.");
            }

            var bid = new SupplierBid(
                BidId: Guid.NewGuid(),
                AuctionId: auctionId,
                SupplierId: request.SupplierId.Trim(),
                SupplierName: string.IsNullOrWhiteSpace(request.SupplierName) ? request.SupplierId.Trim() : request.SupplierName.Trim(),
                UnitPrice: request.UnitPrice,
                LeadTimeDays: request.LeadTimeDays,
                SlaReliabilityScore: request.SlaReliabilityScore,
                AvailableQuantity: request.AvailableQuantity,
                Concessions: [],
                Status: BidStatus.Submitted,
                SubmittedAtUtc: DateTime.UtcNow);

            auction.Bids.Add(bid);
            return bid;
        }
    }

    /// <summary>
    /// Evaluates and ranks all bids for an auction based on multi-criteria utility function,
    /// filtering out-of-spec bids (price > reserve price, delivery after deadline).
    /// </summary>
    public IReadOnlyList<ScoredSupplierBid> EvaluateAndRankBids(Guid auctionId, BiddingMatrixWeights? weights = null)
    {
        weights ??= new BiddingMatrixWeights();
        BiddingAuction auction;

        lock (_lock)
        {
            if (!_auctions.TryGetValue(auctionId, out var foundAuction))
            {
                throw new KeyNotFoundException($"Auction '{auctionId}' was not found.");
            }

            auction = foundAuction;
        }

        if (auction.Bids.Count == 0)
        {
            return [];
        }

        var scoredBids = new List<ScoredSupplierBid>(auction.Bids.Count);

        // Pre-check specifications
        var eligibleBids = new List<SupplierBid>();
        foreach (var bid in auction.Bids)
        {
            var (isEligible, reason) = ValidateBidSpecification(bid, auction);
            if (isEligible)
            {
                eligibleBids.Add(bid);
            }
            else
            {
                scoredBids.Add(new ScoredSupplierBid(
                    Bid: bid,
                    UtilityScore: 0.0,
                    CostScore: 0.0,
                    SlaScore: bid.SlaReliabilityScore,
                    LeadTimeScore: 0.0,
                    IsEligible: false,
                    DisqualificationReason: reason));
            }
        }

        if (eligibleBids.Count > 0)
        {
            var minPrice = eligibleBids.Min(b => b.UnitPrice);
            var maxPrice = eligibleBids.Max(b => b.UnitPrice);
            var maxLeadTime = eligibleBids.Max(b => b.LeadTimeDays);

            foreach (var bid in eligibleBids)
            {
                var costScore = ComputeCostScore(bid.UnitPrice, minPrice, maxPrice);
                var slaScore = Math.Clamp(bid.SlaReliabilityScore, 0.0, 1.0);
                var leadScore = ComputeLeadScore(bid.LeadTimeDays, maxLeadTime);

                var totalWeight = weights.TotalWeight > 0 ? weights.TotalWeight : 1.0;
                var compositeUtility = ((weights.CostWeight * costScore) +
                                        (weights.SlaWeight * slaScore) +
                                        (weights.LeadTimeWeight * leadScore)) / totalWeight;

                compositeUtility = Math.Clamp(compositeUtility, 0.0, 1.0);

                // Update bid composite score in auction record
                var index = auction.Bids.FindIndex(b => b.BidId == bid.BidId);
                if (index >= 0)
                {
                    auction.Bids[index] = bid with { CompositeUtilityScore = compositeUtility };
                }

                scoredBids.Add(new ScoredSupplierBid(
                    Bid: bid with { CompositeUtilityScore = compositeUtility },
                    UtilityScore: compositeUtility,
                    CostScore: costScore,
                    SlaScore: slaScore,
                    LeadTimeScore: leadScore,
                    IsEligible: true,
                    DisqualificationReason: null));
            }
        }

        // Rank eligible bids descending by utility score, followed by disqualified bids
        return [.. scoredBids.OrderByDescending(b => b.IsEligible).ThenByDescending(b => b.UtilityScore)];
    }

    /// <summary>
    /// Calculates utility score for a single bid given known price/lead bounds and weights.
    /// Utility = w_cost * (1 - (P - P_min)/(P_max - P_min)) + w_sla * SLA + w_lead * (1 - L / L_max)
    /// </summary>
    public double CalculateUtilityScore(
        SupplierBid bid,
        decimal minPrice,
        decimal maxPrice,
        int maxLeadTime,
        BiddingMatrixWeights? weights = null)
    {
        ArgumentNullException.ThrowIfNull(bid);
        weights ??= new BiddingMatrixWeights();

        var costScore = ComputeCostScore(bid.UnitPrice, minPrice, maxPrice);
        var slaScore = Math.Clamp(bid.SlaReliabilityScore, 0.0, 1.0);
        var leadScore = ComputeLeadScore(bid.LeadTimeDays, maxLeadTime);

        var totalWeight = weights.TotalWeight > 0 ? weights.TotalWeight : 1.0;
        var utility = ((weights.CostWeight * costScore) +
                       (weights.SlaWeight * slaScore) +
                       (weights.LeadTimeWeight * leadScore)) / totalWeight;

        return Math.Clamp(utility, 0.0, 1.0);
    }

    /// <summary>
    /// Updates a bid in thread-safe storage.
    /// </summary>
    public bool UpdateBid(Guid auctionId, SupplierBid updatedBid)
    {
        ArgumentNullException.ThrowIfNull(updatedBid);

        lock (_lock)
        {
            if (!_auctions.TryGetValue(auctionId, out var auction))
            {
                return false;
            }

            var index = auction.Bids.FindIndex(b => b.BidId == updatedBid.BidId);
            if (index < 0)
            {
                return false;
            }

            auction.Bids[index] = updatedBid;
            return true;
        }
    }

    /// <summary>
    /// Updates an auction's status.
    /// </summary>
    public bool UpdateAuctionStatus(Guid auctionId, AuctionStatus status)
    {
        lock (_lock)
        {
            if (!_auctions.TryGetValue(auctionId, out var auction))
            {
                return false;
            }

            _auctions[auctionId] = auction with { Status = status };
            return true;
        }
    }

    // -----------------------------------------------------------------------
    // Helper Methods
    // -----------------------------------------------------------------------

    private static (bool IsEligible, string? DisqualificationReason) ValidateBidSpecification(
        SupplierBid bid,
        BiddingAuction auction)
    {
        if (bid.UnitPrice > auction.ReserveUnitPrice)
        {
            return (false, $"Unit price {bid.UnitPrice:F2} exceeds auction reserve price {auction.ReserveUnitPrice:F2}.");
        }

        var maxAllowedLeadDays = (auction.DeliveryDeadlineUtc - auction.CreatedAtUtc).TotalDays;
        if (bid.LeadTimeDays > maxAllowedLeadDays)
        {
            return (false, $"Bid lead time ({bid.LeadTimeDays} days) exceeds maximum allowable window ({maxAllowedLeadDays:F1} days).");
        }

        if (bid.SubmittedAtUtc.AddDays(bid.LeadTimeDays) > auction.DeliveryDeadlineUtc)
        {
            return (false, "Estimated delivery date exceeds auction delivery deadline.");
        }

        if (bid.AvailableQuantity <= 0)
        {
            return (false, "Available supplier capacity must be greater than zero.");
        }

        return (true, null);
    }

    private static double ComputeCostScore(decimal price, decimal minPrice, decimal maxPrice)
    {
        if (maxPrice <= minPrice)
        {
            return 1.0;
        }

        if (price <= minPrice)
        {
            return 1.0;
        }

        if (price >= maxPrice)
        {
            return 0.0;
        }

        var normalized = 1.0 - (double)((price - minPrice) / (maxPrice - minPrice));
        return Math.Clamp(normalized, 0.0, 1.0);
    }

    private static double ComputeLeadScore(int leadDays, int maxLeadTime)
    {
        if (maxLeadTime <= 0)
        {
            return 1.0;
        }

        if (leadDays <= 0)
        {
            return 1.0;
        }

        if (leadDays >= maxLeadTime)
        {
            return 0.0;
        }

        var normalized = 1.0 - ((double)leadDays / maxLeadTime);
        return Math.Clamp(normalized, 0.0, 1.0);
    }
}
