namespace SGrowing.Api.Modules.Aerospace.HypersonicScramjetIsolatorShockTrain;

public sealed record HypersonicScramjetTelemetry(
    string ScramjetEngineId,
    double FlightMachNumber, // M: e.g. 6.5
    double IsolatorInletMachNumber, // M_in: e.g. 2.4
    double IsolatorHydraulicDiameterMeters, // D: e.g. 0.08 m
    double IsolatorLengthMeters, // L: e.g. 0.85 m
    double IsolatorInletStaticPressureKilopascals, // P_in: e.g. 45.0 kPa
    double CombustorBackpressureKilopascals, // P_out: e.g. 135.0 kPa
    double FanningFrictionFactor // f: e.g. 0.004
);

public sealed record HypersonicScramjetAnalysisResult(
    string ScramjetEngineId,
    double BackpressureRatio,
    double PredictedShockTrainLengthMeters,
    double UnstartSafetyMarginMeters,
    double UnstartSafetyMarginPercent,
    string IsolatorFlowRegime,
    string CombustorControlGuidance,
    bool IsIsolatorFlowStable
);

public interface IHypersonicScramjetIsolatorShockTrainEngine
{
    HypersonicScramjetAnalysisResult AnalyzeIsolatorShockTrain(HypersonicScramjetTelemetry telemetry);
}
