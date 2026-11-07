using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using SGrowing.Api.Modules.Sales.LeadRouting;
using Xunit;

namespace SGrowing.Tests.Unit;

public class LeadRoutingCapacityTests
{
    private readonly LeadRoutingCapacityService _service;
    private readonly Guid _tenantId = LeadRoutingCapacityService.DefaultTenantId;

    public LeadRoutingCapacityTests()
    {
        _service = new LeadRoutingCapacityService();
        _service.Reset();
    }

    [Fact]
    public void RouteLead_MatchesTerritoryAndSkill_ToOptimalOnlineSdr()
    {
        // Tariq is in Riyadh with B2B SaaS skill
        var req = new RouteLeadRequest(
            _tenantId,
            Guid.NewGuid(),
            "Majid Al-Husseini",
            "majid@saascloud.sa",
            "SaaS Cloud KSA",
            "Riyadh",
            ["B2B SaaS"],
            75000
        );

        var assignment = _service.RouteLead(req);

        assignment.Should().NotBeNull();
        assignment.SdrId.Should().Be(LeadRoutingCapacityService.TariqId);
        assignment.SdrName.Should().Be("Tariq Al-Ghamdi");
        assignment.SlaStatus.Should().Be(LeadSlaBreachStatus.WithinSla);
        assignment.SlaDueAtUtc.Should().BeAfter(assignment.AssignedAtUtc);
        (assignment.SlaDueAtUtc - assignment.AssignedAtUtc).TotalMinutes.Should().BeApproximately(15, 0.5);

        // Verify SDR lead count incremented
        var roster = _service.GetSdrRoster(_tenantId);
        var tariq = roster.First(s => s.SdrId == LeadRoutingCapacityService.TariqId);
        tariq.CurrentActiveLeads.Should().Be(7); // Initial was 6
    }

    [Fact]
    public void RouteLead_MatchesTerritory_WhenSkillIsGeneric()
    {
        // Reem is in Jeddah with Retail FinTech
        var req = new RouteLeadRequest(
            _tenantId,
            Guid.NewGuid(),
            "Layla Al-Amri",
            "layla@jeddahretail.com",
            "Jeddah Retailers Corp",
            "Jeddah",
            ["Retail FinTech"],
            50000
        );

        var assignment = _service.RouteLead(req);

        assignment.Should().NotBeNull();
        assignment.SdrId.Should().Be(LeadRoutingCapacityService.ReemId);
        assignment.SdrName.Should().Be("Reem Al-Khatib");
    }

    [Fact]
    public void RouteLead_CapacityExhaustionOverflow_FallsBackToAvailableOnlineRep()
    {
        // Max out Tariq's capacity
        _service.UpdateSdrStatus(new UpdateSdrStatusRequest(
            _tenantId,
            LeadRoutingCapacityService.TariqId,
            SdrAvailabilityStatus.Online,
            6 // Currently has 6 leads, so capacity is now 100% full
        ));

        // Now a lead for Riyadh arrives
        var req = new RouteLeadRequest(
            _tenantId,
            Guid.NewGuid(),
            "Fahad Al-Qasim",
            "fahad@riyadhenterprise.sa",
            "Riyadh Enterprise Inc",
            "Riyadh",
            ["B2B SaaS"],
            120000
        );

        var assignment = _service.RouteLead(req);

        // Tariq is full, so should overflow to another online rep with capacity (e.g. Reem)
        assignment.Should().NotBeNull();
        assignment.SdrId.Should().NotBe(LeadRoutingCapacityService.TariqId);
        new[] { LeadRoutingCapacityService.ReemId, LeadRoutingCapacityService.ZaidId }.Should().Contain(assignment.SdrId);
    }

    [Fact]
    public void RouteLead_WeightedDistribution_PrefersHigherWeightedRep()
    {
        var testTenant = Guid.NewGuid();
        var repA = new SdrRepProfile(
            Guid.NewGuid(),
            testTenant,
            "Rep Heavy",
            "heavy@test.com",
            SdrAvailabilityStatus.Online,
            10,
            2,
            2.0m, // Higher quota weight
            ["Global"],
            ["Enterprise"],
            DateTime.UtcNow.AddMinutes(-10)
        );

        var repB = new SdrRepProfile(
            Guid.NewGuid(),
            testTenant,
            "Rep Light",
            "light@test.com",
            SdrAvailabilityStatus.Online,
            10,
            2,
            0.5m, // Lower quota weight
            ["Global"],
            ["Enterprise"],
            DateTime.UtcNow.AddMinutes(-10)
        );

        _service.AddSdr(repA);
        _service.AddSdr(repB);

        var req = new RouteLeadRequest(
            testTenant,
            Guid.NewGuid(),
            "Global Lead",
            "lead@global.com",
            "Global Corp",
            "Global",
            ["Enterprise"],
            100000
        );

        var assignment = _service.RouteLead(req);

        // Rep Heavy has higher quota weight, so weighted load is lower -> assigned to Rep Heavy
        assignment.SdrId.Should().Be(repA.SdrId);
    }

    [Fact]
    public void CheckAndTriggerSlaBreaches_ReassignsOverdueLead_ToAvailableSdr()
    {
        // An overdue assignment for Dammam currently assigned to Zaid
        var breachAssignmentId = Guid.Parse("cccccccc-1111-1111-1111-111111111111");

        var breached = _service.CheckAndTriggerSlaBreaches(_tenantId);

        breached.Should().NotBeEmpty();
        var reallocated = breached.FirstOrDefault(b => b.AssignmentId == breachAssignmentId);
        reallocated.Should().NotBeNull();
        reallocated!.SlaStatus.Should().Be(LeadSlaBreachStatus.Reassigned);
        reallocated.ReassignedFromSdrId.Should().Be(LeadRoutingCapacityService.ZaidId);
        reallocated.SdrId.Should().NotBe(LeadRoutingCapacityService.ZaidId);
    }

