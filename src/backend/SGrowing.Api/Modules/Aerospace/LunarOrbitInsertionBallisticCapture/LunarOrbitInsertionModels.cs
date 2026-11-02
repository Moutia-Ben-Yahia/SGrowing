namespace SGrowing.Api.Modules.Aerospace.LunarOrbitInsertionBallisticCapture;

public record LunarOrbitInsertionTelemetry
{
    public required string MissionTrajectoryId { get; init; }
    public string TransferProfileType { get; init; } = "LowEnergyBallisticLunarTransfer_WSB"; // or "DirectPatchedConicHohmann"
    public double JacobiEnergyConstant { get; init; } = 3.00085; // C_J in CR3BP
    public double OsculatingMoonTwoBodyEnergyKm2PerS2 { get; init; } = -0.085; // Negative indicates ballistic gravitational capture
    public double PeriluneAltitudeKm { get; init; } = 125.0; // Target 100 - 200 km
    public double PlannedInsertionDeltaVMetersPerSecond { get; init; } = 645.0; // ~600-750 m/s for BLT
    public double PropellantMarginPercent { get; init; } = 18.5; // Target >= 15%
    public double BurnExecutionErrorMetersPerSecond { get; init; } = 0.08; // Target <= 0.15 m/s
    public double SolarTidalPerturbationEccentricityDriftPerOrbit { get; init; } = 0.0012; // Third-body stability
    public double SpacecraftWetMassKg { get; init; } = 1450.0;
}

public record LunarOrbitInsertionEvaluation
{
    public required string MissionTrajectoryId { get; init; }
    public double CalculatedCaptureStabilityMargin { get; init; }
    public double DeltaVSavingsVsDirectMetersPerSecond { get; init; }
    public bool IsBallisticallyCaptured { get; init; }
    public bool IsPeriluneAltitudeSafe { get; init; }
    public bool IsDeltaVExecutionAccurate { get; init; }
    public bool IsLunarInsertionGoForBurn { get; init; }
    public List<string> ActiveRegimes { get; init; } = new();
    public List<string> RecommendedMitigations { get; init; } = new();
    public DateTime EvaluatedAtUtc { get; init; } = DateTime.UtcNow;
}
