using FluentAssertions;
using SGrowing.Api.Modules.Finance.FxHedging;
using Xunit;

namespace SGrowing.Tests.Finance;

public sealed class FxHedgingTests
{
    private readonly ValueAtRiskExposureEngine _varEngine = new();
    private readonly TriangularArbitrageDetectorService _arbitrageService = new();

    #region Value-at-Risk (VaR) & CVaR Tests

    [Fact]
    public void EvaluatePortfolioRisk_MaterialForeignCurrencyPositions_ComputesVaRAndRecommendsHedge()
    {
        var positions = new List<FxCurrencyPosition>
        {
            // Supplier payable: 50,000 EUR due in 30 days. Spot = 145.50 DZD/EUR
            new(CurrencyCode.Eur, NetAmount: 50000.0m, Direction: ExposureDirection.NetPayable, DateTime.UtcNow.AddDays(30), CurrentSpotRateToDzd: 145.50m, AnnualizedVolatilitySigma: 0.085),
            // Export receivable: 40,000 USD due in 30 days. Spot = 134.20 DZD/USD
            new(CurrencyCode.Usd, NetAmount: 40000.0m, Direction: ExposureDirection.NetReceivable, DateTime.UtcNow.AddDays(30), CurrentSpotRateToDzd: 134.20m, AnnualizedVolatilitySigma: 0.092)
        };

        var req = new EvaluateFxRiskRequest(
            TenantId: "TENANT-IMPORT-EXPORT",
            BaseCurrency: CurrencyCode.Dzd,
            Positions: positions,
            ConfidenceLevel: 0.99,
            HorizonDays: 30,
            MaxLossToleranceBase: 100000.0m // 100,000 DZD max loss tolerance
        );

        var report = _varEngine.EvaluatePortfolioRisk(req);

        report.Should().NotBeNull();
        report.TenantId.Should().Be("TENANT-IMPORT-EXPORT");
        report.TotalNetPortfolioValueBase.Should().BeGreaterThan(1000000.0m);
        report.ParametricVaRBase.Should().BeGreaterThan(0.0m);
        report.ConditionalVaRExpectedShortfallBase.Should().BeGreaterThan(report.ParametricVaRBase, "CVaR (Expected Shortfall) must exceed VaR at 99% confidence.");
        report.HedgeRecommended.Should().BeTrue();
        report.Recommendations.Should().HaveCount(2);

        var eurHedge = report.Recommendations.First(r => r.Currency == CurrencyCode.Eur);
        eurHedge.ExposureToHedge.Should().Be(ExposureDirection.NetPayable);
        eurHedge.ForwardHedgeRate.Should().BeGreaterThan(eurHedge.CurrentSpotRate, "Forward rate includes forward premium points.");
    }

    [Fact]
    public void EvaluatePortfolioRisk_EmptyPositions_ReturnsZeroVaR()
    {
        var req = new EvaluateFxRiskRequest("TENANT-EMPTY", CurrencyCode.Dzd, []);
        var report = _varEngine.EvaluatePortfolioRisk(req);

        report.ParametricVaRBase.Should().Be(0.0m);
        report.HedgeRecommended.Should().BeFalse();
        report.Recommendations.Should().BeEmpty();
    }

    #endregion

    #region Triangular Arbitrage Detection Tests

    [Fact]
    public void DetectTriangularMisalignment_SignificantPriceDiscrepancy_FlagsExecutableOpportunity()
    {
        // Pair 1: EUR/USD = 1.0850
        // Pair 2: USD/DZD = 134.50
        // Synthetic EUR/DZD = 1.0850 * 134.50 = 145.9325 DZD
        // Market EUR/DZD quote = 145.20 DZD (discrepancy ~ 50.4 bps > 15 bps threshold)
        var opp = _arbitrageService.DetectTriangularMisalignment(
            baseCurrency: CurrencyCode.Eur,
            intermediate: CurrencyCode.Usd,
            quote: CurrencyCode.Dzd,
            rateBaseToIntermediate: 1.0850m,
            rateIntermediateToQuote: 134.50m,
            marketRateBaseToQuote: 145.20m
        );

        opp.Should().NotBeNull();
        opp.SyntheticRateBaseToQuote.Should().Be(145.9325m);
        opp.DiscrepancyBps.Should().BeGreaterThan(40.0m);
        opp.IsExecutableSpread.Should().BeTrue();
    }

    [Fact]
    public void DetectTriangularMisalignment_TightSpreadBelowThreshold_NotExecutable()
    {
        // Tight alignment: Synthetic = 145.21 vs Market = 145.20 (< 1 bps diff)
        var opp = _arbitrageService.DetectTriangularMisalignment(
            baseCurrency: CurrencyCode.Eur,
            intermediate: CurrencyCode.Usd,
            quote: CurrencyCode.Dzd,
            rateBaseToIntermediate: 1.0800m,
            rateIntermediateToQuote: 134.45m,
            marketRateBaseToQuote: 145.20m
        );

        opp.DiscrepancyBps.Should().BeLessThan(10.0m);
        opp.IsExecutableSpread.Should().BeFalse();
    }

    #endregion
}
