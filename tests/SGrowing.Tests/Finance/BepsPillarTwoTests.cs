namespace SGrowing.Tests.Finance;

using FluentAssertions;
using SGrowing.Api.Modules.Finance.BepsPillarTwo;
using Xunit;

public class BepsPillarTwoTests
{
    private readonly BepsPillarTwoTaxCalculatorEngine _engine = new();

    [Fact]
    public void CalculateTopUpTax_UnderTaxedJurisdictionWithSbie_CalculatesCorrectTopUpTax()
    {
        var jurisdictions = new List<JurisdictionTaxData>
        {
            new(
                JurisdictionCode: "IE",
                CountryName: "Ireland",
                TotalRevenueEur: 50_000_000m,
                GloBeNetIncomeEur: 10_000_000m,
                CoveredTaxesEur: 1_250_000m, // 12.5% ETR (under 15.0%)
                EligiblePayrollCostsEur: 2_000_000m, // 5% = 100,000
                EligibleTangibleAssetBookValueEur: 4_000_000m // 5% = 200,000 => SBIE = 300,000
            )
        };

        var request = new BepsPillarTwoCalculationRequest("FY2026", Jurisdictions: jurisdictions);
        var result = _engine.CalculateTopUpTax(request);

        result.FiscalYear.Should().Be("FY2026");
        result.TotalJurisdictionsEvaluated.Should().Be(1);
        result.TotalTopUpTaxPayableEur.Should().BeGreaterThan(0m);

        var assessment = result.Assessments.Should().ContainSingle().Subject;
        assessment.JurisdictionCode.Should().Be("IE");
        assessment.EffectiveTaxRate.Should().Be(0.125m);
        assessment.TopUpTaxPercentage.Should().Be(0.025m); // 15% - 12.5%
        assessment.SbieCarveOutEur.Should().Be(300_000m);
        assessment.ExcessProfitEur.Should().Be(9_700_000m); // 10M - 300k
        assessment.TopUpTaxPayableEur.Should().Be(242_500m); // 9,700,000 * 0.025
        result.ComplianceVerdict.Should().Contain("Action Required");
    }

    [Fact]
    public void CalculateTopUpTax_HighTaxJurisdiction_NoTopUpTaxPayable()
    {
        var jurisdictions = new List<JurisdictionTaxData>
        {
            new(
                JurisdictionCode: "FR",
                CountryName: "France",
                TotalRevenueEur: 120_000_000m,
                GloBeNetIncomeEur: 25_000_000m,
                CoveredTaxesEur: 6_250_000m, // 25.0% ETR (> 15%)
                EligiblePayrollCostsEur: 10_000_000m,
                EligibleTangibleAssetBookValueEur: 15_000_000m
            )
        };

        var request = new BepsPillarTwoCalculationRequest("FY2026", Jurisdictions: jurisdictions);
        var result = _engine.CalculateTopUpTax(request);

        result.TotalTopUpTaxPayableEur.Should().Be(0m);
        var assessment = result.Assessments.Should().ContainSingle().Subject;
        assessment.EffectiveTaxRate.Should().Be(0.25m);
        assessment.TopUpTaxPercentage.Should().Be(0m);
        assessment.TopUpTaxPayableEur.Should().Be(0m);
        result.ComplianceVerdict.Should().Contain("Fully Compliant");
    }

    [Fact]
    public void CalculateTopUpTax_DeMinimisExclusion_ExemptsSmallJurisdiction()
    {
        var jurisdictions = new List<JurisdictionTaxData>
        {
            new(
                JurisdictionCode: "MT",
                CountryName: "Malta",
                TotalRevenueEur: 3_000_000m, // < 10M
                GloBeNetIncomeEur: 400_000m,  // < 1M
                CoveredTaxesEur: 20_000m,    // 5.0% ETR
                EligiblePayrollCostsEur: 100_000m,
                EligibleTangibleAssetBookValueEur: 50_000m
            )
        };

        var request = new BepsPillarTwoCalculationRequest("FY2026", Jurisdictions: jurisdictions);
        var result = _engine.CalculateTopUpTax(request);

        result.TotalTopUpTaxPayableEur.Should().Be(0m);
        var assessment = result.Assessments.Should().ContainSingle().Subject;
        assessment.IsDeMinimisExcluded.Should().BeTrue();
        assessment.TopUpTaxPayableEur.Should().Be(0m);
        assessment.StatusReason.Should().Contain("De Minimis Exclusion Safe Harbor");
    }

    [Fact]
    public void CalculateTopUpTax_NetLossJurisdiction_NoTopUpTaxAndAppropriateReason()
    {
        var jurisdictions = new List<JurisdictionTaxData>
        {
            new(
                JurisdictionCode: "DE",
                CountryName: "Germany",
                TotalRevenueEur: 45_000_000m,
                GloBeNetIncomeEur: -2_500_000m, // Net Loss
                CoveredTaxesEur: 0m,
                EligiblePayrollCostsEur: 8_000_000m,
                EligibleTangibleAssetBookValueEur: 12_000_000m
            )
        };

        var request = new BepsPillarTwoCalculationRequest("FY2026", Jurisdictions: jurisdictions);
        var result = _engine.CalculateTopUpTax(request);

        result.TotalTopUpTaxPayableEur.Should().Be(0m);
        var assessment = result.Assessments.Should().ContainSingle().Subject;
        assessment.EffectiveTaxRate.Should().Be(0m);
        assessment.StatusReason.Should().Contain("Loss jurisdiction");
    }
}
