using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using SGrowing.Api.Modules.SupplyChain.Procurement;
using Xunit;

namespace SGrowing.Tests.SupplyChain;

/// <summary>
/// Comprehensive test suite for Phase 26: Autonomous AI Procurement Negotiation
/// &amp; Supplier Bidding Matrix Engine.
/// </summary>
public class ProcurementBiddingTests
{
    private readonly SupplierBiddingMatrixService _matrixService = new();
    private readonly AutonomousProcurementAgent _procurementAgent;
    private readonly ProcurementAwardSealingService _sealingService;

    public ProcurementBiddingTests()
    {
        _procurementAgent = new AutonomousProcurementAgent(_matrixService);
        _sealingService = new ProcurementAwardSealingService(_matrixService);
    }

    #region 1. Multi-Criteria Supplier Utility Scoring Math & Weights Tests

    [Fact]
    public void CalculateUtilityScore_WithDefaultWeights_ComputesAccurateUtility()
    {
        // Default weights: w_cost = 0.50, w_sla = 0.30, w_lead = 0.20
        var bid = new SupplierBid(
            BidId: Guid.NewGuid(),
            AuctionId: Guid.NewGuid(),
            SupplierId: "SUP-A",
            SupplierName: "Alpha Supplies",
            UnitPrice: 150m,
            LeadTimeDays: 10,
            SlaReliabilityScore: 0.90,
            AvailableQuantity: 1000,
            Concessions: [],
            Status: BidStatus.Submitted,
            SubmittedAtUtc: DateTime.UtcNow);

        // MinPrice = 100, MaxPrice = 200 => CostScore = 1 - (150 - 100)/(200 - 100) = 0.50
        // SlaScore = 0.90
        // MaxLeadTime = 20, LeadTime = 10 => LeadScore = 1 - (10 / 20) = 0.50
        // Utility = (0.50 * 0.50) + (0.30 * 0.90) + (0.20 * 0.50) = 0.25 + 0.27 + 0.10 = 0.62
        var utility = _matrixService.CalculateUtilityScore(bid, minPrice: 100m, maxPrice: 200m, maxLeadTime: 20);

        utility.Should().BeApproximately(0.62, 0.001);
    }

    [Fact]
    public void CalculateUtilityScore_WithBestPossibleAttributes_ReturnsOne()
    {
        var perfectBid = new SupplierBid(
            BidId: Guid.NewGuid(),
            AuctionId: Guid.NewGuid(),
            SupplierId: "SUP-PERFECT",
            SupplierName: "Perfect Supplies",
            UnitPrice: 100m, // Lowest price
            LeadTimeDays: 0,   // Instant lead time
            SlaReliabilityScore: 1.0, // Perfect SLA
            AvailableQuantity: 500,
            Concessions: [],
            Status: BidStatus.Submitted,
            SubmittedAtUtc: DateTime.UtcNow);

        var utility = _matrixService.CalculateUtilityScore(perfectBid, minPrice: 100m, maxPrice: 200m, maxLeadTime: 20);

        utility.Should().Be(1.0);
    }

    [Fact]
    public void CalculateUtilityScore_WithWorstPossibleAttributes_ReturnsZero()
    {
        var worstBid = new SupplierBid(
            BidId: Guid.NewGuid(),
            AuctionId: Guid.NewGuid(),
            SupplierId: "SUP-WORST",
            SupplierName: "Worst Supplies",
            UnitPrice: 200m, // Highest price
            LeadTimeDays: 20, // Longest lead time
            SlaReliabilityScore: 0.0, // 0 SLA
            AvailableQuantity: 500,
            Concessions: [],
            Status: BidStatus.Submitted,
            SubmittedAtUtc: DateTime.UtcNow);

        var utility = _matrixService.CalculateUtilityScore(worstBid, minPrice: 100m, maxPrice: 200m, maxLeadTime: 20);

        utility.Should().Be(0.0);
    }

    [Fact]
    public void CalculateUtilityScore_WhenMinPriceEqualsMaxPrice_AssignsFullCostScore()
    {
        var bid = new SupplierBid(
            BidId: Guid.NewGuid(),
            AuctionId: Guid.NewGuid(),
            SupplierId: "SUP-EQUAL",
            SupplierName: "Equal Price Supplies",
            UnitPrice: 150m,
            LeadTimeDays: 5,
            SlaReliabilityScore: 0.80,
            AvailableQuantity: 300,
            Concessions: [],
            Status: BidStatus.Submitted,
            SubmittedAtUtc: DateTime.UtcNow);

        // When minPrice == maxPrice, cost component should be 1.0 (avoids divide-by-zero)
        var utility = _matrixService.CalculateUtilityScore(bid, minPrice: 150m, maxPrice: 150m, maxLeadTime: 10);

        // Cost = 1.0 * 0.50 = 0.50
        // SLA = 0.80 * 0.30 = 0.24
        // Lead = (1 - 5/10) * 0.20 = 0.50 * 0.20 = 0.10
        // Total = 0.84
        utility.Should().BeApproximately(0.84, 0.001);
    }

