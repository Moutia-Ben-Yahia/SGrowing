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
    public void CalculateCommission_AppliesAcceleratorRateFor100To120Attainment()
    {
        // Arrange: 110% attainment -> Accelerator (15% commission)
        var request = new CalculateCommissionRequest(
            DealId: "DEAL-ENT-ACC-01",
            AccountName: "Apex Retail",
            RepId: "REP-406",
            RepName: "Marcus Vance",
            DealArr: 100000m,
            QuotaAttainmentPct: 110.0
        );

        // Act
        var result = _engine.CalculateCommission(request);

        // Assert
        result.Tier.Should().Be(RepTier.Accelerator);
        result.TotalCommission.Should().Be(15000m); // 15% on 100k
        result.ImmediateDisbursedAmount.Should().Be(12000m);
        result.ClawbackEscrowAmount.Should().Be(3000m);
    }

    [Fact]
    public void CalculateCommission_AppliesStandardRateFor80To100Attainment()
    {
        // Arrange: 90% attainment -> Standard (10% commission)
        var request = new CalculateCommissionRequest(
            DealId: "DEAL-ENT-STD-01",
            AccountName: "Nordic Health",
            RepId: "REP-407",
            RepName: "Elena Rostova",
            DealArr: 150000m,
            QuotaAttainmentPct: 90.0
        );

        // Act
        var result = _engine.CalculateCommission(request);

        // Assert
        result.Tier.Should().Be(RepTier.Standard);
        result.TotalCommission.Should().Be(15000m); // 10% on 150k
        result.ImmediateDisbursedAmount.Should().Be(12000m);
        result.ClawbackEscrowAmount.Should().Be(3000m);
    }

    [Fact]
    public void CalculateCommission_AppliesBaselineRateForUnder80Attainment()
    {
        // Arrange: 65% attainment -> Baseline (8% commission)
        var request = new CalculateCommissionRequest(
            DealId: "DEAL-ENT-BASE-01",
            AccountName: "Early Stage Tech",
            RepId: "REP-408",
            RepName: "Junior AE",
            DealArr: 50000m,
            QuotaAttainmentPct: 65.0
        );

        // Act
        var result = _engine.CalculateCommission(request);

        // Assert
        result.Tier.Should().Be(RepTier.Baseline);
        result.TotalCommission.Should().Be(4000m); // 8% on 50k
        result.ImmediateDisbursedAmount.Should().Be(3200m);
        result.ClawbackEscrowAmount.Should().Be(800m);
    }

    [Fact]
    public void CalculateCommission_ComputesExact80ImmediateAnd20EscrowSplit()
    {
        // Arrange: 100k at 20% = 20k commission
        var request = new CalculateCommissionRequest(
            DealId: "DEAL-SPLIT-TEST",
            AccountName: "Split Verification Corp",
            RepId: "REP-999",
            RepName: "Top Performer",
            DealArr: 100000m,
            QuotaAttainmentPct: 150.0
        );

        // Act
        var result = _engine.CalculateCommission(request);

        // Assert
        result.TotalCommission.Should().Be(20000m);
        (result.ImmediateDisbursedAmount + result.ClawbackEscrowAmount).Should().Be(result.TotalCommission);
        result.ImmediateDisbursedAmount.Should().Be(16000m);
        result.ClawbackEscrowAmount.Should().Be(4000m);
        result.EscrowReleaseDate.Should().BeAfter(result.ClosedAt);
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
