using SGrowing.Api.Modules.Finance.TipsRealYieldBreakevenInflation;
using Xunit;

namespace SGrowing.Tests.Finance;

public class TipsRealYieldTests
{
    private readonly TipsRealYieldEngine _engine = new();

    [Fact]
    public void AnalyzeRealYield_NominalTenYearTips_ReturnsHedgingOptimal()
    {
        var telemetry = new TipsRealYieldTelemetry
        {
            SecurityCusipOrPortfolioId = "TIPS-10Y-912828XX",
            MaturityTenorYears = 10.0,
            NominalTreasuryYieldPercent = 4.35,
            TipsRealYieldPercent = 2.05,
            ShortTermBreakevenInflationPercent = 2.25,
            LongTermBreakevenInflationPercent = 2.30,
            EstimatedInflationRiskPremiumBps = 35.0,
            EstimatedTipsLiquidityDiscountBps = 18.0,
            IndexationLagMonths = 3.0,
            DeflationFloorOptionProbabilityPercent = 4.5,
            RealDurationYears = 8.65
        };

        var result = _engine.AnalyzeRealYield(telemetry);

        Assert.NotNull(result);
        Assert.Equal("TIPS-10Y-912828XX", result.SecurityCusipOrPortfolioId);
        Assert.Equal(2.30, result.CalculatedBreakevenInflationPercent);
        Assert.True(result.IsBreakevenStructureNormal);
        Assert.True(result.IsTipsLiquidityNormal);
        Assert.True(result.IsDeflationFloorRiskContained);
        Assert.True(result.IsInflationHedgingOptimal);
        Assert.Empty(result.ActiveRegimes);
        Assert.True(result.ImpliedExpectedInflationPercent > 2.0);
    }

    [Fact]
    public void AnalyzeRealYield_StagflationFrontEndShock_TriggersBreakevenInversion()
    {
        var telemetry = new TipsRealYieldTelemetry
        {
            SecurityCusipOrPortfolioId = "TIPS-INV-002",
            ShortTermBreakevenInflationPercent = 3.85,
            LongTermBreakevenInflationPercent = 2.30 // Slope = -155 bps
        };

        var result = _engine.AnalyzeRealYield(telemetry);

        Assert.False(result.IsBreakevenStructureNormal);
        Assert.False(result.IsInflationHedgingOptimal);
        Assert.Contains("BREAKEVEN_INFLATION_INVERSION_DISLOCATION", result.ActiveRegimes);
    }

    [Fact]
    public void AnalyzeRealYield_IlliquidMarketDislocation_TriggersLiquidityDiscountAlert()
    {
        var telemetry = new TipsRealYieldTelemetry
        {
            SecurityCusipOrPortfolioId = "TIPS-ILLIQ-003",
            EstimatedTipsLiquidityDiscountBps = 55.0
        };

        var result = _engine.AnalyzeRealYield(telemetry);

        Assert.False(result.IsTipsLiquidityNormal);
        Assert.False(result.IsInflationHedgingOptimal);
        Assert.Contains("TIPS_LIQUIDITY_DISCOUNT_DISLOCATION", result.ActiveRegimes);
    }

    [Fact]
    public void AnalyzeRealYield_HighRealYieldAndDeflationShock_TriggersDeflationFloorAlert()
    {
        var telemetry = new TipsRealYieldTelemetry
        {
            SecurityCusipOrPortfolioId = "TIPS-DEFL-004",
            TipsRealYieldPercent = 3.45,
            DeflationFloorOptionProbabilityPercent = 18.5
        };

        var result = _engine.AnalyzeRealYield(telemetry);

        Assert.False(result.IsDeflationFloorRiskContained);
        Assert.False(result.IsInflationHedgingOptimal);
        Assert.Contains("DEFLATION_FLOOR_UNDERPRICING_VULNERABILITY", result.ActiveRegimes);
    }

    [Fact]
    public void AnalyzeRealYield_CompressedBeir_FailsHedgingReadiness()
    {
        var telemetry = new TipsRealYieldTelemetry
        {
            SecurityCusipOrPortfolioId = "TIPS-COMP-005",
            NominalTreasuryYieldPercent = 2.40,
            TipsRealYieldPercent = 2.10 // BEIR = 0.30% <= 0.50%
        };

        var result = _engine.AnalyzeRealYield(telemetry);

        Assert.Equal(0.30, result.CalculatedBreakevenInflationPercent);
        Assert.False(result.IsInflationHedgingOptimal);
    }
}
