using System.Text.Json.Serialization;

namespace SGrowing.Api.Modules.SupplyChain.Procurement;

/// <summary>
/// Supported auction mechanisms for procurement bidding.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum AuctionType
{
    ReverseDutch,
    EnglishReverse,
    SealedFirstPrice
}

/// <summary>
/// Lifecycle status of a procurement bidding auction.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum AuctionStatus
{
    Draft,
    OpenForBids,
    UnderNegotiation,
    Awarded,
    Cancelled
}

/// <summary>
/// Status of an individual supplier's bid during auction and negotiation.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum BidStatus
{
    Submitted,
    UnderNegotiation,
    CounterOffered,
    Accepted,
    Rejected
}

/// <summary>
/// Approval authority mode for procurement award contract execution.
/// Contracts &lt;= $10,000 can be autonomously approved; contracts &gt; $10,000 require human-in-the-loop (HITL).
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum AwardApprovalMode
{
    AutonomousAutonomous = 0,
    RequiresHitl = 1
}

/// <summary>
/// Record of a price or term concession made during bargaining rounds.
/// </summary>
public record BidConcession(
    int RoundNumber,
    decimal OfferedUnitPrice,
    decimal ConcessionDelta,
    DateTime TimestampUtc,
    string? Note = null);

/// <summary>
/// An individual supplier bid submitted to an auction.
/// </summary>
public record SupplierBid(
    Guid BidId,
    Guid AuctionId,
    string SupplierId,
    string SupplierName,
    decimal UnitPrice,
    int LeadTimeDays,
    double SlaReliabilityScore,
    int AvailableQuantity,
    List<BidConcession> Concessions,
    BidStatus Status,
    DateTime SubmittedAtUtc,
    double? CompositeUtilityScore = null);

/// <summary>
/// Procurement reverse auction domain record.
/// </summary>
public record BiddingAuction(
    Guid AuctionId,
    string Sku,
    int RequiredQuantity,
    decimal TargetUnitPrice,
    decimal ReserveUnitPrice,
    DateTime DeliveryDeadlineUtc,
    AuctionStatus Status,
    DateTime CreatedAtUtc,
    DateTime ExpiresAtUtc,
    List<SupplierBid> Bids,
    AuctionType Type = AuctionType.EnglishReverse,
    string? Description = null);

/// <summary>
/// Cryptographically sealed procurement award contract.
/// </summary>
public record ProcurementAwardContract(
    Guid ContractId,
    Guid AuctionId,
    Guid WinningBidId,
    string SupplierId,
    int AwardedQuantity,
    decimal AgreedUnitPrice,
    decimal TotalContractValue,
    double SlaScore,
    string DigitalSignatureSha256,
    DateTime AwardedAtUtc,
    bool AutoApproved,
    AwardApprovalMode ApprovalMode = AwardApprovalMode.AutonomousAutonomous,
    string? ApproverId = null);

/// <summary>
/// Strategic dual-sourcing recommendation to hedge operational and geopolitical risks.
/// </summary>
public record DualSourcingRecommendation(
    string PrimarySupplierId,
    string SecondarySupplierId,
    double PrimarySplitRatio,
    double SecondarySplitRatio,
    string HedgingRationale,
    int PrimaryQuantity = 0,
    int SecondaryQuantity = 0,
    decimal PrimaryUnitPrice = 0m,
    decimal SecondaryUnitPrice = 0m,
    decimal BlendedUnitPrice = 0m,
    decimal TotalCost = 0m);

/// <summary>
/// Detailed multi-criteria utility score breakdown for a supplier bid.
/// </summary>
public record ScoredSupplierBid(
    SupplierBid Bid,
    double UtilityScore,
    double CostScore,
    double SlaScore,
    double LeadTimeScore,
    bool IsEligible,
    string? DisqualificationReason = null);

/// <summary>
/// Configurable weights for multi-criteria bidding matrix evaluation.
/// Defaults: w_cost = 0.50, w_sla = 0.30, w_lead = 0.20.
/// </summary>
public record BiddingMatrixWeights(
    double CostWeight = 0.50,
    double SlaWeight = 0.30,
    double LeadTimeWeight = 0.20)
{
    public double TotalWeight => CostWeight + SlaWeight + LeadTimeWeight;
}

// ---------------------------------------------------------------------------
// API DTOs (Requests & Responses)
// ---------------------------------------------------------------------------

public record CreateAuctionRequest(
    string Sku,
    int RequiredQuantity,
    decimal TargetUnitPrice,
    decimal ReserveUnitPrice,
    DateTime DeliveryDeadlineUtc,
    DateTime? ExpiresAtUtc = null,
    AuctionType Type = AuctionType.EnglishReverse,
    string? Description = null);

public record CreateAuctionResponse(
    bool Success,
    string Message,
    BiddingAuction? Auction = null);

public record SubmitBidRequest(
    string SupplierId,
    string SupplierName,
    decimal UnitPrice,
    int LeadTimeDays,
    double SlaReliabilityScore,
    int AvailableQuantity);

public record SubmitBidResponse(
    bool Success,
    string Message,
    SupplierBid? Bid = null);

public record NegotiationStepRequest(
    Guid BidId,
    int CurrentRound,
    int MaxRounds,
    double PostureBeta = 1.0,
    string? NegotiationNote = null);

public record NegotiationStepResponse(
    bool Success,
    string Message,
    Guid BidId,
    int Round,
    decimal TargetPriceAtRound,
    decimal CounterOfferPrice,
    bool BidAccepted,
    SupplierBid? UpdatedBid = null);

public record DualSourcingRequest(
    bool RequireGeopoliticalHedging = false,
    double TargetPrimaryRatio = 0.60,
    double TargetSecondaryRatio = 0.40);

public record DualSourcingResponse(
    bool Success,
    string Message,
    DualSourcingRecommendation? Recommendation = null);

public record AwardContractRequest(
    Guid WinningBidId,
    int? AwardedQuantity = null,
    string? SealingKey = null,
    string? ApproverId = null);

public record AwardContractResponse(
    bool Success,
    string Message,
    ProcurementAwardContract? Contract = null);

public record ProcurementEngineHealth(
    string Status,
    string Version,
    DateTime TimestampUtc,
    Dictionary<string, string> Capabilities);
