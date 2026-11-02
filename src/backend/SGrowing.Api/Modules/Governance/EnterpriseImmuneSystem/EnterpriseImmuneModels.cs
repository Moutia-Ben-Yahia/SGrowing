namespace SGrowing.Api.Modules.Governance.EnterpriseImmuneSystem;

public record EnterpriseTelemetrySignal(
    string SignalId,
    string SourceModule,
    string Subsystem,
    double MetricValue,
    double BaselineMean,
    double BaselineStdDev,
    string MetricUnit,
    DateTimeOffset TimestampUtc);

public record ByzantineConsensusVote(
    string ValidatorNodeId,
    string AnomalyId,
    bool VoteToIsolate,
    string CryptographicVoteSignature);

public record MitigationAction(
    string ActionId,
    string TargetModule,
    string ActionType,
    string Description,
    int SeverityLevel,
    bool RequiresHumanEscalation);

public record EnterpriseImmuneDiagnosisRequest(
    List<EnterpriseTelemetrySignal> IngestedSignals,
    List<ByzantineConsensusVote> ValidatorVotes,
    int ByzantineToleranceF = 1);

public record DetectedAnomaly(
    string AnomalyId,
    string SourceModule,
    double ZScoreAnomalyMagnitude,
    double SystemicEntropyContribution,
    string AnomalyType);

public record ImmuneMitigationExecution(
    string ActionId,
    string TargetModule,
    string ActionType,
    bool ExecutedSuccessfully,
    string OutcomeSummary);

public record EnterpriseImmuneSystemResult(
    double GlobalSystemicEntropy,
    double EnterpriseVitalityIndexPercent,
    int TotalSignalsAudited,
    List<DetectedAnomaly> DetectedAnomalies,
    bool ByzantineConsensusAchieved,
    List<ImmuneMitigationExecution> ExecutedMitigations,
    string OverallHealthState);
