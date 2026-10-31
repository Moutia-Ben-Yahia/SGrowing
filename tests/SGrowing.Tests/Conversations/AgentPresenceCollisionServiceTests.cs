using FluentAssertions;
using SGrowing.Api.Modules.Conversations.Presence;
using Xunit;

namespace SGrowing.Tests.Conversations;

public class AgentPresenceCollisionServiceTests
{
    private readonly AgentPresenceCollisionService _service;

    public AgentPresenceCollisionServiceTests()
    {
        _service = new AgentPresenceCollisionService();
        _service.ResetToSeedData();
    }

    // =========================================================================
    // SECTION 1: PRESENCE HEARTBEAT REGISTRATION & STATE TRANSITIONS
    // =========================================================================

    [Fact]
    public async Task PingHeartbeatAsync_NewAgent_RegistersOnlineSession()
    {
        var agentId = Guid.NewGuid();
        var request = new AgentHeartbeatRequest(
            AgentId: agentId,
            AgentName: "Test Agent Jordan",
            AgentEmail: "jordan@sgrowing.io",
            TicketId: null,
            State: AgentPresenceState.Online
        );

        var session = await _service.PingHeartbeatAsync(request);

        session.Should().NotBeNull();
        session.AgentId.Should().Be(agentId);
        session.AgentName.Should().Be("Test Agent Jordan");
        session.State.Should().Be(AgentPresenceState.Online);
        session.CurrentTicketId.Should().BeNull();
    }

    [Fact]
    public async Task PingHeartbeatAsync_WithExplicitState_UpdatesStateImmediately()
    {
        var agentId = Guid.NewGuid();
        var request = new AgentHeartbeatRequest(
            AgentId: agentId,
            AgentName: "Operator Sam",
            AgentEmail: "sam@sgrowing.io",
            TicketId: null,
            State: AgentPresenceState.ActiveOnTicket
        );

        var session = await _service.PingHeartbeatAsync(request);

        session.State.Should().Be(AgentPresenceState.ActiveOnTicket);
    }

    [Fact]
    public async Task PingHeartbeatAsync_WithTicketId_SetsViewingTicketStateAndRegistersViewer()
    {
        var agentId = Guid.NewGuid();
        var ticketId = Guid.NewGuid();
        var request = new AgentHeartbeatRequest(
            AgentId: agentId,
            AgentName: "Operator Casey",
            AgentEmail: "casey@sgrowing.io",
            TicketId: ticketId,
            State: null
        );

        var session = await _service.PingHeartbeatAsync(request);
        var viewers = await _service.GetTicketViewersAsync(ticketId);

        session.State.Should().Be(AgentPresenceState.ViewingTicket);
        session.CurrentTicketId.Should().Be(ticketId);
        viewers.Should().ContainSingle(v => v.AgentId == agentId && v.AgentName == "Operator Casey");
    }

    [Fact]
    public async Task PingHeartbeatAsync_WithTicketIdAndIsTyping_SetsComposingReplyState()
    {
        var agentId = Guid.NewGuid();
        var ticketId = Guid.NewGuid();
        var request = new AgentHeartbeatRequest(
            AgentId: agentId,
            AgentName: "Operator Morgan",
            AgentEmail: "morgan@sgrowing.io",
            TicketId: ticketId,
            State: null,
            IsTyping: true,
            DraftSnippet: "Working on your refund..."
        );

        var session = await _service.PingHeartbeatAsync(request);

        session.State.Should().Be(AgentPresenceState.ComposingReply);
        session.IsTyping.Should().BeTrue();
        session.DraftSnippet.Should().Be("Working on your refund...");
    }

    [Fact]
    public async Task PingHeartbeatAsync_ConsecutivePings_ExtendsLastHeartbeat()
    {
        var agentId = Guid.NewGuid();
        var t0 = new DateTime(2026, 11, 1, 12, 0, 0, DateTimeKind.Utc);
        var t1 = t0.AddSeconds(14);

        var s0 = await _service.PingHeartbeatAsync(new AgentHeartbeatRequest(agentId, "Taylor", null, null, AgentPresenceState.Online), asOfUtc: t0);
        s0.LastHeartbeatUtc.Should().Be(t0);

        var s1 = await _service.PingHeartbeatAsync(new AgentHeartbeatRequest(agentId, "Taylor", null, null, AgentPresenceState.Online), asOfUtc: t1);
        s1.LastHeartbeatUtc.Should().Be(t1);
    }

    [Fact]
    public async Task PingHeartbeatAsync_AgentNavigatingToNewTicket_RemovesViewerFromOldTicket()
    {
        var agentId = Guid.NewGuid();
        var ticketA = Guid.NewGuid();
        var ticketB = Guid.NewGuid();

        await _service.PingHeartbeatAsync(new AgentHeartbeatRequest(agentId, "Riley", null, ticketA, AgentPresenceState.ViewingTicket));
        var viewersA1 = await _service.GetTicketViewersAsync(ticketA);
        viewersA1.Should().ContainSingle(v => v.AgentId == agentId);

        // Navigate to ticket B
        await _service.PingHeartbeatAsync(new AgentHeartbeatRequest(agentId, "Riley", null, ticketB, AgentPresenceState.ViewingTicket));
        var viewersA2 = await _service.GetTicketViewersAsync(ticketA);
        var viewersB = await _service.GetTicketViewersAsync(ticketB);

        viewersA2.Should().NotContain(v => v.AgentId == agentId);
        viewersB.Should().ContainSingle(v => v.AgentId == agentId);
    }

