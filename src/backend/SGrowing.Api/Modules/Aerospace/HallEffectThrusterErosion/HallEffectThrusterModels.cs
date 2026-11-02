namespace SGrowing.Api.Modules.Aerospace.HallEffectThrusterErosion;

public sealed record HallEffectThrusterTelemetry
{
    public required string ThrusterUnitId { get; init; }
    public string PropellantType { get; init; } = "Xenon"; // Xenon or Krypton
    public double DischargeVoltageVolts { get; init; } = 300.0; // V_d (V)
    public double DischargeCurrentAmps { get; init; } = 15.0; // I_d (A)
    public double AnodeMassFlowRateMgPerSec { get; init; } = 5.2; // mg/s
    public double PeakBreathingModeOscillationAmps { get; init; } = 3.2; // Delta I_d (10-30 kHz)
    public double WallIonCurrentDensityMaPerCm2 { get; init; } = 1.4; // j_ion (mA/cm^2)
    public double IonImpactAngleDegrees { get; init; } = 65.0; // theta from normal
    public double MeanIonImpactEnergyEv { get; init; } = 85.0; // eV
    public double CeramicWallDensityGPerCm3 { get; init; } = 2.1; // Boron Nitride (BN-SiO2) ~ 2.1 g/cm^3
    public double PlumeDivergenceHalfAngleDegrees { get; init; } = 22.0; // plume beam divergence
}

public sealed record HallEffectThrusterAnalysisResult
{
    public required string ThrusterUnitId { get; init; }
    public required double SputteringErosionRateMicronsPerKiloHour { get; init; }
    public required double ProjectedWallLifeHours { get; init; }
    public required double BreathingModeOscillationFraction { get; init; }
    public required double ThrustMilliNewtons { get; init; }
    public required double SpecificImpulseSeconds { get; init; }
    public required double TotalElectricalEfficiencyPercent { get; init; }
    public required bool IsWallErosionWithinLifeEnvelope { get; init; }
    public required bool IsBreathingModeStable { get; init; }
    public required bool IsEfficiencyTargetMet { get; init; }
    public required string ThrusterOperationRegime { get; init; }
    public required string OperationalGuidance { get; init; }
}
