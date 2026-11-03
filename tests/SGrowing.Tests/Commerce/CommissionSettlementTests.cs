using System;
using System.Collections.Generic;
using FluentAssertions;
using SGrowing.Api.Modules.Commerce.CommissionSettlement;
using Xunit;

namespace SGrowing.Tests.Commerce;

public class CommissionSettlementTests
{
    private readonly CommissionSettlementEngine _engine = new();

    [Fact]
    public void GetOverview_CalculatesCommissionsAndEscrow()
    {
        var overview = _engine.GetOverview();

        overview.Should().NotBeNull();
        overview.TotalDealsCount.Should().Be(3);
        overview.TotalCommissionsPaid.Should().BeGreaterThan(0);
        overview.TotalInEscrow.Should().BeGreaterThan(0);
        overview.AverageAttainmentPct.Should().BeGreaterThan(90.0);
        overview.Commissions.Should().NotBeEmpty();
    }

    [Fact]
    public void CalculateCommission_AppliesSuperAcceleratorForHighAttainment()
    {
        var request = new CalculateCommissionRequest(
            DealId: "DEAL-ENT-TEST-01",
            AccountName: "Titan Logistics Corp",
            RepId: "REP-405",
            RepName: "Victoria Sterling",
            DealArr: 200000m,
            QuotaAttainmentPct: 135.0 // SuperAccelerator tier = 20%
        );

        var result = _engine.CalculateCommission(request);

        result.Should().NotBeNull();
        result.Tier.Should().Be(RepTier.SuperAccelerator);
        result.TotalCommission.Should().Be(40000m); // 20% on 200k
        result.ImmediateDisbursedAmount.Should().Be(32000m); // 80%
        result.ClawbackEscrowAmount.Should().Be(8000m);      // 20%
        result.Status.Should().Be(PayoutStatus.HeldInClawbackEscrow);
        result.PayoutHash.Should().StartWith("COMM-HASH-");
    }

    [Fact]
    public void ReleaseEscrow_DisbursesEscrowBalanceAndGeneratesReceipt()
    {
        var command = new ReleaseEscrowCommand("COMM-2026-101", "CFO-DESK-99");
        var result = _engine.ReleaseEscrow(command);

        result.Should().NotBeNull();
        result.CommissionId.Should().Be("COMM-2026-101");
        result.ReleasedAmount.Should().Be(14000m);
        result.Status.Should().Be(PayoutStatus.FullySettled);
        result.ReleaseReceipt.Should().Contain("ESCROW-RELEASE");
    }

    [Fact]
    public void ReleaseEscrow_ThrowsWhenRecordNotFound()
    {
        var command = new ReleaseEscrowCommand("INVALID-ID", "CFO-DESK-99");
        var act = () => _engine.ReleaseEscrow(command);

        act.Should().Throw<KeyNotFoundException>();
    }
}