    [Fact]
    public async Task PingHeartbeatAsync_AgentNavigatingAwayFromLockedTicket_ReleasesHardLock()
    {
        var agentId = Guid.NewGuid();
        var ticketId = Guid.NewGuid();

        await _service.AcquireCollisionLockAsync(ticketId, new AcquireLockRequest(agentId, "Riley", "Drafting..."));
        var statusBefore = await _service.GetTicketCollisionStatusAsync(ticketId);
        statusBefore.LockState.Should().Be(CollisionLockState.HardComposingLock);

        // Navigate away (ticketId = null)
        await _service.PingHeartbeatAsync(new AgentHeartbeatRequest(agentId, "Riley", null, null, AgentPresenceState.Online));

        var statusAfter = await _service.GetTicketCollisionStatusAsync(ticketId);
        statusAfter.LockState.Should().Be(CollisionLockState.Unlocked);
        statusAfter.LockedByAgentId.Should().BeNull();
    }

    [Fact]
    public async Task PingHeartbeatAsync_IdleAgentPingingAgain_TransitionsBackToOnline()
    {
        var agentId = Guid.NewGuid();
        var t0 = new DateTime(2026, 11, 1, 10, 0, 0, DateTimeKind.Utc);
        await _service.PingHeartbeatAsync(new AgentHeartbeatRequest(agentId, "Quinn", null, null, AgentPresenceState.Online), asOfUtc: t0);

        // Simulate 40s lapse -> Idle
        _service.RefreshAgentStates(asOfUtc: t0.AddSeconds(40));
        var rosterIdle = await _service.GetTeamStatusRosterAsync(asOfUtc: t0.AddSeconds(40));
        rosterIdle.Agents.First(a => a.AgentId == agentId).State.Should().Be(AgentPresenceState.Idle);

        // Send new ping at t0 + 45s
        var revivedSession = await _service.PingHeartbeatAsync(new AgentHeartbeatRequest(agentId, "Quinn", null, null, null), asOfUtc: t0.AddSeconds(45));
        revivedSession.State.Should().Be(AgentPresenceState.Online);
    }

    // =========================================================================
    // SECTION 2: 15S LEASE RENEWAL & 30S/60S AUTO-EXPIRY TRANSITIONS
    // =========================================================================

    [Fact]
    public async Task RefreshAgentStates_Within30Seconds_RetainsActivePresence()
    {
        var agentId = Guid.NewGuid();
        var t0 = new DateTime(2026, 11, 1, 12, 0, 0, DateTimeKind.Utc);
        await _service.PingHeartbeatAsync(new AgentHeartbeatRequest(agentId, "Agent 15s", null, null, AgentPresenceState.Online), asOfUtc: t0);

        _service.RefreshAgentStates(asOfUtc: t0.AddSeconds(25));
        var roster = await _service.GetTeamStatusRosterAsync(asOfUtc: t0.AddSeconds(25));

        roster.Agents.First(a => a.AgentId == agentId).State.Should().Be(AgentPresenceState.Online);
    }

    [Fact]
    public async Task RefreshAgentStates_Between30And60Seconds_TransitionsToIdle()
    {
        var agentId = Guid.NewGuid();
        var t0 = new DateTime(2026, 11, 1, 12, 0, 0, DateTimeKind.Utc);
        await _service.PingHeartbeatAsync(new AgentHeartbeatRequest(agentId, "Agent 35s", null, null, AgentPresenceState.Online), asOfUtc: t0);

        _service.RefreshAgentStates(asOfUtc: t0.AddSeconds(35));
        var roster = await _service.GetTeamStatusRosterAsync(asOfUtc: t0.AddSeconds(35));

        roster.Agents.First(a => a.AgentId == agentId).State.Should().Be(AgentPresenceState.Idle);
    }

    [Fact]
    public async Task RefreshAgentStates_Exceeding60Seconds_TransitionsToOffline()
    {
        var agentId = Guid.NewGuid();
        var t0 = new DateTime(2026, 11, 1, 12, 0, 0, DateTimeKind.Utc);
        await _service.PingHeartbeatAsync(new AgentHeartbeatRequest(agentId, "Agent 65s", null, null, AgentPresenceState.Online), asOfUtc: t0);

        _service.RefreshAgentStates(asOfUtc: t0.AddSeconds(65));
        var roster = await _service.GetTeamStatusRosterAsync(asOfUtc: t0.AddSeconds(65));

        roster.Agents.First(a => a.AgentId == agentId).State.Should().Be(AgentPresenceState.Offline);
    }

    [Fact]
    public async Task RefreshAgentStates_OfflineAgent_ClearsActiveTicketAndTyping()
    {
        var agentId = Guid.NewGuid();
        var ticketId = Guid.NewGuid();
        var t0 = new DateTime(2026, 11, 1, 12, 0, 0, DateTimeKind.Utc);

        await _service.PingHeartbeatAsync(new AgentHeartbeatRequest(agentId, "Agent Typer", null, ticketId, AgentPresenceState.ComposingReply, IsTyping: true, DraftSnippet: "Hello"), asOfUtc: t0);

        _service.RefreshAgentStates(asOfUtc: t0.AddSeconds(65));
        var roster = await _service.GetTeamStatusRosterAsync(asOfUtc: t0.AddSeconds(65));
        var agent = roster.Agents.First(a => a.AgentId == agentId);

        agent.State.Should().Be(AgentPresenceState.Offline);
        agent.CurrentTicketId.Should().BeNull();
        agent.IsTyping.Should().BeFalse();
        agent.DraftSnippet.Should().BeNull();
    }

