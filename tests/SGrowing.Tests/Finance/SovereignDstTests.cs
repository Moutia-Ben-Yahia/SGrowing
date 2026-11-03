using SGrowing.Api.Modules.Finance.SovereignDst;
using Xunit;

namespace SGrowing.Tests.Finance;

public class SovereignDstTests
{
    [Fact]
    public void GetOverview_ReportsJurisdictionsAndTaxLiabilities()
    {
        var engine = new SovereignDstLedgerEngine();

        var overview = engine.GetOverview();

        Assert.NotNull(overview);
        Assert.True(overview.MonitoredTaxJurisdictionsCount > 100);
        Assert.True(overview.TotalDstAccruedEur > 1_000_000m);
        Assert.True(overview.TotalPillar2TopUpTaxEur > 0m);
        Assert.True(overview.GlobalEffectiveTaxRatePct > 10.0);
        Assert.NotEmpty(overview.DstRecords);
        Assert.NotEmpty(overview.GlobeAssessments);
    }

    [Fact]
    public void ComputeDst_CalculatesLiabilityBasedOnStatutoryRate()
    {
        var engine = new SovereignDstLedgerEngine();
        var request = new ComputeDstRequest(
            "FR_DST_3PCT",
            25_000_000m,
            3.0m
        );

        var record = engine.ComputeDst(request);

        Assert.NotNull(record);
        Assert.StartsWith("DST-FR-", record.DstRecordId);
        Assert.Equal("FR_DST_3PCT", record.JurisdictionCode);
        Assert.Equal(25_000_000m, record.LocalDigitalTurnoverEur);
        Assert.Equal(3.0m, record.StatutoryDstRatePct);
        Assert.Equal(750_000m, record.CalculatedTaxLiabilityEur);
        Assert.Equal("ASSESSED", record.Status);

        var overview = engine.GetOverview();
        Assert.Contains(overview.DstRecords, r => r.DstRecordId == record.DstRecordId);
    }

    [Fact]
    public void ComputeGlobeTax_CalculatesPillar2TopUpTaxWhenEtrBelow15Percent()
    {
        var engine = new SovereignDstLedgerEngine();
        var request = new ComputeGlobeTaxRequest(
            "2026",
            "IE_DUBLIN",
            20_000_000m,
            2_000_000m // 10% ETR < 15% threshold
        );

        var assessment = engine.ComputeGlobeTax(request);

        Assert.NotNull(assessment);
        Assert.StartsWith("GLOBE-2026-", assessment.AssessmentId);
        Assert.Equal("IE_DUBLIN", assessment.ConstituentEntityJurisdiction);
        Assert.Equal(10.0, assessment.EffectiveTaxRatePct);
        Assert.Equal(1_000_000m, assessment.TopUpTaxLiabilityEur); // (15 - 10)% * 20M = 5% * 20M = 1M
        Assert.StartsWith("CLR-PILLAR2-SHA256-", assessment.ClearanceTokenHash);

        var overview = engine.GetOverview();
        Assert.Contains(overview.GlobeAssessments, a => a.AssessmentId == assessment.AssessmentId);
    }
}
