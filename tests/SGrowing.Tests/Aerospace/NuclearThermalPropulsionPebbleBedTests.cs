namespace SGrowing.Tests.Aerospace;

using System;
using FluentAssertions;
using SGrowing.Api.Modules.Aerospace.NuclearThermalPropulsionPebbleBed;
using Xunit;

public class NuclearThermalPropulsionPebbleBedTests
{
    private readonly NuclearThermalPropulsionPebbleBedEngine _engine = new();

    [Fact]
    public void AnalyzeReactorPropulsion_NominalHighTemperature_ReturnsOptimalPropulsion()
    {
        var telemetry = new NuclearThermalTelemetry(
            ReactorCoreId: "NTP-PEBBLE-01",
            ReactorThermalPowerMegaWatts: 900.0,
            PeakFuelPebbleCenterlineTemperatureKelvin: 2800.0,
            FuelPebbleMeltingLimitTemperatureKelvin: 3300.0, // 500 K margin
            HydrogenPropellantMassFlowRateKgPerSec: 22.0,
            ReactorChamberPressureMegaPascals: 8.5,
            PebbleBedPoreFractionPorosity: 0.40,
            HydrogenDissociationFractionAlpha: 0.16, // Dissociation lowers M_bar
            NozzleExpansionRatioArea: 280.0
        );

        var result = _engine.AnalyzeReactorPropulsion(telemetry);

        result.ReactorCoreId.Should().Be("NTP-PEBBLE-01");
        result.ReactorThermalRegime.Should().Be("OPTIMAL_HIGH_ISP_NUCLEAR_THERMAL_PROPULSION");
        result.IsReactorOperatingSafe.Should().BeTrue();
        result.CalculatedSpecificImpulseSeconds.Should().BeGreaterThan(950.0);
        result.GeneratedThrustKiloNewtons.Should().BeGreaterThan(200.0);
        result.FuelCenterlineThermalMarginKelvin.Should().Be(500.0);
        result.MeanExhaustMolecularWeightGramsPerMole.Should().BeLessThan(1.80);
        result.PropulsionReactorGuidance.Should().ContainEquivalentOf("ultra-high specific impulse");
    }

    [Fact]
    public void AnalyzeReactorPropulsion_LowThermalMargin_ReturnsMeltdownHazard()
    {
        var telemetry = new NuclearThermalTelemetry(
            ReactorCoreId: "NTP-PEBBLE-02",
            ReactorThermalPowerMegaWatts: 1400.0,
            PeakFuelPebbleCenterlineTemperatureKelvin: 3180.0, // Melting limit 3300 -> Margin = 120 K < 200 K
            FuelPebbleMeltingLimitTemperatureKelvin: 3300.0,
            HydrogenPropellantMassFlowRateKgPerSec: 20.0,
            ReactorChamberPressureMegaPascals: 9.0,
            PebbleBedPoreFractionPorosity: 0.40,
            HydrogenDissociationFractionAlpha: 0.20,
            NozzleExpansionRatioArea: 280.0
        );

        var result = _engine.AnalyzeReactorPropulsion(telemetry);

        result.ReactorThermalRegime.Should().Be("REACTOR_CORE_MELTDOWN_CRITICAL_HAZARD");
        result.IsReactorOperatingSafe.Should().BeFalse();
        result.FuelCenterlineThermalMarginKelvin.Should().BeLessThan(200.0);
        result.PropulsionReactorGuidance.Should().ContainEquivalentOf("Scram reactor safety rods");
    }

    [Fact]
    public void AnalyzeReactorPropulsion_HighPressureDrop_ReturnsChokingWarning()
    {
        var telemetry = new NuclearThermalTelemetry(
            ReactorCoreId: "NTP-PEBBLE-03",
            ReactorThermalPowerMegaWatts: 1000.0,
            PeakFuelPebbleCenterlineTemperatureKelvin: 2750.0,
            FuelPebbleMeltingLimitTemperatureKelvin: 3300.0,
            HydrogenPropellantMassFlowRateKgPerSec: 65.0, // High mass flow -> huge DeltaP > 1.8 MPa
            ReactorChamberPressureMegaPascals: 10.0,
            PebbleBedPoreFractionPorosity: 0.30, // Compact bed
            HydrogenDissociationFractionAlpha: 0.12,
            NozzleExpansionRatioArea: 280.0
        );

        var result = _engine.AnalyzeReactorPropulsion(telemetry);

        result.ReactorThermalRegime.Should().Be("PEBBLE_BED_EXCESSIVE_PRESSURE_DROP_CHOKING");
        result.IsReactorOperatingSafe.Should().BeFalse();
        result.PebbleBedPressureDropMegaPascals.Should().BeGreaterThan(1.8);
        result.PropulsionReactorGuidance.Should().ContainEquivalentOf("Turbopump discharge limit breached");
    }

    [Fact]
    public void AnalyzeReactorPropulsion_LowCoreTemperature_ReturnsThermalDeficit()
    {
        var telemetry = new NuclearThermalTelemetry(
            ReactorCoreId: "NTP-PEBBLE-04",
            ReactorThermalPowerMegaWatts: 250.0,
            PeakFuelPebbleCenterlineTemperatureKelvin: 1600.0, // Cold core -> Isp < 850 s
            FuelPebbleMeltingLimitTemperatureKelvin: 3300.0,
            HydrogenPropellantMassFlowRateKgPerSec: 15.0,
            ReactorChamberPressureMegaPascals: 5.0,
            PebbleBedPoreFractionPorosity: 0.40,
            HydrogenDissociationFractionAlpha: 0.01,
            NozzleExpansionRatioArea: 250.0
        );

        var result = _engine.AnalyzeReactorPropulsion(telemetry);

        result.ReactorThermalRegime.Should().Be("LOW_SPECIFIC_IMPULSE_THERMAL_DEFICIT");
        result.IsReactorOperatingSafe.Should().BeFalse();
        result.CalculatedSpecificImpulseSeconds.Should().BeLessThan(850.0);
        result.PropulsionReactorGuidance.Should().ContainEquivalentOf("Ramp reactor thermal power");
    }

    [Fact]
    public void AnalyzeReactorPropulsion_NullTelemetry_ThrowsArgumentNullException()
    {
        var act = () => _engine.AnalyzeReactorPropulsion(null!);
        act.Should().Throw<ArgumentNullException>();
    }
}
