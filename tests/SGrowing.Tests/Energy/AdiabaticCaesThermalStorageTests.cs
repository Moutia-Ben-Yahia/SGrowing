namespace SGrowing.Tests.Energy;

using System;
using FluentAssertions;
using SGrowing.Api.Modules.Energy.AdiabaticCaesThermalStorage;
using Xunit;

public class AdiabaticCaesThermalStorageTests
{
    private readonly AdiabaticCaesThermalStorageEngine _engine = new();

    [Fact]
    public void AnalyzeAdiabaticCaes_NominalStorageCycle_ReturnsSteadyStateCycling()
    {
        var telemetry = new AdiabaticCaesTelemetry(
            CavernId: "CAVERN-SALT-01",
            CavernPressureBar: 68.0,
            CavernPressureLithostaticRatio: 0.64,
            MinimumPermissibleCavernPressureBar: 42.0,
            MaximumPermissibleCavernPressureBar: 88.0,
            AirMassFlowRateKgPerSec: 120.0,
            ThermalStorageTemperatureCelsius: 285.0,
            CompressorPowerInputMegawatts: 45.0,
            TurbinePowerOutputMegawatts: 31.5,
            CavernDailyAirLeakagePercent: 0.12
        );

        var result = _engine.AnalyzeAdiabaticCaes(telemetry);

        result.CavernId.Should().Be("CAVERN-SALT-01");
        result.CaesOperatingRegime.Should().Be("STEADY_STATE_ADIABATIC_CAES_CYCLING");
        result.IsCaesOperational.Should().BeTrue();
        result.RoundTripExergyEfficiencyPercent.Should().BeGreaterThan(65.0);
        result.AvailableStoredExergyMegaWattHours.Should().BeGreaterThan(200.0);
        result.CavernGeomechanicalSafetyFactor.Should().BeGreaterThan(1.2);
        result.DispatchAndSafetyAction.Should().ContainEquivalentOf("optimal thermodynamic and geomechanical envelope");
    }

    [Fact]
    public void AnalyzeAdiabaticCaes_OverpressurizedCavern_ReturnsLithostaticFractureHazard()
    {
        var telemetry = new AdiabaticCaesTelemetry(
            CavernId: "CAVERN-SALT-02",
            CavernPressureBar: 94.0, // Exceeds max permissible 88 bar
            CavernPressureLithostaticRatio: 0.86, // Exceeds 0.83
            MinimumPermissibleCavernPressureBar: 42.0,
            MaximumPermissibleCavernPressureBar: 88.0,
            AirMassFlowRateKgPerSec: 80.0,
            ThermalStorageTemperatureCelsius: 290.0,
            CompressorPowerInputMegawatts: 50.0,
            TurbinePowerOutputMegawatts: 0.0,
            CavernDailyAirLeakagePercent: 0.20
        );

        var result = _engine.AnalyzeAdiabaticCaes(telemetry);

        result.CaesOperatingRegime.Should().Be("CAVERN_LITHOSTATIC_FRACTURE_HAZARD");
        result.IsCaesOperational.Should().BeFalse();
        result.DispatchAndSafetyAction.Should().ContainEquivalentOf("lithostatic fracture limit");
    }

    [Fact]
    public void AnalyzeAdiabaticCaes_DepletedCushionPressure_ReturnsCreepConvergenceRisk()
    {
        var telemetry = new AdiabaticCaesTelemetry(
            CavernId: "CAVERN-SALT-03",
            CavernPressureBar: 35.0, // Below minimum 42 bar
            CavernPressureLithostaticRatio: 0.35,
            MinimumPermissibleCavernPressureBar: 42.0,
            MaximumPermissibleCavernPressureBar: 88.0,
            AirMassFlowRateKgPerSec: 100.0,
            ThermalStorageTemperatureCelsius: 250.0,
            CompressorPowerInputMegawatts: 0.0,
            TurbinePowerOutputMegawatts: 22.0,
            CavernDailyAirLeakagePercent: 0.15
        );

        var result = _engine.AnalyzeAdiabaticCaes(telemetry);

        result.CaesOperatingRegime.Should().Be("CAVERN_CREEP_CONVERGENCE_COLLAPSE_RISK");
        result.IsCaesOperational.Should().BeFalse();
        result.DispatchAndSafetyAction.Should().ContainEquivalentOf("creep convergence");
    }

    [Fact]
    public void AnalyzeAdiabaticCaes_ColdThermalStorage_ReturnsTesExergyDeficit()
    {
        var telemetry = new AdiabaticCaesTelemetry(
            CavernId: "CAVERN-SALT-04",
            CavernPressureBar: 65.0,
            CavernPressureLithostaticRatio: 0.60,
            MinimumPermissibleCavernPressureBar: 42.0,
            MaximumPermissibleCavernPressureBar: 88.0,
            AirMassFlowRateKgPerSec: 50.0,
            ThermalStorageTemperatureCelsius: 145.0, // Cold TES < 180 C
            CompressorPowerInputMegawatts: 10.0,
            TurbinePowerOutputMegawatts: 0.0,
            CavernDailyAirLeakagePercent: 0.10
        );

        var result = _engine.AnalyzeAdiabaticCaes(telemetry);

        result.CaesOperatingRegime.Should().Be("TES_THERMAL_EXERGY_DEFICIT");
        result.IsCaesOperational.Should().BeFalse();
        result.DispatchAndSafetyAction.Should().ContainEquivalentOf("preheat threshold");
    }

    [Fact]
    public void AnalyzeAdiabaticCaes_NullTelemetry_ThrowsArgumentNullException()
    {
        var act = () => _engine.AnalyzeAdiabaticCaes(null!);
        act.Should().Throw<ArgumentNullException>();
    }
}
