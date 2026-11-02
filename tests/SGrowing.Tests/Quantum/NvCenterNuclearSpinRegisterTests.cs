using System;
using SGrowing.Api.Modules.Quantum.NvCenterNuclearSpinRegister;
using Xunit;

namespace SGrowing.Tests.Quantum;

public sealed class NvCenterNuclearSpinRegisterTests
{
    private readonly NvCenterNuclearSpinRegisterEngine _engine = new();

    [Fact]
    public void AnalyzeNuclearRegister_NullTelemetry_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => _engine.AnalyzeNuclearRegister(null!));
    }

    [Fact]
    public void AnalyzeNuclearRegister_EmptyRegisterId_ThrowsArgumentException()
    {
        var telemetry = new NvCenterNuclearSpinTelemetry(
            DiamondRegisterId: "",
            AxialMagneticFieldGauss: 510.0,
            GreenLaserOpticalPumpingPowerMilliwatts: 15.0,
            OpticalPumpingDurationMicroseconds: 5.0,
            CoupledNuclearSpinsCount: 3,
            HyperfineCouplingMegahertz: 2.16,
            MeasuredNuclearSpinPolarizationPercent: 96.0,
            NuclearQubitCoherenceT2Seconds: 1.5
        );

        Assert.Throws<ArgumentException>(() => _engine.AnalyzeNuclearRegister(telemetry));
    }

    [Fact]
    public void AnalyzeNuclearRegister_HighFidelityHyperpolarization_ReturnsHyperpolarizedMemoryRegime()
    {
        var telemetry = new NvCenterNuclearSpinTelemetry(
            DiamondRegisterId: "NV-DIAMOND-REG-01",
            AxialMagneticFieldGauss: 512.0, // Close to 510 G
            GreenLaserOpticalPumpingPowerMilliwatts: 20.0,
            OpticalPumpingDurationMicroseconds: 6.0,
            CoupledNuclearSpinsCount: 3,
            HyperfineCouplingMegahertz: 2.16,
            MeasuredNuclearSpinPolarizationPercent: 97.5,
            NuclearQubitCoherenceT2Seconds: 1.9
        );

        var result = _engine.AnalyzeNuclearRegister(telemetry);

        Assert.NotNull(result);
        Assert.Equal("NV-DIAMOND-REG-01", result.DiamondRegisterId);
        Assert.True(result.EffectivePolarizationFidelityPercent >= 90.0);
        Assert.True(result.IsNuclearMemoryFaultTolerant);
        Assert.Equal("HIGH_FIDELITY_HYPERPOLARIZED_QUANTUM_MEMORY_REGIME", result.NuclearSpinRegime);
        Assert.Contains("Ideal multi-qubit quantum node memory", result.HyperpolarizationGuidance);
    }

    [Fact]
    public void AnalyzeNuclearRegister_OffEslacDetuning_ReturnsDetuningHazard()
    {
        var telemetry = new NvCenterNuclearSpinTelemetry(
            DiamondRegisterId: "NV-OFF-ESLAC-02",
            AxialMagneticFieldGauss: 620.0, // Far from 510 G
            GreenLaserOpticalPumpingPowerMilliwatts: 15.0,
            OpticalPumpingDurationMicroseconds: 5.0,
            CoupledNuclearSpinsCount: 2,
            HyperfineCouplingMegahertz: 2.16,
            MeasuredNuclearSpinPolarizationPercent: 40.0,
            NuclearQubitCoherenceT2Seconds: 0.5
        );

        var result = _engine.AnalyzeNuclearRegister(telemetry);

        Assert.NotNull(result);
        Assert.Equal("OFF_ESLAC_MAGNETIC_DETUNING_LOW_POLARIZATION_HAZARD", result.NuclearSpinRegime);
        Assert.False(result.IsNuclearMemoryFaultTolerant);
        Assert.Contains("retune electromagnet", result.HyperpolarizationGuidance);
    }

    [Fact]
    public void AnalyzeNuclearRegister_ShortCoherenceBathDephasing_ReturnsParamagneticBathLimit()
    {
        var telemetry = new NvCenterNuclearSpinTelemetry(
            DiamondRegisterId: "NV-NOISY-BATH-03",
            AxialMagneticFieldGauss: 510.0,
            GreenLaserOpticalPumpingPowerMilliwatts: 15.0,
            OpticalPumpingDurationMicroseconds: 5.0,
            CoupledNuclearSpinsCount: 1,
            HyperfineCouplingMegahertz: 2.16,
            MeasuredNuclearSpinPolarizationPercent: 88.0,
            NuclearQubitCoherenceT2Seconds: 0.05 // < 0.1 s
        );

        var result = _engine.AnalyzeNuclearRegister(telemetry);

        Assert.NotNull(result);
        Assert.Equal("PARAMAGNETIC_BATH_DEPHASING_LIMIT", result.NuclearSpinRegime);
        Assert.False(result.IsNuclearMemoryFaultTolerant);
        Assert.Contains("dynamical decoupling", result.HyperpolarizationGuidance);
    }
}
