namespace SGrowing.Tests.Telecom;

using FluentAssertions;
using SGrowing.Api.Modules.Telecom.SubseaDwdmRamanCompensator;
using Xunit;

public class SubseaDwdmRamanCompensatorTests
{
    private readonly SubseaDwdmRamanCompensatorEngine _engine = new();

    [Fact]
    public void EvaluateAndCompensate_NominalSpectrum_ReportsOptimalFlatness()
    {
        var span = new FiberSpanParameters("SPAN-ATLANTIC-01", 80.0, 0.165, 21.0, 110.0, 0.35);

        var channels = new List<DwdmChannelTelemetry>
        {
            new(1, 1530.0, LaunchPowerDbm: 1.0, ReceivedOsnrDb: 24.5, BitErrorRateQ: 8.5),
            new(50, 1550.0, LaunchPowerDbm: 1.0, ReceivedOsnrDb: 25.0, BitErrorRateQ: 8.7),
            new(100, 1570.0, LaunchPowerDbm: 1.0, ReceivedOsnrDb: 25.2, BitErrorRateQ: 8.8)
        };

        var request = new RamanCompensationRequest(span, channels, TotalInputPowerDbm: 16.0);
        var plan = _engine.EvaluateAndCompensate(request);

        plan.SpanId.Should().Be("SPAN-ATLANTIC-01");
        plan.NonlinearityThresholdExceeded.Should().BeFalse();
        plan.SpectralPowerTiltDb.Should().BeLessThan(1.5);
        plan.OpticalEqualizationStrategy.Should().Contain("OPTICAL_CHANNEL_NOMINAL");
        plan.PumpAdjustments.Should().HaveCount(3);
    }

    [Fact]
    public void EvaluateAndCompensate_HighInputPower_FlagsNonlinearityThresholdExceeded()
    {
        var span = new FiberSpanParameters("SPAN-TRANS-PACIFIC-04", 100.0, 0.160, 20.0, 80.0, 0.40);

        var channels = new List<DwdmChannelTelemetry>
        {
            new(1, 1530.0, 5.0, 26.0, 9.0),
            new(100, 1570.0, 5.0, 27.0, 9.2)
        };

        // Extreme total input power (26 dBm = 400 mW) causing severe Kerr self-phase modulation
        var request = new RamanCompensationRequest(span, channels, TotalInputPowerDbm: 26.0);
        var plan = _engine.EvaluateAndCompensate(request);

        plan.NonlinearityThresholdExceeded.Should().BeTrue();
        plan.NonlinearPhaseShiftRadians.Should().BeGreaterThan(1.2);
        plan.OpticalEqualizationStrategy.Should().Contain("KERR_NONLINEAR_OVERDRIVE");
        plan.OpticalEqualizationStrategy.Should().Contain("Digital Back-Propagation");
    }

    [Fact]
    public void EvaluateAndCompensate_SevereSrsTilt_BoostsShortWavelengthRamanPump()
    {
        var span = new FiberSpanParameters();

        // 4.5 dB tilt between short and long wavelengths
        var channels = new List<DwdmChannelTelemetry>
        {
            new(1, 1530.0, 2.0, ReceivedOsnrDb: 21.0, 7.5),
            new(100, 1570.0, 2.0, ReceivedOsnrDb: 25.5, 9.0)
        };

        var request = new RamanCompensationRequest(span, channels, TotalInputPowerDbm: 15.0);
        var plan = _engine.EvaluateAndCompensate(request);

        plan.SpectralPowerTiltDb.Should().Be(4.5);
        var pump1 = plan.PumpAdjustments.First(p => p.PumpIndex == 1);
        var pump3 = plan.PumpAdjustments.First(p => p.PumpIndex == 3);

        pump1.RecommendedPowerMilliwatts.Should().BeGreaterThan(pump3.RecommendedPowerMilliwatts);
        plan.OpticalEqualizationStrategy.Should().Contain("SRS_SPECTRAL_TILT_ACTIVE");
    }

    [Fact]
    public void EvaluateAndCompensate_EmptyChannels_ReturnsStandbyPlan()
    {
        var span = new FiberSpanParameters("SPAN-EMPTY");
        var request = new RamanCompensationRequest(span, []);

        var plan = _engine.EvaluateAndCompensate(request);

        plan.NonlinearityThresholdExceeded.Should().BeFalse();
        plan.PumpAdjustments.Should().BeEmpty();
        plan.OpticalEqualizationStrategy.Should().Contain("NO_ACTIVE_CHANNELS");
    }
}
