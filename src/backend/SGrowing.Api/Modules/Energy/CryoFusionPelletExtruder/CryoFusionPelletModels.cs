namespace SGrowing.Api.Modules.Energy.CryoFusionPelletExtruder;

public record ExtruderCryostatTelemetry(
    string ExtruderId,
    double IceBarrelTemperatureKelvin = 15.2, // Cryogenic solid D-T ice
    double ExtrusionPressureMegaPascals = 3.5,
    double ExtrusionSpeedMmPerSec = 45.0,
    double PelletDiameterMm = 4.0,
    double PelletLengthMm = 5.0,
    double InjectionFrequencyHertz = 25.0,
    double GasGunMuzzleVelocityMPerSec = 1350.0,
    double FuelCompositionTritiumFraction = 0.50 // 50% D, 50% T
);

public record PelletExtrusionLimits(
    double MaxAllowableBarrelTemperatureKelvin = 17.5, // Triple point of D2 ~ 18.7K
    double MaxAllowableExtrusionPressureMpa = 6.0,
    double MinAllowableMuzzleVelocityMPerSec = 1000.0,
    double TargetSeparatrixPenetrationDepthMeters = 0.35
);

public record PelletExtrusionEvaluationRequest(
    ExtruderCryostatTelemetry Telemetry,
    PelletExtrusionLimits Limits
);

public record PelletExtrusionAssessment(
    string ExtruderId,
    double DeuteriumTritiumFuelingRateGramsPerSec,
    double EstimatedSeparatrixPenetrationDepthMeters,
    bool CryogenicThermalSublimationRiskDetected,
    bool MechanicalJammingRiskDetected,
    string ExtruderOperationalRegime,
    string RecommendedCryostatControlDirective
);
