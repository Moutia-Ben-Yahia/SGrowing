namespace SGrowing.Api.Modules.Aerospace.AerobrakingAtmosphericDrag;

public record AerobrakingDragTelemetry
{
    public required string PassIdentifier { get; init; }
    public string TargetPlanet { get; init; } = "Mars"; // Mars, Venus, Titan
    public double PeriapsisAltitudeKm { get; init; } = 124.0; // Nominal corridor 120 - 135 km
    public double AtmosphericDensityKgPerM3 { get; init; } = 4.5e-9; // Atmospheric density at periapsis
    public double DustStormDensitySurgeFactor { get; init; } = 1.15; // <= 1.80
    public double SpacecraftVelocityAtPeriapsisKmPerS { get; init; } = 4.85; // v_peri
    public double SpacecraftDragAreaM2 { get; init; } = 32.5; // Drag cross section
    public double SpacecraftMassKg { get; init; } = 1250.0;
    public double PeakSolarArrayTemperatureCelsius { get; init; } = 135.0; // <= 165 C (limit 175 C)
    public double AerodynamicBendingTorqueNewtonMeters { get; init; } = 32.0; // <= 45 N.m
}

public record AerobrakingDragEvaluation
{
    public required string PassIdentifier { get; init; }
    public double CalculatedPassDeltaVMetersPerSecond { get; init; }
    public double PeakStagnationHeatFluxWattsPerCm2 { get; init; }
    public double OrbitalPeriodDecayMinutesPerPass { get; init; }
    public bool IsPeriapsisCorridorSafe { get; init; }
    public bool IsThermalLoadWithinLimits { get; init; }
    public bool IsStructuralAeroTorqueSafe { get; init; }
    public bool IsPassGoForAtmosphericEntry { get; init; }
    public List<string> ActiveRegimes { get; init; } = new();
    public List<string> RecommendedMitigations { get; init; } = new();
    public DateTime EvaluatedAtUtc { get; init; } = DateTime.UtcNow;
}
