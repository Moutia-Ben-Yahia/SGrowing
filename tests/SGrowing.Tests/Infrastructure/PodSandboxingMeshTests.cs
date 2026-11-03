using FluentAssertions;
using SGrowing.Api.Modules.Infrastructure.PodSandboxingMesh;
using Xunit;

namespace SGrowing.Tests.Infrastructure;

public class PodSandboxingMeshTests
{
    private readonly PodSandboxingMeshEngine _engine = new();

    [Fact]
    public void GetOverview_ReturnsActivePodsAndChaosEvictions()
    {
        var overview = _engine.GetOverview();

        overview.Should().NotBeNull();
        overview.ActiveIsolatedPodsCount.Should().Be(2);
        overview.ZeroLossEvictionSuccessRatePct.Should().Be(100.0);
        overview.AverageContainerStartLatencyMs.Should().BeLessThan(200.0);
        overview.ActivePods.Should().HaveCount(2);
        overview.RecentEvictions.Should().HaveCount(1);
    }

    [Fact]
    public void ProvisionSandboxPod_CreatesGVisorIsolatedContainer()
    {
        var request = new ProvisionSandboxPodRequest(
            "AGT-RESEARCH-99",
            "gVisor (runsc kernel)",
            4096,
            2000,
            new[] { "CAP_SYS_ADMIN", "CAP_NET_RAW", "CAP_DAC_OVERRIDE" }
        );

        var result = _engine.ProvisionSandboxPod(request);

        result.Should().NotBeNull();
        result.PodName.Should().StartWith("sbx-agent-");
        result.RuntimeClass.Should().Be("gVisor (runsc kernel)");
        result.Status.Should().Be("RUNNING_ISOLATED");
        result.ProvisionLatencyMs.Should().BeLessThan(250);
    }

    [Fact]
    public void TriggerChaosEviction_ReschedulesWithSubSecondDowntime()
    {
        var request = new TriggerChaosEvictionRequest(
            "sbx-agent-scrape-4411",
            "NODE_OOM_SIMULATION"
        );

        var receipt = _engine.TriggerChaosEviction(request);

        receipt.Should().NotBeNull();
        receipt.EvictionId.Should().StartWith("RCP-EVCT-");
        receipt.PodName.Should().Be("sbx-agent-scrape-4411");
        receipt.ZeroLossRescheduleTimeMs.Should().BeLessThan(500.0);
        receipt.Status.Should().Be("RESCHEDULED_ZERO_LOSS_SUCCESS");
    }
}
