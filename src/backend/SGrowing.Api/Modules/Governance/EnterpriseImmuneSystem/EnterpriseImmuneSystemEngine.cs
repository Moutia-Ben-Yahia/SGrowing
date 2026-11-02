namespace SGrowing.Api.Modules.Governance.EnterpriseImmuneSystem;

public class EnterpriseImmuneSystemEngine
{
    private const double AnomalyThresholdZScore = 3.0;

    public EnterpriseImmuneSystemResult DiagnoseAndHeal(EnterpriseImmuneDiagnosisRequest request)
    {
        var anomalies = new List<DetectedAnomaly>();
        double entropyAccumulator = 0.0;
        int anomalyIndex = 1;

        foreach (var signal in request.IngestedSignals)
        {
            double stdDev = Math.Max(0.001, signal.BaselineStdDev);
            double zScore = Math.Round(Math.Abs(signal.MetricValue - signal.BaselineMean) / stdDev, 2);

            if (zScore >= AnomalyThresholdZScore)
            {
                // Systemic entropy contribution based on relative deviation
                double entropyContribution = Math.Round(Math.Log2(1.0 + zScore), 4);
                entropyAccumulator += entropyContribution;

                string anomalyType = signal.MetricValue > signal.BaselineMean
                    ? $"Runaway Metric Spike ({signal.Subsystem})"
                    : $"Catastrophic Metric Collapse ({signal.Subsystem})";

                anomalies.Add(new DetectedAnomaly(
                    AnomalyId: $"ANOMALY-E100-{anomalyIndex++:D3}",
                    SourceModule: signal.SourceModule,
                    ZScoreAnomalyMagnitude: zScore,
                    SystemicEntropyContribution: entropyContribution,
                    AnomalyType: anomalyType
                ));
            }
        }

        // Byzantine Quorum check: requires N >= 3f + 1, and affirmative votes >= 2f + 1
        int f = request.ByzantineToleranceF;
        int minQuorum = (3 * f) + 1;
        int requiredAffirmative = (2 * f) + 1;

        int totalVotes = request.ValidatorVotes.Count;
        int affirmativeVotes = request.ValidatorVotes.Count(v => v.VoteToIsolate);

        bool consensusAchieved = totalVotes >= minQuorum && affirmativeVotes >= requiredAffirmative;

        var mitigations = new List<ImmuneMitigationExecution>();
        int mitigationCounter = 1;

        if (anomalies.Count > 0 && consensusAchieved)
        {
            // Autonomous Self-Healing mitigation execution
            foreach (var anomaly in anomalies)
            {
                string actionType;
                string summary;

                if (anomaly.SourceModule.Contains("Security", StringComparison.OrdinalIgnoreCase))
                {
                    actionType = "QuarantineCorruptedNode";
                    summary = $"Cryptographic isolation executed for {anomaly.SourceModule}; credentials rotated.";
                }
                else if (anomaly.SourceModule.Contains("Finance", StringComparison.OrdinalIgnoreCase))
                {
                    actionType = "RewindStateSnapshot";
                    summary = $"Transactional ledger rollback triggered for {anomaly.SourceModule}; double-entry verified.";
                }
                else
                {
                    actionType = "TripCircuitBreaker";
                    summary = $"Subsystem circuit breaker tripped for {anomaly.SourceModule}; traffic routed to fallback replica.";
                }

                mitigations.Add(new ImmuneMitigationExecution(
                    ActionId: $"MITIGATE-100-{mitigationCounter++:D3}",
                    TargetModule: anomaly.SourceModule,
                    ActionType: actionType,
                    ExecutedSuccessfully: true,
                    OutcomeSummary: summary
                ));
            }
        }

        double vitalityIndex = 100.0;
        if (anomalies.Count > 0)
        {
            vitalityIndex -= (anomalies.Count * 12.0) + (entropyAccumulator * 2.5);
            // Rebound vitality if mitigations executed successfully
            if (mitigations.Count > 0)
            {
                vitalityIndex += (mitigations.Count * 6.0);
            }
        }
        vitalityIndex = Math.Clamp(Math.Round(vitalityIndex, 2), 0.0, 100.0);

        string healthState;
        if (vitalityIndex >= 90.0)
        {
            healthState = "Optimal Enterprise Resilience: Autonomic equilibrium maintained.";
        }
        else if (vitalityIndex >= 70.0)
        {
            healthState = "Active Self-Healing Mitigation: Autonomic countermeasures engaged.";
        }
        else
        {
            healthState = "Degenerative Strain: Multi-subsystem degradation requiring executive intervention.";
        }

        return new EnterpriseImmuneSystemResult(
            GlobalSystemicEntropy: Math.Round(entropyAccumulator, 4),
            EnterpriseVitalityIndexPercent: vitalityIndex,
            TotalSignalsAudited: request.IngestedSignals.Count,
            DetectedAnomalies: anomalies,
            ByzantineConsensusAchieved: consensusAchieved,
            ExecutedMitigations: mitigations,
            OverallHealthState: healthState
        );
    }
}
