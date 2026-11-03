using System;
using System.Collections.Generic;
using FluentAssertions;
using SGrowing.Api.Modules.Finance.TransferPricing;
using Xunit;

namespace SGrowing.Tests.Finance;

public class TransferPricingTests
{
    private readonly TransferPricingEngine _engine = new();

    [Fact]
    public void GetOverview_ReturnsEntitiesAgreementsAndCompliance()
    {
        var overview = _engine.GetOverview();

        overview.Should().NotBeNull();
        overview.TotalEntities.Should().Be(4);
        overview.ActiveAgreementsCount.Should().Be(3);
        overview.TotalIntercompanyVolumeQuarter.Should().BeGreaterThan(2000000m);
        overview.ArmLengthComplianceRatePct.Should().Be(100.0);
        overview.Entities.Should().HaveCount(4);
        overview.Agreements.Should().HaveCount(3);
        overview.RecentInvoices.Should().NotBeEmpty();
    }

    [Fact]
    public void CalculateMarkup_CalculatesArmLengthPrice()
    {
        var command = new CalculateMarkupCommand("ENT-UK-RND", "ENT-US-HQ", "SoftwareDevelopmentCostSharing", 500000m);
        var result = _engine.CalculateMarkup(command);

        result.Should().NotBeNull();
        result.RecommendedMarkupPct.Should().Be(8.5m);
        result.TotalArmLengthPrice.Should().Be(542500m);
        result.IsBepsCompliant.Should().BeTrue();
    }

    [Fact]
    public void GenerateInvoices_NetsIntercompanyBalances()
    {
        var command = new GenerateInvoicesCommand("2026-M03", "USD", "CFO-TAX-DIRECTOR");
        var result = _engine.GenerateInvoices(command);

        result.Should().NotBeNull();
        result.BillingPeriod.Should().Be("2026-M03");
        result.InvoicesGeneratedCount.Should().Be(3);
        result.NetSettlementPayable.Should().BeLessThan(result.GrossIntercompanyVolume);
        result.FxNettingReceipt.Should().Contain("FX-NETTING-OECD");
    }
}
