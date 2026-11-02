namespace SGrowing.Api.Modules.Quantum.SuperconductingFluxoniumCoherence;

public sealed record SuperconductingFluxoniumTelemetry(
    string QubitId,
    double ChargingEnergyGigahertz, // E_C: e.g. 1.0 GHz
    double InductiveEnergyGigahertz, // E_L: e.g. 0.5 GHz
    double JosephsonEnergyGigahertz, // E_J: e.g. 4.5 GHz
    double ExternalFluxBiasPhi0, // Phi_ext / Phi_0: e.g. 0.50 sweet spot
    double ReadoutResonatorFrequencyGigahertz, // omega_r: e.g. 7.2 GHz
    double ResonatorCouplingRateMegahertz, // g: e.g. 60.0 MHz
    double MeasuredT1RelaxationMicroseconds, // T_1: e.g. 1200 us
    double MeasuredT2DephasingMicroseconds // T_2*: e.g. 850 us
);

public sealed record SuperconductingFluxoniumAnalysisResult(
    string QubitId,
    double TransitionFrequencyGigahertz,
    double DispersiveShiftMegahertz,
    double FluxNoiseSensitivityGigahertzPerPhi0,
    string QubitCoherenceRegime,
    string ControlGuidance,
    bool IsFaultTolerantCoherenceAchieved
);

public interface ISuperconductingFluxoniumCoherenceEngine
{
    SuperconductingFluxoniumAnalysisResult AnalyzeFluxoniumCoherence(SuperconductingFluxoniumTelemetry telemetry);
}
