using System;
using System.Linq;
using SGrowing.Api.Modules.Voice.CallStateResumption;
using Xunit;

namespace SGrowing.Tests.Voice;

public sealed class CallStateResumptionTests
{
    [Fact]
    public void GetOverview_ReturnsInitialSnapshotsAndResumptions()
    {
        var engine = new CallStateResumptionEngine();
        var overview = engine.GetOverview();

        Assert.NotNull(overview);
        Assert.True(overview.MonitoredActiveCallsCount >= 2);
        Assert.True(overview.AutoCallbackSuccessRatePct > 90.0);
        Assert.NotEmpty(overview.ActiveSnapshots);
        Assert.NotEmpty(overview.RecentResumptions);
    }

    [Fact]
    public void TriggerAutoCallback_ExecutesInstantResumption()
    {
        var engine = new CallStateResumptionEngine();
        var request = new TriggerAutoCallbackRequest("CALL-SES-991", "SIP_BYE_UNEXPECTED");

        var evt = engine.TriggerAutoCallback(request);

        Assert.NotNull(evt);
        Assert.StartsWith("RES-", evt.ResumptionId);
        Assert.Equal("CALL-SES-991", evt.OriginalSessionId);
        Assert.Equal("CALL-SES-991-REC", evt.ReconnectSessionId);
        Assert.Contains("Marcus", evt.GeneratedAcousticGreeting);
        Assert.Equal("AUTO_CALLED_BACK", evt.ResumptionStatus);
    }

    [Fact]
    public void SaveCallSnapshot_UpdatesStateTreeCorrectly()
    {
        var engine = new CallStateResumptionEngine();
        var request = new SaveCallSnapshotRequest(
            "CALL-SES-NEW-11",
            "+1-650-555-0188",
            "+1-800-555-0100",
            "Sophia Loren",
            "Could you explain the enterprise SLA guarantee?",
            "SLA Architecture",
            "SECURITY_ASSESSMENT",
            4
        );

        var snapshot = engine.SaveCallSnapshot(request);

        Assert.NotNull(snapshot);
        Assert.Equal("CALL-SES-NEW-11", snapshot.SessionId);
        Assert.Equal("Sophia Loren", snapshot.CustomerName);

        var overview = engine.GetOverview();
        Assert.Contains(overview.ActiveSnapshots, s => s.SessionId == snapshot.SessionId);
    }
}
