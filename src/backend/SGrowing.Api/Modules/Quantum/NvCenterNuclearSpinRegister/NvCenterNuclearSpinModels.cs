namespace SGrowing.Api.Modules.Quantum.NvCenterNuclearSpinRegister;

public sealed record NvCenterNuclearSpinTelemetry(
    string DiamondRegisterId,
    double AxialMagneticFieldGauss, // B_z: e.g. 510.0 G (ESLAC near 500-515 G)
    double GreenLaserOpticalPumpingPowerMilliwatts, // e.g. 15.0 mW
    double OpticalPumpingDurationMicroseconds, // e.g. 5.0 us
    int CoupledNuclearSpinsCount, // e.g. 3 spins (14N + 2x 13C)
    double HyperfineCouplingMegahertz, // A_zz: e.g. 2.16 MHz
    double MeasuredNuclearSpinPolarizationPercent, // e.g. 96.5%
    double NuclearQubitCoherenceT2Seconds // T_2^(n): e.g. 1.85 s
);

public sealed record NvCenterNuclearSpinAnalysisResult(
    string DiamondRegisterId,
    double EslacFieldDetuningGauss,
    double EffectivePolarizationFidelityPercent,
    double QuantumMemoryQualityFactor,
    string NuclearSpinRegime,
    string HyperpolarizationGuidance,
    bool IsNuclearMemoryFaultTolerant
);

public interface INvCenterNuclearSpinRegisterEngine
{
    NvCenterNuclearSpinAnalysisResult AnalyzeNuclearRegister(NvCenterNuclearSpinTelemetry telemetry);
}
