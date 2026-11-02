namespace SGrowing.Api.Modules.Aerospace.SpacecraftAerocaptureGuidance;

public record SpacecraftEntryState(
    string VehicleId,
    double AltitudeKm,
    double VelocityKmPerSec,
    double FlightPathAngleDegrees, // negative entering atmosphere, e.g. -11.5 deg
    double CurrentBankAngleDegrees,
    double VehicleMassKg = 2500.0,
    double LiftToDragRatio = 0.32
);

public record PlanetaryAtmosphereModel(
    string TargetBody, // "Mars", "Titan", "Venus", "Neptune"
    double ScaleHeightKm = 11.1,
    double SurfaceDensityKgPerM3 = 0.020,
    double PlanetaryRadiusKm = 3389.5,
    double GravitationalParamMuKm3S2 = 42828.3
);

public record AerocaptureCorridorLimits(
    double TargetApoapsisKm = 2000.0,
    double MaxAllowableHeatFluxWattsPerCm2 = 450.0,
    double MaxAllowableDecelerationGs = 8.0
);

public record AerocaptureGuidanceRequest(
    SpacecraftEntryState State,
    PlanetaryAtmosphereModel Atmosphere,
    AerocaptureCorridorLimits CorridorLimits
);

public record AerocaptureGuidanceDecision(
    string VehicleId,
    string TargetBody,
    double RecommendedBankAngleDegrees,
    double ProjectedApoapsisKm,
    double PeakHeatFluxWattsPerCm2,
    double PeakDecelerationGs,
    bool CorridorSatisfied,
    string GuidanceMode, // "LIFT_UP_SKIP_PREVENT", "LIFT_DOWN_DIVE_PREVENT", "BANK_REVERSAL_NOMINAL"
    string ManeuverDirective
);
