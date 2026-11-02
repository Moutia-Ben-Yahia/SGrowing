namespace SGrowing.Api.Modules.Aerospace.SpaceElevatorTether;

public record ClimberTelemetry(
    string ClimberId,
    double AltitudeKm,
    double AscentVelocityKmPerSec,
    double MassMetricTons
);

public record TetherDynamicState(
    string TetherId,
    double TetherLengthKm = 100000.0,
    double BaseAnchorTensionGigaPascals = 65.0,
    double GeoTensionGigaPascals = 78.0,
    double MaxAllowableTensionGigaPascals = 100.0,
    double TransverseOscillationAmplitudeMeters = 120.0,
    double LongitudinalStrainPercent = 0.85,
    IReadOnlyList<ClimberTelemetry>? ActiveClimbers = null
);

public record CounterweightThrusterConfig(
    double MaxThrustKiloNewtons = 50.0,
    double SpecificImpulseSec = 3200.0
);

public record SpaceElevatorTetherRequest(
    TetherDynamicState State,
    CounterweightThrusterConfig ThrusterConfig
);

public record CounterweightThrusterCommand(
    string Axis,
    double ThrustKiloNewtons,
    double BurnDurationSeconds,
    string Objective
);

public record TetherDynamicStabilizationPlan(
    string TetherId,
    double PeakTensionGigaPascals,
    double TotalCoriolisDeflectionForceKiloNewtons,
    double TransverseWaveVelocityKmPerSec,
    bool ResonantInstabilityDetected,
    string TensionSafetyMarginStatus,
    IReadOnlyList<CounterweightThrusterCommand> ThrusterCommands,
    string RecommendedClimberCadenceDirective
);
