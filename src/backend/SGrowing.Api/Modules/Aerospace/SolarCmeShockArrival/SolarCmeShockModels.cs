namespace SGrowing.Api.Modules.Aerospace.SolarCmeShockArrival;

public record SolarCmeShockTelemetry
{
    public required string EventIdentifier { get; init; }
    public string ObservingSpacecraft { get; init; } = "SolarOrbiter_EUI"; // SolarOrbiter, SOHO_LASCO, STEREO, DSCOVR
    public double EuvDopplerLiftOffVelocityKmPerS { get; init; } = 920.0; // >= 750 km/s
    public double BackgroundSolarWindSpeedKmPerS { get; init; } = 410.0; // w_solar ~ 400 km/s
    public double AerodynamicDragDecelerationCoeff { get; init; } = 1.45e-7; // gamma ~ 1.5e-7 km^-1
    public double CoronalMassEjectionHalfWidthDegrees { get; init; } = 48.0; // Halo CME > 45 deg
    public double PredictedGeomagneticDstIndexNanoTesla { get; init; } = -125.0; // Severe storm < -150 nT
    public double SolarEnergeticProtonFluxPfu { get; init; } = 45.0; // > 10 MeV protons (threshold 100 pfu)
    public double ShockTransitDistanceAu { get; init; } = 1.0; // 1 AU for Earth, 0.28 AU for Parker, 0.7 AU for Venus
}

public record SolarCmeShockEvaluation
{
    public required string EventIdentifier { get; init; }
    public double CalculatedTransitTimeHours { get; init; }
    public double ArrivalSpeedAtTargetKmPerS { get; init; }
    public double ShockArrivalUncertaintyWindowHours { get; init; }
    public bool IsEarthDirectedGeoeffective { get; init; }
    public bool IsSevereSuperstormPredicted { get; init; }
    public bool IsProtonRadiationHazardCritical { get; init; }
    public bool IsDefenseGridPreparednessReady { get; init; }
    public List<string> ActiveRegimes { get; init; } = new();
    public List<string> RecommendedMitigations { get; init; } = new();
    public DateTime EvaluatedAtUtc { get; init; } = DateTime.UtcNow;
}
