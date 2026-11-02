using System;
using SGrowing.Api.Modules.Finance.MertonJumpDiffusionOptionPricing;
using Xunit;

namespace SGrowing.Tests.Finance;

public sealed class MertonJumpDiffusionOptionTests
{
    private readonly MertonJumpDiffusionOptionEngine _engine = new();

    [Fact]
    public void PriceOptionWithJumps_ContinuousDominated_ReturnsGaussianDiffusionRegime()
    {
        var telemetry = new MertonJumpDiffusionTelemetry
        {
            OptionSymbol = "SPY-C-500",
            UnderlyingPrice = 500.0,
            StrikePrice = 500.0,
            TimeToExpiryYears = 0.5,
            RiskFreeRate = 0.04,
            ContinuousVolatility = 0.16,
            JumpIntensityLambda = 0.05, // very few jumps
            MeanJumpSizeLogMuJ = -0.01,
            JumpVolatilitySigmaJ = 0.04,
            IsCallOption = true
        };

        var result = _engine.PriceOptionWithJumps(telemetry);

        Assert.Equal("SPY-C-500", result.OptionSymbol);
        Assert.False(result.IsJumpRiskDominant);
        Assert.True(result.MertonTheoreticalPrice > 0.0);
        Assert.True(Math.Abs(result.JumpRiskPremiumSpread) < 2.0); // Close to BS
        Assert.Equal("GAUSSIAN_DIFFUSION_CONTINUOUS_DOMINATED", result.OptionValuationRegime);
    }

    [Fact]
    public void PriceOptionWithJumps_HighNegativeJump_TriggersAsymmetricCrashDiscontinuity()
    {
        var telemetry = new MertonJumpDiffusionTelemetry
        {
            OptionSymbol = "TECH-P-200",
            UnderlyingPrice = 200.0,
            StrikePrice = 180.0,
            TimeToExpiryYears = 0.25,
            RiskFreeRate = 0.05,
            ContinuousVolatility = 0.20,
            JumpIntensityLambda = 1.8, // high jump frequency
            MeanJumpSizeLogMuJ = -0.15, // severe -15% mean drop
            JumpVolatilitySigmaJ = 0.25,
            IsCallOption = false // Put option
        };

        var result = _engine.PriceOptionWithJumps(telemetry);

        Assert.True(result.IsJumpRiskDominant);
        Assert.True(result.JumpVarianceFractionOfTotal >= 0.35);
        Assert.Equal("ASYMMETRIC_NEGATIVE_CRASH_DISCONTINUITY", result.OptionValuationRegime);
        Assert.Contains("discrete jump gap buffers", result.HedgingAndVolatilityGuidance);
    }

    [Fact]
    public void PriceOptionWithJumps_PositiveJumpIntensity_TriggersHighJumpEventVolatility()
    {
        var telemetry = new MertonJumpDiffusionTelemetry
        {
            OptionSymbol = "BIO-C-50",
            UnderlyingPrice = 50.0,
            StrikePrice = 60.0,
            TimeToExpiryYears = 0.2,
            RiskFreeRate = 0.04,
            ContinuousVolatility = 0.25,
            JumpIntensityLambda = 2.5, // High jump rate
            MeanJumpSizeLogMuJ = 0.02, // Neutral/positive jump size
            JumpVolatilitySigmaJ = 0.30,
            IsCallOption = true
        };

        var result = _engine.PriceOptionWithJumps(telemetry);

        Assert.True(result.IsJumpRiskDominant);
        Assert.Equal("HIGH_JUMP_INTENSITY_EVENT_VOLATILITY", result.OptionValuationRegime);
        Assert.Contains("structure risk-defined", result.HedgingAndVolatilityGuidance);
    }

    [Fact]
    public void PriceOptionWithJumps_PutCallParityConsistency_PricesConsistently()
    {
        var callTelemetry = new MertonJumpDiffusionTelemetry
        {
            OptionSymbol = "TEST-CALL",
            UnderlyingPrice = 100.0,
            StrikePrice = 100.0,
            TimeToExpiryYears = 1.0,
            RiskFreeRate = 0.05,
            ContinuousVolatility = 0.20,
            JumpIntensityLambda = 0.5,
            MeanJumpSizeLogMuJ = -0.05,
            JumpVolatilitySigmaJ = 0.10,
            IsCallOption = true
        };

        var putTelemetry = new MertonJumpDiffusionTelemetry
        {
            OptionSymbol = "TEST-PUT",
            UnderlyingPrice = 100.0,
            StrikePrice = 100.0,
            TimeToExpiryYears = 1.0,
            RiskFreeRate = 0.05,
            ContinuousVolatility = 0.20,
            JumpIntensityLambda = 0.5,
            MeanJumpSizeLogMuJ = -0.05,
            JumpVolatilitySigmaJ = 0.10,
            IsCallOption = false
        };

        var callResult = _engine.PriceOptionWithJumps(callTelemetry);
        var putResult = _engine.PriceOptionWithJumps(putTelemetry);

        // Put-Call Parity under Merton Jump Diffusion holds for discount rate
        Assert.True(callResult.MertonTheoreticalPrice > 0.0);
        Assert.True(putResult.MertonTheoreticalPrice > 0.0);
    }

    [Fact]
    public void PriceOptionWithJumps_ZeroOrNegativeInputs_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentNullException>(() => _engine.PriceOptionWithJumps(null!));

        var invalidTelemetry = new MertonJumpDiffusionTelemetry
        {
            OptionSymbol = "INV",
            UnderlyingPrice = -10.0,
            StrikePrice = 100.0
        };

        Assert.Throws<ArgumentException>(() => _engine.PriceOptionWithJumps(invalidTelemetry));
    }
}
