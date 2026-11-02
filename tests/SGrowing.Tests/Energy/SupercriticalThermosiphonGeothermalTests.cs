using FluentAssertions;
using SGrowing.Api.Modules.Energy.SupercriticalThermosiphonGeothermal;
using Xunit;

namespace SGrowing.Tests.Energy;

public sealed class SupercriticalThermosiphonGeothermalTests
{
    private readonly SupercriticalThermosiphonGeothermalEngine _engine = new();

    [Fact]
    public void AnalyzeThermosiphonState_NominalSupercritical_ReturnsSelfDriven()
    {
        var telemetry = new SupercriticalThermosiphonTelemetry(
            WellboreId: "EGS-SUPERCRIT-WELL-01",
            WellboreDepthMeters: 5200.0,
            BottomHoleTemperatureCelsius: 420.0,
            SurfaceInjectionTemperatureCelsius: 45.0,
            WorkingFluidMassFlowRateKgPerSec: 85.0,
            ThermosiphonBuoyancyHeadBar: 42.5,
            WellboreFrictionLossBar: 28.0,
            ThermalPowerExtractedMegawatts: 36.5,
            SurfaceTurbineThrottleValveActive: true
        );

        var result = _engine.AnalyzeThermosiphonState(telemetry);

        result.ThermosiphonRegime.Should().Be("SUPERCRITICAL_THERMOSIPHON_SELF_DRIVEN");
        result.IsThermosiphonCirculationStable.Should().BeTrue();
        result.NetBuoyancyDrivePressureBar.Should().Be(14.5);
        result.ThermodynamicExergyEfficiencyPercent.Should().BeGreaterThan(30.0);
        result.ParasiticPumpingPowerSavedMegawatts.Should().BeGreaterThan(0.20);
    }

    [Fact]
    public void AnalyzeThermosiphonState_HighFrictionLoss_ReturnsStagnationHazard()
    {
        var telemetry = new SupercriticalThermosiphonTelemetry(
            WellboreId: "EGS-CHOKED-WELL-02",
            WellboreDepthMeters: 5500.0,
            BottomHoleTemperatureCelsius: 380.0,
            SurfaceInjectionTemperatureCelsius: 50.0,
            WorkingFluidMassFlowRateKgPerSec: 110.0,
            ThermosiphonBuoyancyHeadBar: 26.0,
            WellboreFrictionLossBar: 32.0, // Friction > Buoyancy -> Net = -6.0 bar
            ThermalPowerExtractedMegawatts: 20.0,
            SurfaceTurbineThrottleValveActive: true
        );

        var result = _engine.AnalyzeThermosiphonState(telemetry);

        result.ThermosiphonRegime.Should().Be("FRICTION_CHOKING_STAGNATION_HAZARD");
        result.IsThermosiphonCirculationStable.Should().BeFalse();
        result.NetBuoyancyDrivePressureBar.Should().BeLessThan(0.0);
        result.WellheadControlCommand.Should().Contain("kick-pump");
    }

    [Fact]
    public void AnalyzeThermosiphonState_LowBuoyancyMarginNoValve_ReturnsOscillationRisk()
    {
        var telemetry = new SupercriticalThermosiphonTelemetry(
            WellboreId: "EGS-OSCILLATING-WELL-03",
            WellboreDepthMeters: 4800.0,
            BottomHoleTemperatureCelsius: 360.0,
            SurfaceInjectionTemperatureCelsius: 45.0,
            WorkingFluidMassFlowRateKgPerSec: 75.0,
            ThermosiphonBuoyancyHeadBar: 28.0,
            WellboreFrictionLossBar: 25.0, // Net = 3.0 bar < 5.0
            ThermalPowerExtractedMegawatts: 22.0,
            SurfaceTurbineThrottleValveActive: false // Throttle valve not active
        );

        var result = _engine.AnalyzeThermosiphonState(telemetry);

        result.ThermosiphonRegime.Should().Be("DENSITY_WAVE_OSCILLATION_RISK");
        result.IsThermosiphonCirculationStable.Should().BeFalse();
        result.WellheadControlCommand.Should().Contain("supercritical sCO2 density-wave resonance");
    }

    [Fact]
    public void AnalyzeThermosiphonState_DepletedReservoir_ReturnsThermalDepletion()
    {
        var telemetry = new SupercriticalThermosiphonTelemetry(
            WellboreId: "EGS-COOLED-WELL-04",
            WellboreDepthMeters: 4500.0,
            BottomHoleTemperatureCelsius: 195.0, // Depleted < 220C
            SurfaceInjectionTemperatureCelsius: 40.0,
            WorkingFluidMassFlowRateKgPerSec: 50.0,
            ThermosiphonBuoyancyHeadBar: 22.0,
            WellboreFrictionLossBar: 14.0,
            ThermalPowerExtractedMegawatts: 6.5, // Low power < 8.0 MW
            SurfaceTurbineThrottleValveActive: true
        );

        var result = _engine.AnalyzeThermosiphonState(telemetry);

        result.ThermosiphonRegime.Should().Be("THERMAL_DEPLETION_DEGRADED");
        result.IsThermosiphonCirculationStable.Should().BeFalse();
        result.WellheadControlCommand.Should().Contain("thermal depletion");
    }
}
