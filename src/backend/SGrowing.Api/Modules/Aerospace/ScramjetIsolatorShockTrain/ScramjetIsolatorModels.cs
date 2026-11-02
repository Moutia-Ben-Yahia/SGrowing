namespace SGrowing.Api.Modules.Aerospace.ScramjetIsolatorShockTrain;

public record ScramjetIsolatorTelemetry(
    string EngineId,
    double FlightMachNumber,
    double IsolatorInletMachNumber,
    double IsolatorDuctHeightMm,
    double IsolatorDuctLengthMm,
    double InletStaticPressureKpa,
    double CombustorBackpressureKpa,
    double BoundaryLayerMomentumThicknessMm,
    double FuelFlowRateKgPerSec);

public record ScramjetIsolatorAnalysisResult(
    string EngineId,
    double ShockTrainLengthMm,
    double ShockTrainLeadingEdgePositionMm,
    double UnstartSafetyMarginPercent,
    bool IsUnstartImminent,
    string EngineOperatingRegime,
    string RecommendedControlActuation,
    string FlightSafetyAdvisory);