    [Fact]
    public void CalculateUtilityScore_WhenMaxLeadTimeIsZero_AssignsFullLeadScore()
    {
        var bid = new SupplierBid(
            BidId: Guid.NewGuid(),
            AuctionId: Guid.NewGuid(),
            SupplierId: "SUP-ZERO-LEAD",
            SupplierName: "Instant Supplies",
            UnitPrice: 100m,
            LeadTimeDays: 0,
            SlaReliabilityScore: 0.80,
            AvailableQuantity: 100,
            Concessions: [],
            Status: BidStatus.Submitted,
            SubmittedAtUtc: DateTime.UtcNow);

        var utility = _matrixService.CalculateUtilityScore(bid, minPrice: 100m, maxPrice: 200m, maxLeadTime: 0);

        // Cost = 1.0 * 0.50 = 0.50
        // SLA = 0.80 * 0.30 = 0.24
        // Lead = 1.0 * 0.20 = 0.20
        // Total = 0.94
        utility.Should().BeApproximately(0.94, 0.001);
    }

    [Fact]
    public void CalculateUtilityScore_WithCustomWeights_AppliesCustomWeightsAccurately()
    {
        var bid = new SupplierBid(
            BidId: Guid.NewGuid(),
            AuctionId: Guid.NewGuid(),
            SupplierId: "SUP-CUSTOM",
            SupplierName: "Custom Supplies",
            UnitPrice: 100m, // CostScore = 1.0
            LeadTimeDays: 10, // LeadScore = 0.0
            SlaReliabilityScore: 0.90, // SlaScore = 0.90
            AvailableQuantity: 100,
            Concessions: [],
            Status: BidStatus.Submitted,
            SubmittedAtUtc: DateTime.UtcNow);

        // Emphasize SLA: w_cost = 0.20, w_sla = 0.70, w_lead = 0.10
        var weights = new BiddingMatrixWeights(CostWeight: 0.20, SlaWeight: 0.70, LeadTimeWeight: 0.10);
        var utility = _matrixService.CalculateUtilityScore(bid, minPrice: 100m, maxPrice: 200m, maxLeadTime: 10, weights);

        // (0.20 * 1.0) + (0.70 * 0.90) + (0.10 * 0.0) = 0.20 + 0.63 + 0 = 0.83
        utility.Should().BeApproximately(0.83, 0.001);
    }

    [Fact]
    public void EvaluateAndRankBids_OrdersBidsDescendingByUtility()
    {
        var deadline = DateTime.UtcNow.AddDays(30);
        var auction = _matrixService.CreateAuction(new CreateAuctionRequest(
            Sku: "SKU-RANK-TEST",
            RequiredQuantity: 500,
            TargetUnitPrice: 100m,
            ReserveUnitPrice: 200m,
            DeliveryDeadlineUtc: deadline));

        // Submit Bid 1: High price, average SLA, long lead
        _matrixService.SubmitBid(auction.AuctionId, new SubmitBidRequest("SUP-1", "Supplier 1", 190m, 20, 0.70, 500));
        // Submit Bid 2: Low price, great SLA, short lead -> should be #1
        _matrixService.SubmitBid(auction.AuctionId, new SubmitBidRequest("SUP-2", "Supplier 2", 110m, 5, 0.95, 500));
        // Submit Bid 3: Medium price, good SLA, medium lead -> should be #2
        _matrixService.SubmitBid(auction.AuctionId, new SubmitBidRequest("SUP-3", "Supplier 3", 140m, 10, 0.85, 500));

        var ranked = _matrixService.EvaluateAndRankBids(auction.AuctionId);

        ranked.Should().HaveCount(3);
        ranked[0].Bid.SupplierId.Should().Be("SUP-2"); // Top utility
        ranked[1].Bid.SupplierId.Should().Be("SUP-3");
        ranked[2].Bid.SupplierId.Should().Be("SUP-1");
        ranked[0].UtilityScore.Should().BeGreaterThan(ranked[1].UtilityScore);
        ranked[1].UtilityScore.Should().BeGreaterThan(ranked[2].UtilityScore);
    }

    #endregion

    #region 2. Out-of-Spec Bid Rejection & Specification Constraints Tests

    [Fact]
    public void EvaluateAndRankBids_DisqualifiesBid_WhenUnitPriceExceedsReserve()
    {
        var auction = _matrixService.CreateAuction(new CreateAuctionRequest(
            Sku: "SKU-SPEC-1",
            RequiredQuantity: 100,
            TargetUnitPrice: 50m,
            ReserveUnitPrice: 75m,
            DeliveryDeadlineUtc: DateTime.UtcNow.AddDays(14)));

        // Submit out-of-spec price ($80 > $75 reserve)
        _matrixService.SubmitBid(auction.AuctionId, new SubmitBidRequest("SUP-EXPENSIVE", "Expensive Co", 80m, 5, 0.99, 100));

        var ranked = _matrixService.EvaluateAndRankBids(auction.AuctionId);

        ranked.Should().HaveCount(1);
        ranked[0].IsEligible.Should().BeFalse();
        ranked[0].DisqualificationReason.Should().Contain("exceeds auction reserve price");
    }

