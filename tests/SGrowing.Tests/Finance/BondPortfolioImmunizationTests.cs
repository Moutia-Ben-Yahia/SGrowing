using System;
using SGrowing.Api.Modules.Finance.BondPortfolioDurationConvexityImmunization;
using Xunit;

namespace SGrowing.Tests.Finance;

public sealed class BondPortfolioImmunizationTests
{
    private readonly BondPortfolioImmunizationEngine _engine = new();

    [Fact]
    public void AnalyzeImmunization_OptimalRedington_ReturnsImmunizedRegime()
    {
        var telemetry = new BondPortfolioImmunizationTelemetry
        {
            PortfolioId = "PENSION-LDI-001",
            MandateName = "Defined-Benefit-Plan-LDI",
            PortfolioPresentValueUsd = 125000000.0,
            LiabilityPresentValueUsd = 120000000.0, // 104.17% funded
            PortfolioMacaulayDurationYears = 10.45,
            LiabilityMacaulayDurationYears = 10.50, // Gap = -0.05 <= 0.15 yrs
            PortfolioConvexityYears2 = 145.0,
            LiabilityConvexityYears2 = 128.0, // Surplus = +17.0 yrs^2
            CurrentYieldCurveRate = 0.045,
            SimulatedParallelRateShiftBps = 100.0
        };

        var result = _engine.AnalyzeImmunization(telemetry);

        Assert.Equal("PENSION-LDI-001", result.PortfolioId);
        Assert.True(result.IsFundingSurplusPresent);
        Assert.True(result.IsDurationMatched);
        Assert.True(result.IsConvexityDominant);
        Assert.True(result.IsRedingtonImmunizationAchieved);
        Assert.True(result.PresentValueFundingRatioPercent > 100.0);
        Assert.True(result.ConvexitySurplusYears2 > 0.0);
        Assert.Equal("REDINGTON_IMMUNIZED_CONVEXITY_DOMINANT", result.ImmunizationStatusRegime);
    }

    [Fact]
    public void AnalyzeImmunization_UnderfundedPortfolio_TriggersFundingDeficitHazard()
    {
        var telemetry = new BondPortfolioImmunizationTelemetry
        {
            PortfolioId = "PENSION-UNDERFUNDED-02",
            MandateName = "Distressed-Pension-Fund",
            PortfolioPresentValueUsd = 95000000.0,
            LiabilityPresentValueUsd = 110000000.0, // 86.36% funded < 100%
            PortfolioMacaulayDurationYears = 10.5,
            LiabilityMacaulayDurationYears = 10.5,
            PortfolioConvexityYears2 = 140.0,
            LiabilityConvexityYears2 = 130.0
        };

        var result = _engine.AnalyzeImmunization(telemetry);

        Assert.False(result.IsFundingSurplusPresent);
        Assert.False(result.IsRedingtonImmunizationAchieved);
        Assert.Equal("UNDERFUNDED_LIABILITY_DEFICIT_HAZARD", result.ImmunizationStatusRegime);
        Assert.Contains("underfunded", result.PortfolioRebalancingGuidance);
    }

    [Fact]
    public void AnalyzeImmunization_DurationMismatch_TriggersRateRiskWarning()
    {
        var telemetry = new BondPortfolioImmunizationTelemetry
        {
            PortfolioId = "PENSION-DURATION-GAP-03",
            MandateName = "Mismatched-Tenor-Book",
            PortfolioPresentValueUsd = 105000000.0,
            LiabilityPresentValueUsd = 100000000.0,
            PortfolioMacaulayDurationYears = 12.2, // Gap = +1.7 > 0.15 yrs
            LiabilityMacaulayDurationYears = 10.5,
            PortfolioConvexityYears2 = 180.0,
            LiabilityConvexityYears2 = 130.0
        };

        var result = _engine.AnalyzeImmunization(telemetry);

        Assert.False(result.IsDurationMatched);
        Assert.Equal("DURATION_MISMATCH_FIRST_ORDER_RATE_RISK", result.ImmunizationStatusRegime);
        Assert.Contains("Duration gap", result.PortfolioRebalancingGuidance);
    }

    [Fact]
    public void AnalyzeImmunization_ConvexityDeficit_TriggersRateShockVulnerability()
    {
        var telemetry = new BondPortfolioImmunizationTelemetry
        {
            PortfolioId = "PENSION-CONVEXITY-DEFICIT-04",
            MandateName = "Callable-Bond-Heavy-Portfolio",
            PortfolioPresentValueUsd = 105000000.0,
            LiabilityPresentValueUsd = 100000000.0,
            PortfolioMacaulayDurationYears = 10.5,
            LiabilityMacaulayDurationYears = 10.5,
            PortfolioConvexityYears2 = 95.0, // Inferior convexity < 130
            LiabilityConvexityYears2 = 130.0
        };

        var result = _engine.AnalyzeImmunization(telemetry);

        Assert.False(result.IsConvexityDominant);
        Assert.Equal("CONVEXITY_DEFICIT_RATE_SHOCK_VULNERABILITY", result.ImmunizationStatusRegime);
        Assert.Contains("inferior to liability convexity", result.PortfolioRebalancingGuidance);
    }

    [Fact]
    public void AnalyzeImmunization_ZeroOrNegativePresentValue_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentNullException>(() => _engine.AnalyzeImmunization(null!));

        var invalidTelemetry = new BondPortfolioImmunizationTelemetry
        {
            PortfolioId = "INV-PORTFOLIO",
            PortfolioPresentValueUsd = 0.0,
            LiabilityPresentValueUsd = 100000.0
        };

        Assert.Throws<ArgumentException>(() => _engine.AnalyzeImmunization(invalidTelemetry));
    }
}
