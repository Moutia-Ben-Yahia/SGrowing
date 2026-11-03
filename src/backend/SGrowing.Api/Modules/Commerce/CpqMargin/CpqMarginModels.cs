using System;
using System.Collections.Generic;

namespace SGrowing.Api.Modules.Commerce.CpqMargin;

public enum DiscountApprovalTier
{
    StandardRepApproved,    // 0% - 15% discount
    VpSalesApproved,        // 15% - 25% discount
    CfoBoardApproved,       // > 25% discount
    RejectedMarginBreach    // Gross Margin < 75%
}

public record ContractYearRamp(
    int YearNumber,
    decimal BasePrice,
    decimal DiscountPercentage,
    decimal NetAnnualContractValue,
    decimal EstimatedCOGS,
    decimal GrossProfit,
    double GrossMarginPercentage
);

public record QuotePricingRequest(
    string QuoteId,
    string CustomerName,
    int ContractTermYears,
    decimal AnnualListPrice,
    decimal RequestedDiscountPercentage,
    decimal InfrastructureCogsYearly,
    decimal SupportTierFeeYearly
);

public record QuotePricingResult(
    string QuoteId,
    string CustomerName,
    int ContractTermYears,
    decimal TotalContractValue,
    decimal TotalGrossProfit,
    double BlendedGrossMarginPercentage,
    DiscountApprovalTier ApprovalTier,
    string CryptographicAuditHash,
    List<ContractYearRamp> YearRamps,
    string GuardrailRecommendation
);
