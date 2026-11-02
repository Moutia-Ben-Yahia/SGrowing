namespace SGrowing.Tests.Aerospace;

using System;
using FluentAssertions;
using SGrowing.Api.Modules.Aerospace.MiniMagnetosphericPlasmaPropulsion;
using Xunit;

public class MiniMagnetosphericPlasmaPropulsionTests
{
    private readonly MiniMagnetosphericPlasmaPropulsionEngine _engine = new();

    [Fact]
    public void AnalyzePlasmaMagnetosphere_NominalSolarWind_ReturnsNominalPropulsion()
    {
        var telemetry = new M2p2Telemetry(
            SpacecraftId: "M2P2-PROBE-01",
            SolarWindSpeedKmPerSec: 460.0,
            SolarWindProtonDensityPerCubicCm: 5.5,
            HeliconPlasmaRfPowerKilowatts: 3.5,
            HeliconPlasmaFeedRateMilligramsPerSec: 1.5,
            SuperconductingDipoleFieldTesla: 2.2,
            SuperconductingCoilTemperatureKelvin: 38.0,
            MagnetosphereRadiusKilometers: 16.0,
            HeliocentricDistanceAstronomicalUnits: 1.2
        );

        var result = _engine.AnalyzePlasmaMagnetosphere(telemetry);

        result.SpacecraftId.Should().Be("M2P2-PROBE-01");
        result.PropulsionAndShieldingRegime.Should().Be("NOMINAL_MAGNETOSPHERIC_PROPULSION_AND_SHIELDING");
        result.IsSystemOperational.Should().BeTrue();
        result.SolarWindDynamicPressureNanoPascals.Should().BeInRange(0.5, 2.0);
        result.NetThrustNewtons.Should().BeInRange(1.0, 4.0);
        result.RadiationDoseAttenuationPercent.Should().BeGreaterThan(75.0);
        result.MagnetosphereControlAction.Should().ContainEquivalentOf("stably inflated");
    }

    [Fact]
    public void AnalyzePlasmaMagnetosphere_SuperconductingThermalRunaway_ReturnsQuenchHazard()
    {
        var telemetry = new M2p2Telemetry(
            SpacecraftId: "M2P2-PROBE-02",
            SolarWindSpeedKmPerSec: 450.0,
            SolarWindProtonDensityPerCubicCm: 5.0,
            HeliconPlasmaRfPowerKilowatts: 3.0,
            HeliconPlasmaFeedRateMilligramsPerSec: 1.2,
            SuperconductingDipoleFieldTesla: 2.0,
            SuperconductingCoilTemperatureKelvin: 92.0, // Exceeds 85 K quench boundary
            MagnetosphereRadiusKilometers: 15.0,
            HeliocentricDistanceAstronomicalUnits: 1.0
        );

        var result = _engine.AnalyzePlasmaMagnetosphere(telemetry);

        result.PropulsionAndShieldingRegime.Should().Be("SUPERCONDUCTING_MAGNET_QUENCH_HAZARD");
        result.IsSystemOperational.Should().BeFalse();
        result.MagnetosphereControlAction.Should().ContainEquivalentOf("quench boundary");
    }

    [Fact]
    public void AnalyzePlasmaMagnetosphere_DeflatedBubble_ReturnsUnderpressurized()
    {
        var telemetry = new M2p2Telemetry(
            SpacecraftId: "M2P2-PROBE-03",
            SolarWindSpeedKmPerSec: 400.0,
            SolarWindProtonDensityPerCubicCm: 4.5,
            HeliconPlasmaRfPowerKilowatts: 0.8,
            HeliconPlasmaFeedRateMilligramsPerSec: 0.3, // Feed rate < 0.5
            SuperconductingDipoleFieldTesla: 1.5,
            SuperconductingCoilTemperatureKelvin: 40.0,
            MagnetosphereRadiusKilometers: 3.8, // Radius < 5.0 km
            HeliocentricDistanceAstronomicalUnits: 1.0
        );

        var result = _engine.AnalyzePlasmaMagnetosphere(telemetry);

        result.PropulsionAndShieldingRegime.Should().Be("PLASMA_DEFLATION_UNDERPRESSURIZED");
        result.IsSystemOperational.Should().BeFalse();
        result.MagnetosphereControlAction.Should().ContainEquivalentOf("helicon RF power");
    }

    [Fact]
    public void AnalyzePlasmaMagnetosphere_CoronalMassEjectionStorm_ReturnsSurgeRegime()
    {
        var telemetry = new M2p2Telemetry(
            SpacecraftId: "M2P2-PROBE-04",
            SolarWindSpeedKmPerSec: 850.0, // CME speed
            SolarWindProtonDensityPerCubicCm: 28.0, // CME density -> high dynamic pressure
            HeliconPlasmaRfPowerKilowatts: 4.0,
            HeliconPlasmaFeedRateMilligramsPerSec: 2.0,
            SuperconductingDipoleFieldTesla: 2.5,
            SuperconductingCoilTemperatureKelvin: 42.0,
            MagnetosphereRadiusKilometers: 14.0,
            HeliocentricDistanceAstronomicalUnits: 1.0
        );

        var result = _engine.AnalyzePlasmaMagnetosphere(telemetry);

        result.PropulsionAndShieldingRegime.Should().Be("SOLAR_STORM_COMPRESSION_DEFLECTION_SURGE");
        result.IsSystemOperational.Should().BeTrue();
        result.SolarWindDynamicPressureNanoPascals.Should().BeGreaterThan(6.0);
        result.NetThrustNewtons.Should().BeGreaterThan(8.0);
        result.MagnetosphereControlAction.Should().ContainEquivalentOf("Coronal Mass Ejection");
    }

    [Fact]
    public void AnalyzePlasmaMagnetosphere_NullTelemetry_ThrowsArgumentNullException()
    {
        var act = () => _engine.AnalyzePlasmaMagnetosphere(null!);
        act.Should().Throw<ArgumentNullException>();
    }
}