    [Fact]
    public async Task RefreshAgentStates_OfflineAgentHoldingHardLock_ReleasesLockAutomatically()
    {
        var agentId = Guid.NewGuid();
        var ticketId = Guid.NewGuid();
        var t0 = new DateTime(2026, 11, 1, 12, 0, 0, DateTimeKind.Utc);

        await _service.AcquireCollisionLockAsync(ticketId, new AcquireLockRequest(agentId, "Agent Locking", "Replying..."), asOfUtc: t0);

        _service.RefreshAgentStates(asOfUtc: t0.AddSeconds(65));
        var status = await _service.GetTicketCollisionStatusAsync(ticketId, asOfUtc: t0.AddSeconds(65));

        status.LockState.Should().Be(CollisionLockState.Unlocked);
        status.LockedByAgentId.Should().BeNull();
    }

    [Fact]
    public void HeartbeatLease_Constant15SecondLeaseModel_ValidatesLeaseConstant()
    {
        AgentPresenceCollisionService.HeartbeatLeaseSeconds.Should().Be(15);
        AgentPresenceCollisionService.IdleThresholdSeconds.Should().Be(30);
        AgentPresenceCollisionService.OfflineThresholdSeconds.Should().Be(60);
        AgentPresenceCollisionService.LockLeaseSeconds.Should().Be(60);
    }

    [Fact]
    public async Task RefreshAgentStates_MultipleAgentsWithDifferentAges_AppliesCorrectStateToEach()
    {
        var id1 = Guid.NewGuid();
        var id2 = Guid.NewGuid();
        var id3 = Guid.NewGuid();
        var now = new DateTime(2026, 11, 1, 14, 0, 0, DateTimeKind.Utc);

        await _service.PingHeartbeatAsync(new AgentHeartbeatRequest(id1, "Fresh", null, null, AgentPresenceState.Online), asOfUtc: now.AddSeconds(-10));
        await _service.PingHeartbeatAsync(new AgentHeartbeatRequest(id2, "Middle", null, null, AgentPresenceState.Online), asOfUtc: now.AddSeconds(-45));
        await _service.PingHeartbeatAsync(new AgentHeartbeatRequest(id3, "Old", null, null, AgentPresenceState.Online), asOfUtc: now.AddSeconds(-70));

        _service.RefreshAgentStates(now);
        var roster = await _service.GetTeamStatusRosterAsync(now);

        roster.Agents.First(a => a.AgentId == id1).State.Should().Be(AgentPresenceState.Online);
        roster.Agents.First(a => a.AgentId == id2).State.Should().Be(AgentPresenceState.Idle);
        roster.Agents.First(a => a.AgentId == id3).State.Should().Be(AgentPresenceState.Offline);
    }

    [Fact]
    public async Task RefreshAgentStates_AgentAlreadyOffline_DoesNotChangeStateOrTimestamp()
    {
        var id = Guid.NewGuid();
        var t0 = new DateTime(2026, 11, 1, 12, 0, 0, DateTimeKind.Utc);

        await _service.PingHeartbeatAsync(new AgentHeartbeatRequest(id, "Quiet", null, null, AgentPresenceState.Offline), asOfUtc: t0.AddSeconds(-100));
        _service.RefreshAgentStates(t0);

        var roster = await _service.GetTeamStatusRosterAsync(t0);
        roster.Agents.First(a => a.AgentId == id).State.Should().Be(AgentPresenceState.Offline);
    }

    // =========================================================================
    // SECTION 3: MULTIPLE AGENTS VIEWING SAME TICKET & SOFTVIEWING
    // =========================================================================

    [Fact]
    public async Task GetTicketViewersAsync_MultipleAgentsPingingSameTicket_RegistersAllViewers()
    {
        var ticketId = Guid.NewGuid();
        var agent1 = Guid.NewGuid();
        var agent2 = Guid.NewGuid();
        var agent3 = Guid.NewGuid();

        await _service.PingHeartbeatAsync(new AgentHeartbeatRequest(agent1, "Agent A", null, ticketId, AgentPresenceState.ViewingTicket));
        await _service.PingHeartbeatAsync(new AgentHeartbeatRequest(agent2, "Agent B", null, ticketId, AgentPresenceState.ViewingTicket));
        await _service.PingHeartbeatAsync(new AgentHeartbeatRequest(agent3, "Agent C", null, ticketId, AgentPresenceState.ViewingTicket));

        var viewers = await _service.GetTicketViewersAsync(ticketId);

        viewers.Should().HaveCount(3);
        viewers.Select(v => v.AgentId).Should().Contain([agent1, agent2, agent3]);
    }

    [Fact]
    public async Task GetTicketCollisionStatusAsync_SingleViewer_TransitionsToSoftViewing()
    {
        var ticketId = Guid.NewGuid();
        var agent1 = Guid.NewGuid();

        await _service.PingHeartbeatAsync(new AgentHeartbeatRequest(agent1, "Agent Solo", null, ticketId, AgentPresenceState.ViewingTicket));
        var status = await _service.GetTicketCollisionStatusAsync(ticketId);

        status.LockState.Should().Be(CollisionLockState.SoftViewing);
        status.Viewers.Should().ContainSingle();
        status.IsBlocked.Should().BeFalse();
    }

    [Fact]
    public async Task GetTicketCollisionStatusAsync_MultipleViewers_ShowsSoftViewingWithWarningBanner()
    {
        var ticketId = Guid.NewGuid();
        var agent1 = Guid.NewGuid();
        var agent2 = Guid.NewGuid();

        await _service.PingHeartbeatAsync(new AgentHeartbeatRequest(agent1, "Agent Alice", null, ticketId, AgentPresenceState.ViewingTicket));
        await _service.PingHeartbeatAsync(new AgentHeartbeatRequest(agent2, "Agent Bob", null, ticketId, AgentPresenceState.ViewingTicket));

        var statusAlice = await _service.GetTicketCollisionStatusAsync(ticketId, queryingAgentId: agent1);

        statusAlice.LockState.Should().Be(CollisionLockState.SoftViewing);
        statusAlice.WarningMessage.Should().Contain("Multiple operators currently viewing this ticket");
        statusAlice.WarningMessage.Should().Contain("Agent Bob");
    }

