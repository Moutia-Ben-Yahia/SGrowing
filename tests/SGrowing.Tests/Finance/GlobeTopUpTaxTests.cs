using SGrowing.Api.Modules.Finance.GlobeTopUpTax;
using Xunit;

namespace SGrowing.Tests.Finance;

public class GlobeTopUpTaxTests
{
    private readonly GlobeTopUpTaxEngine _engine = new();

    [Fact]
    public void EvaluateSafeHarbor_PassesHighTaxJurisdiction()
    {
        // Germany has 30% statutory rate and > 15% ETR
        var eval = _engine.EvaluateSafeHarbor("DE");
        Assert.NotNull(eval);
        Assert.True(eval.PassesSimplifiedEtrTest);
        Assert.Equal(SafeHarborStatus.EligibleSimplifiedEtr, eval.OverallStatus);
    }

    [Fact]
    public void CalculateTopUpTax_ComputesLiabilityAndSbieCarveoutForLowTaxJurisdiction()
    {
        // Bermuda has 0% tax and fails safe harbor -> incurs top-up
        var calc = _engine.CalculateTopUpTax("BM");
        Assert.NotNull(calc);
        Assert.Equal("BM", calc.JurisdictionCode);
        Assert.True(calc.TopUpTaxPercentage > 10.0m);
        Assert.True(calc.SubstanceBasedCarveoutEur > 0m);
        Assert.True(calc.JurisdictionalTopUpTaxEur > 0m);
    }

    [Fact]
    public void GetLedgerSummary_AggregatesGlobalLiabilities()
    {
        var summary = _engine.GetLedgerSummary();
        Assert.NotNull(summary);
        Assert.True(summary.TotalJurisdictionsEvaluated >= 3);
        Assert.True(summary.SafeHarborExemptJurisdictions >= 1);
        Assert.True(summary.JurisdictionsSubjectToTopUp >= 1);
        Assert.True(summary.TotalGlobalTopUpTaxLiabilityEur > 0m);
    }
}
