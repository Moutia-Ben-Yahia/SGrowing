using SGrowing.Api.Modules.Security.CollaborationAudit;
using Xunit;

namespace SGrowing.Tests.Security;

public sealed class CollaborationAuditTests
{
    private readonly CollaborationAuditEngine _engine = new();

    [Fact]
    public void GetSnapshot_ReturnsActiveCollaboratorsAndAuditLogs()
    {
        var snapshot = _engine.GetSnapshot();

        Assert.NotNull(snapshot);
        Assert.True(snapshot.ActiveCollaboratorsCount >= 2);
        Assert.True(snapshot.CriticalAuditEventsCount >= 1);
        Assert.NotEmpty(snapshot.RecentLogs);
    }

    [Fact]
    public void SendHeartbeat_UpdatesCollaboratorPosition()
    {
        var request = new CollaborationHeartbeatRequest(
            ResourceId: "res_funnel_canvas_1",
            ResourceType: "FunnelCanvas",
            UserId: "usr_carol",
            UserName: "Carol Davis",
            CursorX: 520.0,
            CursorY: 410.0,
            ActiveField: "LeadForm"
        );

        var session = _engine.SendHeartbeat(request);

        Assert.NotNull(session);
        Assert.Equal("usr_carol", session.UserId);
        Assert.Equal(520.0, session.CursorX);

        var active = _engine.ListActiveCollaborators("res_funnel_canvas_1");
        Assert.Contains(active, c => c.UserId == "usr_carol");
    }

    [Fact]
    public void RevokeUserSessions_RemovesUserAndLogsCriticalAudit()
    {
        var request = new RevokeUserSessionsRequest("usr_alice", "Suspected Credential Compromise");
        var result = _engine.RevokeUserSessions(request);

        Assert.True(result);

        var active = _engine.ListActiveCollaborators();
        Assert.DoesNotContain(active, c => c.UserId == "usr_alice");

        var logs = _engine.ListAuditLogs();
        Assert.Contains(logs, l => l.Action == "UserSessionsTerminated" && l.UserId == "usr_alice");
    }
}