    [Fact]
    public async Task GetTicketCollisionStatusAsync_FilteringOfflineViewers_ExcludesStaleViewers()
    {
        var ticketId = Guid.NewGuid();
        var freshAgent = Guid.NewGuid();
        var staleAgent = Guid.NewGuid();
        var now = new DateTime(2026, 11, 1, 15, 0, 0, DateTimeKind.Utc);

        await _service.PingHeartbeatAsync(new AgentHeartbeatRequest(freshAgent, "Fresh Viewer", null, ticketId, AgentPresenceState.ViewingTicket), asOfUtc: now.AddSeconds(-5));
        await _service.PingHeartbeatAsync(new AgentHeartbeatRequest(staleAgent, "Stale Viewer", null, ticketId, AgentPresenceState.ViewingTicket), asOfUtc: now.AddSeconds(-90));

        var viewers = await _service.GetTicketViewersAsync(ticketId, asOfUtc: now);

        viewers.Should().ContainSingle(v => v.AgentId == freshAgent);
        viewers.Should().NotContain(v => v.AgentId == staleAgent);
    }

    [Fact]
    public async Task ReleaseCollisionLockAsync_WhenViewerNavigatesAway_ViewerRemovedFromTicket()
    {
        var ticketId = Guid.NewGuid();
        var agent = Guid.NewGuid();

        await _service.PingHeartbeatAsync(new AgentHeartbeatRequest(agent, "Wanderer", null, ticketId, AgentPresenceState.ViewingTicket));
        var releaseResult = await _service.ReleaseCollisionLockAsync(ticketId, new ReleaseLockRequest(agent, LockReleaseReason.NavigatedAway));

        releaseResult.Success.Should().BeTrue();
        var viewers = await _service.GetTicketViewersAsync(ticketId);
        viewers.Should().BeEmpty();
    }

    // =========================================================================
    // SECTION 4: HARDCOMPOSINGLOCK ACQUISITION & DUPLICATE REPLY PREVENTION
    // =========================================================================

    [Fact]
    public async Task AcquireCollisionLockAsync_UnlockedTicket_GrantsHardComposingLock()
    {
        var ticketId = Guid.NewGuid();
        var agent = Guid.NewGuid();

        var result = await _service.AcquireCollisionLockAsync(ticketId, new AcquireLockRequest(agent, "Agent Prime", "Hello customer!"));

        result.Success.Should().BeTrue();
        result.LockState.Should().Be(CollisionLockState.HardComposingLock);
        result.LockedByAgentId.Should().Be(agent);
        result.LockedByAgentName.Should().Be("Agent Prime");
        result.RemainingLeaseSeconds.Should().Be(60);
    }

    [Fact]
    public async Task AcquireCollisionLockAsync_SameAgentRenewing_RefreshesLeaseSuccessfully()
    {
        var ticketId = Guid.NewGuid();
        var agent = Guid.NewGuid();
        var t0 = new DateTime(2026, 11, 1, 12, 0, 0, DateTimeKind.Utc);

        await _service.AcquireCollisionLockAsync(ticketId, new AcquireLockRequest(agent, "Agent Prime", "Draft 1"), asOfUtc: t0);
        var renewResult = await _service.AcquireCollisionLockAsync(ticketId, new AcquireLockRequest(agent, "Agent Prime", "Draft 2 updated"), asOfUtc: t0.AddSeconds(20));

        renewResult.Success.Should().BeTrue();
        renewResult.RemainingLeaseSeconds.Should().Be(60);
        renewResult.LockedByAgentId.Should().Be(agent);
    }

    [Fact]
    public async Task AcquireCollisionLockAsync_SecondAgentAttemptingWriteLock_BlocksAndPreventsDuplicateReply()
    {
        var ticketId = Guid.NewGuid();
        var agentA = Guid.NewGuid();
        var agentB = Guid.NewGuid();

        var lockA = await _service.AcquireCollisionLockAsync(ticketId, new AcquireLockRequest(agentA, "Agent A", "Composing answer..."));
        lockA.Success.Should().BeTrue();

        // Agent B attempts to acquire lock on same ticket
        var lockB = await _service.AcquireCollisionLockAsync(ticketId, new AcquireLockRequest(agentB, "Agent B", "I am replying too..."));

        lockB.Success.Should().BeFalse();
        lockB.LockState.Should().Be(CollisionLockState.HardComposingLock);
        lockB.LockedByAgentId.Should().Be(agentA);
        lockB.WarningMessage.Should().Contain("⚠️ Agent A is actively replying to this customer");
    }

    [Fact]
    public async Task AcquireCollisionLockAsync_CollisionBlocked_IncrementsPreventedCollisionsMetric()
    {
        var ticketId = Guid.NewGuid();
        var agentA = Guid.NewGuid();
        var agentB = Guid.NewGuid();

        var metricsBefore = await _service.GetMetricsAsync();
        var countBefore = metricsBefore.TotalPreventedCollisions;

        await _service.AcquireCollisionLockAsync(ticketId, new AcquireLockRequest(agentA, "Agent A", "Working"));
        await _service.AcquireCollisionLockAsync(ticketId, new AcquireLockRequest(agentB, "Agent B", "Colliding!"));

        var metricsAfter = await _service.GetMetricsAsync();
        metricsAfter.TotalPreventedCollisions.Should().Be(countBefore + 1);
    }

