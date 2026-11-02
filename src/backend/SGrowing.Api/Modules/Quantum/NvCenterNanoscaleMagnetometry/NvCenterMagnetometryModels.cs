namespace SGrowing.Api.Modules.Quantum.NvCenterNanoscaleMagnetometry;

public sealed record NvCenterMagnetometryTelemetry(
    string SensorId,
    double ZeroFieldSplittingFrequencyGigaHertz, // D ~ 2.870 GHz
    double AppliedMicrowaveFrequencyGigaHertz,
    double MeasuredLowerDipFrequencyGigaHertz, // f_minus
    double MeasuredUpperDipFrequencyGigaHertz, // f_plus
    double OpticalFringeContrastPercent, // ODMR dip contrast % (e.g. 12-25%)
    double SpinDephasingCoherenceTimeT2StarMicroseconds, // T2* (e.g. 1.5 - 5.0 us)
    double OpticalReadoutPhotonCountPerShot,
    double SensorStandOffDistanceNanometers // e.g. 15 nm
);

public sealed record NvCenterMagnetometryAnalysisResult(
    string SensorId,
    double ReconstructedZeemanSplittingMegaHertz,
    double CalculatedMagneticFieldMilliTesla,
    double MagneticSensitivityNanoTeslaPerRootHertz,
    string OdmrMagnetometryRegime,
    string NanoscaleProbeGuidance,
    bool IsSensitivityOptimal
);

public interface INvCenterNanoscaleMagnetometryEngine
{
    NvCenterMagnetometryAnalysisResult AnalyzeMagnetometry(NvCenterMagnetometryTelemetry telemetry);
}
