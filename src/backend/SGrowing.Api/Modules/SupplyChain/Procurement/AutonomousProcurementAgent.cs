namespace SGrowing.Api.Modules.SupplyChain.Procurement;

/// <summary>
/// Interface for autonomous AI procurement negotiation, dynamic concession curve calculation,
/// and dual-sourcing split evaluation.
/// </summary>
public interface IAutonomousProcurementAgent
{
    decimal CalculateDynamicTargetPrice(decimal targetPrice, decimal reservePrice, int round, int maxRounds, double beta);
    NegotiationStepResponse ExecuteNegotiationStep(Guid auctionId, NegotiationStepRequest request);
    DualSourcingRecommendation EvaluateDualSourcing(Guid auctionId, DualSourcingRequest? request = null);
}

/// <summary>
/// Implements autonomous bargaining using a parameterized concession curve:
/// P(t) = P_target + (P_reserve - P_target) * (t / T)^(1 / beta)
/// and dual-sourcing risk hedging.
/// </summary>
public class AutonomousProcurementAgent(ISupplierBiddingMatrixService biddingMatrixService) : IAutonomousProcurementAgent
{
    private readonly ISupplierBiddingMatrixService _biddingMatrixService = biddingMatrixService
        ?? throw new ArgumentNullException(nameof(biddingMatrixService));

    /// <summary>
    /// Computes acceptable unit price at negotiation round t out of T using:
    /// P(t) = P_target + (P_reserve - P_target) * (t / T)^(1 / beta)
    /// </summary>
    /// <param name="targetPrice">Target (best-case) unit price for the buyer.</param>
    /// <param name="reservePrice">Reserve (walk-away) maximum acceptable unit price.</param>
    /// <param name="round">Current negotiation round (0 to maxRounds).</param>
    /// <param name="maxRounds">Total maximum negotiation rounds (T).</param>
    /// <param name="beta">Negotiation posture (&gt; 1 aggressive/resistant, &lt; 1 flexible/urgent, = 1 linear).</param>
    public decimal CalculateDynamicTargetPrice(
        decimal targetPrice,
        decimal reservePrice,
        int round,
        int maxRounds,
        double beta)
    {
        if (targetPrice <= 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(targetPrice), "Target price must be positive.");
        }

        if (reservePrice < targetPrice)
        {
            throw new ArgumentException("Reserve price must be greater than or equal to target price.", nameof(reservePrice));
        }

