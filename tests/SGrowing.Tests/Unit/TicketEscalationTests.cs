using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using SGrowing.Api.Common;
using SGrowing.Api.Modules.Support.TicketEscalation;
using Xunit;

namespace SGrowing.Tests.Unit;

public class TicketEscalationTests
{
    private readonly TicketEscalationService _service;
    private readonly Guid _tenantId = TicketEscalationService.DefaultTenantId;

    public TicketEscalationTests()
    {
        _service = new TicketEscalationService();
        _service.Reset();
    }

    [Fact]
    public void GetTickets_ReturnsSeededTickets_ForDefaultTenant()
    {
        var tickets = _service.GetTickets(_tenantId);

        tickets.Should().NotBeNull();
        tickets.Should().HaveCount(5);

        tickets.Should().Contain(t => t.TicketId == TicketEscalationService.SeedTicket1Id && t.State == EscalationState.WarningSlaDueSoon);
        tickets.Should().Contain(t => t.TicketId == TicketEscalationService.SeedTicket2Id && t.State == EscalationState.EscalatedTier2);
        tickets.Should().Contain(t => t.TicketId == TicketEscalationService.SeedTicket3Id && t.State == EscalationState.EscalatedTier3);
        tickets.Should().Contain(t => t.TicketId == TicketEscalationService.SeedTicket4Id && t.State == EscalationState.Normal);
        tickets.Should().Contain(t => t.TicketId == TicketEscalationService.SeedTicket5Id && t.State == EscalationState.Resolved);
    }

    [Fact]
    public void GetTickets_FiltersByTierAndState()
    {
        var tier2Tickets = _service.GetTickets(_tenantId, tier: SupportTier.Tier2_TechnicalSpecialist);
        tier2Tickets.Should().HaveCount(1);
        tier2Tickets.First().TicketId.Should().Be(TicketEscalationService.SeedTicket2Id);

        var resolvedTickets = _service.GetTickets(_tenantId, state: EscalationState.Resolved);
        resolvedTickets.Should().HaveCount(1);
        resolvedTickets.First().TicketId.Should().Be(TicketEscalationService.SeedTicket5Id);
    }

    [Fact]
    public void CreateTicket_AddsTicket_WithCorrectInitialTierAndSla()
    {
        var request = new CreateTicketRequest(
            _tenantId,
            "High Memory Utilization on Redis Cache Replica",
            "Fahad Al-Subaie",
            "fahad@riyadhfintech.sa",
            "Riyadh FinTech Labs",
            TicketPriority.High,
            "Performance",
            CustomerSentiment.Frustrated,
            "Memory utilization spiked above 92% causing cache evictions."
        );

        var created = _service.CreateTicket(request);

        created.Should().NotBeNull();
        created.TicketId.Should().NotBeEmpty();
        created.TicketNumber.Should().StartWith("TICK-");
        created.CurrentTier.Should().Be(SupportTier.Tier1_Frontline);
        created.State.Should().Be(EscalationState.Normal);
        created.Priority.Should().Be(TicketPriority.High);
        created.SlaDueAtUtc.Should().BeAfter(created.CreatedAtUtc);
        (created.SlaDueAtUtc - created.CreatedAtUtc).TotalMinutes.Should().BeApproximately(60, 1.0);
        created.AssignedAgent.Should().Contain("Performance Triage");
        created.EscalationLogs.Should().HaveCount(1);

        var fetched = _service.GetTicketById(_tenantId, created.TicketId);
        fetched.Should().NotBeNull();
        fetched!.Subject.Should().Be("High Memory Utilization on Redis Cache Replica");
    }

    [Fact]
    public void EscalateTicket_TransitionsToTier2_AndLogsHistory()
    {
        var escalateReq = new EscalateTicketRequest(
            _tenantId,
            TicketEscalationService.SeedTicket1Id,
            SupportTier.Tier2_TechnicalSpecialist,
            "Requires deep packet inspection on regional webhook delivery gateway.",
            "Hala Al-Mansoor"
        );

        var escalated = _service.EscalateTicket(escalateReq);

        escalated.Should().NotBeNull();
        escalated!.CurrentTier.Should().Be(SupportTier.Tier2_TechnicalSpecialist);
        escalated.State.Should().Be(EscalationState.EscalatedTier2);
        escalated.AssignedAgent.Should().Contain("Tier 2");
        escalated.EscalationLogs.Should().HaveCount(2);

        var lastLog = escalated.EscalationLogs.Last();
        lastLog.FromTier.Should().Be(SupportTier.Tier1_Frontline);
        lastLog.ToTier.Should().Be(SupportTier.Tier2_TechnicalSpecialist);
        lastLog.Reason.Should().Contain("deep packet inspection");
        lastLog.TriggeredBy.Should().Be("Hala Al-Mansoor");
    }

