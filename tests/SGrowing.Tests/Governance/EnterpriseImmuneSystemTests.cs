namespace SGrowing.Tests.Governance;

using FluentAssertions;
using SGrowing.Api.Modules.Governance.EnterpriseImmuneSystem;
using Xunit;

public class EnterpriseImmuneSystemTests
{
    private readonly EnterpriseImmuneSystemEngine _engine = new();

    [Fact]
    public void DiagnoseAndHeal_NominalSignals_ReportsOptimalResilienceAndZeroMitigations()
    {
        var signals = new List<EnterpriseTelemetrySignal>
        {
            new("SIG-001", "OrderProcessing", "Logistics", 102.0, BaselineMean: 100.0, BaselineStdDev: 5.0, "req/s", DateTimeOffset.UtcNow),
            new("SIG-002", "DatabaseCpu", "Infrastructure", 35.0, BaselineMean: 30.0, BaselineStdDev: 10.0, "%", DateTimeOffset.UtcNow)
        };

        var request = new EnterpriseImmuneDiagnosisRequest(signals, ValidatorVotes: []);
        var result = _engine.DiagnoseAndHeal(request);

        result.TotalSignalsAudited.Should().Be(2);
        result.DetectedAnomalies.Should().BeEmpty();
        result.GlobalSystemicEntropy.Should().Be(0.0);
        result.EnterpriseVitalityIndexPercent.Should().Be(100.0);
        result.ExecutedMitigations.Should().BeEmpty();
        result.OverallHealthState.Should().Contain("Optimal Enterprise Resilience");
    }

    [Fact]
    public void DiagnoseAndHeal_AnomaliesWithByzantineConsensus_ExecutesAutomatedSelfHealingMitigations()
    {
        var signals = new List<EnterpriseTelemetrySignal>
        {
            // Massive runaway spike: 1000 req/s vs baseline 50 (Z-score = 95.0)
            new("SIG-SPIKE-01", "SecurityTokenService", "Security", 1000.0, BaselineMean: 50.0, BaselineStdDev: 10.0, "err/s", DateTimeOffset.UtcNow),
            new("SIG-SPIKE-02", "FinanceSettlementWorker", "Finance", 500.0, BaselineMean: 20.0, BaselineStdDev: 5.0, "err/s", DateTimeOffset.UtcNow)
        };

        // 4 validators for f = 1 (minQuorum = 4, requiredAffirmative = 3)
        var votes = new List<ByzantineConsensusVote>
        {
            new("NODE-VAL-1", "ANOMALY-E100-001", true, "SIG-VAL-1"),
            new("NODE-VAL-2", "ANOMALY-E100-001", true, "SIG-VAL-2"),
            new("NODE-VAL-3", "ANOMALY-E100-001", true, "SIG-VAL-3"),
            new("NODE-VAL-4", "ANOMALY-E100-001", false, "SIG-VAL-4") // 1 dissenting Byzantine node
        };

        var request = new EnterpriseImmuneDiagnosisRequest(signals, votes, ByzantineToleranceF: 1);
        var result = _engine.DiagnoseAndHeal(request);

        result.DetectedAnomalies.Should().HaveCount(2);
        result.ByzantineConsensusAchieved.Should().BeTrue();
        result.ExecutedMitigations.Should().HaveCount(2);

        result.ExecutedMitigations.Should().Contain(m => m.ActionType == "QuarantineCorruptedNode");
        result.ExecutedMitigations.Should().Contain(m => m.ActionType == "RewindStateSnapshot");
        result.EnterpriseVitalityIndexPercent.Should().BeGreaterThan(0.0);
    }

    [Fact]
    public void DiagnoseAndHeal_AnomaliesWithoutQuorum_BlocksMitigationExecution()
    {
        var signals = new List<EnterpriseTelemetrySignal>
        {
            new("SIG-WARN-01", "InventoryWorker", "Logistics", 200.0, BaselineMean: 20.0, BaselineStdDev: 5.0, "latency_ms", DateTimeOffset.UtcNow)
        };

        // Only 2 votes when f=1 requires 4 nodes (no quorum)
        var votes = new List<ByzantineConsensusVote>
        {
            new("NODE-VAL-1", "ANOMALY-E100-001", true, "SIG-1"),
            new("NODE-VAL-2", "ANOMALY-E100-001", true, "SIG-2")
        };

        var request = new EnterpriseImmuneDiagnosisRequest(signals, votes, ByzantineToleranceF: 1);
        var result = _engine.DiagnoseAndHeal(request);

        result.DetectedAnomalies.Should().HaveCount(1);
        result.ByzantineConsensusAchieved.Should().BeFalse();
        result.ExecutedMitigations.Should().BeEmpty(); // No unverified autonomous actions
    }

    [Fact]
    public void DiagnoseAndHeal_CatastrophicMetricCollapse_CategorizesCorrectAnomalyType()
    {
        var signals = new List<EnterpriseTelemetrySignal>
        {
            // Ingested throughput collapsed to 0 (baseline 500, stdDev 50)
            new("SIG-COLLAPSE-01", "PaymentGatewayService", "Payments", 0.0, BaselineMean: 500.0, BaselineStdDev: 50.0, "tps", DateTimeOffset.UtcNow)
        };

        var request = new EnterpriseImmuneDiagnosisRequest(signals, ValidatorVotes: []);
        var result = _engine.DiagnoseAndHeal(request);

        var anomaly = result.DetectedAnomalies.Should().ContainSingle().Subject;
        anomaly.AnomalyType.Should().Contain("Catastrophic Metric Collapse");
        anomaly.ZScoreAnomalyMagnitude.Should().Be(10.0); // |0 - 500| / 50
    }

    [Fact]
    public void DiagnoseAndHeal_SevereSystemicDegradation_ReportsDegenerativeState()
    {
        var signals = new List<EnterpriseTelemetrySignal>();
        for (int i = 0; i < 10; i++)
        {
            signals.Add(new EnterpriseTelemetrySignal(
                $"SIG-CRIT-{i}",
                $"Subsystem-{i}",
                "Core",
                1000.0,
                BaselineMean: 10.0,
                BaselineStdDev: 2.0,
                "errors",
                DateTimeOffset.UtcNow
            ));
        }

        var request = new EnterpriseImmuneDiagnosisRequest(signals, ValidatorVotes: []);
        var result = _engine.DiagnoseAndHeal(request);

        result.DetectedAnomalies.Should().HaveCount(10);
        result.EnterpriseVitalityIndexPercent.Should().BeLessThan(70.0);
        result.OverallHealthState.Should().Contain("Degenerative Strain");
    }
}
