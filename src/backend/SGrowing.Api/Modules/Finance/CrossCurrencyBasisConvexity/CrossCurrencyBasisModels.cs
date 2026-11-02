namespace SGrowing.Api.Modules.Finance.CrossCurrencyBasisConvexity;

public record CrossCurrencyBasisTelemetry
{
    public required string CurrencyPairOrPortfolioUid { get; init; }
    public string CurrencyPair { get; init; } = "EUR_USD"; // EUR_USD, USD_JPY, GBP_USD
    public double CoveredInterestParityBasisBps { get; init; } = -14.5; // Target within +/- 25 bps
    public double SpotExchangeRate { get; init; } = 1.0850;
    public double ForwardExchangeRateOneYear { get; init; } = 1.1025;
    public double DomesticSofrRatePercent { get; init; } = 4.35;
    public double ForeignEstrRatePercent { get; init; } = 2.65;
    public double PortfolioConvexityYearsSquared { get; init; } = -12.4; // Negative convexity in MBS/hedging
    public double DurationMismatchYears { get; init; } = 0.85; // Target <= 1.5 years
    public double CollateralDiscountCurveSpreadBps { get; init; } = 8.5; // Target <= 20.0 bps
    public double ProjectedYieldShockBasisPoints { get; init; } = 50.0;
}

public record CrossCurrencyBasisEvaluation
{
    public required string CurrencyPairOrPortfolioUid { get; init; }
    public double AbsoluteCipDeviationBps { get; init; }
    public double ForcedDurationHedgingImpactYears { get; init; }
    public bool IsCipBasisArbitrageBounded { get; init; }
    public bool IsConvexityCascadeContained { get; init; }
    public bool IsCollateralCurveAligned { get; init; }
    public bool IsCrossCurrencyPortfolioHedgingSound { get; init; }
    public List<string> ActiveRegimes { get; init; } = new();
    public List<string> RecommendedMitigations { get; init; } = new();
    public DateTime EvaluatedAtUtc { get; init; } = DateTime.UtcNow;
}