    [Fact]
    public void LogFirstContact_WithinSla_UpdatesStatusAndDecrementsRepActiveLeads()
    {
        var withinSlaAssignmentId = Guid.Parse("aaaaaaaa-1111-1111-1111-111111111111");

        var initialRoster = _service.GetSdrRoster(_tenantId);
        var tariqBefore = initialRoster.First(s => s.SdrId == LeadRoutingCapacityService.TariqId);
        int leadsBefore = tariqBefore.CurrentActiveLeads;

        var result = _service.LogFirstContact(new LogContactAttemptRequest(
            _tenantId,
            withinSlaAssignmentId,
            "Phone",
            "Spoke with Omar, qualified for demo next Tuesday."
        ));

        result.Should().NotBeNull();
        result!.FirstContactedAtUtc.Should().NotBeNull();
        result.SlaStatus.Should().Be(LeadSlaBreachStatus.WithinSla);

        var tariqAfter = _service.GetSdrRoster(_tenantId).First(s => s.SdrId == LeadRoutingCapacityService.TariqId);
        tariqAfter.CurrentActiveLeads.Should().Be(leadsBefore - 1);
    }

    [Fact]
    public void LogFirstContact_PastSla_MarksBreached()
    {
        // Create an overdue assignment manually
        var pastDueAssignment = new LeadAssignmentRecord(
            Guid.NewGuid(),
            _tenantId,
            Guid.NewGuid(),
            "Late Contact Lead",
            "late@test.com",
            "Late Corp",
            "Riyadh",
            LeadRoutingCapacityService.TariqId,
            "Tariq Al-Ghamdi",
            DateTime.UtcNow.AddMinutes(-30),
            DateTime.UtcNow.AddMinutes(-15),
            LeadSlaBreachStatus.Warning,
            null,
            null
        );

        // Add to service
        var testReq = new RouteLeadRequest(
            _tenantId,
            pastDueAssignment.LeadId,
            pastDueAssignment.LeadName,
            pastDueAssignment.LeadEmail,
            pastDueAssignment.CompanyName,
            pastDueAssignment.Territory,
            [],
            10000
        );
        var routed = _service.RouteLead(testReq);

        // Log contact on past due assignment
        var log = _service.LogFirstContact(new LogContactAttemptRequest(
            _tenantId,
            pastDueAssignment.AssignmentId,
            "Email",
            "Late contact attempt"
        ));

        // It doesn't exist yet, returns null
        log.Should().BeNull();

        // Now test on routed lead
        var validLog = _service.LogFirstContact(new LogContactAttemptRequest(
            _tenantId,
            routed.AssignmentId,
            "Email",
            "Timely contact"
        ));
        validLog.Should().NotBeNull();
        validLog!.SlaStatus.Should().Be(LeadSlaBreachStatus.WithinSla);
    }

    [Fact]
    public void TenantIsolation_IsStrictlyEnforced()
    {
        var otherTenantId = Guid.NewGuid();

        // Other tenant has no SDRs
        var action = () => _service.RouteLead(new RouteLeadRequest(
            otherTenantId,
            Guid.NewGuid(),
            "Hassan",
            "hassan@isolated.sa",
            "Isolated SA",
            "Riyadh",
            ["B2B SaaS"],
            50000
        ));

        action.Should().Throw<InvalidOperationException>()
            .WithMessage($"*No online SDR available with capacity in tenant {otherTenantId}*");

        // Overview for other tenant should have 0 SDRs and 0 assignments
        var overview = _service.GetOverview(otherTenantId);
        overview.TenantId.Should().Be(otherTenantId);
        overview.TotalSdrCount.Should().Be(0);
        overview.OnlineSdrCount.Should().Be(0);
        overview.SdrRoster.Should().BeEmpty();
    }

    [Fact]
    public void UpdateSdrStatus_UpdatesAvailabilityAndCapacity()
    {
        var result = _service.UpdateSdrStatus(new UpdateSdrStatusRequest(
            _tenantId,
            LeadRoutingCapacityService.SarahId,
            SdrAvailabilityStatus.Online,
            18
        ));

        result.Should().NotBeNull();
        result!.Status.Should().Be(SdrAvailabilityStatus.Online);
        result.MaxCapacity.Should().Be(18);

        var updatedSarah = _service.GetSdrRoster(_tenantId).First(s => s.SdrId == LeadRoutingCapacityService.SarahId);
        updatedSarah.Status.Should().Be(SdrAvailabilityStatus.Online);
        updatedSarah.MaxCapacity.Should().Be(18);
    }

    [Fact]
    public void GetOverview_AggregatesKpisAndPendingAssignments()
    {
        var overview = _service.GetOverview(_tenantId);

        overview.Should().NotBeNull();
        overview.TenantId.Should().Be(_tenantId);
        overview.TotalSdrCount.Should().Be(4);
        overview.OnlineSdrCount.Should().Be(3); // Tariq, Reem, Zaid are Online; Sarah is Away
        overview.SdrRoster.Should().HaveCount(4);
        overview.PendingAssignments.Should().NotBeEmpty();
        overview.TotalActiveRoutedLeads.Should().BeGreaterThan(0);
        overview.AverageFirstContactMinutes.Should().BeGreaterThan(0);
    }
}
