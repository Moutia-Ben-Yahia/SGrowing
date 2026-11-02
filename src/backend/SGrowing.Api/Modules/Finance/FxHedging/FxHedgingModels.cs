namespace SGrowing.Api.Modules.Finance.FxHedging;

public enum CurrencyCode
{
    Dzd, // Algerian Dinar
    Eur, // Euro
    Usd, // US Dollar
    Aed, // UAE Dirham
    Gbp  // British Pound
}

public enum ExposureDirection
{
    NetReceivable, // Inflow (e.g. Export receivables)
    NetPayable     // Outflow (e.g. Import supplier invoices)
}

public sealed record FxCurrencyPosition(
    CurrencyCode Currency,
    decimal NetAmount,
    ExposureDirection Direction,
    DateTime SettlementDateUtc,
    decimal CurrentSpotRateToDzd,
    double AnnualizedVolatilitySigma
);

public sealed record ValueAtRiskReport(
    string ReportId,
    string TenantId,
    CurrencyCode BaseCurrency, // Typically DZD or EUR
    decimal TotalNetPortfolioValueBase,
    double ConfidenceLevel, // e.g. 0.99 (99%)
    int HorizonDays,        // e.g. 30 days
    decimal ParametricVaRBase,
    decimal ConditionalVaRExpectedShortfallBase,
    decimal MaxTolerableLossBase,
    bool HedgeRecommended,
    IReadOnlyList<ForwardHedgeRecommendation> Recommendations,
    DateTime EvaluatedAtUtc
);

public sealed record ForwardHedgeRecommendation(
    CurrencyCode Currency,
    ExposureDirection ExposureToHedge,
    decimal NotionalAmount,
    decimal CurrentSpotRate,
    decimal ForwardHedgeRate,
    int ForwardTenorDays,
    string RecommendedInstrument, // "ForwardContract", "CollaredOption"
    string Rationale
);

public sealed record TriangularArbitrageOpportunity(
    string OpportunityId,
    CurrencyCode Base,
    CurrencyCode Intermediate,
    CurrencyCode Quote,
    decimal RateBaseToIntermediate,
    decimal RateIntermediateToQuote,
    decimal MarketRateBaseToQuote,
    decimal SyntheticRateBaseToQuote,
    decimal DiscrepancyBps,
    bool IsExecutableSpread,
    DateTime DetectedAtUtc
);

public sealed record EvaluateFxRiskRequest(
    string TenantId,
    CurrencyCode BaseCurrency,
    IReadOnlyList<FxCurrencyPosition> Positions,
    double ConfidenceLevel = 0.99,
    int HorizonDays = 30,
    decimal MaxLossToleranceBase = 500000.0m
);
