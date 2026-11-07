using System;
using System.Collections.Generic;

namespace SGrowing.Api.Modules.Marketing.Loyalty;

/// <summary>
/// Loyalty membership tiers governed by lifetime points earned.
/// Thresholds: Bronze (0-499), Silver (500-1999), Gold (2000-4999), VipDiamond (5000+).
/// </summary>
public enum LoyaltyMembershipTier
{
    Bronze = 0,
    Silver = 1,
    Gold = 2,
    VipDiamond = 3
}

/// <summary>
/// Double-entry transaction types for customer loyalty point ledger entries.
/// </summary>
public enum LoyaltyTransactionType
{
    EarnPurchase = 0,
    EarnReview = 1,
    EarnReferral = 2,
    BurnRedemption = 3,
    ManualAdjustment = 4
}

/// <summary>
/// Represents an immutable double-entry ledger record for point earning or burning.
/// Invariant: BalanceAfter = previous balance + Points.
/// </summary>
public record LoyaltyPointLedgerEntry(
    Guid EntryId,
    Guid TenantId,
    Guid ContactId,
    LoyaltyTransactionType Type,
    int Points,
    int BalanceAfter,
    string ReferenceId,
    string Description,
    DateTime CreatedAtUtc
);

/// <summary>
/// Consolidated account view representing current balance, lifetime earn, tier standing, and ledger history.
/// </summary>
public record LoyaltyAccountSummary(
    Guid ContactId,
    Guid TenantId,
    string ContactName,
    string ContactEmail,
    int CurrentPointsBalance,
    int LifetimePointsEarned,
    LoyaltyMembershipTier CurrentTier,
    int NextTierPointsRemaining,
    string ReferralCode,
    List<string> ActiveTierPerks,
    List<LoyaltyPointLedgerEntry> RecentTransactions
);

/// <summary>
/// Immutable record of a successful referral conversion and dual-sided attribution reward.
/// </summary>
public record ReferralAttributionRecord(
    Guid ReferralId,
    Guid TenantId,
    Guid ReferrerContactId,
    Guid RefereeContactId,
    string ReferralCode,
    int ReferrerPointsAwarded,
    decimal RefereeDiscountPercent,
    DateTime AttributedAtUtc
);

/// <summary>
/// Command request to credit points into a contact's loyalty ledger.
/// </summary>
public record EarnPointsRequest(
    Guid TenantId,
    Guid ContactId,
    LoyaltyTransactionType Type,
    int Points,
    string ReferenceId,
    string Description
);

/// <summary>
/// Command request to redeem/burn points against an order or voucher.
/// </summary>
public record RedeemPointsRequest(
    Guid TenantId,
    Guid ContactId,
    int PointsToRedeem,
    string OrderReferenceId
);

/// <summary>
/// Command request to attribute a referee purchase to an existing referral code.
/// </summary>
public record AttributeReferralRequest(
    Guid TenantId,
    string ReferralCode,
    Guid RefereeContactId,
    decimal QualifyingOrderTotalSar
);

/// <summary>
/// Metadata definition of a loyalty tier, qualification thresholds, point multiplier, and member perks.
/// </summary>
public record LoyaltyTierInfo(
    LoyaltyMembershipTier Tier,
    string TierName,
    int MinPoints,
    int? MaxPoints,
    decimal PointsMultiplier,
    List<string> Perks
);

/// <summary>
/// DTO for earning points via Minimal API route.
/// </summary>
public record EarnPointsDto(
    LoyaltyTransactionType Type,
    int Points,
    string ReferenceId,
    string Description,
    Guid? TenantId = null
);

/// <summary>
/// DTO for redeeming points via Minimal API route.
/// </summary>
public record RedeemPointsDto(
    int PointsToRedeem,
    string OrderReferenceId,
    Guid? TenantId = null
);
