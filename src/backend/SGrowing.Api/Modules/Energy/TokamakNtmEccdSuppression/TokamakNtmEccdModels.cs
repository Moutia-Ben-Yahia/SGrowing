namespace SGrowing.Api.Modules.Energy.TokamakNtmEccdSuppression;

public record TokamakNtmEccdTelemetry
{
    public required string PulseDischargeId { get; init; }
    public string TokamakFacility { get; init; } = "ITER"; // ITER, SPARC, DIII-D, ASDEX
    public double MagneticIslandWidthCm { get; init; } = 2.4; // w (crit ~ 1.5 - 2.0 cm, locked limit ~ 7.5 cm)
    public double CriticalSeedIslandWidthCm { get; init; } = 1.8;
    public double IslandRotationFrequencyKhz { get; init; } = 7.5; // Mode rotation > 2.0 kHz
    public double PoloidalBetaTheta { get; init; } = 1.35; // beta_theta
    public double EccdInjectedPowerMegaWatts { get; init; } = 12.0; // P_ECCD
    public double EccdDepositionRadialOffsetCm { get; init; } = 0.45; // delta_r <= 1.2 cm
    public double EccdDepositionWidthCm { get; init; } = 2.2;
    public double NormalizedBetaDegradationPercent { get; init; } = 6.5; // <= 15.0%
    public double SafetyFactorAtRationalSurfaceQ { get; init; } = 2.0; // q = 2/1 surface
}

public record TokamakNtmEccdEvaluation
{
    public required string PulseDischargeId { get; init; }
    public double CalculatedIslandGrowthRateInvSec { get; init; }
    public double EccdStabilizationEfficiencyPercent { get; init; }
    public double ProjectedIslandSuppressionTimeMs { get; init; }
    public bool IsEccdBeamAligned { get; init; }
    public bool IsIslandWidthControlled { get; init; }
    public bool IsModeLockedDisruptionAverted { get; init; }
    public bool IsPlasmaDischargeStable { get; init; }
    public List<string> ActiveRegimes { get; init; } = new();
    public List<string> RecommendedMitigations { get; init; } = new();
    public DateTime EvaluatedAtUtc { get; init; } = DateTime.UtcNow;
}
