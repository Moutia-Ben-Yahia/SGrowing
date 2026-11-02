namespace SGrowing.Tests.Quantum;

using System;
using FluentAssertions;
using SGrowing.Api.Modules.Quantum.StrontiumOpticalLatticeClock;
using Xunit;

public class StrontiumOpticalLatticeClockTests
{
    private readonly StrontiumOpticalLatticeClockEngine _engine = new();

    [Fact]
    public void AnalyzeOpticalAtomicClock_NominalLocked_ReturnsQuantumProjectionNoiseLimited()
    {
        var telemetry = new StrontiumClockTelemetry(
            ClockSystemId: "SR87-OPT-CLOCK-01",
            TrappedSr87AtomCount: 10_000.0,
            LatticeLaserWavelengthNanometers: 813.427,
            ClockLaserLinewidthHertz: 0.15,
            MeasuredFractionalFrequencyOffset: 1.2e-17,
            BlackbodyRadiationTemperatureKelvin: 295.0,
            GeodeticElevationMeters: 142.5,
            FrequencyCombRepetitionRateMegaHertz: 250.0,
            CarrierEnvelopeOffsetLocked: true
        );

        var result = _engine.AnalyzeOpticalAtomicClock(telemetry);

        result.ClockSystemId.Should().Be("SR87-OPT-CLOCK-01");
        result.MetrologyPrecisionRegime.Should().Be("QUANTUM_PROJECTION_NOISE_LIMITED");
        result.IsClockMetrologicallyValid.Should().BeTrue();
        result.AllanDeviationInstabilityAtOneSecond.Should().BeLessThan(1e-15);
        result.BlackbodyRadiationFractionalShift.Should().BeNegative();
        result.GravitationalRedshiftCorrection.Should().BeApproximately(142.5 * 1.09e-16, 1e-18);
        result.LaserFrequencySynthesisAction.Should().ContainEquivalentOf("quantum projection noise limit");
    }

    [Fact]
    public void AnalyzeOpticalAtomicClock_CombCarrierOffsetSlip_ReturnsCarrierOffsetSlip()
    {
        var telemetry = new StrontiumClockTelemetry(
            ClockSystemId: "SR87-OPT-CLOCK-02",
            TrappedSr87AtomCount: 8_000.0,
            LatticeLaserWavelengthNanometers: 813.427,
            ClockLaserLinewidthHertz: 0.20,
            MeasuredFractionalFrequencyOffset: 2.0e-17,
            BlackbodyRadiationTemperatureKelvin: 296.0,
            GeodeticElevationMeters: 50.0,
            FrequencyCombRepetitionRateMegaHertz: 250.0,
            CarrierEnvelopeOffsetLocked: false
        );

        var result = _engine.AnalyzeOpticalAtomicClock(telemetry);

        result.MetrologyPrecisionRegime.Should().Be("OPTICAL_COMB_CARRIER_OFFSET_SLIP");
        result.IsClockMetrologicallyValid.Should().BeFalse();
        result.LaserFrequencySynthesisAction.Should().ContainEquivalentOf("f-to-2f interferometer");
    }

    [Fact]
    public void AnalyzeOpticalAtomicClock_HighLaserLinewidth_ReturnsCavityDriftUnlocked()
    {
        var telemetry = new StrontiumClockTelemetry(
            ClockSystemId: "SR87-OPT-CLOCK-03",
            TrappedSr87AtomCount: 5_000.0,
            LatticeLaserWavelengthNanometers: 813.427,
            ClockLaserLinewidthHertz: 2.4, // Linewidth > 1.8 Hz
            MeasuredFractionalFrequencyOffset: 1.0e-17,
            BlackbodyRadiationTemperatureKelvin: 295.0,
            GeodeticElevationMeters: 100.0,
            FrequencyCombRepetitionRateMegaHertz: 250.0,
            CarrierEnvelopeOffsetLocked: true
        );

        var result = _engine.AnalyzeOpticalAtomicClock(telemetry);

        result.MetrologyPrecisionRegime.Should().Be("OPTICAL_CAVITY_DRIFT_UNLOCKED");
        result.IsClockMetrologicallyValid.Should().BeFalse();
        result.LaserFrequencySynthesisAction.Should().ContainEquivalentOf("Pound-Drever-Hall");
    }

    [Fact]
    public void AnalyzeOpticalAtomicClock_LatticeLaserWavelengthOffset_ReturnsBbrWarning()
    {
        var telemetry = new StrontiumClockTelemetry(
            ClockSystemId: "SR87-OPT-CLOCK-04",
            TrappedSr87AtomCount: 12_000.0,
            LatticeLaserWavelengthNanometers: 813.520, // offset > 0.035 nm
            ClockLaserLinewidthHertz: 0.10,
            MeasuredFractionalFrequencyOffset: 1.0e-17,
            BlackbodyRadiationTemperatureKelvin: 295.0,
            GeodeticElevationMeters: 10.0,
            FrequencyCombRepetitionRateMegaHertz: 250.0,
            CarrierEnvelopeOffsetLocked: true
        );

        var result = _engine.AnalyzeOpticalAtomicClock(telemetry);

        result.MetrologyPrecisionRegime.Should().Be("BLACKBODY_RADIATION_UNCERTAINTY_WARNING");
        result.IsClockMetrologicallyValid.Should().BeFalse();
        result.LaserFrequencySynthesisAction.Should().ContainEquivalentOf("magic wavelength");
    }

    [Fact]
    public void AnalyzeOpticalAtomicClock_NullTelemetry_ThrowsArgumentNullException()
    {
        var act = () => _engine.AnalyzeOpticalAtomicClock(null!);
        act.Should().Throw<ArgumentNullException>();
    }
}
