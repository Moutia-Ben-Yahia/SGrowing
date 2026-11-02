namespace SGrowing.Api.Modules.Quantum.NeutralAtomRydbergGates;

public sealed record NeutralAtomRydbergTelemetry(
    string RegisterId,
    int NeutralAtomQubitCount,
    double InteratomicSpacingMicrometers,
    double RydbergPrincipalQuantumNumberN,
    double LaserRabiFrequencyMegahertz,
    double VanDerWaalsC6CoefficientGhzMicrometer6,
    double SpontaneousEmissionLifetimeMicroseconds,
    double OpticalTweezerPositionJitterNanometers
);

public sealed record NeutralAtomRydbergAnalysisResult(
    string RegisterId,
    double RydbergBlockadeRadiusMicrometers,
    double CalculatedTwoQubitGateFidelityPercent,
    double SpontaneousEmissionInfidelityBasisPoints,
    double BlockadeInteractionEnergyGigaHertz,
    string RydbergGateRegime,
    string ControlGuidance,
    bool IsGateFidelityHigh
);

public interface INeutralAtomRydbergGatesEngine
{
    NeutralAtomRydbergAnalysisResult AnalyzeRydbergGate(NeutralAtomRydbergTelemetry telemetry);
}