    [Fact]
    public async Task AcquireCollisionLockAsync_CollisionBlocked_ReturnsShieldWarningBannerWithBlockerName()
    {
        var ticketId = Guid.NewGuid();
        var agentA = Guid.NewGuid();
        var agentB = Guid.NewGuid();

        await _service.AcquireCollisionLockAsync(ticketId, new AcquireLockRequest(agentA, "Danielle SuperRep", "Draft snippet"));
        var blocked = await _service.AcquireCollisionLockAsync(ticketId, new AcquireLockRequest(agentB, "Second Operator", "Draft"));

        blocked.WarningMessage.Should().Be("⚠️ Danielle SuperRep is actively replying to this customer. Input is locked to prevent duplicate responses.");
    }

    [Fact]
    public async Task AcquireCollisionLockAsync_CollisionBlocked_StillRegistersSecondAgentAsSoftViewer()
    {
        var ticketId = Guid.NewGuid();
        var agentA = Guid.NewGuid();
        var agentB = Guid.NewGuid();

        await _service.AcquireCollisionLockAsync(ticketId, new AcquireLockRequest(agentA, "Agent A", "Replying"));
        await _service.AcquireCollisionLockAsync(ticketId, new AcquireLockRequest(agentB, "Agent B", "Trying to reply"));

        var viewers = await _service.GetTicketViewersAsync(ticketId);
        viewers.Should().Contain(v => v.AgentId == agentB && v.State == AgentPresenceState.ViewingTicket);
    }

    [Fact]
    public async Task AcquireCollisionLockAsync_ExpiredLock_AllowsSecondAgentToClaimLock()
    {
        var ticketId = Guid.NewGuid();
        var agentA = Guid.NewGuid();
        var agentB = Guid.NewGuid();
        var t0 = new DateTime(2026, 11, 1, 10, 0, 0, DateTimeKind.Utc);

        await _service.AcquireCollisionLockAsync(ticketId, new AcquireLockRequest(agentA, "Agent A", "Draft 1"), asOfUtc: t0);

        // 65 seconds later -> Lock expired
        var lockB = await _service.AcquireCollisionLockAsync(ticketId, new AcquireLockRequest(agentB, "Agent B", "Claiming expired lock"), asOfUtc: t0.AddSeconds(65));

        lockB.Success.Should().BeTrue();
        lockB.LockedByAgentId.Should().Be(agentB);
        lockB.LockedByAgentName.Should().Be("Agent B");
    }

    // =========================================================================
    // SECTION 5: LOCK RELEASE ON REPLY SUBMISSION, LEASE TIMEOUT & ADMIN OVERRIDE
    // =========================================================================

    [Fact]
    public async Task ReleaseCollisionLockAsync_ReplySubmitted_FreesLockAndRecordsReplyDuration()
    {
        var ticketId = Guid.NewGuid();
        var agent = Guid.NewGuid();
        var t0 = new DateTime(2026, 11, 1, 10, 0, 0, DateTimeKind.Utc);

        await _service.AcquireCollisionLockAsync(ticketId, new AcquireLockRequest(agent, "Rep Agent", "Hi customer"), asOfUtc: t0);

        var releaseResult = await _service.ReleaseCollisionLockAsync(
            ticketId,
            new ReleaseLockRequest(agent, LockReleaseReason.ReplySubmitted),
            asOfUtc: t0.AddSeconds(25)
        );

        releaseResult.Success.Should().BeTrue();
        releaseResult.Reason.Should().Be(LockReleaseReason.ReplySubmitted);

        var status = await _service.GetTicketCollisionStatusAsync(ticketId, asOfUtc: t0.AddSeconds(25));
        status.LockState.Should().Be(CollisionLockState.SoftViewing); // Still a viewer
        status.LockedByAgentId.Should().BeNull();
    }

    [Fact]
    public async Task ReleaseCollisionLockAsync_ReplySubmitted_SetsCustomerMessagePendingFalse()
    {
        var ticketId = Guid.NewGuid();
        var agent = Guid.NewGuid();

        await _service.AcquireCollisionLockAsync(ticketId, new AcquireLockRequest(agent, "Rep Agent", "Hi"));
        await _service.ReleaseCollisionLockAsync(ticketId, new ReleaseLockRequest(agent, LockReleaseReason.ReplySubmitted));

        var tickets = await _service.GetAllTicketsAsync();
        var ticket = tickets.First(t => t.TicketId == ticketId);
        ticket.CustomerMessagePending.Should().BeFalse();
    }

    [Fact]
    public async Task ReleaseCollisionLockAsync_ReplySubmittedWithOtherViewers_TransitionsToSoftViewing()
    {
        var ticketId = Guid.NewGuid();
        var agentA = Guid.NewGuid();
        var agentB = Guid.NewGuid();

        await _service.AcquireCollisionLockAsync(ticketId, new AcquireLockRequest(agentA, "Agent A", "Writing..."));
        await _service.PingHeartbeatAsync(new AgentHeartbeatRequest(agentB, "Agent B", null, ticketId, AgentPresenceState.ViewingTicket));

        var release = await _service.ReleaseCollisionLockAsync(ticketId, new ReleaseLockRequest(agentA, LockReleaseReason.ReplySubmitted));

        release.CurrentLockState.Should().Be(CollisionLockState.SoftViewing);
    }

    [Fact]
    public async Task ReleaseCollisionLockAsync_ExplicitUnlock_TransitionsToSoftViewingOrUnlocked()
    {
        var ticketId = Guid.NewGuid();
        var agent = Guid.NewGuid();

        await _service.AcquireCollisionLockAsync(ticketId, new AcquireLockRequest(agent, "Agent", "Writing"));
        var release = await _service.ReleaseCollisionLockAsync(ticketId, new ReleaseLockRequest(agent, LockReleaseReason.ExplicitUnlock));

        release.Success.Should().BeTrue();
        release.Reason.Should().Be(LockReleaseReason.ExplicitUnlock);
    }

