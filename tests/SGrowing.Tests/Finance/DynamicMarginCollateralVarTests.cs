namespace SGrowing.Tests.Finance;

using System;
using FluentAssertions;
using SGrowing.Api.Modules.Finance.DynamicMarginCollateralVar;
using Xunit;

public class DynamicMarginCollateralVarTests
{
    private readonly DynamicMarginCollateralVarEngine _engine = new();

    [Fact]
    public void AnalyzeMarginRisk_PrudentBuffer_ReturnsResilientRegime()
    {
        var telemetry = new DynamicMarginVarTelemetry(
            AccountPortfolioId: "PORT-ALPHA-01",
            TotalCollateralValueUsd: 500_000.0,
            TotalGrossNotionalPositionUsd: 1_200_000.0,
            PortfolioLeverageRatio: 2.4,
            HistoricalVolatilityAnnualized: 0.65,
            EvtTailIndexShapeParameterXi: 0.15,
            ConcentratedLiquidityImpermanentLossPercent: 2.5,
            WeightedCollateralHaircutPercent: 5.0,
            DailyPositionFundingRateBasisPoints: 4.5
        );

        var result = _engine.AnalyzeMarginRisk(telemetry);

        result.AccountPortfolioId.Should().Be("PORT-ALPHA-01");
        result.MarginRiskRegime.Should().Be("PRUDENT_DYNAMIC_MARGIN_RESILIENCE_REGIME");
        result.IsMarginCompliant.Should().BeTrue();
        result.MarginBufferUtilizationRatio.Should().BeLessThan(0.80);
        result.ValueAtRisk999Usd.Should().BeGreaterThan(50_000.0);
        result.ExpectedShortfallUsd.Should().BeGreaterThan(result.ValueAtRisk999Usd);
        result.CollateralRebalancingGuidance.Should().ContainEquivalentOf("Dynamic maintenance margin fully satisfied");
    }

    [Fact]
    public void AnalyzeMarginRisk_ExcessiveLeverage_ReturnsImminentLiquidation()
    {
        var telemetry = new DynamicMarginVarTelemetry(
            AccountPortfolioId: "PORT-DEGEN-02",
            TotalCollateralValueUsd: 100_000.0,
            TotalGrossNotionalPositionUsd: 1_800_000.0, // High leverage 18x
            PortfolioLeverageRatio: 18.0,
            HistoricalVolatilityAnnualized: 0.90,
            EvtTailIndexShapeParameterXi: 0.25,
            ConcentratedLiquidityImpermanentLossPercent: 4.0,
            WeightedCollateralHaircutPercent: 10.0,
            DailyPositionFundingRateBasisPoints: 12.0
        );

        var result = _engine.AnalyzeMarginRisk(telemetry);

        result.MarginRiskRegime.Should().Be("IMMINENT_LIQUIDATION_WATERFALL_HAZARD");
        result.IsMarginCompliant.Should().BeFalse();
        result.MarginBufferUtilizationRatio.Should().BeGreaterThan(1.0);
        result.CollateralRebalancingGuidance.Should().ContainEquivalentOf("automated TWAP position deleveraging");
    }

    [Fact]
    public void AnalyzeMarginRisk_HighUtilization_ReturnsCriticalDeficitWarning()
    {
        var telemetry = new DynamicMarginVarTelemetry(
            AccountPortfolioId: "PORT-BETA-03",
            TotalCollateralValueUsd: 450_000.0, // Utilization ~ 0.99 in [0.80, 1.0]
            TotalGrossNotionalPositionUsd: 1_200_000.0,
            PortfolioLeverageRatio: 6.0,
            HistoricalVolatilityAnnualized: 0.70,
            EvtTailIndexShapeParameterXi: 0.20,
            ConcentratedLiquidityImpermanentLossPercent: 3.0,
            WeightedCollateralHaircutPercent: 8.0,
            DailyPositionFundingRateBasisPoints: 6.0
        );

        var result = _engine.AnalyzeMarginRisk(telemetry);

        result.MarginRiskRegime.Should().Be("CRITICAL_COLLATERAL_DEFICIT_WARNING");
        result.IsMarginCompliant.Should().BeFalse();
        result.MarginBufferUtilizationRatio.Should().BeInRange(0.80, 1.0);
        result.CollateralRebalancingGuidance.Should().ContainEquivalentOf("Restrict new levered positions");
    }

    [Fact]
    public void AnalyzeMarginRisk_FatTailedImpermanentLoss_ReturnsImpermanentLossStress()
    {
        var telemetry = new DynamicMarginVarTelemetry(
            AccountPortfolioId: "PORT-AMM-04",
            TotalCollateralValueUsd: 500_000.0,
            TotalGrossNotionalPositionUsd: 600_000.0,
            PortfolioLeverageRatio: 1.2,
            HistoricalVolatilityAnnualized: 0.40,
            EvtTailIndexShapeParameterXi: 0.42, // Heavy tail > 0.35
            ConcentratedLiquidityImpermanentLossPercent: 16.5, // IL > 12%
            WeightedCollateralHaircutPercent: 5.0,
            DailyPositionFundingRateBasisPoints: 2.0
        );

        var result = _engine.AnalyzeMarginRisk(telemetry);

        result.MarginRiskRegime.Should().Be("FAT_TAILED_IMPERMANENT_LOSS_STRESS");
        result.IsMarginCompliant.Should().BeFalse();
        result.CollateralRebalancingGuidance.Should().ContainEquivalentOf("Re-center liquidity tick range");
    }

    [Fact]
    public void AnalyzeMarginRisk_NullTelemetry_ThrowsArgumentNullException()
    {
        var act = () => _engine.AnalyzeMarginRisk(null!);
        act.Should().Throw<ArgumentNullException>();
    }
}
