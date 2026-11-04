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

    [Fact]
    public void TriggerAutoCallback_UnknownSession_UsesGenericFallbackGreeting()
    {
        var engine = new CallStateResumptionEngine();
        var request = new TriggerAutoCallbackRequest("CALL-NONEXISTENT-777", "NETWORK_TIMEOUT");

        var evt = engine.TriggerAutoCallback(request);

        Assert.NotNull(evt);
        Assert.Equal("CALL-NONEXISTENT-777", evt.OriginalSessionId);
        Assert.Equal("CALL-NONEXISTENT-777-REC", evt.ReconnectSessionId);
        Assert.Equal("NETWORK_TIMEOUT", evt.DisconnectReason);
        Assert.Contains("Hi there", evt.GeneratedAcousticGreeting);
        Assert.Contains("our conversation", evt.GeneratedAcousticGreeting);
        Assert.Equal("AUTO_CALLED_BACK", evt.ResumptionStatus);
    }

    [Fact]
    public void SaveCallSnapshot_OverwritesExistingSessionSnapshot()
    {
        var engine = new CallStateResumptionEngine();
        var initialRequest = new SaveCallSnapshotRequest(
            "CALL-SES-OVERWRITE-1",
            "+1-555-0101",
            "+1-800-0101",
            "Initial Contact",
            "Initial utterance",
            "Topic 1",
            "DISCOVERY",
            1
        );
        engine.SaveCallSnapshot(initialRequest);

        var updatedRequest = new SaveCallSnapshotRequest(
            "CALL-SES-OVERWRITE-1",
            "+1-555-0101",
            "+1-800-0101",
            "Updated Contact",
            "Updated utterance about enterprise SLA",
            "Topic SLA",
            "PROPOSAL",
            5
        );
        var updated = engine.SaveCallSnapshot(updatedRequest);

        Assert.Equal("CALL-SES-OVERWRITE-1", updated.SessionId);
        Assert.Equal("Updated Contact", updated.CustomerName);
        Assert.Equal("PROPOSAL", updated.CurrentContractPhase);
        Assert.Equal(5, updated.TurnCounter);

        var overview = engine.GetOverview();
        var snapshot = overview.ActiveSnapshots.FirstOrDefault(s => s.SessionId == "CALL-SES-OVERWRITE-1");
        Assert.NotNull(snapshot);
        Assert.Equal("Topic SLA", snapshot.DialogGoalTopic);
    }

    [Fact]
    public void GetOverview_IncludesMultipleResumptionEvents()
    {
        var engine = new CallStateResumptionEngine();
        engine.TriggerAutoCallback(new TriggerAutoCallbackRequest("CALL-SES-991", "SIP_BYE_UNEXPECTED"));
        engine.TriggerAutoCallback(new TriggerAutoCallbackRequest("CALL-SES-992", "CELLULAR_TOWER_HANDOFF_DROP"));

        var overview = engine.GetOverview();
        Assert.True(overview.TotalDisconnectionsHandled >= 3);
        Assert.True(overview.AutoCallbackSuccessRatePct > 90.0);
        Assert.True(overview.AverageReconnectLatencySeconds > 0.0);
    }
}
