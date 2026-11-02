using SGrowing.Api.Modules.Finance.AlmgrenChrissOptimalLiquidation;
using Xunit;

namespace SGrowing.Tests.Finance;

public class AlmgrenChrissLiquidationTests
{
    private readonly AlmgrenChrissLiquidationEngine _engine = new();

    [Fact]
    public void AnalyzeLiquidation_NominalParentOrder_ReturnsOptimalTrajectory()
    {
        var telemetry = new AlmgrenChrissLiquidationTelemetry
        {
            OrderExecutionId = "EXEC-ALMGREN-001",
            AssetTicker = "SPY",
            TotalOrderQuantityShares = 120000.0,
            ExecutionHorizonHours = 4.0,
            NumberOfTradingIntervals = 16,
            DailyAssetVolatilityPercent = 1.35,
            TemporaryPriceImpactEta = 2.0e-6,
            PermanentPriceImpactGamma = 1.0e-7,
            RiskAversionParameterLambda = 1.2e-5,
            RealizedArrivalPriceUsd = 510.00,
            RealizedAverageExecutionPriceUsd = 509.70, // ~5.8 bps slippage
            MarketParticipationRatePercent = 8.0
        };

        var result = _engine.AnalyzeLiquidation(telemetry);

        Assert.Equal("EXEC-ALMGREN-001", result.OrderExecutionId);
        Assert.True(result.IsTrajectoryRiskOptimal);
        Assert.True(result.IsParticipationRateSafe);
        Assert.True(result.IsSlippageWithinTolerance);
        Assert.True(result.CalculatedUrgencyParameterKappa > 0.0);
        Assert.True(result.ExpectedImplementationShortfallBps > 0.0);
        Assert.True(result.TotalEstimatedCostUsd > 0.0);
        Assert.Empty(result.ActiveRegimes);
    }

    [Fact]
    public void AnalyzeLiquidation_ExcessiveParticipation_TriggersCapAlert()
    {
        var telemetry = new AlmgrenChrissLiquidationTelemetry
        {
            OrderExecutionId = "EXEC-AGGRESSIVE-PARTICIPATION",
            MarketParticipationRatePercent = 22.5 // Exceeds 15.0%
        };

        var result = _engine.AnalyzeLiquidation(telemetry);

        Assert.False(result.IsParticipationRateSafe);
        Assert.False(result.IsTrajectoryRiskOptimal);
        Assert.Contains("PARTICIPATION_RATE_CAP_EXCEEDANCE", result.ActiveRegimes);
        Assert.NotEmpty(result.RecommendedMitigations);
    }

    [Fact]
    public void AnalyzeLiquidation_AdverseSlippageBlowout_TriggersSlippageAlert()
    {
        var telemetry = new AlmgrenChrissLiquidationTelemetry
        {
            OrderExecutionId = "EXEC-SLIPPAGE-BLOWOUT",
            RealizedArrivalPriceUsd = 500.00,
            RealizedAverageExecutionPriceUsd = 497.00 // 60 bps slippage > 25 bps
        };

        var result = _engine.AnalyzeLiquidation(telemetry);

        Assert.False(result.IsSlippageWithinTolerance);
        Assert.Contains("ADVERSE_MARKET_IMPACT_SLIPPAGE_BLOWOUT", result.ActiveRegimes);
    }

    [Fact]
    public void AnalyzeLiquidation_HighUrgency_TriggersInventoryRiskAlert()
    {
        var telemetry = new AlmgrenChrissLiquidationTelemetry
        {
            OrderExecutionId = "EXEC-VOLATILITY-RUSH",
            DailyAssetVolatilityPercent = 4.8,
            RiskAversionParameterLambda = 5.0e-3 // Extremely high risk aversion
        };

        var result = _engine.AnalyzeLiquidation(telemetry);

        Assert.True(result.CalculatedUrgencyParameterKappa > 0.50);
        Assert.Contains("INVENTORY_HOLDING_VOLATILITY_RISK", result.ActiveRegimes);
    }

    [Fact]
    public void AnalyzeLiquidation_UrgencyParameter_ScalesWithVolatility()
    {
        var lowVolTelemetry = new AlmgrenChrissLiquidationTelemetry
        {
            OrderExecutionId = "LOW-VOL",
            DailyAssetVolatilityPercent = 0.8
        };

        var highVolTelemetry = new AlmgrenChrissLiquidationTelemetry
        {
            OrderExecutionId = "HIGH-VOL",
            DailyAssetVolatilityPercent = 3.2
        };

        var lowRes = _engine.AnalyzeLiquidation(lowVolTelemetry);
        var highRes = _engine.AnalyzeLiquidation(highVolTelemetry);

        Assert.True(highRes.CalculatedUrgencyParameterKappa > lowRes.CalculatedUrgencyParameterKappa);
    }
}
