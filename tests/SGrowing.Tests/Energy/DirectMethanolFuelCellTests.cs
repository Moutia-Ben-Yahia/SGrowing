namespace SGrowing.Tests.Energy;

using System;
using FluentAssertions;
using SGrowing.Api.Modules.Energy.DirectMethanolFuelCell;
using Xunit;

public class DirectMethanolFuelCellTests
{
    private readonly DirectMethanolFuelCellEngine _engine = new();

    [Fact]
    public void AnalyzeFuelCell_NominalOperatingState_ReturnsOptimalRegime()
    {
        var telemetry = new DmfcTelemetry(
            CellStackId: "DMFC-STACK-01",
            AnodeMethanolConcentrationMolarity: 1.5,
            OperatingTemperatureCelsius: 70.0,
            OperatingCurrentDensityMilliAmpsPerCm2: 120.0,
            MembraneThicknessMicrometers: 125.0, // Nafion 115
            CathodeAirStoichiometricRatio: 3.0,
            OpenCircuitVoltageVolts: 0.82,
            TotalCellActiveAreaCm2: 50.0
        );

        var result = _engine.AnalyzeFuelCell(telemetry);

        result.CellStackId.Should().Be("DMFC-STACK-01");
        result.DmfcOperatingRegime.Should().Be("OPTIMAL_DIRECT_METHANOL_ELECTROCHEMICAL_REGIME");
        result.IsFuelCellPerformanceNominal.Should().BeTrue();
        result.MethanolCrossoverFluxMillimolesPerM2Sec.Should().BeInRange(40.0, 70.0);
        result.MeasuredOperatingCellVoltageVolts.Should().BeGreaterThan(0.40);
        result.ElectricPowerDensityMilliWattsPerCm2.Should().BeGreaterThan(45.0);
        result.TotalStackElectricPowerWatts.Should().BeGreaterThan(2.0);
        result.FuelCellControlGuidance.Should().ContainEquivalentOf("High fuel utilization confirmed");
    }

    [Fact]
    public void AnalyzeFuelCell_HighConcentration_ReturnsSevereCrossoverPoisoning()
    {
        var telemetry = new DmfcTelemetry(
            CellStackId: "DMFC-STACK-02",
            AnodeMethanolConcentrationMolarity: 3.8, // Excessive molarity > 3.0 M
            OperatingTemperatureCelsius: 75.0,
            OperatingCurrentDensityMilliAmpsPerCm2: 100.0,
            MembraneThicknessMicrometers: 80.0, // Thin membrane -> huge crossover
            CathodeAirStoichiometricRatio: 3.0,
            OpenCircuitVoltageVolts: 0.78,
            TotalCellActiveAreaCm2: 50.0
        );

        var result = _engine.AnalyzeFuelCell(telemetry);

        result.DmfcOperatingRegime.Should().Be("SEVERE_METHANOL_CROSSOVER_CATALYST_POISONING");
        result.IsFuelCellPerformanceNominal.Should().BeFalse();
        result.MethanolCrossoverFluxMillimolesPerM2Sec.Should().BeGreaterThan(110.0);
        result.FuelCellControlGuidance.Should().ContainEquivalentOf("Dilute anode feed");
    }

    [Fact]
    public void AnalyzeFuelCell_ColdCell_ReturnsSubOptimalTemperatureActivation()
    {
        var telemetry = new DmfcTelemetry(
            CellStackId: "DMFC-STACK-03",
            AnodeMethanolConcentrationMolarity: 1.2,
            OperatingTemperatureCelsius: 32.0, // Cold cell < 45 C
            OperatingCurrentDensityMilliAmpsPerCm2: 80.0,
            MembraneThicknessMicrometers: 125.0,
            CathodeAirStoichiometricRatio: 2.5,
            OpenCircuitVoltageVolts: 0.75,
            TotalCellActiveAreaCm2: 50.0
        );

        var result = _engine.AnalyzeFuelCell(telemetry);

        result.DmfcOperatingRegime.Should().Be("SUB_OPTIMAL_TEMPERATURE_ACTIVATION_LOSS");
        result.IsFuelCellPerformanceNominal.Should().BeFalse();
        result.FuelCellControlGuidance.Should().ContainEquivalentOf("coolant loop preheater");
    }

    [Fact]
    public void AnalyzeFuelCell_LowCurrentDensity_ReturnsLowPowerDensityWarning()
    {
        var telemetry = new DmfcTelemetry(
            CellStackId: "DMFC-STACK-04",
            AnodeMethanolConcentrationMolarity: 1.0,
            OperatingTemperatureCelsius: 60.0,
            OperatingCurrentDensityMilliAmpsPerCm2: 30.0, // Very low current density -> Power < 25 mW/cm2
            MembraneThicknessMicrometers: 175.0,
            CathodeAirStoichiometricRatio: 2.0,
            OpenCircuitVoltageVolts: 0.65,
            TotalCellActiveAreaCm2: 50.0
        );

        var result = _engine.AnalyzeFuelCell(telemetry);

        result.DmfcOperatingRegime.Should().Be("LOW_ELECTRIC_POWER_DENSITY_WARNING");
        result.IsFuelCellPerformanceNominal.Should().BeFalse();
        result.ElectricPowerDensityMilliWattsPerCm2.Should().BeLessThan(25.0);
    }

    [Fact]
    public void AnalyzeFuelCell_NullTelemetry_ThrowsArgumentNullException()
    {
        var act = () => _engine.AnalyzeFuelCell(null!);
        act.Should().Throw<ArgumentNullException>();
    }
}
