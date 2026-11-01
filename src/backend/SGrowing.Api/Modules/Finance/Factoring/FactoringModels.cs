namespace SGrowing.Api.Modules.Finance.Factoring;

/// <summary>
/// Primary mechanism for early supplier payment financing.
/// </summary>
public enum FactoringType
{
    DynamicDiscounting,
    ReverseFactoringConsortium,
    DirectEarlyPay
}

/// <summary>
/// Lifecycle status of an invoice within the factoring and early payment pipeline.
/// </summary>
public enum InvoiceFundingStatus
{
    PendingOffer,
    OfferAccepted,
    AssignedToBank,
    SettledEarly,
    MaturedAtFullTerm,
    Defaulted
}

/// <summary>
/// Partner financial institutions participating in the reverse factoring consortium.
/// </summary>
public enum PartnerBankCode
{
    BNA_Algeria,
    CPA_Algeria,
    SocGen_Algeria,
    BNP_Algeria,
    DubaiIslamicBank_UAE
}

/// <summary>
/// Feedback outcome recorded for Thompson Sampling bandit learning.
/// </summary>
public enum SupplierOfferOutcome
{
    Accepted,
    Rejected,
    Expired
}

/// <summary>
/// Represents an approved buyer payable eligible for dynamic discounting or reverse factoring.
/// </summary>
public record InvoicePayable(
    string InvoiceId,
    string BuyerTenantId,
    string SupplierTenantId,
    decimal InvoiceAmountDzd,
    DateTime InvoiceDateUtc,
    DateTime DueDateUtc,
    int PaymentTermsDays,
    bool ApprovedForPayment
);

/// <summary>
/// An actionable early payment offer generated for a supplier invoice.
/// </summary>
public record EarlyPaymentOffer(
    string OfferId,
    string InvoiceId,
    int DaysEarly,
    decimal DiscountRatePct,
    decimal AnnualizedAprYieldPct,
    decimal NetPaymentAmountDzd,
    decimal BuyerSavingsDzd,
    DateTime ExpirationDateUtc,
    FactoringType FundingType,
    InvoiceFundingStatus Status = InvoiceFundingStatus.PendingOffer
);

/// <summary>
/// Non-recourse assignment record of an approved receivable to a consortium partner bank.
/// </summary>
public record BankAssignmentRecord(
    string AssignmentId,
    string InvoiceId,
    PartnerBankCode PartnerBank,
    decimal FinancedAmountDzd,
    decimal BankFeeDzd,
    bool NonRecourseProtection = true,
    DateTime AssignedAtUtc = default,
    InvoiceFundingStatus Status = InvoiceFundingStatus.AssignedToBank
);

/// <summary>
/// Dynamic working capital and cash urgency profile for a supplier.
/// </summary>
public record SupplierCashUrgencyProfile(
    string SupplierId,
    double UrgencyScore, // 0.0 (High Liquidity) to 1.0 (Severe Working Capital Distress)
    double HistoricalAcceptanceRate,
    string WorkingCapitalBand, // 'Low', 'Moderate', 'Critical'
    decimal RecommendedDiscountMaxPct
);

/// <summary>
/// Request to compute a sliding-scale dynamic discount offer for an invoice.
/// </summary>
public record CalculateDiscountRequest(
    InvoicePayable Invoice,
    DateTime TargetPaymentDateUtc,
    decimal? AnnualAprTarget = null,
    bool ApplyDynamicCurve = false
);

/// <summary>
/// Request to generate prioritized early payment offers under buyer liquidity constraints.
/// </summary>
public record GenerateOffersRequest(
    List<InvoicePayable> Invoices,
    decimal AvailableLiquidityDzd,
    DateTime? TargetPaymentDateUtc = null,
    decimal? AnnualAprTarget = null,
    bool ApplyDynamicCurve = false
);

/// <summary>
/// Request for a supplier to accept an early payment offer.
/// </summary>
public record AcceptOfferRequest(
    string OfferId,
    string SupplierTenantId
);

/// <summary>
/// Request to assign an approved invoice to a partner bank for non-recourse reverse factoring.
/// </summary>
public record AssignToBankRequest(
    InvoicePayable Invoice,
    PartnerBankCode BankCode,
    decimal BankDiscountFeePct
);

/// <summary>
/// Executive summary of factoring and discounting operations across the enterprise.
/// </summary>
public record FactoringSummaryResponse(
    decimal TotalInvoicesVolumeDzd,
    decimal TotalEarlyPaidDzd,
    decimal TotalBuyerSavingsDzd,
    decimal ActiveBankFinancedVolumeDzd,
    int ActiveOffersCount,
    int CompletedAssignmentsCount
);

/// <summary>
/// Credit line capacity and utilization state for a consortium partner bank.
/// </summary>
public record BankCreditFacility(
    PartnerBankCode BankCode,
    string BankName,
    decimal TotalCreditLineDzd,
    decimal UtilizedCreditDzd,
    decimal AvailableLiquidityDzd,
    decimal BaseFeeRatePct
);

/// <summary>
/// Aggregated multi-bank consortium liquidity summary.
/// </summary>
public record ConsortiumLiquiditySummary(
    decimal TotalConsortiumCapacityDzd,
    decimal TotalUtilizedDzd,
    decimal AvailableConsortiumLiquidityDzd,
    IReadOnlyList<BankCreditFacility> BankFacilities
);

/// <summary>
/// Request to predict the optimal discount rate via the contextual bandit.
/// </summary>
public record BanditOptimizeRequest(
    string SupplierId,
    SupplierCashUrgencyProfile Profile
);

/// <summary>
/// Bandit recommendation response with acceptance probability and strategy rationale.
/// </summary>
public record BanditOptimizeResponse(
    string SupplierId,
    decimal RecommendedDiscountRatePct,
    double EstimatedAcceptanceProbability,
    string UrgencyBand,
    string StrategyRationale
);

/// <summary>
/// Feedback request to ingest supplier offer response for Bayesian posterior parameter updates.
/// </summary>
public record BanditFeedbackRequest(
    string SupplierId,
    decimal OfferedDiscountRatePct,
    bool Accepted,
    bool Expired = false
);

/// <summary>
/// Internal state metrics for a single discount rate arm in the multi-armed bandit.
/// </summary>
public record BanditArmDetail(
    decimal DiscountRatePct,
    double Alpha,
    double Beta,
    long TotalPulls,
    long Successes,
    double MeanAcceptanceRate
);

/// <summary>
/// Complete bandit state representation for a supplier.
/// </summary>
public record SupplierBanditSummary(
    string SupplierId,
    IReadOnlyList<BanditArmDetail> Arms
);
