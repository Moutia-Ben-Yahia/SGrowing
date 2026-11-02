namespace SGrowing.Api.Modules.Finance.StatisticalArbitrageOrnsteinUhlenbeck;

public sealed record StatisticalArbitrageTelemetry(
    string PairSymbol,
    double CointegrationHedgeRatioBeta, // beta: e.g. 1.05
    double CurrentSpreadResidual, // X_t: e.g. 0.042
    double LongTermMeanMu, // mu: e.g. 0.005
    double MeanReversionSpeedTheta, // theta: e.g. 0.14 / day
    double VolatilitySigma, // sigma: e.g. 0.018 / sqrt(day)
    double AugmentedDickeyFullerPValue, // p-value: e.g. 0.012 (stationary if < 0.05)
    double DaysSinceStructuralBreak // e.g. 180.0
);

public sealed record StatisticalArbitrageAnalysisResult(
    string PairSymbol,
    double MeanReversionHalfLifeDays,
    double NormalizedSpreadZScore,
    double EquilibriumStandardDeviation,
    string StatArbRegime,
    string QuantitativeExecutionGuidance,
    bool IsStatArbTradeValid
);

public interface IStatisticalArbitrageOrnsteinUhlenbeckEngine
{
    StatisticalArbitrageAnalysisResult AnalyzePairMeanReversion(StatisticalArbitrageTelemetry telemetry);
}
