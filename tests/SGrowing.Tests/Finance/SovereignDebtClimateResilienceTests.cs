namespace SGrowing.Tests.Finance;

using System;
using FluentAssertions;
using SGrowing.Api.Modules.Finance.SovereignDebtClimateResilience;
using Xunit;

public class SovereignDebtClimateResilienceTests
{
    private readonly SovereignDebtClimateResilienceEngine _engine = new();

    [Fact]
    public void AnalyzeDebtSustainability_NominalSustainableEconomy_ReturnsSustainablePath()
    {
        var telemetry = new SovereignDebtTelemetry(
            SovereignCountryCode: "CL-STABLE",
            DebtToGdpRatioPercent: 52.0,
            RealEffectiveInterestRatePercent: 2.2,
            RealGdpGrowthRatePercent: 3.8, // r - g is negative (-1.6%)
            PrimaryBalancePercentOfGdp: 1.5,
            ClimateVulnerabilityIndex: 0.30,
            ProjectedAnnualClimateDamagePercentOfGdp: 0.6,
            SovereignBondSpreadBasisPoints: 120.0,
            ForeignCurrencyDebtSharePercent: 18.0
        );

        var result = _engine.AnalyzeDebtSustainability(telemetry);

        result.SovereignCountryCode.Should().Be("CL-STABLE");
        result.FiscalSustainabilityRegime.Should().Be("SUSTAINABLE_CLIMATE_RESILIENT_FISCAL_PATH");
        result.IsFiscalSustainable.Should().BeTrue();
        result.SnowballDifferentialPercent.Should().BeNegative();
        result.ProjectedFiscalGapPercentOfGdp.Should().BeLessThan(2.0);
        result.ClimateAdjustedDefaultProbabilityPercent.Should().BeLessThan(20.0);
        result.PolicyRecommendation.Should().ContainEquivalentOf("within sustainable boundary");
    }

    [Fact]
    public void AnalyzeDebtSustainability_ExcessiveDebtAndSpreads_ReturnsDefaultHazard()
    {
        var telemetry = new SovereignDebtTelemetry(
            SovereignCountryCode: "CL-DISTRESS",
            DebtToGdpRatioPercent: 128.0, // Exceeds 115%
            RealEffectiveInterestRatePercent: 8.5,
            RealGdpGrowthRatePercent: 1.0,
            PrimaryBalancePercentOfGdp: -2.5,
            ClimateVulnerabilityIndex: 0.75,
            ProjectedAnnualClimateDamagePercentOfGdp: 2.5,
            SovereignBondSpreadBasisPoints: 1150.0, // Exceeds 900 bps
            ForeignCurrencyDebtSharePercent: 65.0
        );

        var result = _engine.AnalyzeDebtSustainability(telemetry);

        result.FiscalSustainabilityRegime.Should().Be("SOVEREIGN_DEBT_DISTRESS_DEFAULT_HAZARD");
        result.IsFiscalSustainable.Should().BeFalse();
        result.ClimateAdjustedDefaultProbabilityPercent.Should().BeGreaterThan(50.0);
        result.PolicyRecommendation.Should().ContainEquivalentOf("Paris Club");
    }

    [Fact]
    public void AnalyzeDebtSustainability_ClimateDamageShocks_ReturnsClimateFiscalCliff()
    {
        var telemetry = new SovereignDebtTelemetry(
            SovereignCountryCode: "CL-ISLAND",
            DebtToGdpRatioPercent: 75.0,
            RealEffectiveInterestRatePercent: 3.5,
            RealGdpGrowthRatePercent: 2.5, // r - g = 1.0%
            PrimaryBalancePercentOfGdp: -2.0, // Deficit
            ClimateVulnerabilityIndex: 0.85, // Highly vulnerable
            ProjectedAnnualClimateDamagePercentOfGdp: 4.5, // High climate loss
            SovereignBondSpreadBasisPoints: 450.0,
            ForeignCurrencyDebtSharePercent: 35.0
        );

        var result = _engine.AnalyzeDebtSustainability(telemetry);

        result.FiscalSustainabilityRegime.Should().Be("CLIMATE_INDUCED_FISCAL_CLIFF_WARNING");
        result.IsFiscalSustainable.Should().BeFalse();
        result.ProjectedFiscalGapPercentOfGdp.Should().BeGreaterThan(4.5);
        result.PolicyRecommendation.Should().ContainEquivalentOf("debt-for-nature swap");
    }

    [Fact]
    public void AnalyzeDebtSustainability_AdverseSnowballDynamics_ReturnsTighteningRegime()
    {
        var telemetry = new SovereignDebtTelemetry(
            SovereignCountryCode: "CL-SNOWBALL",
            DebtToGdpRatioPercent: 88.0,
            RealEffectiveInterestRatePercent: 6.8,
            RealGdpGrowthRatePercent: 1.5, // r - g = 5.3% > 3.0%
            PrimaryBalancePercentOfGdp: 1.8, // Surplus, but r-g too high
            ClimateVulnerabilityIndex: 0.20,
            ProjectedAnnualClimateDamagePercentOfGdp: 0.4,
            SovereignBondSpreadBasisPoints: 320.0,
            ForeignCurrencyDebtSharePercent: 20.0
        );

        var result = _engine.AnalyzeDebtSustainability(telemetry);

        result.FiscalSustainabilityRegime.Should().Be("ADVERSE_SNOWBALL_DYNAMICS_TIGHTENING");
        result.IsFiscalSustainable.Should().BeFalse();
        result.SnowballDifferentialPercent.Should().BeGreaterThan(3.0);
        result.PolicyRecommendation.Should().ContainEquivalentOf("extend debt maturity");
    }

    [Fact]
    public void AnalyzeDebtSustainability_NullTelemetry_ThrowsArgumentNullException()
    {
        var act = () => _engine.AnalyzeDebtSustainability(null!);
        act.Should().Throw<ArgumentNullException>();
    }
}
