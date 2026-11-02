namespace SGrowing.Api.Modules.Finance.CdsIndexTrancheCorrelation;

public record CdsIndexTrancheTelemetry
{
    public required string TranchePortfolioUid { get; init; }
    public string IndexFamily { get; init; } = "CDX_NA_IG_Series43"; // or "iTraxx_Europe_Series42"
    public string TrancheAttachmentRange { get; init; } = "3-7_Mezzanine"; // 0-3 Equity, 3-7 Mezz, 7-15 Senior
    public double IndexSpreadBasisPoints { get; init; } = 54.2; // bps
    public double AverageConstituentCdsSpreadBps { get; init; } = 52.8; // bps
    public double BaseCorrelationPercent { get; init; } = 38.5; // 20 - 75%
    public double BaseCorrelationSlopePerDetachmentPoint { get; init; } = 1.45; // Target >= 0 (monotonic)
    public double TrancheExpectedLossDensity { get; init; } = 0.082; // Target >= 0
    public double EquityTrancheDelta { get; init; } = 0.62; // Target <= 0.85
    public double ImpliedCompoundCorrelationPercent { get; init; } = 28.4;
}

public record CdsIndexTrancheEvaluation
{
    public required string TranchePortfolioUid { get; init; }
    public double IndexCdsBasisBps { get; init; }
    public bool IsIndexCdsBasisNormal { get; init; }
    public bool IsBaseCorrelationMonotonic { get; init; }
    public bool IsEquityTrancheSolvent { get; init; }
    public bool IsTranchePricingArbitrageFree { get; init; }
    public List<string> ActiveRegimes { get; init; } = new();
    public List<string> RecommendedMitigations { get; init; } = new();
    public DateTime EvaluatedAtUtc { get; init; } = DateTime.UtcNow;
}
