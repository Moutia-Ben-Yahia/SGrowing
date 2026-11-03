namespace SGrowing.Api.Modules.Sales.ValueRealization;

public record CustomerValueMetric(
    string MetricId,
    string CustomerAccountName,
    string ObjectiveCategory, // REVENUE_GROWTH, COST_REDUCTION, CYCLE_TIME_SPEEDUP, COMPLIANCE_SECURITY
    decimal BaselineValue,
    decimal RealizedCurrentValue,
    decimal AnnualizedRoiDollars,
    double RealizationMultiplier, // e.g. 3.4x
    string VerificationSource
);

public record QbrExecutiveSlide(
    int SlideIndex,
    string SlideTitle,
    string ExecutiveKeyTakeaway,
    string SlideVisualType, // KPI_SCORECARD, ROI_WATERFALL, PEER_BENCHMARK, EXPANSION_PROPOSAL
    List<string> BulletHighlights
);

public record QbrExecutiveDeck(
    string DeckId,
    string CustomerAccountName,
    string FiscalQuarter,
    decimal TotalRealizedRoiDollars,
    double OverallAccountHealthScore,
    decimal ProposedExpansionArrDollars,
    List<QbrExecutiveSlide> GeneratedSlides,
    DateTime CreatedAt
);

public record ValueRealizationOverview(
    int MonitoredEnterpriseAccountsCount,
    decimal AggregateRealizedRoiDollars,
    double AverageAccountNpsHealthScore,
    int ScheduledQbrPresentationsCount,
    List<CustomerValueMetric> TopRealizedMetrics,
    List<QbrExecutiveDeck> RecentExecutiveDecks
);

public record GenerateQbrDeckRequest(
    string CustomerAccountName,
    string FiscalQuarter,
    decimal TotalRealizedRoiDollars,
    double OverallAccountHealthScore,
    decimal ProposedExpansionArrDollars
);

public record AttestRoiRequest(
    string CustomerAccountName,
    string ObjectiveCategory,
    decimal BaselineValue,
    decimal RealizedCurrentValue,
    string VerificationSource
);
