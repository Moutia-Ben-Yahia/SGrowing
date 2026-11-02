using System;
using SGrowing.Api.Modules.Aerospace.LunarRegolithMicrowaveSintering;
using Xunit;

namespace SGrowing.Tests.Aerospace;

public sealed class LunarRegolithMicrowaveSinteringTests
{
    private readonly LunarRegolithMicrowaveSinteringEngine _engine = new();

    [Fact]
    public void AnalyzeSinteringProcess_HighTemperatureLongSoak_AttainsStructuralGrade()
    {
        var telemetry = new LunarRegolithSinteringTelemetry(
            BatchBrickId: "ARTEMIS-BRICK-MARE-401",
            MicrowaveFrequencyGhz: 2.45,
            MagnetronIncidentPowerWatts: 6000.0,
            CavityElectricFieldVPerM: 35000.0,
            RegolithCoreTemperatureCelsius: 1150.0,
            HeatingRateCelsiusPerMin: 28.0,
            RelativeDielectricPermittivity: 3.2,
            LossTangent: 0.12,
            InitialBulkDensityGPerCm3: 1.50,
            SinteringDurationMinutes: 50.0);

        var result = _engine.AnalyzeSinteringProcess(telemetry);

        Assert.Equal("SINTERING_OPTIMAL_STRUCTURAL_GRADE", result.SinteringQualityRegime);
        Assert.Equal("MAINTAIN_COOLING_CYCLE_FOR_RADIATION_SHIELDING", result.ThermalControlRecommendation);
        Assert.True(result.IsStructuralGrade);
        Assert.True(result.CompressiveStrengthMpa >= 25.0);
        Assert.True(result.FinalSinteredDensityGPerCm3 >= 2.80);
    }

    [Fact]
    public void AnalyzeSinteringProcess_ExcessiveHeatingRate_TriggersThermalRunawayAlert()
    {
        var telemetry = new LunarRegolithSinteringTelemetry(
            BatchBrickId: "ARTEMIS-BRICK-MARE-402",
            MicrowaveFrequencyGhz: 2.45,
            MagnetronIncidentPowerWatts: 8000.0,
            CavityElectricFieldVPerM: 45000.0,
            RegolithCoreTemperatureCelsius: 980.0,
            HeatingRateCelsiusPerMin: 135.0, // Exceeds 120.0 C/min
            RelativeDielectricPermittivity: 3.5,
            LossTangent: 0.15,
            InitialBulkDensityGPerCm3: 1.50,
            SinteringDurationMinutes: 15.0);

        var result = _engine.AnalyzeSinteringProcess(telemetry);

        Assert.Equal("THERMAL_RUNAWAY_WARNING", result.SinteringQualityRegime);
        Assert.Equal("PULSE_MODULATE_MICROWAVE_MAGNETRON_DUTY_CYCLE_TO_40_PERCENT", result.ThermalControlRecommendation);
        Assert.False(result.IsStructuralGrade);
    }

    [Fact]
    public void AnalyzeSinteringProcess_OverheatingAboveBasaltMelting_TriggersRunawayWarning()
    {
        var telemetry = new LunarRegolithSinteringTelemetry(
            BatchBrickId: "ARTEMIS-BRICK-MARE-403",
            MicrowaveFrequencyGhz: 2.45,
            MagnetronIncidentPowerWatts: 7000.0,
            CavityElectricFieldVPerM: 40000.0,
            RegolithCoreTemperatureCelsius: 1285.0, // Exceeds 1250 C
            HeatingRateCelsiusPerMin: 40.0,
            RelativeDielectricPermittivity: 3.8,
            LossTangent: 0.18,
            InitialBulkDensityGPerCm3: 1.50,
            SinteringDurationMinutes: 40.0);

        var result = _engine.AnalyzeSinteringProcess(telemetry);

        Assert.Equal("THERMAL_RUNAWAY_WARNING", result.SinteringQualityRegime);
        Assert.Equal("PULSE_MODULATE_MICROWAVE_MAGNETRON_DUTY_CYCLE_TO_40_PERCENT", result.ThermalControlRecommendation);
        Assert.False(result.IsStructuralGrade);
    }

    [Fact]
    public void AnalyzeSinteringProcess_LowTemperatureShortDuration_FlagsUnderSintered()
    {
        var telemetry = new LunarRegolithSinteringTelemetry(
            BatchBrickId: "ARTEMIS-BRICK-MARE-404",
            MicrowaveFrequencyGhz: 2.45,
            MagnetronIncidentPowerWatts: 3000.0,
            CavityElectricFieldVPerM: 20000.0,
            RegolithCoreTemperatureCelsius: 600.0,
            HeatingRateCelsiusPerMin: 15.0,
            RelativeDielectricPermittivity: 2.8,
            LossTangent: 0.04,
            InitialBulkDensityGPerCm3: 1.50,
            SinteringDurationMinutes: 10.0);

        var result = _engine.AnalyzeSinteringProcess(telemetry);

        Assert.Equal("UNDER_SINTERED_LOW_STRENGTH", result.SinteringQualityRegime);
        Assert.Equal("EXTEND_MICROWAVE_SOAK_TIME_AND_INCREASE_POWER", result.ThermalControlRecommendation);
        Assert.False(result.IsStructuralGrade);
        Assert.True(result.CompressiveStrengthMpa < 25.0);
    }
}
