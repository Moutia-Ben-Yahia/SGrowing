using System;
using SGrowing.Api.Modules.Finance.MicrostructureKylesLambdaAdverseSelection;
using Xunit;

namespace SGrowing.Tests.Finance;

public sealed class MicrostructureKylesLambdaTests
{
    private readonly MicrostructureKylesLambdaEngine _engine = new();

    [Fact]
    public void AnalyzeMicrostructure_BalancedFlow_ReturnsNoiseTraderRegime()
    {
        var telemetry = new MicrostructureKylesLambdaTelemetry
        {
            Ticker = "NVDA",
            CovariancePriceChangeOrderFlow = 0.015,
            VarianceOrderFlow = 15000.0,
            RollingBaselineLambda = 0.0000010,
            HasbrouckPermanentVarianceFraction = 0.45,
            VolumeSynchronizedToxicityVpin = 0.28,
            EffectiveHalfSpreadBps = 1.2,
            CurrentBookDepthShares = 80000.0
        };

        var result = _engine.AnalyzeMicrostructure(telemetry);

        Assert.Equal("NVDA", result.Ticker);
        Assert.False(result.IsLambdaSpikeDetected);
        Assert.False(result.IsInformationDominantRegime);
        Assert.False(result.IsToxicityCritical);
        Assert.Equal(1.0, result.RecommendedSpreadWideningMultiplier);
        Assert.Equal("BALANCED_NOISE_TRADER_LIQUIDITY_HARVEST", result.MarketMicrostructureRegime);
    }

    [Fact]
    public void AnalyzeMicrostructure_HighVpinAndLambdaSpike_TriggersToxicCascade()
    {
        var telemetry = new MicrostructureKylesLambdaTelemetry
        {
            Ticker = "TSLA",
            CovariancePriceChangeOrderFlow = 0.080,
            VarianceOrderFlow = 10000.0, // lambda = 8.0e-6
            RollingBaselineLambda = 0.0000020, // ratio = 4.0 >= 2.5
            HasbrouckPermanentVarianceFraction = 0.85,
            VolumeSynchronizedToxicityVpin = 0.76, // VPIN >= 0.65
            EffectiveHalfSpreadBps = 3.5,
            CurrentBookDepthShares = 12000.0
        };

        var result = _engine.AnalyzeMicrostructure(telemetry);

        Assert.True(result.IsLambdaSpikeDetected);
        Assert.True(result.IsToxicityCritical);
        Assert.True(result.RecommendedSpreadWideningMultiplier > 2.0);
        Assert.Equal("TOXIC_INFORMED_CASCADE_LIQUIDITY_RUN", result.MarketMicrostructureRegime);
        Assert.Contains("cancel passive bids", result.ExecutionDefenseProtocol);
    }

    [Fact]
    public void AnalyzeMicrostructure_CriticalVpinOnly_TriggersToxicityWarning()
    {
        var telemetry = new MicrostructureKylesLambdaTelemetry
        {
            Ticker = "SPY",
            CovariancePriceChangeOrderFlow = 0.010,
            VarianceOrderFlow = 10000.0, // lambda = 1.0e-6
            RollingBaselineLambda = 0.0000010, // ratio = 1.0 < 2.5
            HasbrouckPermanentVarianceFraction = 0.50,
            VolumeSynchronizedToxicityVpin = 0.72, // VPIN >= 0.65
            EffectiveHalfSpreadBps = 1.0,
            CurrentBookDepthShares = 250000.0
        };

        var result = _engine.AnalyzeMicrostructure(telemetry);

        Assert.False(result.IsLambdaSpikeDetected);
        Assert.True(result.IsToxicityCritical);
        Assert.Equal("HIGH_VPIN_ORDER_TOXICITY_WARNING", result.MarketMicrostructureRegime);
        Assert.Contains("Elevate quoting spread", result.ExecutionDefenseProtocol);
    }

    [Fact]
    public void AnalyzeMicrostructure_PermanentInfoDominance_IdentifiesInformedRegime()
    {
        var telemetry = new MicrostructureKylesLambdaTelemetry
        {
            Ticker = "AAPL",
            CovariancePriceChangeOrderFlow = 0.015,
            VarianceOrderFlow = 10000.0, // lambda = 1.5e-6
            RollingBaselineLambda = 0.0000012, // ratio = 1.25 < 2.5
            HasbrouckPermanentVarianceFraction = 0.74, // info >= 0.60
            VolumeSynchronizedToxicityVpin = 0.35,
            EffectiveHalfSpreadBps = 1.5,
            CurrentBookDepthShares = 65000.0
        };

        var result = _engine.AnalyzeMicrostructure(telemetry);

        Assert.True(result.IsInformationDominantRegime);
        Assert.Equal("PERMANENT_INFORMATION_DOMINATED_FLOW", result.MarketMicrostructureRegime);
        Assert.Contains("reflect fundamental information", result.ExecutionDefenseProtocol);
    }

    [Fact]
    public void AnalyzeMicrostructure_ZeroOrNegativeVariance_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentNullException>(() => _engine.AnalyzeMicrostructure(null!));

        var invalidTelemetry = new MicrostructureKylesLambdaTelemetry
        {
            Ticker = "INV",
            VarianceOrderFlow = 0.0
        };

        Assert.Throws<ArgumentException>(() => _engine.AnalyzeMicrostructure(invalidTelemetry));
    }
}
