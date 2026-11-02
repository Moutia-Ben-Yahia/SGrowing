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
}