        if (maxRounds <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxRounds), "Max rounds must be positive.");
        }

        if (beta <= 0.0)
        {
            throw new ArgumentOutOfRangeException(nameof(beta), "Posture beta must be strictly positive.");
        }

        if (round <= 0)
        {
            return Math.Round(targetPrice, 4, MidpointRounding.AwayFromZero);
        }

        if (round >= maxRounds)
        {
            return Math.Round(reservePrice, 4, MidpointRounding.AwayFromZero);
        }

        var progressRatio = Math.Clamp((double)round / maxRounds, 0.0, 1.0);
        var concessionFactor = Math.Pow(progressRatio, 1.0 / beta);

        var priceSpread = reservePrice - targetPrice;
        var dynamicPrice = targetPrice + (priceSpread * (decimal)concessionFactor);

        return Math.Round(dynamicPrice, 4, MidpointRounding.AwayFromZero);
    }

    /// <summary>
    /// Executes an autonomous negotiation round against a supplier's bid.
    /// If the supplier's price is within P(t), the bid is accepted.
    /// Otherwise, a counter-offer is returned at P(t) with recorded concession delta.
    /// </summary>
    public NegotiationStepResponse ExecuteNegotiationStep(Guid auctionId, NegotiationStepRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var auction = _biddingMatrixService.GetAuction(auctionId)
            ?? throw new KeyNotFoundException($"Auction '{auctionId}' was not found.");

        var bid = auction.Bids.FirstOrDefault(b => b.BidId == request.BidId)
            ?? throw new KeyNotFoundException($"Bid '{request.BidId}' was not found in auction '{auctionId}'.");

        var maxRounds = request.MaxRounds > 0 ? request.MaxRounds : 5;
        var currentRound = Math.Clamp(request.CurrentRound, 1, maxRounds);
        var beta = request.PostureBeta > 0.0 ? request.PostureBeta : 1.0;

        var acceptablePriceAtRound = CalculateDynamicTargetPrice(
            auction.TargetUnitPrice,
            auction.ReserveUnitPrice,
            currentRound,
            maxRounds,
            beta);

        // Check if supplier's current bid price is acceptable to the AI
        if (bid.UnitPrice <= acceptablePriceAtRound)
        {
            var acceptedConcession = new BidConcession(
                RoundNumber: currentRound,
                OfferedUnitPrice: bid.UnitPrice,
                ConcessionDelta: 0m,
                TimestampUtc: DateTime.UtcNow,
                Note: $"Accepted by autonomous agent at round {currentRound} (Supplier unit price {bid.UnitPrice:F2} <= target {acceptablePriceAtRound:F2}).");

            var acceptedBid = bid with
            {
                Status = BidStatus.Accepted,
                Concessions = [.. bid.Concessions, acceptedConcession]
            };

            _biddingMatrixService.UpdateBid(auctionId, acceptedBid);

            return new NegotiationStepResponse(
                Success: true,
                Message: $"Bid '{bid.BidId}' accepted at round {currentRound}.",
                BidId: bid.BidId,
                Round: currentRound,
                TargetPriceAtRound: acceptablePriceAtRound,
                CounterOfferPrice: bid.UnitPrice,
                BidAccepted: true,
                UpdatedBid: acceptedBid);
        }

        // Price is too high: generate AI counter-offer at P(t)
        var concessionDelta = bid.UnitPrice - acceptablePriceAtRound;
        var counterConcession = new BidConcession(
            RoundNumber: currentRound,
            OfferedUnitPrice: acceptablePriceAtRound,
            ConcessionDelta: concessionDelta,
            TimestampUtc: DateTime.UtcNow,
            Note: request.NegotiationNote ?? $"AI counter-offer at round {currentRound} (Beta={beta:F2}).");

        var counterOfferedBid = bid with
        {
            Status = BidStatus.CounterOffered,
            Concessions = [.. bid.Concessions, counterConcession]
        };

        _biddingMatrixService.UpdateBid(auctionId, counterOfferedBid);
        _biddingMatrixService.UpdateAuctionStatus(auctionId, AuctionStatus.UnderNegotiation);

        return new NegotiationStepResponse(
            Success: true,
            Message: $"Counter-offer generated for bid '{bid.BidId}' at round {currentRound}.",
            BidId: bid.BidId,
            Round: currentRound,
            TargetPriceAtRound: acceptablePriceAtRound,
            CounterOfferPrice: acceptablePriceAtRound,
            BidAccepted: false,
            UpdatedBid: counterOfferedBid);
    }

    /// <summary>
    /// Evaluates multi-supplier dual-sourcing splits (e.g. 60/40) to mitigate supplier capacity
    /// bottlenecks and hedge geopolitical/delivery disruptions.
    /// </summary>
    public DualSourcingRecommendation EvaluateDualSourcing(Guid auctionId, DualSourcingRequest? request = null)
    {
        request ??= new DualSourcingRequest();

        var auction = _biddingMatrixService.GetAuction(auctionId)
            ?? throw new KeyNotFoundException($"Auction '{auctionId}' was not found.");

        var rankedBids = _biddingMatrixService.EvaluateAndRankBids(auctionId);
        var eligibleBids = rankedBids.Where(b => b.IsEligible).ToList();

        if (eligibleBids.Count < 2)
        {
            // If only 1 eligible supplier is available, cannot dual source
            if (eligibleBids.Count == 1)
            {
                var single = eligibleBids[0].Bid;
                var singleQty = Math.Min(single.AvailableQuantity, auction.RequiredQuantity);
                return new DualSourcingRecommendation(
                    PrimarySupplierId: single.SupplierId,
                    SecondarySupplierId: string.Empty,
                    PrimarySplitRatio: 1.0,
                    SecondarySplitRatio: 0.0,
                    HedgingRationale: "Single-source fallback: Insufficient eligible suppliers available for dual-sourcing.",
                    PrimaryQuantity: singleQty,
                    SecondaryQuantity: 0,
                    PrimaryUnitPrice: single.UnitPrice,
                    SecondaryUnitPrice: 0m,
                    BlendedUnitPrice: single.UnitPrice,
                    TotalCost: singleQty * single.UnitPrice);
            }

            throw new InvalidOperationException("No eligible suppliers found to evaluate dual-sourcing.");
        }

        var primary = eligibleBids[0].Bid;
        var secondary = eligibleBids[1].Bid;
        var requiredQty = auction.RequiredQuantity;

        int primaryQty;
        int secondaryQty;
        string rationale;

        // Condition A: Capacity Constraint
        if (primary.AvailableQuantity < requiredQty)
        {
            primaryQty = Math.Min(primary.AvailableQuantity, requiredQty);
            var deficit = requiredQty - primaryQty;
            secondaryQty = Math.Min(secondary.AvailableQuantity, deficit);

            rationale = $"Capacity Constraint: Primary supplier {primary.SupplierName} capacity ({primary.AvailableQuantity}) " +
                        $"is below required quantity ({requiredQty}). Secondary supplier {secondary.SupplierName} satisfies remaining {secondaryQty} units.";
        }
        // Condition B: Strategic Risk / Geopolitical Hedging
        else
        {
            var targetPrimaryRatio = Math.Clamp(request.TargetPrimaryRatio, 0.10, 0.90);
            var targetSecondaryRatio = Math.Clamp(request.TargetSecondaryRatio, 0.10, 0.90);
            var ratioSum = targetPrimaryRatio + targetSecondaryRatio;
            targetPrimaryRatio /= ratioSum;
            targetSecondaryRatio /= ratioSum;

            primaryQty = (int)Math.Round(requiredQty * targetPrimaryRatio);
            secondaryQty = requiredQty - primaryQty;

            // Ensure capacities are respected
            if (primaryQty > primary.AvailableQuantity)
            {
                primaryQty = primary.AvailableQuantity;
                secondaryQty = requiredQty - primaryQty;
            }
            if (secondaryQty > secondary.AvailableQuantity)
            {
                secondaryQty = secondary.AvailableQuantity;
                primaryQty = requiredQty - secondaryQty;
            }

            var hedgeReason = request.RequireGeopoliticalHedging
                ? "Geopolitical risk mitigation"
                : "Operational redundancy & SLA performance hedging";

            rationale = $"Strategic Hedging ({hedgeReason}): Split {primaryQty} units to primary ({primary.SupplierName}, SLA: {primary.SlaReliabilityScore:P0}) " +
                        $"and {secondaryQty} units to secondary ({secondary.SupplierName}, SLA: {secondary.SlaReliabilityScore:P0}).";
        }

        var totalAllocated = primaryQty + secondaryQty;
        var primaryRatio = totalAllocated > 0 ? Math.Round((double)primaryQty / totalAllocated, 4) : 0.0;
        var secondaryRatio = totalAllocated > 0 ? Math.Round((double)secondaryQty / totalAllocated, 4) : 0.0;

        var totalCost = (primaryQty * primary.UnitPrice) + (secondaryQty * secondary.UnitPrice);
        var blendedPrice = totalAllocated > 0 ? Math.Round(totalCost / totalAllocated, 4) : 0m;

        return new DualSourcingRecommendation(
            PrimarySupplierId: primary.SupplierId,
            SecondarySupplierId: secondary.SupplierId,
            PrimarySplitRatio: primaryRatio,
            SecondarySplitRatio: secondaryRatio,
            HedgingRationale: rationale,
            PrimaryQuantity: primaryQty,
            SecondaryQuantity: secondaryQty,
            PrimaryUnitPrice: primary.UnitPrice,
            SecondaryUnitPrice: secondary.UnitPrice,
            BlendedUnitPrice: blendedPrice,
            TotalCost: totalCost);
    }
}
