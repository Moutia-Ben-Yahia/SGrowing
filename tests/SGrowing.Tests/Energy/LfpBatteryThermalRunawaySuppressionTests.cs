using System;
using SGrowing.Api.Modules.Energy.LfpBatteryThermalRunawaySuppression;
using Xunit;

namespace SGrowing.Tests.Energy;

public sealed class LfpBatteryThermalRunawaySuppressionTests
{
    private readonly LfpBatteryThermalRunawaySuppressionEngine _engine = new();

    [Fact]
    public void AnalyzeBatterySafety_NullTelemetry_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => _engine.AnalyzeBatterySafety(null!));
    }

    [Fact]
    public void AnalyzeBatterySafety_NegativeGasPpm_ThrowsArgumentException()
    {
        var telemetry = new LfpBatteryThermalRunawayTelemetry(
            RackEnclosureId: "BESS-RACK-01",
            MaxCellSurfaceTemperatureCelsius: 30.0,
            TemperatureRateOfRiseCelsiusPerSecond: 0.05,
            OffGasCarbonMonoxidePpm: -5.0, // Negative!
            OffGasHydrogenPpm: 2.0,
            LowerExplosiveLimitContainerAtmospherePercent: 1.0,
            HighVoltageContactorClosed: true,
            SuppressionDelugeArmed: true
        );

        Assert.Throws<ArgumentException>(() => _engine.AnalyzeBatterySafety(telemetry));
    }

    [Fact]
    public void AnalyzeBatterySafety_NominalBess_ReturnsNominalStableRegime()
    {
        var telemetry = new LfpBatteryThermalRunawayTelemetry(
            RackEnclosureId: "BESS-RACK-NOMINAL",
            MaxCellSurfaceTemperatureCelsius: 28.5,
            TemperatureRateOfRiseCelsiusPerSecond: 0.01,
            OffGasCarbonMonoxidePpm: 1.2,
            OffGasHydrogenPpm: 0.8,
            LowerExplosiveLimitContainerAtmospherePercent: 0.5,
            HighVoltageContactorClosed: true,
            SuppressionDelugeArmed: true
        );

        var result = _engine.AnalyzeBatterySafety(telemetry);

        Assert.NotNull(result);
        Assert.Equal("BESS-RACK-NOMINAL", result.RackEnclosureId);
        Assert.True(result.IsEnclosureSafe);
        Assert.False(result.ShouldTripHvContactor);
        Assert.False(result.ShouldDeploySuppressionDeluge);
        Assert.Equal("NOMINAL_STABLE_BESS_STORAGE_REGIME", result.BessSafetyRegime);
    }

    [Fact]
    public void AnalyzeBatterySafety_EarlyOffGasVenting_ReturnsIsolationTriggered()
    {
        var telemetry = new LfpBatteryThermalRunawayTelemetry(
            RackEnclosureId: "BESS-RACK-OFFGAS",
            MaxCellSurfaceTemperatureCelsius: 75.0,
            TemperatureRateOfRiseCelsiusPerSecond: 0.35,
            OffGasCarbonMonoxidePpm: 38.0, // CO >= 25 ppm!
            OffGasHydrogenPpm: 65.0, // H2 >= 50 ppm!
            LowerExplosiveLimitContainerAtmospherePercent: 6.2,
            HighVoltageContactorClosed: true,
            SuppressionDelugeArmed: true
        );

        var result = _engine.AnalyzeBatterySafety(telemetry);

        Assert.NotNull(result);
        Assert.Equal("EARLY_OFF_GAS_VENTING_ELECTRICAL_ISOLATION_TRIGGERED", result.BessSafetyRegime);
        Assert.False(result.IsEnclosureSafe);
        Assert.True(result.ShouldTripHvContactor);
        Assert.False(result.ShouldDeploySuppressionDeluge);
        Assert.Contains("Open HV contactors", result.EmergencyMitigationGuidance);
    }

    [Fact]
    public void AnalyzeBatterySafety_CriticalThermalRunaway_ReturnsDelugeTriggered()
    {
        var telemetry = new LfpBatteryThermalRunawayTelemetry(
            RackEnclosureId: "BESS-RACK-FIRE",
            MaxCellSurfaceTemperatureCelsius: 195.0, // >= 180 C!
            TemperatureRateOfRiseCelsiusPerSecond: 2.8, // >= 1.5 C/s!
            OffGasCarbonMonoxidePpm: 250.0,
            OffGasHydrogenPpm: 400.0,
            LowerExplosiveLimitContainerAtmospherePercent: 45.0,
            HighVoltageContactorClosed: true,
            SuppressionDelugeArmed: true
        );

        var result = _engine.AnalyzeBatterySafety(telemetry);

        Assert.NotNull(result);
        Assert.Equal("IMMINENT_THERMAL_RUNAWAY_DELUGE_SUPPRESSION_TRIGGERED", result.BessSafetyRegime);
        Assert.False(result.IsEnclosureSafe);
        Assert.True(result.ShouldTripHvContactor);
        Assert.True(result.ShouldDeploySuppressionDeluge);
        Assert.Contains("discharging water/aerosol deluge", result.EmergencyMitigationGuidance);
    }
}
