namespace SGrowing.Api.Modules.Aerospace.ReactionWheelMicroVibrationJitter;

public sealed record ReactionWheelJitterTelemetry
{
    public required string WheelAssemblyId { get; init; }
    public string SpacecraftPlatformName { get; init; } = "DeepSpace-Space-Telescope";
    public double RotorSpeedRpm { get; init; } = 3200.0; // 0 - 6000 RPM
    public double StaticMassImbalanceGCm { get; init; } = 0.22; // U_s (g*cm, target <= 0.5)
    public double DynamicMassImbalanceGCm2 { get; init; } = 0.65; // U_d (g*cm^2, target <= 1.2)
    public double IsolationPlatformDampingRatio { get; init; } = 0.22; // zeta (target >= 0.15)
    public double StructuralResonanceProximityPercent { get; init; } = 18.0; // distance to nearest mode (%)
    public double BearingHarmonicDisturbanceFactor { get; init; } = 1.15; // bearing waviness harmonic factor
    public double MaxAllowableLosJitterNrad { get; init; } = 15.0; // Line of sight jitter target (nrad)
}

public sealed record ReactionWheelJitterAnalysisResult
{
    public required string WheelAssemblyId { get; init; }
    public required double CentrifugalRadialDisturbanceForceNewtons { get; init; }
    public required double RockingDynamicDisturbanceTorqueMilliNm { get; init; }
    public required double PredictedPayloadLosJitterNrad { get; init; }
    public required double JitterMarginRatioPercent { get; init; }
    public required bool IsStaticImbalanceWithinSpec { get; init; }
    public required bool IsDynamicImbalanceWithinSpec { get; init; }
    public required bool IsLosJitterRequirementMet { get; init; }
    public required bool IsSpeedNearStructuralResonance { get; init; }
    public required string WheelJitterStabilityRegime { get; init; }
    public required string AttitudeControlMitigationPlan { get; init; }
}
