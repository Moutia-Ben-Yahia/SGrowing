namespace SGrowing.Api.Modules.Finance.MicrostructureKylesLambdaAdverseSelection;

public sealed record MicrostructureKylesLambdaTelemetry
{
    public required string Ticker { get; init; }
    public double CovariancePriceChangeOrderFlow { get; init; } = 0.042; // Cov(Delta P, Q) ($*shares)
    public double VarianceOrderFlow { get; init; } = 12000.0; // Var(Q) (shares^2)
    public double RollingBaselineLambda { get; init; } = 0.0000015; // baseline $/share
    public double HasbrouckPermanentVarianceFraction { get; init; } = 0.68; // rho_info = sigma_perm^2 / sigma_total^2
    public double VolumeSynchronizedToxicityVpin { get; init; } = 0.42; // VPIN (0.0 to 1.0)
    public double EffectiveHalfSpreadBps { get; init; } = 2.5; // bps
    public double CurrentBookDepthShares { get; init; } = 45000.0; // top 5 levels depth
}

public sealed record MicrostructureKylesLambdaAnalysisResult
{
    public required string Ticker { get; init; }
    public required double KylesLambdaPriceImpactPerShare { get; init; }
    public required double LambdaRatioToRollingBaseline { get; init; }
    public required double HasbrouckPermanentInformationFraction { get; init; }
    public required double VolumeSynchronizedToxicityVpin { get; init; }
    public required double RecommendedSpreadWideningMultiplier { get; init; }
    public required bool IsLambdaSpikeDetected { get; init; }
    public required bool IsInformationDominantRegime { get; init; }
    public required bool IsToxicityCritical { get; init; }
    public required string MarketMicrostructureRegime { get; init; }
    public required string ExecutionDefenseProtocol { get; init; }
}