    [Fact]
    public void EvaluateAndRankBids_DisqualifiesBid_WhenLeadTimeExceedsDeliveryDeadline()
    {
        var deadline = DateTime.UtcNow.AddDays(7);
        var auction = _matrixService.CreateAuction(new CreateAuctionRequest(
            Sku: "SKU-SPEC-2",
            RequiredQuantity: 200,
            TargetUnitPrice: 80m,
            ReserveUnitPrice: 100m,
            DeliveryDeadlineUtc: deadline));

        // Submit out-of-spec lead time (14 days > 7 days window)
        _matrixService.SubmitBid(auction.AuctionId, new SubmitBidRequest("SUP-LATE", "Slow Logistics", 85m, 14, 0.90, 200));

        var ranked = _matrixService.EvaluateAndRankBids(auction.AuctionId);

        ranked.Should().HaveCount(1);
        ranked[0].IsEligible.Should().BeFalse();
        ranked[0].DisqualificationReason.Should().Contain("exceeds");
    }

    [Fact]
    public void EvaluateAndRankBids_DisqualifiesBid_WhenAvailableQuantityIsZeroOrNegative()
    {
        var auction = _matrixService.CreateAuction(new CreateAuctionRequest(
            Sku: "SKU-SPEC-3",
            RequiredQuantity: 100,
            TargetUnitPrice: 80m,
            ReserveUnitPrice: 100m,
            DeliveryDeadlineUtc: DateTime.UtcNow.AddDays(30)));

        _matrixService.SubmitBid(auction.AuctionId, new SubmitBidRequest("SUP-NO-STOCK", "Zero Stock Inc", 85m, 5, 0.90, 0));

        var ranked = _matrixService.EvaluateAndRankBids(auction.AuctionId);

        ranked.Should().HaveCount(1);
        ranked[0].IsEligible.Should().BeFalse();
        ranked[0].DisqualificationReason.Should().Contain("Available supplier capacity must be greater than zero");
    }

    [Fact]
    public void EvaluateAndRankBids_RanksEligibleBidsAheadOfDisqualifiedBids()
    {
        var auction = _matrixService.CreateAuction(new CreateAuctionRequest(
            Sku: "SKU-SPEC-4",
            RequiredQuantity: 100,
            TargetUnitPrice: 50m,
            ReserveUnitPrice: 100m,
            DeliveryDeadlineUtc: DateTime.UtcNow.AddDays(20)));

        // Eligible bid
        _matrixService.SubmitBid(auction.AuctionId, new SubmitBidRequest("SUP-VALID", "Valid Supplier", 60m, 5, 0.85, 100));
        // Out-of-spec bid (price $120 > $100 reserve)
        _matrixService.SubmitBid(auction.AuctionId, new SubmitBidRequest("SUP-OVER", "Overpriced Supplier", 120m, 2, 0.99, 100));

        var ranked = _matrixService.EvaluateAndRankBids(auction.AuctionId);

        ranked.Should().HaveCount(2);
        ranked[0].IsEligible.Should().BeTrue();
        ranked[0].Bid.SupplierId.Should().Be("SUP-VALID");
        ranked[1].IsEligible.Should().BeFalse();
        ranked[1].Bid.SupplierId.Should().Be("SUP-OVER");
    }

    [Fact]
    public void CreateAuction_WithInvalidParameters_ThrowsArgumentException()
    {
        // 1. Empty SKU
        var actEmptySku = () => _matrixService.CreateAuction(new CreateAuctionRequest(
            Sku: "",
            RequiredQuantity: 10,
            TargetUnitPrice: 50m,
            ReserveUnitPrice: 60m,
            DeliveryDeadlineUtc: DateTime.UtcNow.AddDays(7)));
        actEmptySku.Should().Throw<ArgumentException>().WithMessage("*SKU*");

        // 2. Non-positive quantity
        var actZeroQty = () => _matrixService.CreateAuction(new CreateAuctionRequest(
            Sku: "SKU-TEST",
            RequiredQuantity: 0,
            TargetUnitPrice: 50m,
            ReserveUnitPrice: 60m,
            DeliveryDeadlineUtc: DateTime.UtcNow.AddDays(7)));
        actZeroQty.Should().Throw<ArgumentException>().WithMessage("*quantity*");

        // 3. Reserve price less than target price
        var actInvalidReserve = () => _matrixService.CreateAuction(new CreateAuctionRequest(
            Sku: "SKU-TEST",
            RequiredQuantity: 10,
            TargetUnitPrice: 100m,
            ReserveUnitPrice: 80m, // Less than target!
            DeliveryDeadlineUtc: DateTime.UtcNow.AddDays(7)));
        actInvalidReserve.Should().Throw<ArgumentException>().WithMessage("*Reserve*");
    }

    [Fact]
    public void SubmitBid_ToNonExistentAuction_ThrowsKeyNotFoundException()
    {
        var act = () => _matrixService.SubmitBid(Guid.NewGuid(), new SubmitBidRequest("SUP-X", "Supplier X", 10m, 2, 0.9, 100));
        act.Should().Throw<KeyNotFoundException>();
    }

