namespace SGrowing.Api.Modules.Aerospace.LunarCraterTetherRappelling;

public sealed record LunarCraterTetherTelemetry(
    string RoverId,
    double CraterWallSlopeAngleDegrees, // theta: e.g. 42.0 deg
    double RoverMassKg, // m: e.g. 280.0 kg
    double RegolithFrictionCoefficient, // mu_s: e.g. 0.42
    double AnchorPullOutCapacityNewtons, // F_anchor: e.g. 3200.0 N
    double MeasuredTetherTensionNewtons, // T_measured: e.g. 280.0 N
    double RoverTrackWidthMeters, // w: e.g. 1.4 m
    double CenterOfGravityHeightMeters // h_cg: e.g. 0.65 m
);

public sealed record LunarCraterTetherAnalysisResult(
    string RoverId,
    double TheoreticalLineTensionNewtons,
    double AnchorSafetyFactor,
    double StaticRolloverThresholdDegrees,
    string RappellingRegime,
    string WinchControlGuidance,
    bool IsRappellingDescentSafe
);

public interface ILunarCraterTetherRappellingEngine
{
    LunarCraterTetherAnalysisResult AnalyzeRappellingDynamics(LunarCraterTetherTelemetry telemetry);
}
