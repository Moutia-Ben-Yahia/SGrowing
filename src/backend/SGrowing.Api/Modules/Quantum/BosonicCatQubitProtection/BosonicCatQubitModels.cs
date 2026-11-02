namespace SGrowing.Api.Modules.Quantum.BosonicCatQubitProtection;

public sealed record BosonicCatQubitTelemetry(
    string CavityQubitId,
    double CatStateMeanPhotonNumber, // |alpha|^2: e.g. 4.5 photons
    double SinglePhotonLossRateKappa1Kilohertz, // kappa_1: e.g. 5.0 kHz
    double TwoPhotonDriveRateEpsilon2Kilohertz, // epsilon_2: e.g. 450.0 kHz
    double TwoPhotonDissipationRateKappa2Kilohertz, // kappa_2: e.g. 120.0 kHz
    double MeasuredBitFlipLifetimeMilliseconds, // T_X: e.g. 850.0 ms
    double MeasuredPhaseFlipLifetimeMicroseconds // T_Z: e.g. 4.2 us
);

public sealed record BosonicCatQubitAnalysisResult(
    string CavityQubitId,
    double NoiseBiasRatio,
    double ConfinementFidelityPercent,
    string CatQubitRegime,
    string ErrorCorrectionGuidance,
    bool IsNoiseBiasPreserved
);

public interface IBosonicCatQubitProtectionEngine
{
    BosonicCatQubitAnalysisResult AnalyzeCatQubitProtection(BosonicCatQubitTelemetry telemetry);
}