    [Fact]
    public void SubmitBid_WithInvalidPriceOrSla_ThrowsExpectedException()
    {
        var auction = _matrixService.CreateAuction(new CreateAuctionRequest(
            Sku: "SKU-VAL",
            RequiredQuantity: 50,
            TargetUnitPrice: 20m,
            ReserveUnitPrice: 40m,
            DeliveryDeadlineUtc: DateTime.UtcNow.AddDays(10)));

        // Non-positive price
        var actZeroPrice = () => _matrixService.SubmitBid(auction.AuctionId, new SubmitBidRequest("SUP-1", "Name", 0m, 2, 0.9, 50));
        actZeroPrice.Should().Throw<ArgumentException>();

        // SLA > 1.0
        var actInvalidSla = () => _matrixService.SubmitBid(auction.AuctionId, new SubmitBidRequest("SUP-1", "Name", 25m, 2, 1.5, 50));
        actInvalidSla.Should().Throw<ArgumentOutOfRangeException>();
    }

    #endregion

    #region 3. Dynamic Concession Curve & Autonomous Bargaining Tests

    [Fact]
    public void CalculateDynamicTargetPrice_AtRoundZero_ReturnsTargetPriceExactly()
    {
        var price = _procurementAgent.CalculateDynamicTargetPrice(
            targetPrice: 100m,
            reservePrice: 200m,
            round: 0,
            maxRounds: 5,
            beta: 1.5);

        price.Should().Be(100m);
    }

    [Fact]
    public void CalculateDynamicTargetPrice_AtFinalRound_ReturnsReservePriceExactly()
    {
        var price = _procurementAgent.CalculateDynamicTargetPrice(
            targetPrice: 100m,
            reservePrice: 200m,
            round: 5,
            maxRounds: 5,
            beta: 1.5);

        price.Should().Be(200m);
    }

    [Fact]
    public void CalculateDynamicTargetPrice_WithLinearBeta_YieldsExactLinearProgression()
    {
        // With beta = 1.0: P(t) = P_target + (P_reserve - P_target) * (t / T)^1
        // For target = 100, reserve = 200, T = 4, t = 2: P(2) = 100 + 100 * (2/4) = 150
        var price = _procurementAgent.CalculateDynamicTargetPrice(
            targetPrice: 100m,
            reservePrice: 200m,
            round: 2,
            maxRounds: 4,
            beta: 1.0);

        price.Should().Be(150m);
    }

    [Fact]
    public void CalculateDynamicTargetPrice_WithAggressiveBeta_YieldsSublinearProgression()
    {
        // Beta > 1 (e.g. beta = 2.0, exponent = 0.5): (0.25)^0.5 = 0.50
        // Price at t=1, T=4: 100 + 100 * (1/4)^0.5 = 100 + 100 * 0.5 = 150
        var price = _procurementAgent.CalculateDynamicTargetPrice(
            targetPrice: 100m,
            reservePrice: 200m,
            round: 1,
            maxRounds: 4,
            beta: 2.0);

        price.Should().Be(150m);
    }

    [Fact]
    public void CalculateDynamicTargetPrice_WithFlexibleBeta_YieldsSuperlinearProgression()
    {
        // Beta < 1 (e.g. beta = 0.5, exponent = 2.0): (0.50)^2 = 0.25
        // Price at t=2, T=4: 100 + 100 * (2/4)^2 = 100 + 100 * 0.25 = 125
        var price = _procurementAgent.CalculateDynamicTargetPrice(
            targetPrice: 100m,
            reservePrice: 200m,
            round: 2,
            maxRounds: 4,
            beta: 0.5);

        price.Should().Be(125m);
    }

