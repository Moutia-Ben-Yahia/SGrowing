namespace SGrowing.Api.Modules.Aerospace.CryoTurbopumpCavitation;

public record CryoPropellantCondition(
    string PropellantName, // "LiquidOxygen_LOX", "LiquidMethane_LCH4", "LiquidHydrogen_LH2"
    double InletPressureBar,
    double VaporPressureBar,
    double FluidDensityKgPerM3,
    double TemperatureKelvin
);

public record InducerFlowTelemetry(
    string TurbopumpId,
    double RotationalSpeedRpm,
    double MassFlowKgPerSec,
    double TipSpeedMps,
    double SuctionHeadMeters,
    double VibrationRmsG
);

public record CavitationEvaluationRequest(
    CryoPropellantCondition Propellant,
    InducerFlowTelemetry Inducer
);

public record TurbopumpThrottleDirective(
    string TurbopumpId,
    string PropellantName,
    double CavitationNumberSigma,
    double NpshMarginMeters,
    bool CavitationDetected,
    bool ChuggingResonanceRisk,
    string ActionUrgency, // "NOMINAL", "ADAPTIVE_TRIM", "EMERGENCY_THROTTLE_DOWN"
    string ThrottleAndPressurizationDirective
);
