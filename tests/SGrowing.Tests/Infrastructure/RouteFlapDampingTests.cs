using SGrowing.Api.Modules.Infrastructure.RouteFlapDamping;
using Xunit;

namespace SGrowing.Tests.Infrastructure;

public sealed class RouteFlapDampingTests
{
    private readonly RouteFlapDampingEngine _engine = new();

    [Fact]
    public void GetOverview_ReturnsMonitoredPrefixesAndFlapHistory()
    {
        var overview = _engine.GetOverview();

        Assert.NotNull(overview);
        Assert.True(overview.TotalMonitoredPrefixesCount >= 2);
        Assert.True(overview.CurrentlySuppressedPrefixesCount >= 1);
        Assert.NotEmpty(overview.MonitoredPrefixes);
        Assert.NotEmpty(overview.RecentFlapEvents);
    }

    [Fact]
    public void RecordFlapEvent_ExceedingCutoffThreshold_SuppressesPrefix()
    {
        var prefix = "192.0.2.0/24";
        _engine.RecordFlapEvent(prefix, "AS65001", "192.0.2.1");
        _engine.RecordFlapEvent(prefix, "AS65001", "192.0.2.1");
        var evt3 = _engine.RecordFlapEvent(prefix, "AS65001", "192.0.2.1");

        Assert.NotNull(evt3);
        Assert.Equal(3000.0, evt3.NewCumulativePenalty);

        var state = _engine.CheckPrefixState(prefix);
        Assert.Equal(BgpPrefixFlapState.SuppressedDamped, state.State);
        Assert.NotNull(state.SuppressedUntilUtc);
    }

    [Fact]
    public void EvacuatePoP_RoutesTrafficAwayFromDegradedAnycastPoP()
    {
        var evac = _engine.EvacuatePoP("Frankfurt-FRA1", "AWS_GLOBAL_ACCELERATOR", "Unstable transatlantic transit link flap");

        Assert.NotNull(evac);
        Assert.StartsWith("EVAC-", evac.EvacuationId);
        Assert.Equal("Frankfurt-FRA1", evac.TargetPoPName);
        Assert.Equal("EVACUATED_TO_SECONDARY_POP", evac.Status);
        Assert.True(evac.DivertedTrafficBps > 0.0);
    }
}
