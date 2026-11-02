using SGrowing.Api.Modules.Finance.CrossCurrencyBasisConvexity;
using Xunit;

namespace SGrowing.Tests.Finance;

public class CrossCurrencyBasisTests
{
    private readonly CrossCurrencyBasisEngine _engine = new();

    [Fact]
    public void AnalyzePortfolio_NominalEurUsdPortfolio_ReturnsHedgingSound()
    {
        var telemetry = new CrossCurrencyBasisTelemetry
        {
            CurrencyPairOrPortfolioUid = "EUR-USD-XCCY-01",
            CurrencyPair = "EUR_USD",
            CoveredInterestParityBasisBps = -14.5,
            SpotExchangeRate = 1.0850,
            ForwardExchangeRateOneYear = 1.1025,
            DomesticSofrRatePercent = 4.35,
            ForeignEstrRatePercent = 2.65,
            PortfolioConvexityYearsSquared = -12.4,
            DurationMismatchYears = 0.85,
            CollateralDiscountCurveSpreadBps = 8.5,
            ProjectedYieldShockBasisPoints = 50.0
        };

        var result = _engine.AnalyzePortfolio(telemetry);

        Assert.NotNull(result);
        Assert.Equal("EUR-USD-XCCY-01", result.CurrencyPairOrPortfolioUid);
        Assert.Equal(14.50, result.AbsoluteCipDeviationBps);
        Assert.True(result.IsCipBasisArbitrageBounded);
        Assert.True(result.IsConvexityCascadeContained);
        Assert.True(result.IsCollateralCurveAligned);
        Assert.True(result.IsCrossCurrencyPortfolioHedgingSound);
        Assert.Empty(result.ActiveRegimes);
        Assert.True(result.ForcedDurationHedgingImpactYears < 0.1);
    }

    [Fact]
    public void AnalyzePortfolio_CipBasisBlowout_TriggersBasisBlowoutAlert()
    {
        var telemetry = new CrossCurrencyBasisTelemetry
        {
            CurrencyPairOrPortfolioUid = "EUR-USD-XCCY-02",
            CoveredInterestParityBasisBps = -48.0 // Extreme basis dislocation
        };

        var result = _engine.AnalyzePortfolio(telemetry);

        Assert.False(result.IsCipBasisArbitrageBounded);
        Assert.False(result.IsCrossCurrencyPortfolioHedgingSound);
        Assert.Contains("CROSS_CURRENCY_CIP_BASIS_BLOWOUT", result.ActiveRegimes);
    }

    [Fact]
    public void AnalyzePortfolio_NegativeConvexityCascade_TriggersConvexityAlert()
    {
        var telemetry = new CrossCurrencyBasisTelemetry
        {
            CurrencyPairOrPortfolioUid = "EUR-USD-XCCY-03",
            PortfolioConvexityYearsSquared = -95.0,
            ProjectedYieldShockBasisPoints = 200.0, // forced impact = 1.9 > 1.2
            DurationMismatchYears = 2.4
        };

        var result = _engine.AnalyzePortfolio(telemetry);

        Assert.False(result.IsConvexityCascadeContained);
        Assert.False(result.IsCrossCurrencyPortfolioHedgingSound);
        Assert.Contains("CONVEXITY_CONUNDRUM_FORCED_DURATION_CASCADE", result.ActiveRegimes);
    }

    [Fact]
    public void AnalyzePortfolio_CollateralDislocation_TriggersDiscountingAlert()
    {
        var telemetry = new CrossCurrencyBasisTelemetry
        {
            CurrencyPairOrPortfolioUid = "EUR-USD-XCCY-04",
            CollateralDiscountCurveSpreadBps = 32.5
        };

        var result = _engine.AnalyzePortfolio(telemetry);

        Assert.False(result.IsCollateralCurveAligned);
        Assert.False(result.IsCrossCurrencyPortfolioHedgingSound);
        Assert.Contains("COLLATERAL_CURVE_DISCOUNT_DISLOCATION", result.ActiveRegimes);
    }

    [Fact]
    public void AnalyzePortfolio_ZeroYieldShock_CalculatesZeroDurationImpact()
    {
        var telemetry = new CrossCurrencyBasisTelemetry
        {
            CurrencyPairOrPortfolioUid = "EUR-USD-XCCY-05",
            ProjectedYieldShockBasisPoints = 0.0
        };

        var result = _engine.AnalyzePortfolio(telemetry);

        Assert.Equal(0.0, result.ForcedDurationHedgingImpactYears);
        Assert.True(result.IsConvexityCascadeContained);
    }
}
