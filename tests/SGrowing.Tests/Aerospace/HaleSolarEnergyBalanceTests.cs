using FluentAssertions;
using SGrowing.Api.Modules.Aerospace.HaleSolarEnergyBalance;
using Xunit;

namespace SGrowing.Tests.Aerospace;

public sealed class HaleSolarEnergyBalanceTests
{
    private readonly HaleSolarEnergyBalanceEngine _engine = new();

    [Fact]
    public void AnalyzeDiurnalEnergyBalance_NominalFlight_ReturnsPerpetualStationKeeping()
    {
        var telemetry = new HaleSolarTelemetry(
            AircraftTailNumber: "HALE-ZEPHYR-01",
            FlightAltitudeKilometers: 19.5,
            SolarZenithAngleDegrees: 35.0,
            UpperWingPvAreaMetersSquared: 80.0,
            SolarArrayPowerGeneratedKilowatts: 6.5,
            PropulsionAndAvionicsPowerDemandKilowatts: 2.5,
            BatteryPackStateOfChargePercent: 75.0,
            BatteryCapacityKilowattHours: 60.0,
            NightDurationHours: 10.0,
            GravitationalGlidePotentialBufferingActive: true
        );

        var result = _engine.AnalyzeDiurnalEnergyBalance(telemetry);

        result.DiurnalEnergyRegime.Should().Be("DIURNAL_PERPETUAL_STATION_KEEPING");
        result.IsPerpetualFlightSustained.Should().BeTrue();
        result.ProjectedMidnightMinimumSocPercent.Should().BeGreaterThan(18.0);
        result.GravitationalPotentialEnergyStoredKWh.Should().BeGreaterThan(0.0);
    }

    [Fact]
    public void AnalyzeDiurnalEnergyBalance_LowChargeLongNight_ReturnsBatteryExhaustionWarning()
    {
        var telemetry = new HaleSolarTelemetry(
            AircraftTailNumber: "HALE-DRAIN-02",
            FlightAltitudeKilometers: 18.2,
            SolarZenithAngleDegrees: 65.0,
            UpperWingPvAreaMetersSquared: 75.0,
            SolarArrayPowerGeneratedKilowatts: 2.0,
            PropulsionAndAvionicsPowerDemandKilowatts: 3.5,
            BatteryPackStateOfChargePercent: 40.0,
            BatteryCapacityKilowattHours: 40.0,
            NightDurationHours: 12.0,
            GravitationalGlidePotentialBufferingActive: false
        );

        var result = _engine.AnalyzeDiurnalEnergyBalance(telemetry);

        result.DiurnalEnergyRegime.Should().Be("NIGHT_BATTERY_EXHAUSTION_WARNING");
        result.IsPerpetualFlightSustained.Should().BeFalse();
        result.ProjectedMidnightMinimumSocPercent.Should().BeLessThan(18.0);
    }

    [Fact]
    public void AnalyzeDiurnalEnergyBalance_HighJetstreamDrag_ReturnsJetstreamCritical()
    {
        var telemetry = new HaleSolarTelemetry(
            AircraftTailNumber: "HALE-SHEAR-03",
            FlightAltitudeKilometers: 18.5,
            SolarZenithAngleDegrees: 40.0,
            UpperWingPvAreaMetersSquared: 80.0,
            SolarArrayPowerGeneratedKilowatts: 8.0,
            PropulsionAndAvionicsPowerDemandKilowatts: 14.5, // High demand > 12 kW
            BatteryPackStateOfChargePercent: 80.0,
            BatteryCapacityKilowattHours: 70.0,
            NightDurationHours: 9.0,
            GravitationalGlidePotentialBufferingActive: true
        );

        var result = _engine.AnalyzeDiurnalEnergyBalance(telemetry);

        result.DiurnalEnergyRegime.Should().Be("STRATOSPHERIC_JETSTREAM_DRAG_CRITICAL");
        result.IsPerpetualFlightSustained.Should().BeFalse();
        result.FlightProfileEnergyCommand.Should().Contain("shear winds");
    }

    [Fact]
    public void AnalyzeDiurnalEnergyBalance_SolarNoonExcessPower_ReturnsPotentialClimbCharging()
    {
        var telemetry = new HaleSolarTelemetry(
            AircraftTailNumber: "HALE-CLIMB-04",
            FlightAltitudeKilometers: 19.8,
            SolarZenithAngleDegrees: 15.0, // Near zenith
            UpperWingPvAreaMetersSquared: 85.0,
            SolarArrayPowerGeneratedKilowatts: 16.0, // High solar power
            PropulsionAndAvionicsPowerDemandKilowatts: 3.0,
            BatteryPackStateOfChargePercent: 92.0, // > 80%
            BatteryCapacityKilowattHours: 65.0,
            NightDurationHours: 9.5,
            GravitationalGlidePotentialBufferingActive: true
        );

        var result = _engine.AnalyzeDiurnalEnergyBalance(telemetry);

        result.DiurnalEnergyRegime.Should().Be("SOLAR_PEAK_POTENTIAL_CLIMB_CHARGING");
        result.IsPerpetualFlightSustained.Should().BeTrue();
        result.NetPowerBalanceKilowatts.Should().BeGreaterThan(6.0);
    }
}
