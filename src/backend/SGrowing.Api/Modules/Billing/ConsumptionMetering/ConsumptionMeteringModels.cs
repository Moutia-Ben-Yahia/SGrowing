using System;
using System.Collections.Generic;

namespace SGrowing.Api.Modules.Billing.ConsumptionMetering;

public enum UsageMetric
{
    ApiQueries,
    AiTokensMillion,
    VoiceMinutes,
    WhatsAppMessages
}

public record MetricQuota(
    UsageMetric Metric,
    double CurrentUsage,
    double TierQuotaLimit,
    double BurnRatePercentage,
    decimal UnitOverageRateDollars
);

public record TenantConsumptionProfile(
    string TenantId,
    string TenantName,
    string CurrentTier,
    List<MetricQuota> Metrics,
    decimal EstimatedMidCycleInvoiceDollars,
    bool OverQuotaWarningActive,
    DateTime BillingCycleEndUtc
);

public record IngestUsageEventRequest(
    string TenantId,
    UsageMetric Metric,
    double UnitsConsumed
);

public record AutoUpgradeTierRequest(
    string TenantId,
    string NewTier
);
