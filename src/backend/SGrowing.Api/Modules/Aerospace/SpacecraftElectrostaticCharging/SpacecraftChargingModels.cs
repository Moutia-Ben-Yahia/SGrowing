namespace SGrowing.Api.Modules.Aerospace.SpacecraftElectrostaticCharging;

public record SpacecraftChargingTelemetry
{
    public required string SpacecraftSubsystemId { get; init; }
    public string OrbitEnvironment { get; init; } = "GeosynchronousSubstorm_GEO"; // GEO, Polar_LEO, OuterPlanetary
    public double SpacecraftFramePotentialVolts { get; init; } = -85.0; // Clamped by neutralizer; can reach -5000V
    public double DifferentialDielectricPotentialVolts { get; init; } = 280.0; // Target <= 450 V
    public double EnergeticElectronFluxPerCm2Sec { get; init; } = 4.5e7; // E > 10 keV
    public double DeepDielectricCurrentDensityPicoAmpsPerCm2 { get; init; } = 0.95; // Target <= 2.0 pA/cm2
    public double PlasmaContactorEmissionCurrentMilliAmps { get; init; } = 22.5; // Target >= 15.0 mA
    public double SolarArrayInterconnectElectricFieldVoltsPerCm { get; init; } = 1800.0; // Target <= 3500 V/cm
    public double ArcDischargeTransientRatePerHour { get; init; } = 0.0; // Target == 0
    public bool IsHollowCathodeNeutralizerActive { get; init; } = true;
}

public record SpacecraftChargingEvaluation
{
    public required string SpacecraftSubsystemId { get; init; }
    public double EsdBreakdownVoltageMarginVolts { get; init; }
    public double FramePotentialSafetyMarginVolts { get; init; }
    public bool IsDifferentialPotentialSafe { get; init; }
    public bool IsDeepDielectricChargingSuppressed { get; init; }
    public bool IsPlasmaContactorOperational { get; init; }
    public bool IsSpacecraftElectrostaticSafe { get; init; }
    public List<string> ActiveRegimes { get; init; } = new();
    public List<string> RecommendedMitigations { get; init; } = new();
    public DateTime EvaluatedAtUtc { get; init; } = DateTime.UtcNow;
}
