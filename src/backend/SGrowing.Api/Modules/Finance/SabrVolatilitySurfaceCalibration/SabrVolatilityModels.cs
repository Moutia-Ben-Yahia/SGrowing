namespace SGrowing.Api.Modules.Finance.SabrVolatilitySurfaceCalibration;

public sealed record SabrVolatilityTelemetry(
    string SurfaceId,
    double ForwardPrice, // F: e.g. 100.0
    double StrikePrice, // K: e.g. 100.0
    double TimeToMaturityYears, // T: e.g. 1.0
    double AlphaInitialVol, // alpha: e.g. 0.22
    double BetaElasticity, // beta: e.g. 0.5
    double RhoCorrelation, // rho: e.g. -0.45 (-1 < rho < 1)
    double NuVolOfVol // nu: e.g. 0.55
);

public sealed record SabrVolatilityAnalysisResult(
    string SurfaceId,
    double ImpliedBlackVolatilityPercent,
    double AtmLevellingFactor,
    double AtmSkewSlopeBps,
    string VolatilitySurfaceRegime,
    string CalibrationGuidance,
    bool IsArbitrageFree
);

public interface ISabrVolatilitySurfaceCalibrationEngine
{
    SabrVolatilityAnalysisResult AnalyzeSabrSurface(SabrVolatilityTelemetry telemetry);
}
