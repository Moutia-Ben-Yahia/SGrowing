using System;
using SGrowing.Api.Modules.Environmental.DacSolidAmineChemisorption;
using Xunit;

namespace SGrowing.Tests.Environmental;

public sealed class DacSolidAmineChemisorptionTests
{
    private readonly DacSolidAmineChemisorptionEngine _engine = new();

    [Fact]
    public void AnalyzeContactorCycle_NominalAdsorption_ContinuesBlowerSweep()
    {
        var telemetry = new DacContactorTelemetry(
            ContactorModuleId: "CLIMEWORKS-DAC-MODULE-01",
            AmbientCo2Ppm: 420.0,
            AmbientRelativeHumidityPercent: 45.0,
            AmbientTemperatureCelsius: 18.0,
            AirFlowRateM3PerSec: 12.0,
            AdsorbentBedMassKg: 200.0,
            CurrentCo2LoadingMmolPerG: 1.20,
            CurrentWaterLoadingMmolPerG: 1.80,
            ContactorPressureDropKpa: 0.85,
            AdsorptionCycleDurationMinutes: 45.0);

        var result = _engine.AnalyzeContactorCycle(telemetry);

        Assert.Equal("ADSORPTION_PHASE_OPTIMAL", result.ContactorOperatingState);
        Assert.Equal("CONTINUE_AMBIENT_AIR_CONTACTOR_BLOWER_SWEEP", result.CycleControlRecommendation);
        Assert.False(result.IsDesorptionRequired);
        Assert.True(result.BedSaturationFractionPercent < 85.0);
        Assert.True(result.EstimatedDesorptionEnergyGjPerTonCo2 < 5.5);
    }

    [Fact]
    public void AnalyzeContactorCycle_HighSaturation_TriggersSteamDesorption()
    {
        // Loading 2.6 mmol/g vs max cap ~ 2.85 -> saturation ~ 91.2% >= 85%
        var telemetry = new DacContactorTelemetry(
            ContactorModuleId: "CLIMEWORKS-DAC-MODULE-02",
            AmbientCo2Ppm: 420.0,
            AmbientRelativeHumidityPercent: 45.0,
            AmbientTemperatureCelsius: 20.0,
            AirFlowRateM3PerSec: 12.0,
            AdsorbentBedMassKg: 200.0,
            CurrentCo2LoadingMmolPerG: 2.60,
            CurrentWaterLoadingMmolPerG: 2.50,
            ContactorPressureDropKpa: 1.10,
            AdsorptionCycleDurationMinutes: 180.0);

        var result = _engine.AnalyzeContactorCycle(telemetry);

        Assert.Equal("DESORPTION_CYCLE_DUE", result.ContactorOperatingState);
        Assert.Equal("INITIATE_LOW_PRESSURE_STEAM_DESORPTION_SWING_AT_95_CELSIUS", result.CycleControlRecommendation);
        Assert.True(result.IsDesorptionRequired);
        Assert.True(result.BedSaturationFractionPercent >= 85.0);
    }

    [Fact]
    public void AnalyzeContactorCycle_HighFilterPressureDrop_TriggersDesorptionRegeneration()
    {
        var telemetry = new DacContactorTelemetry(
            ContactorModuleId: "CLIMEWORKS-DAC-MODULE-03",
            AmbientCo2Ppm: 420.0,
            AmbientRelativeHumidityPercent: 50.0,
            AmbientTemperatureCelsius: 22.0,
            AirFlowRateM3PerSec: 12.0,
            AdsorbentBedMassKg: 200.0,
            CurrentCo2LoadingMmolPerG: 1.50,
            CurrentWaterLoadingMmolPerG: 2.00,
            ContactorPressureDropKpa: 1.95, // Exceeds 1.8 kPa
            AdsorptionCycleDurationMinutes: 90.0);

        var result = _engine.AnalyzeContactorCycle(telemetry);

        Assert.Equal("DESORPTION_CYCLE_DUE", result.ContactorOperatingState);
        Assert.True(result.IsDesorptionRequired);
    }

    [Fact]
    public void AnalyzeContactorCycle_HighMoistureCoAdsorption_TriggersDehumidification()
    {
        // CO2 = 0.7 mmol/g, Water = 3.5 mmol/g -> ratio = 5.0 > 3.0
        // Desorption energy = 3.3 + 1.05 * 5.0 = 8.55 > 6.5 GJ/t
        var telemetry = new DacContactorTelemetry(
            ContactorModuleId: "CLIMEWORKS-DAC-MODULE-04",
            AmbientCo2Ppm: 420.0,
            AmbientRelativeHumidityPercent: 85.0,
            AmbientTemperatureCelsius: 25.0,
            AirFlowRateM3PerSec: 10.0,
            AdsorbentBedMassKg: 200.0,
            CurrentCo2LoadingMmolPerG: 0.70,
            CurrentWaterLoadingMmolPerG: 3.50,
            ContactorPressureDropKpa: 1.00,
            AdsorptionCycleDurationMinutes: 30.0);

        var result = _engine.AnalyzeContactorCycle(telemetry);

        Assert.Equal("EXCESSIVE_MOISTURE_PARASITIC_PENALTY", result.ContactorOperatingState);
        Assert.Equal("ENGAGE_PRE_DEHUMIDIFICATION_AIR_BYPASS", result.CycleControlRecommendation);
        Assert.False(result.IsDesorptionRequired);
        Assert.True(result.EstimatedDesorptionEnergyGjPerTonCo2 > 6.5);
    }
}
