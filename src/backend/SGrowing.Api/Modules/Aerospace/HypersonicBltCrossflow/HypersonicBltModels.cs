namespace SGrowing.Api.Modules.Aerospace.HypersonicBltCrossflow;

public record HypersonicBltTelemetry(
    string VehicleId,
    double MachNumber,
    double FreestreamDynamicPressureKpa,
    double AcousticSensorFrequencyKhz,
    double PressureFluctuationRmsRatio,
    double CrossflowReynoldsNumber,
    double SurfaceLaminarHeatFluxKwM2,
    double ThermalProtectionTileLimitKwM2);

public record HypersonicBltAnalysisResult(
    string VehicleId,
    string FlowRegime,
    double TurbulentHeatFluxMultiplier,
    double PredictedSurfaceHeatFluxKwM2,
    double ThermalProtectionSafetyMarginPercent,
    bool IsTileOverheatRiskDetected,
    string ActiveFlowControlCommand,
    string FlightControlAdvisory);
