namespace SGrowing.Api.Modules.Aerospace.HallThrusterPoleErosion;

public record HallThrusterTelemetry(
    string ThrusterId,
    string PropellantType = "Xenon",
    double AnodeDischargeVoltageVolts = 300.0,
    double AnodeDischargeCurrentAmperes = 15.0,
    double PropellantMassFlowRateMgPerSec = 5.2,
    double MagneticCoilCurrentAmperes = 3.5,
    double BreathingModeOscillationPeakKHz = 22.0,
    double InnerPolePieceErosionMicronsPer1000Hours = 18.0,
    double CumulativeFiredHours = 4500.0
);

public record HallThrusterLimits(
    double MaxAllowablePoleErosionMicronsPer1000Hours = 40.0,
    double MaxAllowableBreathingOscillationAmps = 4.5,
    double MinAllowableSpecificImpulseSeconds = 1600.0
);

public record HallThrusterEvaluationRequest(
    HallThrusterTelemetry Telemetry,
    HallThrusterLimits Limits
);

public record HallThrusterErosionAssessment(
    string ThrusterId,
    double ThrustMilliNewtons,
    double SpecificImpulseSeconds,
    double AnodeEfficiencyPercent,
    double EstimatedRemainingOperationalHours,
    bool PolePieceErosionCriticalDetected,
    string ThrusterHealthState,
    string MagneticFieldAdjustmentDirective
);
