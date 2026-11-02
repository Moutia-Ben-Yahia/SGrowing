namespace SGrowing.Api.Modules.Analytics.RevenueIntelligence;

public record MrrWaterfall(
    double StartingMrr,
    double NewMrr,
    double ExpansionMrr,
    double ContractionMrr,
    double ChurnedMrr,
    double NetEndingMrr,
    double QuickRatio // (New + Expansion) / (Contraction + Churn)
);

public record CohortRetentionCell(
    string CohortMonth,
    int MonthOffset,
    int StartingAccounts,
    int ActiveAccounts,
    double RetentionRatePercent,
    double RevenueRetainedPercent
);

public record RevenueIntelligenceSnapshot(
    string TenantId,
    DateTime CalculatedAtUtc,
    double Arr,
    double Mrr,
    double NrrPercent, // Net Revenue Retention (e.g. 118.5%)
    double GrrPercent, // Gross Revenue Retention (e.g. 94.2%)
    double LtvCacRatio,
    MrrWaterfall Waterfall,
    List<CohortRetentionCell> CohortMatrix
);