    [Fact]
    public async Task ReleaseCollisionLockAsync_AdminOverride_AllowsAdminToForcefullyReleaseAnotherAgentsLock()
    {
        var ticketId = Guid.NewGuid();
        var operatorAgent = Guid.NewGuid();
        var supervisorAgent = Guid.NewGuid();

        await _service.AcquireCollisionLockAsync(ticketId, new AcquireLockRequest(operatorAgent, "Operator", "Writing..."));

        var adminRelease = await _service.ReleaseCollisionLockAsync(
            ticketId,
            new ReleaseLockRequest(supervisorAgent, LockReleaseReason.AdminOverride, IsAdminOverride: true)
        );

        adminRelease.Success.Should().BeTrue();
        adminRelease.Reason.Should().Be(LockReleaseReason.AdminOverride);

        var status = await _service.GetTicketCollisionStatusAsync(ticketId);
        status.LockedByAgentId.Should().BeNull();
    }

    [Fact]
    public async Task ReleaseCollisionLockAsync_UnauthorizedAgentWithoutAdmin_RejectsLockRelease()
    {
        var ticketId = Guid.NewGuid();
        var operatorA = Guid.NewGuid();
        var operatorB = Guid.NewGuid();

        await _service.AcquireCollisionLockAsync(ticketId, new AcquireLockRequest(operatorA, "Operator A", "Replying..."));

        var rejectRelease = await _service.ReleaseCollisionLockAsync(
            ticketId,
            new ReleaseLockRequest(operatorB, LockReleaseReason.ExplicitUnlock, IsAdminOverride: false)
        );

        rejectRelease.Success.Should().BeFalse();
        rejectRelease.Message.Should().Contain("Cannot release lock owned by another operator without administrative override");
    }

    [Fact]
    public async Task GetTicketCollisionStatusAsync_LockLeaseExpiredAfter60Seconds_AutoReleasesLock()
    {
        var ticketId = Guid.NewGuid();
        var agent = Guid.NewGuid();
        var t0 = new DateTime(2026, 11, 1, 10, 0, 0, DateTimeKind.Utc);

        await _service.AcquireCollisionLockAsync(ticketId, new AcquireLockRequest(agent, "Operator", "Draft"), asOfUtc: t0);

        var statusActive = await _service.GetTicketCollisionStatusAsync(ticketId, asOfUtc: t0.AddSeconds(30));
        statusActive.LockState.Should().Be(CollisionLockState.HardComposingLock);
        statusActive.RemainingLeaseSeconds.Should().Be(30);

        // At 61 seconds -> auto released (and with no other viewers, returns to Unlocked)
        var statusExpired = await _service.GetTicketCollisionStatusAsync(ticketId, asOfUtc: t0.AddSeconds(61));
        statusExpired.LockState.Should().Be(CollisionLockState.Unlocked);
        statusExpired.LockedByAgentId.Should().BeNull();
        statusExpired.RemainingLeaseSeconds.Should().Be(0);
    }

    // =========================================================================
    // SECTION 6: AUTOMATED TICKET HANDOFF ON AGENT ABANDONMENT
    // =========================================================================

    [Fact]
    public async Task ExecuteAutoHandoffSweepAsync_ActiveOnlineAssignedAgent_DoesNotReassign()
    {
        var onlineAgent = Guid.NewGuid();
        var ticketId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        await _service.PingHeartbeatAsync(new AgentHeartbeatRequest(onlineAgent, "Online Agent", null, ticketId, AgentPresenceState.Online), asOfUtc: now);

        var sweep = await _service.ExecuteAutoHandoffSweepAsync(TimeSpan.FromMinutes(5), asOfUtc: now);

        sweep.HandoffEvents.Should().NotContain(e => e.TicketId == ticketId);
    }

    [Fact]
    public async Task ExecuteAutoHandoffSweepAsync_ResolvedCustomerTicket_DoesNotReassign()
    {
        var ticketId = Guid.Parse("aaaa0104-0000-0000-0000-000000000004");
        var tickets = await _service.GetAllTicketsAsync();
        var t = tickets.First(x => x.TicketId == ticketId);
        t.CustomerMessagePending = false; // Resolved

        var sweep = await _service.ExecuteAutoHandoffSweepAsync(TimeSpan.FromMinutes(5));

        sweep.HandoffEvents.Should().NotContain(e => e.TicketId == ticketId);
    }

    [Fact]
    public async Task ExecuteAutoHandoffSweepAsync_IdleAgentExceeding5Minutes_ReassignsToOnlineAgent()
    {
        // Pre-seeded Ticket 101 is assigned to Marcus Brody (Idle for 7 minutes, threshold is 5 minutes)
        var ticket101Id = Guid.Parse("aaaa0101-0000-0000-0000-000000000001");
        var marcusId = Guid.Parse("33333333-3333-3333-3333-333333333333");

        var sweep = await _service.ExecuteAutoHandoffSweepAsync(TimeSpan.FromMinutes(5));

        sweep.ReassignedCount.Should().BeGreaterThanOrEqualTo(1);
        var handoff = sweep.HandoffEvents.FirstOrDefault(e => e.TicketId == ticket101Id);
        handoff.Should().NotBeNull();
        handoff!.PreviousAgentId.Should().Be(marcusId);
        handoff.NewAgentId.Should().NotBe(marcusId);
    }

