namespace SGrowing.Tests.Aerospace;

using System;
using FluentAssertions;
using SGrowing.Api.Modules.Aerospace.AerogelTpsAblationRecession;
using Xunit;

public class AerogelTpsAblationRecessionTests
{
    private readonly AerogelTpsAblationRecessionEngine _engine = new();

    [Fact]
    public void AnalyzeTpsAblationRecession_NominalFlightEnvelope_ReturnsQuasiSteadyAblation()
    {
        var telemetry = new AerogelTpsTelemetry(
            VehicleId: "GLIDER-HYPER-01",
            FlightMachNumber: 16.5,
            DynamicPressureKiloPascals: 35.0,
            StagnationHeatFluxMegawattsPerSquareMeter: 4.8,
            AerogelLayerThicknessMillimeters: 55.0,
            AerogelBulkDensityKgPerCubicMeter: 220.0,
            HeatOfAblationMegaJoulesPerKg: 34.0,
            SubstructureBondlineTemperatureKelvin: 320.0,
            AngleOfAttackDegrees: 24.0
        );

        var result = _engine.AnalyzeTpsAblationRecession(telemetry);

        result.VehicleId.Should().Be("GLIDER-HYPER-01");
        result.AblationRegime.Should().Be("NOMINAL_QUASI_STEADY_REENTRY_ABLATION");
        result.IsTpsNominal.Should().BeTrue();
        result.SurfaceRecessionRateMillimetersPerSec.Should().BeInRange(0.4, 0.9);
        result.ProjectedBondlinePeakTemperatureKelvin.Should().BeLessThan(450.0);
        result.ThermalShockStressRatio.Should().BeLessThan(0.70);
        result.ThermalControlAction.Should().ContainEquivalentOf("Substructure integrity assured");
    }

    [Fact]
    public void AnalyzeTpsAblationRecession_SupercriticalHeatFlux_ReturnsSupercriticalAblation()
    {
        var telemetry = new AerogelTpsTelemetry(
            VehicleId: "GLIDER-HYPER-02",
            FlightMachNumber: 22.0,
            DynamicPressureKiloPascals: 70.0,
            StagnationHeatFluxMegawattsPerSquareMeter: 15.5, // Exceeds 14.0 MW/m2
            AerogelLayerThicknessMillimeters: 60.0,
            AerogelBulkDensityKgPerCubicMeter: 220.0,
            HeatOfAblationMegaJoulesPerKg: 30.0,
            SubstructureBondlineTemperatureKelvin: 340.0,
            AngleOfAttackDegrees: 30.0
        );

        var result = _engine.AnalyzeTpsAblationRecession(telemetry);

        result.AblationRegime.Should().Be("HYPERSONIC_SUPERCRITICAL_ABLATION_RECESSION");
        result.IsTpsNominal.Should().BeFalse();
        result.SurfaceRecessionRateMillimetersPerSec.Should().BeGreaterThan(1.5);
        result.ThermalControlAction.Should().ContainEquivalentOf("sublimation enthalpy limit");
    }

    [Fact]
    public void AnalyzeTpsAblationRecession_ThinAerogelHeatSoak_ReturnsBondlineThermalExcursion()
    {
        var telemetry = new AerogelTpsTelemetry(
            VehicleId: "GLIDER-HYPER-03",
            FlightMachNumber: 18.0,
            DynamicPressureKiloPascals: 30.0,
            StagnationHeatFluxMegawattsPerSquareMeter: 8.5,
            AerogelLayerThicknessMillimeters: 15.0, // Depleted TPS layer
            AerogelBulkDensityKgPerCubicMeter: 220.0,
            HeatOfAblationMegaJoulesPerKg: 32.0,
            SubstructureBondlineTemperatureKelvin: 460.0, // High bondline temp
            AngleOfAttackDegrees: 20.0
        );

        var result = _engine.AnalyzeTpsAblationRecession(telemetry);

        result.AblationRegime.Should().Be("BONDLINE_ADHESIVE_THERMAL_EXCURSION");
        result.IsTpsNominal.Should().BeFalse();
        result.ProjectedBondlinePeakTemperatureKelvin.Should().BeGreaterThan(525.0);
        result.ThermalControlAction.Should().ContainEquivalentOf("glass transition limit");
    }

    [Fact]
    public void AnalyzeTpsAblationRecession_HighDynamicPressureThermalShock_ReturnsThermalShockRegime()
    {
        var telemetry = new AerogelTpsTelemetry(
            VehicleId: "GLIDER-HYPER-04",
            FlightMachNumber: 15.0,
            DynamicPressureKiloPascals: 85.0, // High dynamic pressure
            StagnationHeatFluxMegawattsPerSquareMeter: 10.5,
            AerogelLayerThicknessMillimeters: 55.0,
            AerogelBulkDensityKgPerCubicMeter: 220.0,
            HeatOfAblationMegaJoulesPerKg: 32.0,
            SubstructureBondlineTemperatureKelvin: 310.0,
            AngleOfAttackDegrees: 38.0 // High angle of attack
        );

        var result = _engine.AnalyzeTpsAblationRecession(telemetry);

        result.AblationRegime.Should().Be("AEROGEL_MICROCRACKING_THERMAL_SHOCK");
        result.IsTpsNominal.Should().BeFalse();
        result.ThermalShockStressRatio.Should().BeGreaterThan(0.82);
        result.ThermalControlAction.Should().ContainEquivalentOf("microcracking risk");
    }

    [Fact]
    public void AnalyzeTpsAblationRecession_NullTelemetry_ThrowsArgumentNullException()
    {
        var act = () => _engine.AnalyzeTpsAblationRecession(null!);
        act.Should().Throw<ArgumentNullException>();
    }
}
