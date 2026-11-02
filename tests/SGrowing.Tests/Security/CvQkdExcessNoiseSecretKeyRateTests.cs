using System;
using SGrowing.Api.Modules.Security.CvQkdExcessNoiseSecretKeyRate;
using Xunit;

namespace SGrowing.Tests.Security;

public sealed class CvQkdExcessNoiseSecretKeyRateTests
{
    private readonly CvQkdExcessNoiseSecretKeyRateEngine _engine = new();

    [Fact]
    public void AnalyzeQuantumChannel_LowExcessNoise_MaintainsHighRateKeyDistribution()
    {
        // L = 15.0 km, alpha = 0.20 -> 3.0 dB loss -> T = 0.5012
        // eta = 0.65, va = 5.0, vElec = 0.05 -> nominal = 1 + 0.65 * 0.5012 * 5.0 + 0.05 = 2.6789
        // measured = 2.6795 -> excess = (2.6795 - 2.6789) / (0.65 * 0.5012) = 0.0006 / 0.3258 = 0.0018 SNU < 0.008
        var telemetry = new CvQkdTelemetry(
            FiberLinkId: "QKD-FIBER-METRO-01",
            FiberDistanceKilometers: 15.0,
            FiberAttenuationDbPerKm: 0.20,
            AliceModulationVarianceSnu: 5.0,
            MeasuredTotalNoiseVarianceSnu: 2.6795,
            DetectorQuantumEfficiency: 0.65,
            ElectronicNoiseVarianceSnu: 0.05,
            ReconciliationEfficiencyBeta: 0.95,
            PulseRepetitionRateMhz: 50.0);

        var result = _engine.AnalyzeQuantumChannel(telemetry);

        Assert.Equal("QUANTUM_CHANNEL_SECURE", result.ChannelSecurityStatus);
        Assert.Equal("MAINTAIN_HIGH_RATE_CRYPTOGRAPHIC_KEY_DISTRIBUTION", result.ProtocolActionRecommendation);
        Assert.True(result.IsKeyExchangeSecure);
        Assert.True(result.AsymptoticSecretKeyRateBitsPerPulse > 0.01);
        Assert.True(result.ExcessNoiseSnu < 0.008);
    }

    [Fact]
    public void AnalyzeQuantumChannel_ElevatedExcessNoise_IssuesPhaseDriftWarning()
    {
        // excess noise around 0.010 SNU: (0.010 * 0.3258) + 2.6789 = 2.6822
        var telemetry = new CvQkdTelemetry(
            FiberLinkId: "QKD-FIBER-METRO-02",
            FiberDistanceKilometers: 15.0,
            FiberAttenuationDbPerKm: 0.20,
            AliceModulationVarianceSnu: 5.0,
            MeasuredTotalNoiseVarianceSnu: 2.6822,
            DetectorQuantumEfficiency: 0.65,
            ElectronicNoiseVarianceSnu: 0.05,
            ReconciliationEfficiencyBeta: 0.95,
            PulseRepetitionRateMhz: 50.0);

        var result = _engine.AnalyzeQuantumChannel(telemetry);

        Assert.Equal("PHASE_DRIFT_WARNING", result.ChannelSecurityStatus);
        Assert.Equal("INCREASE_PILOT_TONE_HOMODYNE_PHASE_CORRECTION_CADENCE", result.ProtocolActionRecommendation);
        Assert.True(result.IsKeyExchangeSecure);
        Assert.True(result.ExcessNoiseSnu >= 0.008 && result.ExcessNoiseSnu < 0.015);
    }

    [Fact]
    public void AnalyzeQuantumChannel_SevereExcessNoise_AbortsDueToEavesdropping()
    {
        // excess noise = 0.025 SNU >= 0.015: (0.025 * 0.3258) + 2.6789 = 2.6870
        var telemetry = new CvQkdTelemetry(
            FiberLinkId: "QKD-FIBER-METRO-03",
            FiberDistanceKilometers: 15.0,
            FiberAttenuationDbPerKm: 0.20,
            AliceModulationVarianceSnu: 5.0,
            MeasuredTotalNoiseVarianceSnu: 2.6870,
            DetectorQuantumEfficiency: 0.65,
            ElectronicNoiseVarianceSnu: 0.05,
            ReconciliationEfficiencyBeta: 0.95,
            PulseRepetitionRateMhz: 50.0);

        var result = _engine.AnalyzeQuantumChannel(telemetry);

        Assert.Equal("EAVESDROPPING_TAMPER_SUSPECTED", result.ChannelSecurityStatus);
        Assert.Equal("ABORT_QUANTUM_KEY_EXCHANGE_AND_REROUTE_FIBER_PATH", result.ProtocolActionRecommendation);
        Assert.False(result.IsKeyExchangeSecure);
        Assert.True(result.ExcessNoiseSnu >= 0.015);
    }

    [Fact]
    public void AnalyzeQuantumChannel_ZeroKeyRateCollapse_AbortsKeyDistribution()
    {
        // Huge excess noise 0.080 SNU: (0.080 * 0.3258) + 2.6789 = 2.7050
        var telemetry = new CvQkdTelemetry(
            FiberLinkId: "QKD-FIBER-METRO-04",
            FiberDistanceKilometers: 15.0,
            FiberAttenuationDbPerKm: 0.20,
            AliceModulationVarianceSnu: 5.0,
            MeasuredTotalNoiseVarianceSnu: 2.7050,
            DetectorQuantumEfficiency: 0.65,
            ElectronicNoiseVarianceSnu: 0.05,
            ReconciliationEfficiencyBeta: 0.95,
            PulseRepetitionRateMhz: 50.0);

        var result = _engine.AnalyzeQuantumChannel(telemetry);

        Assert.Equal("EAVESDROPPING_TAMPER_SUSPECTED", result.ChannelSecurityStatus);
        Assert.False(result.IsKeyExchangeSecure);
        Assert.Equal(0.0, result.AsymptoticSecretKeyRateBitsPerPulse);
    }
}
