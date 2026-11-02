namespace SGrowing.Tests.Finance;

using SGrowing.Api.Modules.Finance.AmmLossVersusRebalancing;
using Xunit;

public class AmmLossVersusRebalancingTests
{
    private readonly AmmLossVersusRebalancingEngine _engine = new();

    [Fact]
    public void EvaluatePoolLvr_ProfitableOrganicVolume_MaintainsBaseFeeAndSurplus()
    {
        // TVL = $10M, Vol = 40% (0.40) -> sigma^2 / 8 = 0.16 / 8 = 0.02 / yr
        // 24 hours (1/365 yr) -> LVR = $10M * 0.02 / 365 = $547.95
        // Volume = $10M at 30 bps (0.003) -> Fee revenue = $30,000.00
        // Net LP PnL = +$29,452.05 >> 0
        var telemetry = new AmmLvrTelemetry(
            PoolId: "UNISWAP-V3-ETH-USDC-030",
            TokenPair: "ETH/USDC",
            TotalValueLockedUsd: 10000000.00m,
            CurrentAnnualizedVolatility: 0.40,
            BaseSwapFeeBps: 30,
            ObservationWindowHours: 24.0,
            CumulativeTradingVolumeUsd: 10000000.00m,
            ToxicOrderFlowFraction: 0.10,
            PriceTickConcentrationWidthPercent: 10.0);

        var result = _engine.EvaluatePoolLvr(telemetry);

        Assert.Equal("UNISWAP-V3-ETH-USDC-030", result.PoolId);
        Assert.Equal("PROFITABLE_LP_CONFINEMENT", result.RebalancingState);
        Assert.Equal("MAINTAIN_BASE_FEE_TIER", result.RecommendedAmmAction);
        Assert.False(result.IsLvrBleedExceedingFees);
        Assert.True(result.NetLpPnLUsd > 0m);
        Assert.Equal(30000.00m, result.AccumulatedFeeRevenueUsd);
    }

    [Fact]
    public void EvaluatePoolLvr_HighToxicFlowAndVolatility_TriggersCriticalLvrDrainAlert()
    {
        // TVL = $50M, Vol = 100% (1.00) -> sigma^2 / 8 = 0.125 / yr
        // 24 hours -> LVR = $50M * 0.125 / 365 = $17,123.29
        // Volume = $5M at 5 bps (0.0005) -> Fee revenue = $2,500.00
        // Net LP PnL = -$14,623.29 (Critical loss!)
        var telemetry = new AmmLvrTelemetry(
            PoolId: "UNISWAP-V3-WBTC-USDT-005",
            TokenPair: "WBTC/USDT",
            TotalValueLockedUsd: 50000000.00m,
            CurrentAnnualizedVolatility: 1.00,
            BaseSwapFeeBps: 5,
            ObservationWindowHours: 24.0,
            CumulativeTradingVolumeUsd: 5000000.00m,
            ToxicOrderFlowFraction: 0.65, // High toxic flow
            PriceTickConcentrationWidthPercent: 5.0);

        var result = _engine.EvaluatePoolLvr(telemetry);

        Assert.True(result.IsLvrBleedExceedingFees);
        Assert.Equal("CRITICAL_TOXIC_ARBITRAGE_DRAIN", result.RebalancingState);
        Assert.Equal("WIDEN_CONCENTRATED_TICKS_AND_HIKE_FEE", result.RecommendedAmmAction);
        Assert.True(result.NetLpPnLUsd < 0m);
        Assert.True(result.OptimalDynamicFeeBps > telemetry.BaseSwapFeeBps);
        Assert.Contains("CRITICAL LVR BLEED", result.ExecutiveSummary);
    }

    [Fact]
    public void EvaluatePoolLvr_ModerateBleed_ActivatesVolatilitySurcharge()
    {
        // TVL = $20M, Vol = 70% (0.70) -> LVR ~ $3,353
        // Volume = $5M at 5 bps -> Fee = $2,500 -> Loss = -$853
        // Toxic fraction = 0.20 (< 0.35) -> Moderate
        var telemetry = new AmmLvrTelemetry(
            PoolId: "CURVE-CRV-USD-004",
            TokenPair: "CRV/USD",
            TotalValueLockedUsd: 20000000.00m,
            CurrentAnnualizedVolatility: 0.70,
            BaseSwapFeeBps: 5,
            ObservationWindowHours: 24.0,
            CumulativeTradingVolumeUsd: 5000000.00m,
            ToxicOrderFlowFraction: 0.20,
            PriceTickConcentrationWidthPercent: 8.0);

        var result = _engine.EvaluatePoolLvr(telemetry);

        Assert.True(result.IsLvrBleedExceedingFees);
        Assert.Equal("LVR_EQUILIBRIUM_NEUTRAL", result.RebalancingState);
        Assert.Equal("ACTIVATE_DYNAMIC_VOLATILITY_SURCHARGE", result.RecommendedAmmAction);
        Assert.Contains("MODERATE LVR BLEED", result.ExecutiveSummary);
    }

    [Fact]
    public void EvaluatePoolLvr_ZeroTvl_ThrowsArgumentOutOfRangeException()
    {
        var invalid = new AmmLvrTelemetry(
            PoolId: "ERR-001",
            TokenPair: "ETH/USDC",
            TotalValueLockedUsd: 0m,
            CurrentAnnualizedVolatility: 0.5,
            BaseSwapFeeBps: 30,
            ObservationWindowHours: 24.0,
            CumulativeTradingVolumeUsd: 1000m,
            ToxicOrderFlowFraction: 0.1,
            PriceTickConcentrationWidthPercent: 5.0);

        Assert.Throws<ArgumentOutOfRangeException>(() => _engine.EvaluatePoolLvr(invalid));
    }
}
