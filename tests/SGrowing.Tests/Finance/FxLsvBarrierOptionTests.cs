using SGrowing.Api.Modules.Finance.FxLocalStochasticVolatility;
using Xunit;

namespace SGrowing.Tests.Finance;

public class FxLsvBarrierOptionTests
{
    private readonly FxLsvBarrierEngine _engine = new();

    [Fact]
    public void AnalyzeBarrierOption_NominalUpAndOutCall_ReturnsPricingReliable()
    {
        var telemetry = new FxLsvBarrierTelemetry
        {
            DealContractId = "FX-BARRIER-EURUSD-001",
            CurrencyPair = "EURUSD",
            SpotExchangeRate = 1.0850,
            DomesticInterestRatePercent = 4.25,
            ForeignInterestRatePercent = 3.50,
            BarrierLevel = 1.1250, // ~3.7% out of the money
            BarrierType = "UpAndOutCall",
            AtTheMoneyImpliedVolatilityPercent = 8.0,
            RiskReversal25DeltaPercent = -0.40,
            Butterfly25DeltaPercent = 0.25,
            StochasticVolOfVolXi = 0.40,
            MeanReversionSpeedKappa = 1.8,
            OptionMaturityYears = 0.5
        };

        var result = _engine.AnalyzeBarrierOption(telemetry);

        Assert.Equal("FX-BARRIER-EURUSD-001", result.DealContractId);
        Assert.True(result.IsLsvPricingReliable);
        Assert.True(result.IsLeverageFunctionCalibrated);
        Assert.True(result.IsLocalVarianceArbitrageFree);
        Assert.True(result.IsBarrierPinningRiskContained);
        Assert.True(result.CalculatedBarrierOptionPricePips > 0.0);
        Assert.True(result.BarrierHittingProbabilityPercent > 0.0 && result.BarrierHittingProbabilityPercent < 100.0);
        Assert.Empty(result.ActiveRegimes);
    }

    [Fact]
    public void AnalyzeBarrierOption_ExtremeSkew_TriggersLeverageDivergence()
    {
        var telemetry = new FxLsvBarrierTelemetry
        {
            DealContractId = "FX-LEVERAGE-DIVERGENCE",
            RiskReversal25DeltaPercent = 2.45, // Extreme skew pushing leverage > 3.0
            Butterfly25DeltaPercent = 1.85,
            StochasticVolOfVolXi = 1.6
        };

        var result = _engine.AnalyzeBarrierOption(telemetry);

        Assert.False(result.IsLeverageFunctionCalibrated);
        Assert.False(result.IsLsvPricingReliable);
        Assert.Contains("LEVERAGE_FUNCTION_DIVERGENCE_INSTABILITY", result.ActiveRegimes);
        Assert.NotEmpty(result.RecommendedMitigations);
    }

    [Fact]
    public void AnalyzeBarrierOption_FlatSmile_TriggersCalendarArbitrageAlert()
    {
        var telemetry = new FxLsvBarrierTelemetry
        {
            DealContractId = "FX-CALENDAR-ARBITRAGE",
            AtTheMoneyImpliedVolatilityPercent = 0.8, // Low vol
            Butterfly25DeltaPercent = 0.01 // Butterfly violation
        };

        var result = _engine.AnalyzeBarrierOption(telemetry);

        Assert.False(result.IsLocalVarianceArbitrageFree);
        Assert.Contains("LOCAL_VOLATILITY_SURFACE_CALENDAR_ARBITRAGE", result.ActiveRegimes);
    }

    [Fact]
    public void AnalyzeBarrierOption_NearBarrierPinning_TriggersPinningAlert()
    {
        var telemetry = new FxLsvBarrierTelemetry
        {
            DealContractId = "FX-PINNING-ALERT",
            SpotExchangeRate = 1.0850,
            BarrierLevel = 1.0885, // Only 0.32% away from barrier
            StochasticVolOfVolXi = 0.95 // High vol of vol
        };

        var result = _engine.AnalyzeBarrierOption(telemetry);

        Assert.False(result.IsBarrierPinningRiskContained);
        Assert.Contains("BARRIER_PINNING_GAP_SLIPPAGE_ALERT", result.ActiveRegimes);
    }

    [Fact]
    public void AnalyzeBarrierOption_HittingProbability_IncreasesNearBarrier()
    {
        var distantTelemetry = new FxLsvBarrierTelemetry
        {
            DealContractId = "DISTANT-BARRIER",
            SpotExchangeRate = 1.0850,
            BarrierLevel = 1.1500
        };

        var closeTelemetry = new FxLsvBarrierTelemetry
        {
            DealContractId = "CLOSE-BARRIER",
            SpotExchangeRate = 1.0850,
            BarrierLevel = 1.0950
        };

        var distantRes = _engine.AnalyzeBarrierOption(distantTelemetry);
        var closeRes = _engine.AnalyzeBarrierOption(closeTelemetry);

        Assert.True(closeRes.BarrierHittingProbabilityPercent > distantRes.BarrierHittingProbabilityPercent);
    }
}