    [Fact]
    public void EscalateTicket_TransitionsToTier3_WhenUrgent()
    {
        var escalateReq = new EscalateTicketRequest(
            _tenantId,
            TicketEscalationService.SeedTicket2Id,
            SupportTier.Tier3_EngineeringOps,
            "Core database cluster master failover required.",
            "Zaid Al-Najjar"
        );

        var escalated = _service.EscalateTicket(escalateReq);

        escalated.Should().NotBeNull();
        escalated!.CurrentTier.Should().Be(SupportTier.Tier3_EngineeringOps);
        escalated.State.Should().Be(EscalationState.EscalatedTier3);
        escalated.AssignedAgent.Should().Contain("DevOps Core SWAT");
        escalated.EscalationLogs.Should().HaveCount(3);
    }

    [Fact]
    public void ResolveTicket_SetsResolvedState_AndRecordsTimestamp()
    {
        var resolveReq = new ResolveTicketRequest(
            _tenantId,
            TicketEscalationService.SeedTicket1Id,
            "Redis queue consumer thread restarted and delayed webhooks flushed successfully.",
            "Zaid Al-Najjar"
        );

        var resolved = _service.ResolveTicket(resolveReq);

        resolved.Should().NotBeNull();
        resolved!.State.Should().Be(EscalationState.Resolved);
        resolved.ResolvedAtUtc.Should().NotBeNull();
        resolved.ResolutionNotes.Should().Contain("Redis queue consumer");
        resolved.Tags.Should().Contain("Resolved");

        var report = _service.GetOverviewReport(_tenantId);
        report.TotalActiveTickets.Should().Be(3); // 4 initial active minus 1 resolved = 3
    }

    [Fact]
    public void SuggestMacro_GeneratesTailoredCannedResponse_ForFrustratedCustomer()
    {
        // SeedTicket1 has Frustrated sentiment
        var suggestionFrustrated = _service.SuggestMacro(_tenantId, TicketEscalationService.SeedTicket1Id);

        suggestionFrustrated.Should().NotBeNull();
        suggestionFrustrated.MacroTitle.Should().Contain("Specialist Deep-Dive");
        suggestionFrustrated.RecommendedResponse.Should().Contain("Tariq Al-Otaibi");
        suggestionFrustrated.ConfidenceScore.Should().BeGreaterThanOrEqualTo(0.90);
        suggestionFrustrated.ActionSteps.Should().NotBeEmpty();

        // SeedTicket3 has CriticalChurnRisk sentiment
        var suggestionCritical = _service.SuggestMacro(_tenantId, TicketEscalationService.SeedTicket3Id);
        suggestionCritical.Should().NotBeNull();
        suggestionCritical.MacroTitle.Should().Contain("Executive Incident Hotline");
        suggestionCritical.RecommendedResponse.Should().Contain("Dr. Amina Al-Zahrani");
        suggestionCritical.ConfidenceScore.Should().BeGreaterThanOrEqualTo(0.95);
    }

    [Fact]
    public void GetOverviewReport_CalculatesCorrectKpis_AndComplianceRate()
    {
        var report = _service.GetOverviewReport(_tenantId);

        report.Should().NotBeNull();
        report.TenantId.Should().Be(_tenantId);
        report.TotalActiveTickets.Should().Be(4); // SeedTicket1, 2, 3, 4 are active
        report.WarningSlaCount.Should().BeGreaterThanOrEqualTo(1);
        report.EscalatedTier2Count.Should().Be(1);
        report.EscalatedTier3Count.Should().Be(1);
        report.ResolvedTodayCount.Should().Be(1);
        report.SlaCompliancePercentage.Should().BeInRange(0.0, 100.0);
        report.Tickets.Should().HaveCount(5);
    }

    [Fact]
    public void StrictTenantBoundary_PreventsAccessToOtherTenantsData()
    {
        var foreignTenantId = Guid.NewGuid();

        // Querying foreign tenant returns empty list
        var foreignTickets = _service.GetTickets(foreignTenantId);
        foreignTickets.Should().BeEmpty();

        // Getting ticket with foreign tenant returns null
        var ticket = _service.GetTicketById(foreignTenantId, TicketEscalationService.SeedTicket1Id);
        ticket.Should().BeNull();

        // Escalating with foreign tenant returns null
        var escalateResult = _service.EscalateTicket(new EscalateTicketRequest(
            foreignTenantId,
            TicketEscalationService.SeedTicket1Id,
            SupportTier.Tier2_TechnicalSpecialist,
            "Illegal cross-tenant escalation attempt",
            "Attacker"
        ));
        escalateResult.Should().BeNull();

        // Suggesting macro for foreign tenant throws KeyNotFoundException
        Action act = () => _service.SuggestMacro(foreignTenantId, TicketEscalationService.SeedTicket1Id);
        act.Should().Throw<KeyNotFoundException>();
    }

    [Fact]
    public void Endpoints_ImplementsIEndpointModule_ParameterlessConstructor()
    {
        var moduleType = typeof(TicketEscalationEndpoints);
        typeof(IEndpointModule).IsAssignableFrom(moduleType).Should().BeTrue();

        var instance = Activator.CreateInstance(moduleType) as IEndpointModule;
        instance.Should().NotBeNull();
    }
}
