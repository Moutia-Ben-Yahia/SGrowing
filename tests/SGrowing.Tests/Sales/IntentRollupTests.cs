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
}
