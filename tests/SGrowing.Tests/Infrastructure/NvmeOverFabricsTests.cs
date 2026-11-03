using SGrowing.Api.Modules.Infrastructure.NvmeOverFabrics;
using Xunit;

namespace SGrowing.Tests.Infrastructure;

public class NvmeOverFabricsTests
{
    private readonly NvmeOverFabricsEngine _engine = new();

    [Fact]
    public void SeededSubsystems_ContainNqnAndAnaState()
    {
        var subsystems = _engine.GetSubsystems();
        Assert.NotEmpty(subsystems);

        var checkpointPool = subsystems.FirstOrDefault(s => s.Nqn.Contains("ai-checkpoint"));
        Assert.NotNull(checkpointPool);
        Assert.Equal(NvmeTransport.RdmaRocev2, checkpointPool.TransportProtocol);
        Assert.Equal("Optimized", checkpointPool.AnaMultipathState);
        Assert.NotEmpty(checkpointPool.Namespaces);
    }

    [Fact]
    public void IngestTelemetry_SamplesIopsAndSubMillisecondLatency()
    {
        var sample = _engine.IngestTelemetry("nqn.2026-11.io.sgrowing:ai-checkpoint-pool-01", 1500000, 800000, 64.0, 48.0);

        Assert.NotNull(sample);
        Assert.True(sample.ReadIops > 1000000);
        Assert.True(sample.AverageLatencyMicros < 20.0); // Sub-20 microseconds latency
    }

    [Fact]
    public void TriggerAnaFailover_ExecutesUnderTenMillisecondsWithZeroDataLoss()
    {
        var ev = _engine.TriggerAnaFailover("nqn.2026-11.io.sgrowing:ai-checkpoint-pool-01", "PcieAERErrorThreshold");

        Assert.NotNull(ev);
        Assert.True(ev.ZeroDataLossVerified);
        Assert.True(ev.FailoverDurationMs <= 10); // Sub-10ms failover target
    }
}
