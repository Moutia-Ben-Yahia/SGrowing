namespace SGrowing.Api.Modules.Quantum.TopologicalMajoranaBraiding;

public sealed record TopologicalMajoranaTelemetry(
    string QubitJunctionId,
    double NanowireLengthMicrometers, // L: e.g. 1.8 um
    double MajoranaLocalizationLengthNanometers, // xi_M: e.g. 180 nm
    double AppliedZeemanMagneticFieldTesla, // B: e.g. 0.85 T
    double CriticalTopologicalFieldThresholdTesla, // B_c: e.g. 0.40 T
    double TopologicalSuperconductingGapMicroElectronVolts, // Delta_top: e.g. 120 ueV
    double BraidingOperationDurationNanoseconds, // T_braid: e.g. 45 ns
    double QuasiparticlePoisoningTimeMicroseconds, // tau_qp: e.g. 15 us
    double CryogenicBaseTemperatureMilliKelvin // T: e.g. 20 mK
);

public sealed record TopologicalMajoranaAnalysisResult(
    string QubitJunctionId,
    double MajoranaWavefunctionOverlapSplittingMicroEv,
    double AdiabaticBraidingFidelityPercent,
    double ParityProtectionLifetimeMicroseconds,
    string TopologicalQuantumRegime,
    string BraidingControlGuidance,
    bool IsBraidingOperationFaultTolerant
);

public interface ITopologicalMajoranaBraidingEngine
{
    TopologicalMajoranaAnalysisResult AnalyzeMajoranaBraiding(TopologicalMajoranaTelemetry telemetry);
}
