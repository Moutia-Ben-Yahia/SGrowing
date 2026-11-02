namespace SGrowing.Api.Modules.Growth.Affiliates;

public enum LoyaltyTier
{
    Bronze,
    Silver,
    Gold,
    Platinum
}

public record LoyaltyAccount(
    string AccountId,
    string CustomerEmail,
    LoyaltyTier Tier,
    int TotalPoints,
    int PointsRedeemed,
    decimal LifetimeSpendUsd,
    DateTime UpdatedAtUtc
);

public record AffiliatePartner(
    string PartnerId,
    string Name,
    string Email,
    string ReferralCode,
    string? ParentPartnerId,
    decimal Tier1RatePercent,
    decimal Tier2RatePercent,
    decimal TotalEarnedUsd,
    decimal PendingPayoutUsd,
    bool IsActive
);

public record ReferralConversionResult(
    string TransactionId,
    string ReferralCode,
    string PartnerId,
    decimal OrderAmountUsd,
    decimal DirectCommissionUsd,
    decimal? ParentCommissionUsd,
    string? ParentPartnerId,
    int AwardedLoyaltyPoints,
    bool Success
);

public record CreateAffiliateRequest(
    string Name,
    string Email,
    string? ParentPartnerId = null,
    decimal Tier1RatePercent = 20.0m,
    decimal Tier2RatePercent = 5.0m
);

public record TrackConversionRequest(
    string ReferralCode,
    string CustomerId,
    string CustomerEmail,
    decimal OrderAmountUsd
);

public record FraudAuditReport(
    int TotalTransactionsAudited,
    int FlaggedCount,
    List<string> AuditWarnings
);
