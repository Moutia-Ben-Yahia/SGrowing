using System;
using SGrowing.Api.Modules.Energy.KilopowerSpaceNuclearBrayton;
using Xunit;

namespace SGrowing.Tests.Energy;

public sealed class KilopowerSpaceNuclearBraytonTests
{
    private readonly KilopowerSpaceNuclearBraytonEngine _engine = new();

    [Fact]
    public void AnalyzeSurfacePowerCycle_NominalLunarNight_ProducesAdequatePower()
    {
        var telemetry = new KilopowerTelemetry(
            ReactorId: "KILOPOWER-KRUSTY-LUNAR-01",
            CoreSodiumHeatPipeTempCelsius: 700.0,
            BraytonTurbineInletTempCelsius: 680.0,
            BraytonCompressorInletTempCelsius: 120.0,
            WorkingFluidMassFlowKgPerSec: 0.45,
            RadiatorReturnFluidTempCelsius: 48.0,
            RadiatorFreezeThresholdTempCelsius: 20.0,
            RadiatorPanelAreaM2: 32.0,
            DeepSpaceSinkTempKelvin: 85.0,
            RadiatorReturnTempRateCelsiusPerMin: -0.1,
            ElectricalLoadDemandKilowatts: 10.0);

        var result = _engine.AnalyzeSurfacePowerCycle(telemetry);

        Assert.Equal("STEADY_STATE_SURFACE_POWER", result.ThermalSystemOperatingState);
        Assert.Equal("MAINTAIN_CLOSED_BRAYTON_EQUILIBRIUM", result.AutonomousControlRecommendation);
        Assert.True(result.IsPowerAdequate);
        Assert.True(result.NetElectricalPowerKilowatts >= 10.0);
        Assert.True(result.RadiatorFreezeOutMarginKelvin >= 15.0);
    }

    [Fact]
    public void AnalyzeSurfacePowerCycle_RadiatorFreezeMarginCollapse_TriggersPanelRetraction()
    {
        // Margin = 22.0 - 20.0 = 2.0 K < 5.0 K
        var telemetry = new KilopowerTelemetry(
            ReactorId: "KILOPOWER-KRUSTY-LUNAR-02",
            CoreSodiumHeatPipeTempCelsius: 690.0,
            BraytonTurbineInletTempCelsius: 660.0,
            BraytonCompressorInletTempCelsius: 110.0,
            WorkingFluidMassFlowKgPerSec: 0.40,
            RadiatorReturnFluidTempCelsius: 22.0,
            RadiatorFreezeThresholdTempCelsius: 20.0,
            RadiatorPanelAreaM2: 35.0,
            DeepSpaceSinkTempKelvin: 70.0,
            RadiatorReturnTempRateCelsiusPerMin: -0.5,
            ElectricalLoadDemandKilowatts: 10.0);

        var result = _engine.AnalyzeSurfacePowerCycle(telemetry);

        Assert.Equal("RADIATOR_FREEZE_OUT_IMMINENT", result.ThermalSystemOperatingState);
        Assert.Equal("RETRACT_RADIATOR_PANEL_AND_ENGAGE_ELECTRIC_HEAT_TRACING", result.AutonomousControlRecommendation);
        Assert.False(result.IsPowerAdequate);
    }

    [Fact]
    public void AnalyzeSurfacePowerCycle_HighReturnCoolingRate_TriggersFreezeWarning()
    {
        var telemetry = new KilopowerTelemetry(
            ReactorId: "KILOPOWER-KRUSTY-LUNAR-03",
            CoreSodiumHeatPipeTempCelsius: 700.0,
            BraytonTurbineInletTempCelsius: 670.0,
            BraytonCompressorInletTempCelsius: 115.0,
            WorkingFluidMassFlowKgPerSec: 0.42,
            RadiatorReturnFluidTempCelsius: 38.0,
            RadiatorFreezeThresholdTempCelsius: 20.0,
            RadiatorPanelAreaM2: 35.0,
            DeepSpaceSinkTempKelvin: 75.0,
            RadiatorReturnTempRateCelsiusPerMin: -2.8, // < -2.0 C/min
            ElectricalLoadDemandKilowatts: 10.0);

        var result = _engine.AnalyzeSurfacePowerCycle(telemetry);

        Assert.Equal("RADIATOR_FREEZE_OUT_IMMINENT", result.ThermalSystemOperatingState);
        Assert.False(result.IsPowerAdequate);
    }

    [Fact]
    public void AnalyzeSurfacePowerCycle_CoreOverheating_RampsUpBraytonFlow()
    {
        var telemetry = new KilopowerTelemetry(
            ReactorId: "KILOPOWER-KRUSTY-LUNAR-04",
            CoreSodiumHeatPipeTempCelsius: 795.0, // Exceeds 780.0 C
            BraytonTurbineInletTempCelsius: 750.0,
            BraytonCompressorInletTempCelsius: 130.0,
            WorkingFluidMassFlowKgPerSec: 0.50,
            RadiatorReturnFluidTempCelsius: 52.0,
            RadiatorFreezeThresholdTempCelsius: 20.0,
            RadiatorPanelAreaM2: 35.0,
            DeepSpaceSinkTempKelvin: 90.0,
            RadiatorReturnTempRateCelsiusPerMin: 0.2,
            ElectricalLoadDemandKilowatts: 10.0);

        var result = _engine.AnalyzeSurfacePowerCycle(telemetry);

        Assert.Equal("REACTOR_CORE_OVERHEAT_WARNING", result.ThermalSystemOperatingState);
        Assert.Equal("RAMP_UP_BRAYTON_COMPRESSOR_SPEED_TO_MAXIMUM_FLOW", result.AutonomousControlRecommendation);
        Assert.True(result.IsPowerAdequate);
    }
}
