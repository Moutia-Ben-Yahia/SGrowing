using SGrowing.Api.Modules.Infrastructure.BgpBfd;
using Xunit;

namespace SGrowing.Tests.Infrastructure;

public class BgpBfdTests
{
    [Fact]
    public void GetOverview_ReportsGlobalEdgeHealthAndBfdSessions()
    {
        var engine = new BgpBfdMeshEngine();

        var overview = engine.GetOverview();

        Assert.NotNull(overview);
        Assert.True(overview.TotalBfdSessionsCount >= 3);
        Assert.True(overview.HealthySessionsCount >= 3);
        Assert.Equal(100.0, overview.GlobalEdgeHealthIndexPct);
        Assert.Equal(800.0, overview.SubSecondFailoverLatencyTargetMs);
        Assert.NotEmpty(overview.ActiveSessions);
        Assert.NotEmpty(overview.RecentRouteEvents);
    }

    [Fact]
    public void SimulateFailover_WithdrawsAnycastRouteSubSecond()
    {
        var engine = new BgpBfdMeshEngine();
        var request = new SimulateFailoverRequest(
            "BFD-FRA-01",
            "Fiber cut on Frankfurt-Amsterdam transit ring"
        );

        var routeEvent = engine.SimulateFailover(request);

        Assert.NotNull(routeEvent);
        Assert.StartsWith("EVT-", routeEvent.EventId);
        Assert.Equal("ROUTE_WITHDRAWN", routeEvent.ActionTaken);
        Assert.True(routeEvent.TriggerLatencyMs < 800.0);

        var overview = engine.GetOverview();
        var session = overview.ActiveSessions.FirstOrDefault(s => s.SessionId == "BFD-FRA-01");
        Assert.NotNull(session);
        Assert.Equal("POISONED", session.SessionState);
        Assert.True(overview.GlobalEdgeHealthIndexPct < 100.0);
    }

    [Fact]
    public void RestoreRoute_StabilizesSessionAndReAdvertisesAnycast()
    {
        var engine = new BgpBfdMeshEngine();
        engine.SimulateFailover(new SimulateFailoverRequest("BFD-FRA-01", "Simulated degradation"));

        var restoreRequest = new RestoreRouteRequest("BFD-FRA-01");
        var routeEvent = engine.RestoreRoute(restoreRequest);

        Assert.NotNull(routeEvent);
        Assert.StartsWith("EVT-", routeEvent.EventId);
        Assert.Equal("ROUTE_ANNOUNCED", routeEvent.ActionTaken);

        var overview = engine.GetOverview();
        var session = overview.ActiveSessions.FirstOrDefault(s => s.SessionId == "BFD-FRA-01");
        Assert.NotNull(session);
        Assert.Equal("UP", session.SessionState);
        Assert.Equal(100.0, overview.GlobalEdgeHealthIndexPct);
    }
}
