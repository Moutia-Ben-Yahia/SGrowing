namespace SGrowing.Api.Modules.Aerospace.SpacecraftRcsPlumeImpingement;

public record SpacecraftRcsTelemetry
{
    public required string SpacecraftVehicleUid { get; init; }
    public string PropellantCombination { get; init; } = "MMH_NTO_Hypergolic"; // or "Hydrazine_Monopropellant"
    public double MinimumImpulseBitDurationMilliseconds { get; init; } = 15.0; // Target >= 10 ms
    public double ValveJitterMilliseconds { get; init; } = 0.85; // Target <= 1.2 ms
    public double PeakPlumeImpingementPressurePascals { get; init; } = 8.5; // Target <= 12.0 Pa
    public double PlumeHeatFluxKiloWattsPerMeter2 { get; init; } = 3.2; // Target <= 4.5 kW/m2
    public double InducedDisturbanceTorqueNewtonMeters { get; init; } = 11.4; // Target <= 18.0 N*m
    public double RcsValveSeatTemperatureCelsius { get; init; } = 108.0; // Target <= 140 C
    public double StationKeepingPwmDutyCyclePercent { get; init; } = 6.5; // Target <= 14.0%
    public double AttitudeDeadbandErrorDegrees { get; init; } = 0.045; // Target <= 0.08 deg
}

public record SpacecraftRcsEvaluation
{
    public required string SpacecraftVehicleUid { get; init; }
    public double PlumeImpingementSafetyMarginPercent { get; init; }
    public double ValveThermalHeadroomCelsius { get; init; }
    public bool IsPlumeImpingementTolerated { get; init; }
    public bool IsRcsValveThermalStateStable { get; init; }
    public bool IsDutyCycleFuelOptimal { get; init; }
    public bool IsRcsFiringApprovedForProximityOps { get; init; }
    public List<string> ActiveRegimes { get; init; } = new();
    public List<string> RecommendedMitigations { get; init; } = new();
    public DateTime EvaluatedAtUtc { get; init; } = DateTime.UtcNow;
}
