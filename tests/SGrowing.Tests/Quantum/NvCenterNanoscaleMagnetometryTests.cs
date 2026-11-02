namespace SGrowing.Tests.Quantum;

using System;
using FluentAssertions;
using SGrowing.Api.Modules.Quantum.NvCenterNanoscaleMagnetometry;
using Xunit;

public class NvCenterNanoscaleMagnetometryTests
{
    private readonly NvCenterNanoscaleMagnetometryEngine _engine = new();

    [Fact]
    public void AnalyzeMagnetometry_NominalOdMr_ReturnsOptimalRegime()
    {
        var telemetry = new NvCenterMagnetometryTelemetry(
            SensorId: "NV-DIAMOND-01",
            ZeroFieldSplittingFrequencyGigaHertz: 2.870,
            AppliedMicrowaveFrequencyGigaHertz: 2.870,
            MeasuredLowerDipFrequencyGigaHertz: 2.842, // f- = 2.842 GHz
            MeasuredUpperDipFrequencyGigaHertz: 2.898, // f+ = 2.898 GHz -> Delta f = 56 MHz
            OpticalFringeContrastPercent: 18.0,
            SpinDephasingCoherenceTimeT2StarMicroseconds: 2.5,
            OpticalReadoutPhotonCountPerShot: 40_000.0,
            SensorStandOffDistanceNanometers: 12.0
        );

        var result = _engine.AnalyzeMagnetometry(telemetry);

        result.SensorId.Should().Be("NV-DIAMOND-01");
        result.OdmrMagnetometryRegime.Should().Be("OPTIMAL_NANOSCALE_QUANTUM_MAGNETOMETRY");
        result.IsSensitivityOptimal.Should().BeTrue();
        result.ReconstructedZeemanSplittingMegaHertz.Should().BeApproximately(56.0, 0.1);
        result.CalculatedMagneticFieldMilliTesla.Should().BeApproximately(1.0, 0.05); // ~ 1.0 mT
        result.MagneticSensitivityNanoTeslaPerRootHertz.Should().BeLessThan(10.0);
        result.NanoscaleProbeGuidance.Should().ContainEquivalentOf("Sub-10 nm NV color center");
    }

    [Fact]
    public void AnalyzeMagnetometry_LowContrast_ReturnsContrastWashout()
    {
        var telemetry = new NvCenterMagnetometryTelemetry(
            SensorId: "NV-DIAMOND-02",
            ZeroFieldSplittingFrequencyGigaHertz: 2.870,
            AppliedMicrowaveFrequencyGigaHertz: 2.870,
            MeasuredLowerDipFrequencyGigaHertz: 2.850,
            MeasuredUpperDipFrequencyGigaHertz: 2.890,
            OpticalFringeContrastPercent: 2.5, // Below 4.0%
            SpinDephasingCoherenceTimeT2StarMicroseconds: 2.0,
            OpticalReadoutPhotonCountPerShot: 25_000.0,
            SensorStandOffDistanceNanometers: 15.0
        );

        var result = _engine.AnalyzeMagnetometry(telemetry);

        result.OdmrMagnetometryRegime.Should().Be("ODMR_CONTRAST_WASHOUT_COLLAPSE");
        result.IsSensitivityOptimal.Should().BeFalse();
        result.NanoscaleProbeGuidance.Should().ContainEquivalentOf("microwave waveguide impedance");
    }

    [Fact]
    public void AnalyzeMagnetometry_ShortT2Star_ReturnsCoherenceDephasing()
    {
        var telemetry = new NvCenterMagnetometryTelemetry(
            SensorId: "NV-DIAMOND-03",
            ZeroFieldSplittingFrequencyGigaHertz: 2.870,
            AppliedMicrowaveFrequencyGigaHertz: 2.870,
            MeasuredLowerDipFrequencyGigaHertz: 2.845,
            MeasuredUpperDipFrequencyGigaHertz: 2.895,
            OpticalFringeContrastPercent: 12.0,
            SpinDephasingCoherenceTimeT2StarMicroseconds: 0.18, // Degraded < 0.30 us
            OpticalReadoutPhotonCountPerShot: 20_000.0,
            SensorStandOffDistanceNanometers: 8.0
        );

        var result = _engine.AnalyzeMagnetometry(telemetry);

        result.OdmrMagnetometryRegime.Should().Be("COHERENCE_DEPHASING_PARAMAGNETIC_NOISE");
        result.IsSensitivityOptimal.Should().BeFalse();
        result.NanoscaleProbeGuidance.Should().ContainEquivalentOf("dynamical decoupling pulse sequence");
    }

    [Fact]
    public void AnalyzeMagnetometry_LowPhotonYield_ReturnsSensitivityMarginWarning()
    {
        var telemetry = new NvCenterMagnetometryTelemetry(
            SensorId: "NV-DIAMOND-04",
            ZeroFieldSplittingFrequencyGigaHertz: 2.870,
            AppliedMicrowaveFrequencyGigaHertz: 2.870,
            MeasuredLowerDipFrequencyGigaHertz: 2.848,
            MeasuredUpperDipFrequencyGigaHertz: 2.892,
            OpticalFringeContrastPercent: 5.0,
            SpinDephasingCoherenceTimeT2StarMicroseconds: 0.40,
            OpticalReadoutPhotonCountPerShot: 80.0, // Low photon count -> sensitivity > 50 nT/rtHz
            SensorStandOffDistanceNanometers: 25.0
        );

        var result = _engine.AnalyzeMagnetometry(telemetry);

        result.OdmrMagnetometryRegime.Should().Be("SUB_OPTIMAL_SENSITIVITY_MARGIN_WARNING");
        result.IsSensitivityOptimal.Should().BeFalse();
        result.MagneticSensitivityNanoTeslaPerRootHertz.Should().BeGreaterThan(50.0);
    }

    [Fact]
    public void AnalyzeMagnetometry_NullTelemetry_ThrowsArgumentNullException()
    {
        var act = () => _engine.AnalyzeMagnetometry(null!);
        act.Should().Throw<ArgumentNullException>();
    }
}
