using SGrowing.Api.Modules.Infrastructure.SelfHealing;
using Xunit;

namespace SGrowing.Tests.Infrastructure;

public sealed class SelfHealingTests
{
    private readonly SelfHealingMeshEngine _engine = new();

    [Fact]
    public async Task GetHealthProbes_ReturnsActiveProbesWithLatency()
    {
        var probes = await _engine.GetHealthProbesAsync();

        Assert.NotNull(probes);
        Assert.NotEmpty(probes);
        Assert.Contains(probes, p => p.ResourceType == "DatabasePool");
        Assert.Contains(probes, p => p.ResourceType == "RedisCache");
        Assert.True(probes.All(p => p.LatencyMs > 0));
    }

    [Fact]
    public async Task TriggerRemediation_LogsActionAndRecoversSubsystem()
    {
        var log = await _engine.TriggerRemediationAsync("Redis L2 Global Distributed Cache");

        Assert.NotNull(log);
        Assert.Equal("Succeeded", log.ResultStatus);
        Assert.True(log.RecoveryTimeMs > 0);
        Assert.Contains("standby", log.ActionTaken, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteChaosExperiment_SimulatesNetworkPartitionWithAutoRecovery()
    {
        var request = new ChaosExperimentRequest(
            ExperimentType: "ReplicaPartition",
            TargetResource: "PostgreSQL Primary Connection Pool",
            DurationSeconds: 15
        );

        var result = await _engine.ExecuteChaosExperimentAsync(request);

        Assert.NotNull(result);
        Assert.Equal("ReplicaPartition", result.ExperimentType);
        Assert.True(result.AutomatedRecoveryTriggered);
        Assert.True(result.MttrSeconds <= 5);
        Assert.Equal("A+ Resilience", result.ResilienceGrade);
    }

    [Fact]
    public async Task GetResilienceScorecard_ReturnsMissionCriticalGradeAndMttr()
    {
        var scorecard = await _engine.GetResilienceScorecardAsync();

        Assert.NotNull(scorecard);
        Assert.Equal(99.994, scorecard.PlatformAvailabilityPercent);
        Assert.Equal(4, scorecard.MeanTimeToRecoverySeconds);
        Assert.True(scorecard.TotalRemediationsCount >= 1);
        Assert.True(scorecard.ActiveHealthyProbesCount >= 3);
        Assert.Equal("A+ Mission-Critical", scorecard.GlobalHealthGrade);
    }

    [Fact]
    public async Task ExecuteChaosExperiment_LatencyInjection_AssessmentContainsCircuitBreaker()
    {
        var request = new ChaosExperimentRequest(
            ExperimentType: "LatencyInjection",
            TargetResource: "External Meta CAPI / WhatsApp Egress Gateway",
            DurationSeconds: 20
        );

        var result = await _engine.ExecuteChaosExperimentAsync(request);

        Assert.NotNull(result);
        Assert.Equal("LatencyInjection", result.ExperimentType);
        Assert.Contains("circuit breakers tripped", result.BlastRadiusAssessment);
        Assert.True(result.AutomatedRecoveryTriggered);
    }

    [Fact]
    public async Task ExecuteChaosExperiment_ConnectionExhaustion_AssessmentContainsBackpressure()
    {
        var request = new ChaosExperimentRequest(
            ExperimentType: "ConnectionExhaustion",
            TargetResource: "PostgreSQL Primary Connection Pool",
            DurationSeconds: 10
        );

        var result = await _engine.ExecuteChaosExperimentAsync(request);

        Assert.NotNull(result);
        Assert.Equal("ConnectionExhaustion", result.ExperimentType);
        Assert.Contains("token-bucket backpressure", result.BlastRadiusAssessment);
    }

    [Fact]
    public async Task ExecuteChaosExperiment_NullRequest_ThrowsArgumentNullException()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() => _engine.ExecuteChaosExperimentAsync(null!));
    }

    [Fact]
    public async Task ExecuteChaosExperiment_IncrementsTotalRemediationCountInScorecard()
    {
        var initial = await _engine.GetResilienceScorecardAsync();

        var request = new ChaosExperimentRequest(
            ExperimentType: "ReplicaPartition",
            TargetResource: "PostgreSQL Primary Connection Pool",
            DurationSeconds: 5
        );
        await _engine.ExecuteChaosExperimentAsync(request);

        var updated = await _engine.GetResilienceScorecardAsync();
        Assert.Equal(initial.TotalRemediationsCount + 1, updated.TotalRemediationsCount);
    }
}