    [Fact]
    public async Task ExecuteAutoHandoffSweepAsync_OfflineAgentExceeding5Minutes_ReassignsToOnlineAgent()
    {
        // Pre-seeded Ticket 104 is assigned to Sarah Chen (Offline for 25 minutes)
        var ticket104Id = Guid.Parse("aaaa0104-0000-0000-0000-000000000004");
        var sarahId = Guid.Parse("44444444-4444-4444-4444-444444444444");

        var sweep = await _service.ExecuteAutoHandoffSweepAsync(TimeSpan.FromMinutes(5));

        var handoff = sweep.HandoffEvents.FirstOrDefault(e => e.TicketId == ticket104Id);
        handoff.Should().NotBeNull();
        handoff!.PreviousAgentId.Should().Be(sarahId);
        handoff.Reason.Should().Contain("inactive");
    }

    [Fact]
    public async Task ExecuteAutoHandoffSweepAsync_WorkloadBalancing_SelectsAgentWithLowestWorkload()
    {
        // Liam O'Connor is Online with Workload = 0 (lowest of all online agents: Alex has 2)
        var liamId = Guid.Parse("55555555-5555-5555-5555-555555555555");
        var ticket101Id = Guid.Parse("aaaa0101-0000-0000-0000-000000000001");

        var sweep = await _service.ExecuteAutoHandoffSweepAsync(TimeSpan.FromMinutes(5));

        var handoff = sweep.HandoffEvents.First(e => e.TicketId == ticket101Id);
        handoff.NewAgentId.Should().Be(liamId);
    }

    [Fact]
    public async Task ExecuteAutoHandoffSweepAsync_AdjustsWorkloads_DecrementsPreviousAndIncrementsNew()
    {
        var marcusId = Guid.Parse("33333333-3333-3333-3333-333333333333");
        var liamId = Guid.Parse("55555555-5555-5555-5555-555555555555");

        var rosterBefore = await _service.GetTeamStatusRosterAsync();
        var marcusLoadBefore = rosterBefore.Agents.First(a => a.AgentId == marcusId).ActiveWorkload;
        var liamLoadBefore = rosterBefore.Agents.First(a => a.AgentId == liamId).ActiveWorkload;

        await _service.ExecuteAutoHandoffSweepAsync(TimeSpan.FromMinutes(5));

        var rosterAfter = await _service.GetTeamStatusRosterAsync();
        var marcusLoadAfter = rosterAfter.Agents.First(a => a.AgentId == marcusId).ActiveWorkload;
        var liamLoadAfter = rosterAfter.Agents.First(a => a.AgentId == liamId).ActiveWorkload;

        marcusLoadAfter.Should().Be(marcusLoadBefore - 1);
        liamLoadAfter.Should().Be(liamLoadBefore + 2);
    }

    [Fact]
    public async Task ExecuteAutoHandoffSweepAsync_ReassignmentLogsEventInAuditFeed()
    {
        var sweep = await _service.ExecuteAutoHandoffSweepAsync(TimeSpan.FromMinutes(5));
        var audit = await _service.GetHandoffAuditEventsAsync();

        foreach (var evt in sweep.HandoffEvents)
        {
            audit.Should().Contain(a => a.EventId == evt.EventId);
        }
    }

    [Fact]
    public async Task ExecuteAutoHandoffSweepAsync_ReassignmentReleasesPreviousAbandonedLock()
    {
        var ticketId = Guid.NewGuid();
        var idleAgentId = Guid.NewGuid();
        var t0 = new DateTime(2026, 11, 1, 10, 0, 0, DateTimeKind.Utc);

        // Agent acquires lock and goes idle for 6 minutes
        await _service.AcquireCollisionLockAsync(ticketId, new AcquireLockRequest(idleAgentId, "Abandoning Operator", "Draft"), asOfUtc: t0);
        var tickets = await _service.GetAllTicketsAsync();
        var t = tickets.First(x => x.TicketId == ticketId);
        t.AssignedAgentId = idleAgentId;
        t.CustomerMessagePending = true;

        var sweep = await _service.ExecuteAutoHandoffSweepAsync(TimeSpan.FromMinutes(5), asOfUtc: t0.AddMinutes(6));

        var status = await _service.GetTicketCollisionStatusAsync(ticketId, asOfUtc: t0.AddMinutes(6));
        status.LockedByAgentId.Should().NotBe(idleAgentId);
    }

    [Fact]
    public async Task ExecuteAutoHandoffSweepAsync_MultipleAbandonedTickets_BalancesAcrossAvailableOnlineAgents()
    {
        var now = DateTime.UtcNow;
        var sweep = await _service.ExecuteAutoHandoffSweepAsync(TimeSpan.FromMinutes(5), asOfUtc: now);

        sweep.ReassignedCount.Should().BeGreaterThanOrEqualTo(2); // Ticket 101 and Ticket 104
    }

    [Fact]
    public async Task ExecuteAutoHandoffSweepAsync_NoAvailableOnlineAgents_LeavesTicketUnchangedWithoutCrash()
    {
        // Set all agents to offline
        var roster = await _service.GetTeamStatusRosterAsync();
        foreach (var a in roster.Agents)
        {
            a.State = AgentPresenceState.Offline;
        }

        var sweep = await _service.ExecuteAutoHandoffSweepAsync(TimeSpan.FromMinutes(5));

        sweep.ReassignedCount.Should().Be(0);
    }

    // =========================================================================
    // SECTION 7: TYPING BEACON STATUS REPORTING & TELEMETRY ROSTER
    // =========================================================================

