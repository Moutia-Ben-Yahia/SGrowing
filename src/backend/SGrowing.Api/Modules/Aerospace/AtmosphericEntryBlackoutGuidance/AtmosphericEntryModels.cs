namespace SGrowing.Api.Modules.Aerospace.AtmosphericEntryBlackoutGuidance;

public record AtmosphericEntryTelemetry
{
    public required string EntryVehicleMissionId { get; init; }
    public string VehicleConfiguration { get; init; } = "LiftingBody_Capsule"; // LiftingBody, MidLiftToDrag, Starship
    public double FlightPathAngleDegrees { get; init; } = -5.85; // Optimal corridor -5.2 to -6.8 deg
    public double EntryVelocityKilometersPerSecond { get; init; } = 11.2; // Lunar/Mars return ~11 km/s
    public double LiftToDragRatio { get; init; } = 0.38; // L/D
    public double PeakDecelerationGs { get; init; } = 3.85; // Target <= 4.5 g
    public double StagnationHeatFluxWattsPerCm2 { get; init; } = 195.0; // Target <= 250 W/cm2
    public double PeakPlasmaSheathElectronDensityPerCm3 { get; init; } = 4.5e13; // 1e13 - 1e15 cm^-3
    public double SbandBlackoutDurationSeconds { get; init; } = 240.0; // Normal ~ 180 - 300 s
    public bool IsKaBandRelayTransceiverActive { get; init; } = true;
    public double MagneticWindowFieldTesla { get; init; } = 0.45; // Magnetohydrodynamic window
}

public record AtmosphericEntryEvaluation
{
    public required string EntryVehicleMissionId { get; init; }
    public double PlasmaCutoffFrequencyGigaHertz { get; init; }
    public double ReEntryCorridorSafetyMarginPercent { get; init; }
    public bool IsSkipOutOvershootAverted { get; init; }
    public bool IsPeakDecelerationThermalSafe { get; init; }
    public bool IsContinuousTelemetryPreserved { get; init; }
    public bool IsEntryGuidanceGoForLanding { get; init; }
    public List<string> ActiveRegimes { get; init; } = new();
    public List<string> RecommendedMitigations { get; init; } = new();
    public DateTime EvaluatedAtUtc { get; init; } = DateTime.UtcNow;
}
