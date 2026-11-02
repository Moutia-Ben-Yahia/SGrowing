namespace SGrowing.Api.Modules.Finance.CdoGaussianCopulaTrancheCorrelation;

public record CdoCopulaTrancheTelemetry
{
    public required string DealIdentifier { get; init; }
    public string TrancheCategory { get; init; } = "Mezzanine_3_7_Percent"; // Equity_0_3, Mezzanine_3_7, Senior_7_15, SuperSenior_15_100
    public double TrancheAttachmentPointPercent { get; init; } = 3.0; // 3%
    public double TrancheDetachmentPointPercent { get; init; } = 7.0; // 7%
    public double AssetPairwiseDefaultCorrelation { get; init; } = 0.28; // rho in [0, 1]
    public double ExtremeTailCorrelationMultiplier { get; init; } = 1.6; // Tail contagion factor
    public double AverageObligorAnnualDefaultProbabilityPercent { get; init; } = 0.55; // 0.55% investment grade
    public double ExpectedPortfolioRecoveryRatePercent { get; init; } = 40.0; // Standard 40% senior unsecured recovery
    public int ObligorCountInCollateralBasket { get; init; } = 125;
    public double FiveYearCdsSpreadBasisPoints { get; init; } = 210.0;
}

public record CdoCopulaTrancheEvaluation
{
    public required string DealIdentifier { get; init; }
    public double CalculatedTrancheExpectedLossPercent { get; init; }
    public double StressedTailContagionLossPercent { get; init; }
    public double BaseCorrelationParSpreadBps { get; init; }
    public bool IsTrancheInvestmentGradeProtected { get; init; }
    public bool IsCorrelationRiskContained { get; init; }
    public bool IsSuperSeniorSafeFromSpillover { get; init; }
    public List<string> ActiveRegimes { get; init; } = new();
    public List<string> RecommendedMitigations { get; init; } = new();
    public DateTime EvaluatedAtUtc { get; init; } = DateTime.UtcNow;
}
