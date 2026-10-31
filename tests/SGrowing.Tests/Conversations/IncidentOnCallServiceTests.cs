using FluentAssertions;
using SGrowing.Api.Modules.Conversations.Incidents;
using Xunit;

namespace SGrowing.Tests.Conversations;

public class IncidentOnCallServiceTests
{
    private readonly IncidentOnCallService _service = new();
    private readonly Guid _tenantId = Guid.NewGuid();

    [Fact]
    public async Task CreateRosterAsync_ShouldSucceed_WithValidInputs()
    {
        var req = new CreateRosterRequest(
            _tenantId,
            "APAC Primary Shift",
            ShiftRotationType.Daily,
            Guid.NewGuid(),
            "Agent Alpha",
            Guid.NewGuid(),
            "Agent Beta",
            DateTime.UtcNow.AddHours(-1),
            DateTime.UtcNow.AddHours(7),
            "UTC"
        );

        var roster = await _service.CreateRosterAsync(req);

        roster.Should().NotBeNull();
        roster.RosterId.Should().NotBeEmpty();
        roster.Name.Should().Be("APAC Primary Shift");
        roster.PrimaryAgentName.Should().Be("Agent Alpha");
        roster.SecondaryAgentName.Should().Be("Agent Beta");
        roster.IsActive.Should().BeTrue();
    }

