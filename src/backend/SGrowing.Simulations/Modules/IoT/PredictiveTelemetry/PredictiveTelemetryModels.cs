namespace SGrowing.Api.Modules.IoT.PredictiveTelemetry;

public enum SensorMetricType
{
    VibrationRmsMmSec,
    BearingTemperatureCelsius,
    MotorCurrentAmperes,
    CompressorPressurePsi,
    AcousticEmissionDb
}

public enum AnomalySeverity
{
    Nominal,
    Warning,
    Critical,
    EmergencyShutdown
}

public enum MaintenancePriority
{
    P1EmergencyRepair,
    P2PreventiveSchedule,
    P3RoutineInspection
}

public sealed record MachineTelemetrySample(
    string MachineId,
    DateTime TimestampUtc,
    double VibrationRmsMmSec,
    double BearingTemperatureCelsius,
    double MotorCurrentAmperes,
    double CompressorPressurePsi,
    double AcousticEmissionDb
);

public sealed record AnomalyDetectionResult(
    string MachineId,
    DateTime TimestampUtc,
    AnomalySeverity Severity,
    double MahalanobisDistance,
    double ThresholdDistance,
    bool IsAnomaly,
    string DominantMetricAnomaly,
    double ForecastedValue,
    double ActualValue,
    string DiagnosticExplanation
);

public sealed record MachineryRulEstimate(
    string MachineId,
    double EstimatedRulHours,
    double CumulativeStressCycles,
    double FailureProbabilityNext48Hours,
    double FailureProbabilityNext168Hours, // 7 days
    double WeibullBetaShape,
    double WeibullEtaScaleHours,
    string RecommendedAction,
    DateTime EvaluatedAtUtc
);

public sealed record AutonomousWorkOrder(
    string WorkOrderId,
    string MachineId,
    MaintenancePriority Priority,
    string RequiredSparePartSku,
    int QuantityNeeded,
    DateTime ScheduledWindowUtc,
    double EstimatedLaborHours,
    string FailureModeHypothesis,
    bool AutoReservedSparePart,
    DateTime CreatedAtUtc
);

public sealed record IngestTelemetryRequest(
    string MachineId,
    IReadOnlyList<MachineTelemetrySample> Samples
);
