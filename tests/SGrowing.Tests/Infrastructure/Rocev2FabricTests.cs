using SGrowing.Api.Modules.Infrastructure.Rocev2Fabric;
using Xunit;

namespace SGrowing.Tests.Infrastructure;

public class Rocev2FabricTests
{
    private readonly Rocev2FabricEngine _engine = new();

    [Fact]
    public void SeededNodes_ContainGpuAndLosslessRoceConfiguration()
    {
        var nodes = _engine.GetNodes();
        Assert.NotEmpty(nodes);
        var gpuNode = nodes.FirstOrDefault(n => n.NodeId == "ROCE-GPU-01");
        Assert.NotNull(gpuNode);
        Assert.Equal(8, gpuNode.GpuCount);
        Assert.Equal(3, gpuNode.PfcPriorityQueue);
        Assert.True(gpuNode.LosslessRoceEnabled);
    }

    [Fact]
    public void IngestTelemetry_DetectsPfcDeadlockWhenBuffersSaturated()
    {
        // Simulate high PFC frames and 95% buffer occupancy
        var sample = _engine.IngestTelemetry("ROCE-GPU-01", 380.0, 95.0, 2500, 2400);

        Assert.NotNull(sample);
        Assert.True(sample.EcnMarkedPacketsPerSec > 0);
        Assert.True(sample.TailLatencyMicros > 10.0);

        var alerts = _engine.GetActiveDeadlockAlerts();
        Assert.NotEmpty(alerts);
        var alert = alerts.FirstOrDefault(a => a.NodeId == "ROCE-GPU-01");
        Assert.NotNull(alert);
        Assert.True(alert.QueueDrainedSuccessfully);
    }

    [Fact]
    public void GetFabricSummary_ReportsAggregateThroughputAndHealth()
    {
        var summary = _engine.GetFabricSummary();
        Assert.NotNull(summary);
        Assert.True(summary.TotalGpuNodesConnected >= 2);
        Assert.True(summary.TotalActiveGpus >= 16);
        Assert.True(summary.AggregateThroughputTbps > 0.0);
    }
}