    [Fact]
    public void CalculateDynamicTargetPrice_WithInvalidParameters_ThrowsException()
    {
        var actZeroTarget = () => _procurementAgent.CalculateDynamicTargetPrice(0m, 100m, 1, 5, 1.0);
        actZeroTarget.Should().Throw<ArgumentOutOfRangeException>();

        var actReserveLessThanTarget = () => _procurementAgent.CalculateDynamicTargetPrice(100m, 50m, 1, 5, 1.0);
        actReserveLessThanTarget.Should().Throw<ArgumentException>();

        var actZeroRounds = () => _procurementAgent.CalculateDynamicTargetPrice(50m, 100m, 1, 0, 1.0);
        actZeroRounds.Should().Throw<ArgumentOutOfRangeException>();

        var actZeroBeta = () => _procurementAgent.CalculateDynamicTargetPrice(50m, 100m, 1, 5, 0.0);
        actZeroBeta.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void ExecuteNegotiationStep_WhenBidPriceIsBelowDynamicTarget_AcceptsBid()
    {
        var auction = _matrixService.CreateAuction(new CreateAuctionRequest(
            Sku: "SKU-NEG-1",
            RequiredQuantity: 100,
            TargetUnitPrice: 100m,
            ReserveUnitPrice: 200m,
            DeliveryDeadlineUtc: DateTime.UtcNow.AddDays(14)));

        // Bid unit price is $110. In round 2 of 4 (linear beta=1.0), acceptable target is $150.
        // Since $110 <= $150, the AI should accept!
        var bid = _matrixService.SubmitBid(auction.AuctionId, new SubmitBidRequest("SUP-CHEAP", "Cheap Supplier", 110m, 5, 0.90, 100));

        var response = _procurementAgent.ExecuteNegotiationStep(auction.AuctionId, new NegotiationStepRequest(
            BidId: bid.BidId,
            CurrentRound: 2,
            MaxRounds: 4,
            PostureBeta: 1.0));

        response.Success.Should().BeTrue();
        response.BidAccepted.Should().BeTrue();
        response.CounterOfferPrice.Should().Be(110m);
        response.UpdatedBid.Should().NotBeNull();
        response.UpdatedBid!.Status.Should().Be(BidStatus.Accepted);
        response.UpdatedBid.Concessions.Should().ContainSingle();
    }

    [Fact]
    public void ExecuteNegotiationStep_WhenBidPriceExceedsDynamicTarget_CounterOffersAtDynamicTarget()
    {
        var auction = _matrixService.CreateAuction(new CreateAuctionRequest(
            Sku: "SKU-NEG-2",
            RequiredQuantity: 100,
            TargetUnitPrice: 100m,
            ReserveUnitPrice: 200m,
            DeliveryDeadlineUtc: DateTime.UtcNow.AddDays(14)));

        // Bid unit price is $180. In round 1 of 4 (linear beta=1.0), dynamic target is $125.
        // Since $180 > $125, the AI should counter-offer at $125.
        var bid = _matrixService.SubmitBid(auction.AuctionId, new SubmitBidRequest("SUP-PRICEY", "Pricey Supplier", 180m, 5, 0.85, 100));

        var response = _procurementAgent.ExecuteNegotiationStep(auction.AuctionId, new NegotiationStepRequest(
            BidId: bid.BidId,
            CurrentRound: 1,
            MaxRounds: 4,
            PostureBeta: 1.0));

        response.Success.Should().BeTrue();
        response.BidAccepted.Should().BeFalse();
        response.CounterOfferPrice.Should().Be(125m);
        response.UpdatedBid.Should().NotBeNull();
        response.UpdatedBid!.Status.Should().Be(BidStatus.CounterOffered);

        var concession = response.UpdatedBid.Concessions.Should().ContainSingle().Subject;
        concession.RoundNumber.Should().Be(1);
        concession.OfferedUnitPrice.Should().Be(125m);
        concession.ConcessionDelta.Should().Be(55m); // 180 - 125 = 55
    }

    [Fact]
    public void ExecuteNegotiationStep_TracksConcessionsAcrossMultipleRounds()
    {
        var auction = _matrixService.CreateAuction(new CreateAuctionRequest(
            Sku: "SKU-MULTI-ROUND",
            RequiredQuantity: 100,
            TargetUnitPrice: 100m,
            ReserveUnitPrice: 200m,
            DeliveryDeadlineUtc: DateTime.UtcNow.AddDays(14)));

        var bid = _matrixService.SubmitBid(auction.AuctionId, new SubmitBidRequest("SUP-ROUNDS", "Rounds Supplier", 195m, 3, 0.90, 100));

        // Round 1
        _procurementAgent.ExecuteNegotiationStep(auction.AuctionId, new NegotiationStepRequest(bid.BidId, CurrentRound: 1, MaxRounds: 5, PostureBeta: 1.0));
        // Round 2
        _procurementAgent.ExecuteNegotiationStep(auction.AuctionId, new NegotiationStepRequest(bid.BidId, CurrentRound: 2, MaxRounds: 5, PostureBeta: 1.0));
        // Round 3
        var step3 = _procurementAgent.ExecuteNegotiationStep(auction.AuctionId, new NegotiationStepRequest(bid.BidId, CurrentRound: 3, MaxRounds: 5, PostureBeta: 1.0));

        step3.UpdatedBid!.Concessions.Should().HaveCount(3);
        step3.UpdatedBid.Concessions[0].RoundNumber.Should().Be(1);
        step3.UpdatedBid.Concessions[1].RoundNumber.Should().Be(2);
        step3.UpdatedBid.Concessions[2].RoundNumber.Should().Be(3);
        step3.UpdatedBid.Concessions[2].OfferedUnitPrice.Should().BeGreaterThan(step3.UpdatedBid.Concessions[0].OfferedUnitPrice);
    }

    #endregion

    #region 4. Dual-Sourcing Split Analyzer & Risk Hedging Tests

    [Fact]
    public void EvaluateDualSourcing_WhenPrimaryCapacityIsConstrained_SplitsVolumeToCoverDeficit()
    {
        var auction = _matrixService.CreateAuction(new CreateAuctionRequest(
            Sku: "SKU-CAP-SPLIT",
            RequiredQuantity: 1000,
            TargetUnitPrice: 50m,
            ReserveUnitPrice: 100m,
            DeliveryDeadlineUtc: DateTime.UtcNow.AddDays(30)));

        // Primary supplier has higher utility (lower price, high SLA), but only 600 units capacity
        _matrixService.SubmitBid(auction.AuctionId, new SubmitBidRequest("SUP-CAP-1", "Primary Limited", 60m, 5, 0.95, AvailableQuantity: 600));
        // Secondary supplier has 800 units capacity
        _matrixService.SubmitBid(auction.AuctionId, new SubmitBidRequest("SUP-CAP-2", "Secondary Backfill", 70m, 7, 0.85, AvailableQuantity: 800));

        var recommendation = _procurementAgent.EvaluateDualSourcing(auction.AuctionId);

        recommendation.PrimarySupplierId.Should().Be("SUP-CAP-1");
        recommendation.SecondarySupplierId.Should().Be("SUP-CAP-2");
        recommendation.PrimaryQuantity.Should().Be(600);
        recommendation.SecondaryQuantity.Should().Be(400); // 1000 - 600
        recommendation.PrimarySplitRatio.Should().Be(0.60);
        recommendation.SecondarySplitRatio.Should().Be(0.40);
        recommendation.HedgingRationale.Should().Contain("Capacity Constraint");

        // Total cost: (600 * 60) + (400 * 70) = 36,000 + 28,000 = 64,000
        recommendation.TotalCost.Should().Be(64_000m);
        // Blended price: 64,000 / 1000 = 64.00
        recommendation.BlendedUnitPrice.Should().Be(64m);
    }

    [Fact]
    public void EvaluateDualSourcing_ForStrategicGeopoliticalHedging_AppliesConfiguredSplit()
    {
        var auction = _matrixService.CreateAuction(new CreateAuctionRequest(
            Sku: "SKU-GEO-SPLIT",
            RequiredQuantity: 1000,
            TargetUnitPrice: 50m,
            ReserveUnitPrice: 100m,
            DeliveryDeadlineUtc: DateTime.UtcNow.AddDays(30)));

        // Both suppliers have full capacity (1000 units)
        _matrixService.SubmitBid(auction.AuctionId, new SubmitBidRequest("SUP-DOMESTIC", "Domestic Hub", 60m, 4, 0.95, AvailableQuantity: 1000));
        _matrixService.SubmitBid(auction.AuctionId, new SubmitBidRequest("SUP-OFFSHORE", "Offshore Partner", 55m, 12, 0.80, AvailableQuantity: 1000));

        var request = new DualSourcingRequest(
            RequireGeopoliticalHedging: true,
            TargetPrimaryRatio: 0.60,
            TargetSecondaryRatio: 0.40);

        var recommendation = _procurementAgent.EvaluateDualSourcing(auction.AuctionId, request);

        recommendation.PrimaryQuantity.Should().Be(600);
        recommendation.SecondaryQuantity.Should().Be(400);
        recommendation.PrimarySplitRatio.Should().Be(0.60);
        recommendation.SecondarySplitRatio.Should().Be(0.40);
        recommendation.HedgingRationale.Should().Contain("Geopolitical risk mitigation");
    }

    [Fact]
    public void EvaluateDualSourcing_WhenOnlyOneEligibleSupplierExists_FallsBackToSingleSource()
    {
        var auction = _matrixService.CreateAuction(new CreateAuctionRequest(
            Sku: "SKU-SINGLE-FALLBACK",
            RequiredQuantity: 500,
            TargetUnitPrice: 50m,
            ReserveUnitPrice: 100m,
            DeliveryDeadlineUtc: DateTime.UtcNow.AddDays(20)));

        // Eligible
        _matrixService.SubmitBid(auction.AuctionId, new SubmitBidRequest("SUP-SOLO", "Solo Supplier", 60m, 5, 0.90, 500));
        // Disqualified (exceeds reserve $150 > $100)
        _matrixService.SubmitBid(auction.AuctionId, new SubmitBidRequest("SUP-DISQ", "Disqualified Supplier", 150m, 5, 0.90, 500));

        var recommendation = _procurementAgent.EvaluateDualSourcing(auction.AuctionId);

        recommendation.PrimarySupplierId.Should().Be("SUP-SOLO");
        recommendation.SecondarySupplierId.Should().BeEmpty();
        recommendation.PrimarySplitRatio.Should().Be(1.0);
        recommendation.SecondarySplitRatio.Should().Be(0.0);
        recommendation.HedgingRationale.Should().Contain("Single-source fallback");
    }

    [Fact]
    public void EvaluateDualSourcing_WhenNoEligibleSuppliersExist_ThrowsInvalidOperationException()
    {
        var auction = _matrixService.CreateAuction(new CreateAuctionRequest(
            Sku: "SKU-NO-ELIGIBLE",
            RequiredQuantity: 500,
            TargetUnitPrice: 50m,
            ReserveUnitPrice: 70m,
            DeliveryDeadlineUtc: DateTime.UtcNow.AddDays(20)));

        // Both bids exceed reserve
        _matrixService.SubmitBid(auction.AuctionId, new SubmitBidRequest("SUP-A", "Supplier A", 100m, 5, 0.9, 500));
        _matrixService.SubmitBid(auction.AuctionId, new SubmitBidRequest("SUP-B", "Supplier B", 120m, 5, 0.9, 500));

        var act = () => _procurementAgent.EvaluateDualSourcing(auction.AuctionId);
        act.Should().Throw<InvalidOperationException>().WithMessage("*No eligible suppliers*");
    }

    #endregion

    #region 5. Cryptographic Award Sealing & Budget Cap Authority Tests

    [Fact]
    public void SealAwardContract_WhenValueIsUnderTenThousand_AutoApprovesWithoutHitl()
    {
        var auction = _matrixService.CreateAuction(new CreateAuctionRequest(
            Sku: "SKU-AUTO-AWARD",
            RequiredQuantity: 100,
            TargetUnitPrice: 50m,
            ReserveUnitPrice: 80m,
            DeliveryDeadlineUtc: DateTime.UtcNow.AddDays(14)));

        // 100 units * $50 = $5,000 <= $10,000 cap
        var bid = _matrixService.SubmitBid(auction.AuctionId, new SubmitBidRequest("SUP-CHEAP", "Cheap Co", 50m, 5, 0.92, 100));

        var contract = _sealingService.SealAwardContract(auction.AuctionId, bid.BidId);

        contract.Should().NotBeNull();
        contract.TotalContractValue.Should().Be(5_000m);
        contract.AutoApproved.Should().BeTrue();
        contract.ApprovalMode.Should().Be(AwardApprovalMode.AutonomousAutonomous);
        contract.ApproverId.Should().Be("AUTONOMOUS-AI-AGENT");
        contract.DigitalSignatureSha256.Should().NotBeNullOrWhiteSpace();

        // Verify auction and bid statuses transitioned
        var updatedAuction = _matrixService.GetAuction(auction.AuctionId);
        updatedAuction!.Status.Should().Be(AuctionStatus.Awarded);

        var updatedBid = updatedAuction.Bids.First(b => b.BidId == bid.BidId);
        updatedBid.Status.Should().Be(BidStatus.Accepted);
    }

    [Fact]
    public void SealAwardContract_WhenValueEqualsTenThousand_AutoApproves()
    {
        var auction = _matrixService.CreateAuction(new CreateAuctionRequest(
            Sku: "SKU-EXACT-CAP",
            RequiredQuantity: 100,
            TargetUnitPrice: 100m,
            ReserveUnitPrice: 120m,
            DeliveryDeadlineUtc: DateTime.UtcNow.AddDays(14)));

        // 100 units * $100 = $10,000 (boundary condition)
        var bid = _matrixService.SubmitBid(auction.AuctionId, new SubmitBidRequest("SUP-BOUNDARY", "Boundary Co", 100m, 5, 0.90, 100));

        var contract = _sealingService.SealAwardContract(auction.AuctionId, bid.BidId);

        contract.TotalContractValue.Should().Be(10_000m);
        contract.AutoApproved.Should().BeTrue();
        contract.ApprovalMode.Should().Be(AwardApprovalMode.AutonomousAutonomous);
    }

    [Fact]
    public void SealAwardContract_WhenValueExceedsTenThousand_RequiresHitl()
    {
        var auction = _matrixService.CreateAuction(new CreateAuctionRequest(
            Sku: "SKU-HITL-AWARD",
            RequiredQuantity: 500,
            TargetUnitPrice: 100m,
            ReserveUnitPrice: 120m,
            DeliveryDeadlineUtc: DateTime.UtcNow.AddDays(14)));

        // 500 units * $100 = $50,000 > $10,000 cap
        var bid = _matrixService.SubmitBid(auction.AuctionId, new SubmitBidRequest("SUP-ENTERPRISE", "Enterprise Co", 100m, 5, 0.95, 500));

        var contract = _sealingService.SealAwardContract(auction.AuctionId, bid.BidId);

        contract.Should().NotBeNull();
        contract.TotalContractValue.Should().Be(50_000m);
        contract.AutoApproved.Should().BeFalse();
        contract.ApprovalMode.Should().Be(AwardApprovalMode.RequiresHitl);
        contract.ApproverId.Should().Be("PENDING-HITL-APPROVAL");
    }

    [Fact]
    public void VerifyContractSignature_ReturnsTrue_ForValidContract()
    {
        var auction = _matrixService.CreateAuction(new CreateAuctionRequest(
            Sku: "SKU-SIG-TEST",
            RequiredQuantity: 50,
            TargetUnitPrice: 80m,
            ReserveUnitPrice: 100m,
            DeliveryDeadlineUtc: DateTime.UtcNow.AddDays(14)));

        var bid = _matrixService.SubmitBid(auction.AuctionId, new SubmitBidRequest("SUP-SIG", "Sig Supplier", 80m, 3, 0.90, 50));
        var contract = _sealingService.SealAwardContract(auction.AuctionId, bid.BidId);

        var isValid = _sealingService.VerifyContractSignature(contract);

        isValid.Should().BeTrue();
    }

    [Fact]
    public void VerifyContractSignature_ReturnsFalse_WhenContractPayloadIsTampered()
    {
        var auction = _matrixService.CreateAuction(new CreateAuctionRequest(
            Sku: "SKU-TAMPER-TEST",
            RequiredQuantity: 50,
            TargetUnitPrice: 80m,
            ReserveUnitPrice: 100m,
            DeliveryDeadlineUtc: DateTime.UtcNow.AddDays(14)));

        var bid = _matrixService.SubmitBid(auction.AuctionId, new SubmitBidRequest("SUP-TAMPER", "Tamper Supplier", 80m, 3, 0.90, 50));
        var contract = _sealingService.SealAwardContract(auction.AuctionId, bid.BidId);

        // Tamper with contract price: $80 -> $99
        var tamperedContract = contract with { AgreedUnitPrice = 99m, TotalContractValue = 50 * 99m };

        var isValid = _sealingService.VerifyContractSignature(tamperedContract);

        isValid.Should().BeFalse();
    }

    [Fact]
    public void VerifyContractSignature_ReturnsFalse_WhenSignedWithDifferentSecretKey()
    {
        var auction = _matrixService.CreateAuction(new CreateAuctionRequest(
            Sku: "SKU-KEY-TEST",
            RequiredQuantity: 20,
            TargetUnitPrice: 40m,
            ReserveUnitPrice: 50m,
            DeliveryDeadlineUtc: DateTime.UtcNow.AddDays(14)));

        var bid = _matrixService.SubmitBid(auction.AuctionId, new SubmitBidRequest("SUP-KEY", "Key Supplier", 45m, 2, 0.88, 20));
        var contract = _sealingService.SealAwardContract(auction.AuctionId, bid.BidId, secretKey: "KEY-1");

        // Verify with another key
        var isValid = _sealingService.VerifyContractSignature(contract, secretKey: "KEY-2-DIFFERENT");

        isValid.Should().BeFalse();
    }

    [Fact]
    public void SealAwardContract_WhenAlreadyAwarded_ThrowsInvalidOperationException()
    {
        var auction = _matrixService.CreateAuction(new CreateAuctionRequest(
            Sku: "SKU-DOUBLE-AWARD",
            RequiredQuantity: 10,
            TargetUnitPrice: 50m,
            ReserveUnitPrice: 60m,
            DeliveryDeadlineUtc: DateTime.UtcNow.AddDays(7)));

        var bid = _matrixService.SubmitBid(auction.AuctionId, new SubmitBidRequest("SUP-D", "Supplier D", 50m, 2, 0.9, 10));
        _sealingService.SealAwardContract(auction.AuctionId, bid.BidId);

        // Attempting to seal second time
        var act = () => _sealingService.SealAwardContract(auction.AuctionId, bid.BidId);
        act.Should().Throw<InvalidOperationException>().WithMessage("*already been awarded*");
    }

    #endregion

    #region 6. Thread Safety & Auction Management Tests

    [Fact]
    public void SubmitBid_ConcurrentThreads_MaintainsThreadSafeCollectionIntegrity()
    {
        var auction = _matrixService.CreateAuction(new CreateAuctionRequest(
            Sku: "SKU-CONCURRENT",
            RequiredQuantity: 1000,
            TargetUnitPrice: 100m,
            ReserveUnitPrice: 150m,
            DeliveryDeadlineUtc: DateTime.UtcNow.AddDays(30)));

        const int threadCount = 20;
        Parallel.For(0, threadCount, i =>
        {
            _matrixService.SubmitBid(auction.AuctionId, new SubmitBidRequest(
                SupplierId: $"SUP-THREAD-{i}",
                SupplierName: $"Thread Supplier {i}",
                UnitPrice: 100m + i,
                LeadTimeDays: 5 + (i % 5),
                SlaReliabilityScore: 0.80 + (i * 0.005),
                AvailableQuantity: 100 + i));
        });

        var updatedAuction = _matrixService.GetAuction(auction.AuctionId);
        updatedAuction.Should().NotBeNull();
        updatedAuction!.Bids.Should().HaveCount(threadCount);
    }

    [Fact]
    public void GetAllAuctions_ReturnsAllCreatedAuctions()
    {
        var initialCount = _matrixService.GetAllAuctions().Count;

        _matrixService.CreateAuction(new CreateAuctionRequest("SKU-ALL-1", 100, 50m, 60m, DateTime.UtcNow.AddDays(10)));
        _matrixService.CreateAuction(new CreateAuctionRequest("SKU-ALL-2", 200, 70m, 80m, DateTime.UtcNow.AddDays(10)));

        var all = _matrixService.GetAllAuctions();
        all.Count.Should().Be(initialCount + 2);
    }

    #endregion

    #region 7. Dependency Injection Registration Tests

    [Fact]
    public void AddProcurementBiddingServices_RegistersAllCoreServicesAsSingletons_WithoutCallingBuildServiceProvider()
    {
        // Explicit boundary requirement: Direct ServiceDescriptor assertion on ServiceCollection without BuildServiceProvider()
        var services = new ServiceCollection();

        services.AddProcurementBiddingServices();

        services.Should().Contain(d =>
            d.ServiceType == typeof(ISupplierBiddingMatrixService) &&
            d.ImplementationType == typeof(SupplierBiddingMatrixService) &&
            d.Lifetime == ServiceLifetime.Singleton);

        services.Should().Contain(d =>
            d.ServiceType == typeof(IAutonomousProcurementAgent) &&
            d.ImplementationType == typeof(AutonomousProcurementAgent) &&
            d.Lifetime == ServiceLifetime.Singleton);

        services.Should().Contain(d =>
            d.ServiceType == typeof(IProcurementAwardSealingService) &&
            d.ImplementationType == typeof(ProcurementAwardSealingService) &&
            d.Lifetime == ServiceLifetime.Singleton);
    }

    #endregion
}
