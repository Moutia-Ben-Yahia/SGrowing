namespace SGrowing.Api.Modules.Infrastructure.SelfHealing;

public record HealthProbeTelemetry(
    string ProbeId,
    string ResourceName,
    string ResourceType,
    int LatencyMs,
    double SaturationPercent,
    string Status,
    DateTime LastCheckedUtc
);

public record RemediationActionLog(
    string ActionId,
    string ResourceName,
    string TriggerCondition,
    string ActionTaken,
    string ResultStatus,
    int RecoveryTimeMs,
    DateTime ExecutedAtUtc
);

public record ChaosExperimentRequest(
    string ExperimentType,
    string TargetResource,
    int DurationSeconds = 30
);

public record ChaosExperimentResult(
    string ExperimentId,
    string ExperimentType,
    string TargetResource,
    string BlastRadiusAssessment,
    bool AutomatedRecoveryTriggered,
    int MttrSeconds,
    string ResilienceGrade
);

public record MeshResilienceScorecard(
    double PlatformAvailabilityPercent,
    int MeanTimeToRecoverySeconds,
    int TotalRemediationsCount,
    int ActiveHealthyProbesCount,
    string GlobalHealthGrade
);