    [Theory]
    [InlineData("", "Agent Alpha")]
    [InlineData("   ", "Agent Alpha")]
    public async Task CreateRosterAsync_ShouldThrow_WhenNameIsMissing(string name, string agentName)
    {
        var req = new CreateRosterRequest(
            _tenantId,
            name,
            ShiftRotationType.Daily,
            Guid.NewGuid(),
            agentName,
            null,
            null,
            DateTime.UtcNow,
            DateTime.UtcNow.AddHours(8)
        );

        var act = () => _service.CreateRosterAsync(req);
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task CreateRosterAsync_ShouldThrow_WhenTenantOrAgentIsEmpty()
    {
        var req1 = new CreateRosterRequest(Guid.Empty, "Shift", ShiftRotationType.Daily, Guid.NewGuid(), "Agent", null, null, DateTime.UtcNow, DateTime.UtcNow.AddHours(8));
        var req2 = new CreateRosterRequest(_tenantId, "Shift", ShiftRotationType.Daily, Guid.Empty, "Agent", null, null, DateTime.UtcNow, DateTime.UtcNow.AddHours(8));

        await Assert.ThrowsAsync<ArgumentException>(() => _service.CreateRosterAsync(req1));
        await Assert.ThrowsAsync<ArgumentException>(() => _service.CreateRosterAsync(req2));
    }

    [Fact]
    public async Task GetCurrentOnCallAgentAsync_ShouldReturnActiveAgent_WhenWithinSchedule()
    {
        var agentId = Guid.NewGuid();
        var req = new CreateRosterRequest(
            _tenantId,
            "EU Support Roster",
            ShiftRotationType.Weekly,
            agentId,
            "Agent Charlie",
            null,
            null,
            DateTime.UtcNow.AddHours(-2),
            DateTime.UtcNow.AddHours(6)
        );

        await _service.CreateRosterAsync(req);

        var (resolvedAgentId, resolvedName) = await _service.GetCurrentOnCallAgentAsync(_tenantId);

        resolvedAgentId.Should().Be(agentId);
        resolvedName.Should().Be("Agent Charlie");
    }

    [Fact]
    public async Task CreateEscalationPolicyAsync_ShouldConfigurePolicy()
    {
        var req = new CreateEscalationPolicyRequest(
            _tenantId,
            "P1 Critical Immediate Page",
            IncidentSeverity.P1_Critical,
            10,
            new List<PagingChannel> { PagingChannel.Webhook, PagingChannel.VoiceCall },
            new List<EscalationTier> { EscalationTier.Tier1_AssignedAgent, EscalationTier.Tier2_OnCallLead, EscalationTier.Tier3_DepartmentHead }
        );

        var policy = await _service.CreateEscalationPolicyAsync(req);

        policy.PolicyId.Should().NotBeEmpty();
        policy.AckTimeoutMinutes.Should().Be(10);
        policy.PagingChannels.Should().Contain(PagingChannel.VoiceCall);
        policy.TargetTiers.Should().HaveCount(3);
    }

    [Fact]
    public async Task CreateEscalationPolicyAsync_ShouldThrow_WhenTimeoutIsZeroOrNegative()
    {
        var req = new CreateEscalationPolicyRequest(_tenantId, "Invalid", IncidentSeverity.P1_Critical, 0, new(), new());
        await Assert.ThrowsAsync<ArgumentException>(() => _service.CreateEscalationPolicyAsync(req));
    }

    [Theory]
    [InlineData(IncidentSeverity.P1_Critical, 15)]
    [InlineData(IncidentSeverity.P2_High, 30)]
    [InlineData(IncidentSeverity.P3_Medium, 60)]
    [InlineData(IncidentSeverity.P4_Low, 120)]
    public async Task TriggerIncidentAsync_ShouldApplyDefaultSeverityDeadlines(IncidentSeverity severity, int expectedMinutes)
    {
        var req = new TriggerIncidentRequest(_tenantId, $"Database Outage {severity}", "Immediate action required", severity);

        var inc = await _service.TriggerIncidentAsync(req);

        inc.Should().NotBeNull();
        inc.Severity.Should().Be(severity);
        inc.Status.Should().Be(IncidentStatus.Triggered);
        inc.CurrentTier.Should().Be(EscalationTier.Tier1_AssignedAgent);

        var diff = (inc.AckDeadlineUtc - inc.TriggeredAtUtc).TotalMinutes;
        diff.Should().BeApproximately(expectedMinutes, 1.0);

        inc.Timeline.Should().HaveCount(1);
        inc.DispatchedPages.Should().NotBeEmpty();
    }

    [Fact]
    public async Task TriggerIncidentAsync_ShouldUsePolicyTimeout_WhenDefined()
    {
        var policyReq = new CreateEscalationPolicyRequest(
            _tenantId,
            "VIP P1 Policy",
            IncidentSeverity.P1_Critical,
            8,
            new List<PagingChannel> { PagingChannel.InAppBanner, PagingChannel.Webhook },
            new List<EscalationTier> { EscalationTier.Tier1_AssignedAgent }
        );
        await _service.CreateEscalationPolicyAsync(policyReq);

        var inc = await _service.TriggerIncidentAsync(new TriggerIncidentRequest(_tenantId, "Core API Crash", "Down", IncidentSeverity.P1_Critical));

        var diff = (inc.AckDeadlineUtc - inc.TriggeredAtUtc).TotalMinutes;
        diff.Should().BeApproximately(8.0, 1.0);
    }

    [Fact]
    public async Task AcknowledgeIncidentAsync_ShouldHaltEscalation_AndLogTimeline()
    {
        var inc = await _service.TriggerIncidentAsync(new TriggerIncidentRequest(_tenantId, "Payment Gateway 504", "Failing", IncidentSeverity.P1_Critical));
        var agentId = Guid.NewGuid();

        var acked = await _service.AcknowledgeIncidentAsync(_tenantId, inc.IncidentId, agentId, "Alice DevOps");

        acked.Status.Should().Be(IncidentStatus.Acknowledged);
        acked.AcknowledgedAtUtc.Should().NotBeNull();
        acked.AssignedToAgentId.Should().Be(agentId);
        acked.AssignedToAgentName.Should().Be("Alice DevOps");

        acked.Timeline.Should().Contain(t => t.Action == "Acknowledged");
    }

    [Fact]
    public async Task AcknowledgeIncidentAsync_ShouldBeIdempotent_WhenAlreadyAcknowledged()
    {
        var inc = await _service.TriggerIncidentAsync(new TriggerIncidentRequest(_tenantId, "SSL Expired", "Warning", IncidentSeverity.P2_High));
        var agentId = Guid.NewGuid();

        await _service.AcknowledgeIncidentAsync(_tenantId, inc.IncidentId, agentId, "Alice");
        var reAck = await _service.AcknowledgeIncidentAsync(_tenantId, inc.IncidentId, agentId, "Alice");

        reAck.Status.Should().Be(IncidentStatus.Acknowledged);
    }

    [Fact]
    public async Task AcknowledgeIncidentAsync_ShouldThrow_WhenIncidentIsResolved()
    {
        var inc = await _service.TriggerIncidentAsync(new TriggerIncidentRequest(_tenantId, "Memory Leak", "High RAM", IncidentSeverity.P2_High));
        var agentId = Guid.NewGuid();
        await _service.ResolveIncidentAsync(_tenantId, inc.IncidentId, agentId, "Service restarted", "Worker leak");

        var act = () => _service.AcknowledgeIncidentAsync(_tenantId, inc.IncidentId, agentId, "Alice");
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task ResolveIncidentAsync_ShouldCalculateMttr_AndSetStatus()
    {
        var inc = await _service.TriggerIncidentAsync(new TriggerIncidentRequest(_tenantId, "WhatsApp Gateway Throttled", "Down", IncidentSeverity.P2_High));
        var agentId = Guid.NewGuid();

        var resolved = await _service.ResolveIncidentAsync(
            _tenantId,
            inc.IncidentId,
            agentId,
            "Increased Meta API burst limit and cleared local queue.",
            "Meta Cloud API rate limit exceeded."
        );

        resolved.Status.Should().Be(IncidentStatus.Resolved);
        resolved.ResolvedAtUtc.Should().NotBeNull();
        resolved.ResolutionSummary.Should().Contain("Increased Meta API burst limit");
        resolved.RootCause.Should().Contain("Meta Cloud API rate limit");
        resolved.Timeline.Should().Contain(t => t.Action == "Resolved");
    }

    [Fact]
    public async Task EvaluatePendingEscalationsSweepAsync_ShouldEscalateExpiredIncidents()
    {
        var inc = await _service.TriggerIncidentAsync(new TriggerIncidentRequest(_tenantId, "Server CPU 100%", "Unresponsive", IncidentSeverity.P1_Critical));
        inc.CurrentTier.Should().Be(EscalationTier.Tier1_AssignedAgent);

        // Simulate time advancing past deadline
        var future = inc.AckDeadlineUtc.AddMinutes(5);

        var escalatedList = await _service.EvaluatePendingEscalationsSweepAsync(_tenantId, nowUtc: future);

        escalatedList.Should().Contain(i => i.IncidentId == inc.IncidentId);

        var updated = await _service.GetIncidentByIdAsync(_tenantId, inc.IncidentId);
        updated!.CurrentTier.Should().Be(EscalationTier.Tier2_OnCallLead);
        updated.DispatchedPages.Should().Contain(p => p.Contains("AUTO-ESCALATED"));
        updated.Timeline.Should().Contain(t => t.Action == "TierEscalated");
    }

    [Fact]
    public async Task GetIncidentMetricsAsync_ShouldComputeAveragesAndBreaches()
    {
        var inc1 = await _service.TriggerIncidentAsync(new TriggerIncidentRequest(_tenantId, "Inc 1", "D1", IncidentSeverity.P1_Critical));
        var inc2 = await _service.TriggerIncidentAsync(new TriggerIncidentRequest(_tenantId, "Inc 2", "D2", IncidentSeverity.P2_High));

        var agentId = Guid.NewGuid();
        await _service.AcknowledgeIncidentAsync(_tenantId, inc1.IncidentId, agentId, "Agent X");
        await _service.ResolveIncidentAsync(_tenantId, inc1.IncidentId, agentId, "Fixed", "Bug");

        var metrics = await _service.GetIncidentMetricsAsync(_tenantId);

        metrics.TotalIncidents.Should().Be(2);
        metrics.ActiveIncidents.Should().Be(1);
        metrics.AcknowledgedCount.Should().Be(1);
        metrics.ResolvedCount.Should().Be(1);
        metrics.AverageMttaMinutes.Should().BeGreaterThanOrEqualTo(0.0);
        metrics.AverageMttrMinutes.Should().BeGreaterThanOrEqualTo(0.0);
    }

    [Fact]
    public async Task ListIncidentsAsync_ShouldFilterByStatusAndSeverity()
    {
        var incP1 = await _service.TriggerIncidentAsync(new TriggerIncidentRequest(_tenantId, "Major Crash", "P1", IncidentSeverity.P1_Critical));
        var incP4 = await _service.TriggerIncidentAsync(new TriggerIncidentRequest(_tenantId, "Typo in footer", "P4", IncidentSeverity.P4_Low));

        var p1Only = await _service.ListIncidentsAsync(_tenantId, severityFilter: IncidentSeverity.P1_Critical);
        p1Only.Should().ContainSingle(i => i.IncidentId == incP1.IncidentId);
        p1Only.Should().NotContain(i => i.IncidentId == incP4.IncidentId);
    }
}