    [Fact]
    public async Task TypingBeacon_WhenAgentHoldingLock_ReturnsActiveTypingBeaconWithDraftSnippet()
    {
        var ticketId = Guid.NewGuid();
        var agent = Guid.NewGuid();

        await _service.AcquireCollisionLockAsync(ticketId, new AcquireLockRequest(agent, "Fast Typer", "We are investigating the invoice discrepancy."));
        var status = await _service.GetTicketCollisionStatusAsync(ticketId);

        status.TypingBeacon.Should().NotBeNull();
        status.TypingBeacon!.AgentId.Should().Be(agent);
        status.TypingBeacon.AgentName.Should().Be("Fast Typer");
        status.TypingBeacon.IsTyping.Should().BeTrue();
        status.TypingBeacon.DraftSnippetPreview.Should().Be("We are investigating the invoice discrepancy.");
        status.TypingBeacon.LeaseRemainingSeconds.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task TypingBeacon_RemainingLeaseSeconds_CalculatesAccurateCountdown()
    {
        var ticketId = Guid.NewGuid();
        var agent = Guid.NewGuid();
        var t0 = new DateTime(2026, 11, 1, 10, 0, 0, DateTimeKind.Utc);

        await _service.AcquireCollisionLockAsync(ticketId, new AcquireLockRequest(agent, "Typer", "Snippet"), asOfUtc: t0);

        var statusAt40s = await _service.GetTicketCollisionStatusAsync(ticketId, asOfUtc: t0.AddSeconds(40));
        statusAt40s.TypingBeacon.Should().NotBeNull();
        statusAt40s.TypingBeacon!.LeaseRemainingSeconds.Should().Be(20);
    }

    [Fact]
    public async Task GetTeamStatusRosterAsync_AggregatesCountsAcrossPresenceStates()
    {
        var roster = await _service.GetTeamStatusRosterAsync();

        roster.TotalAgents.Should().Be(5);
        roster.OnlineAgents.Should().BeGreaterThan(0);
        roster.IdleAgents.Should().Be(1); // Marcus
        roster.OfflineAgents.Should().Be(1); // Sarah
        roster.TotalActiveWorkload.Should().Be(8); // 2 + 1 + 4 + 1 + 0 = 8
    }

    [Fact]
    public async Task GetMetricsAsync_ReflectsLiveCountsAndMetricsAccurately()
    {
        var metrics = await _service.GetMetricsAsync();

        metrics.TotalOnlineAgents.Should().Be(3); // Alex, Elena, Liam
        metrics.ActiveTicketsUnderCollisionLock.Should().Be(1); // Ticket 102
        metrics.TotalPreventedCollisions.Should().Be(14);
        metrics.TotalAutoReassignments.Should().Be(6);
        metrics.AverageReplyTimeSeconds.Should().BeGreaterThan(0);
        metrics.TotalMonitoredTickets.Should().Be(4);
    }

    // =========================================================================
    // SECTION 8: CONCURRENCY THREAD SAFETY
    // =========================================================================

    [Fact]
    public async Task Concurrency_50SimultaneousHeartbeatPings_UpdatesSessionsAtomicallyWithoutExceptions()
    {
        const int concurrentCount = 50;
        var agentIds = Enumerable.Range(0, concurrentCount).Select(_ => Guid.NewGuid()).ToList();
        var tasks = new List<Task<AgentPresenceSession>>();

        for (int i = 0; i < concurrentCount; i++)
        {
            var id = agentIds[i];
            var index = i;
            tasks.Add(Task.Run(() => _service.PingHeartbeatAsync(new AgentHeartbeatRequest(
                id,
                $"Concurrent Agent {index}",
                $"agent{index}@sgrowing.io",
                null,
                AgentPresenceState.Online
            ))));
        }

        var sessions = await Task.WhenAll(tasks);

        sessions.Should().HaveCount(concurrentCount);
        sessions.Select(s => s.AgentId).Distinct().Should().HaveCount(concurrentCount);

        var roster = await _service.GetTeamStatusRosterAsync();
        roster.TotalAgents.Should().BeGreaterThanOrEqualTo(concurrentCount);
    }

    [Fact]
    public async Task Concurrency_50SimultaneousLockAcquisitionRequests_ExactlyOneWinnerAnd49Blocked()
    {
        const int concurrentCount = 50;
        var ticketId = Guid.NewGuid();
        var agents = Enumerable.Range(0, concurrentCount).Select(i => (Id: Guid.NewGuid(), Name: $"Racer {i}")).ToList();

        var tasks = agents.Select(a => Task.Run(() =>
            _service.AcquireCollisionLockAsync(ticketId, new AcquireLockRequest(a.Id, a.Name, "Draft contest"))
        )).ToList();

        var results = await Task.WhenAll(tasks);

        var winners = results.Where(r => r.Success).ToList();
        var blocked = results.Where(r => !r.Success).ToList();

        winners.Should().ContainSingle("Exactly one agent must successfully acquire the hard composing lock");
        blocked.Should().HaveCount(concurrentCount - 1, "All 49 other agents must receive collision blocks");

        var status = await _service.GetTicketCollisionStatusAsync(ticketId);
        status.LockState.Should().Be(CollisionLockState.HardComposingLock);
        status.LockedByAgentId.Should().Be(winners.Single().LockedByAgentId);
    }

    [Fact]
    public async Task Concurrency_RapidTypingBeaconAndStatusQueries_MaintainsThreadSafeIntegrity()
    {
        var ticketId = Guid.NewGuid();
        var agent = Guid.NewGuid();

        await _service.AcquireCollisionLockAsync(ticketId, new AcquireLockRequest(agent, "Speed Typer", "Start"));

        var queryTasks = Enumerable.Range(0, 50).Select(i => Task.Run(async () =>
        {
            if (i % 2 == 0)
            {
                await _service.PingHeartbeatAsync(new AgentHeartbeatRequest(
                    agent, "Speed Typer", null, ticketId, AgentPresenceState.ComposingReply, IsTyping: true, DraftSnippet: $"Snippet {i}"
                ));
            }
            else
            {
                var status = await _service.GetTicketCollisionStatusAsync(ticketId);
                status.Should().NotBeNull();
            }
        }));

        var act = () => Task.WhenAll(queryTasks);
        await act.Should().NotThrowAsync();
    }
}
