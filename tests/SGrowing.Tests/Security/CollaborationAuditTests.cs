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

    [Fact]
    public void ListActiveCollaborators_WithNonExistentResourceId_ReturnsEmpty()
    {
        var active = _engine.ListActiveCollaborators("res_non_existent_canvas");
        Assert.Empty(active);
    }

    [Fact]
    public void ListAuditLogs_WithTakeParameter_LimitsReturnedLogCount()
    {
        var logs = _engine.ListAuditLogs(take: 1);
        Assert.Single(logs);
    }

    [Fact]
    public void RevokeUserSessions_NonExistentUser_StillLogsAuditAndReturnsTrue()
    {
        var request = new RevokeUserSessionsRequest("usr_ghost", "Routine account decommissioning");
        var result = _engine.RevokeUserSessions(request);

        Assert.True(result);
        var logs = _engine.ListAuditLogs();
        Assert.Contains(logs, l => l.Action == "UserSessionsTerminated" && l.UserId == "usr_ghost");
    }

    [Fact]
    public void SendHeartbeat_SameUserDifferentResource_CreatesMultipleActiveSessions()
    {
        var req1 = new CollaborationHeartbeatRequest("res_canvas_A", "Canvas", "usr_multires", "Multi User", 10, 20, "Field1");
        var req2 = new CollaborationHeartbeatRequest("res_canvas_B", "Canvas", "usr_multires", "Multi User", 30, 40, "Field2");

        _engine.SendHeartbeat(req1);
        _engine.SendHeartbeat(req2);

        var activeA = _engine.ListActiveCollaborators("res_canvas_A");
        var activeB = _engine.ListActiveCollaborators("res_canvas_B");

        Assert.Contains(activeA, c => c.UserId == "usr_multires" && c.ActiveField == "Field1");
        Assert.Contains(activeB, c => c.UserId == "usr_multires" && c.ActiveField == "Field2");
    }

    [Fact]
    public void SendHeartbeat_ConsecutiveHeartbeats_UpdatesPositionInPlaceWithoutDuplicatingSession()
    {
        const string resId = "res_dynamic_board";
        const string userId = "usr_updater";

        var initial = new CollaborationHeartbeatRequest(resId, "Board", userId, "Updater", 100, 200, "NodeA");
        var updated = new CollaborationHeartbeatRequest(resId, "Board", userId, "Updater", 450, 650, "NodeB");

        _engine.SendHeartbeat(initial);
        var session = _engine.SendHeartbeat(updated);

        Assert.Equal(450, session.CursorX);
        Assert.Equal(650, session.CursorY);
        Assert.Equal("NodeB", session.ActiveField);

        var activeForResource = _engine.ListActiveCollaborators(resId).Where(c => c.UserId == userId).ToList();
        Assert.Single(activeForResource);
        Assert.Equal(450, activeForResource[0].CursorX);
        Assert.Equal(650, activeForResource[0].CursorY);
    }
}
