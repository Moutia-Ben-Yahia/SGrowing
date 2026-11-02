namespace SGrowing.Api.Modules.Quantum.CavityOptomechanicsPhononSqueezing;

public sealed record CavityOptomechanicsTelemetry(
    string ResonatorId,
    double MechanicalFrequencyMegahertz, // Omega_m: e.g. 5.0 MHz
    double MechanicalQualityFactor, // Q_m: e.g. 1.2e7
    double CavityLinewidthKilohertz, // kappa: e.g. 250 kHz (resolved sideband: kappa << Omega_m)
    double LaserDetuningMegahertz, // Delta: e.g. -5.0 MHz (red-detuned sideband cooling)
    double OptomechanicalCouplingRateG0Hertz, // g_0: e.g. 200 Hz
    double IntracavityPhotonCount, // n_c: e.g. 500,000 photons
    double EnvironmentalBathTemperatureKelvin, // T_bath: e.g. 0.05 K (50 mK dilution fridge)
    double SqueezingDriveModulationDepthPercent // eta_sq: e.g. 15.0%
);

public sealed record CavityOptomechanicsAnalysisResult(
    string ResonatorId,
    double InitialThermalPhononOccupancy,
    double EffectiveCooledPhononOccupancy,
    double OptomechanicalCooperativity,
    double QuadratureFluctuationVariance,
    double SqueezingBelowSqlDecibels,
    string OptomechanicalRegime,
    string ControlGuidance,
    bool IsSubSqlSqueezingAchieved
);

public interface ICavityOptomechanicsPhononSqueezingEngine
{
    CavityOptomechanicsAnalysisResult AnalyzePhononSqueezing(CavityOptomechanicsTelemetry telemetry);
}
