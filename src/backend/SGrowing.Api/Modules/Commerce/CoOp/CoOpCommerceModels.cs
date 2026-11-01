namespace SGrowing.Api.Modules.Commerce.CoOp;

/// <summary>
/// Type of cooperative cross-merchant SKU bundling strategy.
/// </summary>
public enum BundleCoOpType
{
    ComplementaryCategory,
    CrossMerchantCart,
    SeasonalPromotion
}

/// <summary>
/// Settlement lifecycle status for unified multi-merchant cart orders.
/// </summary>
public enum SettlementSplitStatus
{
    PendingFulfillment,
    DeliveredSettled,
    PartialRefundDisputed
}

/// <summary>
/// Cryptographic protocol stages for Diffie-Hellman Private Set Intersection.
/// </summary>
public enum PsiProtocolState
{
    Initiated,
    BlindedKeysExchanged,
    IntersectionComputed
}

/// <summary>
/// Represents an individual SKU in a merchant's catalog.
/// </summary>
public record SkuItem(
    string SkuId,
    string MerchantId,
    string Name,
    string Category,
    decimal RetailPriceDzd,
    decimal CostPriceDzd,
    double WeightKg = 0.5,
    double VolumeM3 = 0.001
);

/// <summary>
/// Represents a historical customer cart or basket transaction for association rule mining.
/// </summary>
public record TransactionRecord(
    string TransactionId,
    IReadOnlyList<string> SkuIds,
    DateTime TimestampUtc
);

/// <summary>
/// Represents an association rule extracted from market basket analysis.
/// Support(X =&gt; Y) = Count(X u Y) / N
/// Confidence(X =&gt; Y) = Support(X u Y) / Support(X)
/// Lift = Confidence(X =&gt; Y) / Support(Y)
/// </summary>
public record AssociationRule(
    IReadOnlyList<string> AntecedentSkus,
    IReadOnlyList<string> ConsequentSkus,
    double Support,
    double Confidence,
    double Lift,
    int TransactionCount
);

/// <summary>
/// Cross-merchant synthesized SKU bundle with algorithmic pricing and margin optimization.
/// </summary>
public record CrossMerchantSkuBundle(
    string BundleId,
    string Name,
    BundleCoOpType CoOpType,
    IReadOnlyList<string> MerchantIds,
    IReadOnlyList<SkuItem> Skus,
    decimal RegularPriceDzd,
    decimal BundleDiscountPct,
    decimal CombinedPriceDzd,
    double LiftScore,
    double SupportScore,
    decimal EstimatedJointMarginDzd,
    string Justification
);

/// <summary>
/// An item placed in a multi-merchant cart.
/// </summary>
public record CartItem(
    string SkuId,
    string MerchantId,
    string Name,
    decimal UnitPriceDzd,
    decimal CostPriceDzd,
    int Quantity,
    double WeightKg = 0.5,
    double VolumeM3 = 0.001
);

/// <summary>
/// Checkout request for a multi-merchant cart.
/// </summary>
public record SplitCartRequest(
    string CustomerId,
    IReadOnlyList<CartItem> Items,
    decimal BaseShippingRateDzd,
    decimal PlatformFeePct = 0.05m,
    decimal CoOpCommissionPct = 0.03m,
    string? ShippingAddress = null
);

/// <summary>
/// Sub-order dispatched to an individual merchant from a unified checkout cart.
/// </summary>
public record SubOrder(
    string SubOrderId,
    string ParentOrderId,
    string MerchantId,
    IReadOnlyList<CartItem> Items,
    decimal ItemsSubtotalDzd,
    decimal AllocatedShippingDzd,
    decimal PlatformFeeDzd,
    decimal CoOpCommissionDzd,
    decimal MerchantNetPayoutDzd,
    SettlementSplitStatus Status
);

/// <summary>
/// Unified multi-merchant cart order with split fulfillment and net attribution.
/// </summary>
public record SplitCartOrder(
    string OrderId,
    string CustomerId,
    decimal TotalItemsDzd,
    decimal TotalShippingDzd,
    decimal GrandTotalDzd,
    IReadOnlyList<SubOrder> SubOrders,
    IReadOnlyDictionary<string, decimal> NetAttributionMap,
    SettlementSplitStatus OverallStatus,
    DateTime CreatedAtUtc
);

/// <summary>
/// Request to cancel or refund a line item within a multi-merchant order.
/// </summary>
public record PartialRefundRequest(
    string OrderId,
    string MerchantId,
    string SkuId,
    int QuantityToRefund,
    string Reason
);

/// <summary>
/// Result of a partial line-item refund calculation, including shipping proration and fee clawbacks.
/// </summary>
public record RefundAllocationResult(
    string OrderId,
    string MerchantId,
    string SkuId,
    int QuantityRefunded,
    decimal ItemRefundAmountDzd,
    decimal ShippingRefundAmountDzd,
    decimal TotalCustomerRefundDzd,
    decimal ClawbackPlatformFeeDzd,
    decimal ClawbackCoOpCommissionDzd,
    decimal MerchantDebitDzd,
    SettlementSplitStatus UpdatedStatus
);

/// <summary>
/// Blinded cryptographic token for Diffie-Hellman Private Set Intersection.
/// </summary>
public record PsiBlindedToken(
    string ItemId,
    string BlindedHashHex
);

/// <summary>
/// Active Private Set Intersection protocol session between two merchants.
/// </summary>
public record PsiSession(
    string SessionId,
    string InitiatorMerchantId,
    string PartnerMerchantId,
    PsiProtocolState State,
    DateTime CreatedAtUtc,
    int InitiatorElementCount,
    int PartnerElementCount,
    int IntersectionSize = 0,
    double OverlapPercentage = 0.0
);

/// <summary>
/// Request to initiate a Private Set Intersection session.
/// </summary>
public record PsiInitiateRequest(
    string InitiatorMerchantId,
    string PartnerMerchantId,
    IReadOnlyList<string> RawIdentifiers
);

/// <summary>
/// Request to exchange blinded tokens in the Diffie-Hellman PSI protocol.
/// </summary>
public record PsiExchangeRequest(
    string SessionId,
    string MerchantId,
    IReadOnlyList<PsiBlindedToken> BlindedTokens,
    int Step
);

/// <summary>
/// Result of Private Set Intersection computation.
/// </summary>
public record PsiMatchResult(
    string SessionId,
    int IntersectionCount,
    double OverlapPercentageInitiator,
    double OverlapPercentagePartner,
    IReadOnlyList<string> MatchedBlindedHashes,
    string ComplianceNote
);

/// <summary>
/// Request to discover cross-merchant bundles.
/// </summary>
public record DiscoverBundlesRequest(
    IReadOnlyList<string>? MerchantFilter = null,
    double MinSupport = 0.01,
    double MinLift = 2.0,
    int MaxBundles = 10
);

/// <summary>
/// Summary of collaborative commerce partnership potential between merchants.
/// </summary>
public record MerchantPartnershipSummary(
    string MerchantId,
    string PartnerMerchantId,
    int MutualCustomerOverlapCount,
    double CustomerOverlapRatio,
    int DiscoveredBundleCount,
    decimal PotentialGrossRevenueDzd
);
