namespace SGrowing.Api.Modules.Aerospace.HypersonicWaveriderShock;

public record WaveriderFlightState(
    string VehicleId,
    double MachNumber = 7.5,
    double AltitudeKm = 32.0,
    double DynamicPressureKPa = 65.0,
    double AngleOfAttackDegrees = 6.0,
    double LeadingEdgeTemperatureKelvin = 1850.0,
    double ActiveTranspirationFlowRateGramsPerSecM2 = 15.0
);

public record WaveriderSafetyEnvelope(
    double MaxAllowableLeadingEdgeTempKelvin = 2200.0, // UHTC ZrB2/SiC ceramic matrix limit
    double MaxAllowableDynamicPressureKPa = 90.0,
    double DesignShockAttachmentMach = 6.0
);

public record HypersonicWaveriderEvaluationRequest(
    WaveriderFlightState FlightState,
    WaveriderSafetyEnvelope SafetyEnvelope
);

public record WaveriderAerothermalPlan(
    string VehicleId,
    double LiftToDragRatio,
    double ObliqueShockAngleDegrees,
    double StagnationHeatFluxKiloWattsPerM2,
    bool ShockAttachedToLeadingEdge,
    bool ThermalExcursionWarningDetected,
    double RecommendedTranspirationCoolingFlowRate,
    string AerodynamicFlightRegime,
    string FlightGuidanceDirective
);
