using System;
using System.Collections.Generic;
using FluentAssertions;
using SGrowing.Api.Modules.Sales.IntentRollup;
using Xunit;

namespace SGrowing.Tests.Sales;

public class IntentRollupTests
{
    private readonly IntentRollupEngine _engine = new();

    [Fact]
    public void GetOverview_ReturnsConsolidatedHierarchiesAndCollisionRate()
    {
        var overview = _engine.GetOverview();

        overview.Should().NotBeNull();
        overview.TotalMonitoredAccounts.Should().Be(3);
        overview.HighIntentAccountsCount.Should().BeGreaterThanOrEqualTo(2);
        overview.DeDuplicatedSignalsCount.Should().BeGreaterThan(1000);
        overview.OutboundCollisionPreventionRatePct.Should().BeGreaterThan(95.0);
        overview.Accounts.Should().HaveCount(3);
    }

    [Fact]
    public void IngestIntentSignal_RollsUpUnderCorporateParentAndSurgesScore()
    {
        var command = new IngestIntentSignalCommand("acme.corp", "vp.product@acme.corp", "VP Product", "G2_BuyerIntent", 88);
        var result = _engine.IngestIntentSignal(command);

        result.Should().NotBeNull();
        result.RollupAccountId.Should().Be("ACC-CORP-01");
        result.ParentCorporateEntity.Should().Contain("Acme Worldwide");
        result.UpdatedAggregateIntentScore.Should().BeGreaterThanOrEqualTo(88);
        result.RecommendedAction.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void LockAccountOutreach_LocksHierarchyWithExclusivityReceipt()
    {
        var command = new LockAccountOutreachCommand("ACC-CORP-02", "REP-MICHAEL-02", 30);
        var result = _engine.LockAccountOutreach(command);

        result.Should().NotBeNull();
        result.AccountId.Should().Be("ACC-CORP-02");
        result.LockedToRepId.Should().Be("REP-MICHAEL-02");
        result.ExpiresAt.Should().BeAfter(DateTime.UtcNow);
        result.AuditReceipt.Should().Contain("AUDIT-OUTREACH-LOCK");
    }

    [Fact]
    public void IngestIntentSignal_WithDuplicateSignal_IncrementsScoreByTwo()
    {
        // Existing signal on ACC-CORP-01: cto@acme.corp + G2_BuyerIntent
        var command = new IngestIntentSignalCommand("acme.corp", "cto@acme.corp", "CTO", "G2_BuyerIntent", 92);
        var result = _engine.IngestIntentSignal(command);

        result.Should().NotBeNull();
        result.IsDeDuplicatedDuplicate.Should().BeTrue();
        result.UpdatedAggregateIntentScore.Should().Be(90); // 88 + 2 = 90
    }

    [Fact]
    public void IngestIntentSignal_WhenAccountOutreachAlreadyLocked_RecommendsInboxRouting()
    {
        // ACC-CORP-01 has OutreachLocked = true
        var command = new IngestIntentSignalCommand("acme.corp", "new.lead@acme.corp", "Lead", "WebsiteVisit", 50);
        var result = _engine.IngestIntentSignal(command);

        result.Should().NotBeNull();
        result.RecommendedAction.Should().Contain("already locked to REP-SARAH-01");
        result.RecommendedAction.Should().Contain("routing intelligence to owner's CRM inbox");
    }

    [Fact]
    public void LockAccountOutreach_WithZeroOrNegativeDays_DefaultsToThirtyDays()
    {
        var command = new LockAccountOutreachCommand("ACC-CORP-03", "REP-ALEX-03", -10);
        var result = _engine.LockAccountOutreach(command);

        result.Should().NotBeNull();
        result.ExpiresAt.Should().BeAfter(DateTime.UtcNow.AddDays(29));
        result.ExpiresAt.Should().BeBefore(DateTime.UtcNow.AddDays(31));
    }

    [Fact]
    public void IngestIntentSignal_WithUnknownDomain_FallsBackToDefaultCorporateParent()
    {
        var command = new IngestIntentSignalCommand("unrecognized-domain.net", "admin@unrecognized-domain.net", "Director", "OrganicSearch", 65);
        var result = _engine.IngestIntentSignal(command);

        result.Should().NotBeNull();
        result.RollupAccountId.Should().Be("ACC-CORP-01");
        result.ParentCorporateEntity.Should().Contain("Acme Worldwide");
    }

    [Fact]
    public void IngestIntentSignal_HighInitialScore_CapsAtOneHundred()
    {
        // ACC-CORP-01 initial score is 88; adding fresh signal (+12) reaches 100
        var command = new IngestIntentSignalCommand("acme.corp", "brandnew@acme.corp", "VP Strategy", "WebinarAttendance", 95);
        var result = _engine.IngestIntentSignal(command);

        result.Should().NotBeNull();
        result.UpdatedAggregateIntentScore.Should().Be(100);
    }
}
