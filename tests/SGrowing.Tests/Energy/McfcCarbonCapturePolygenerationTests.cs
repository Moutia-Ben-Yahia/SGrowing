using FluentAssertions;
using SGrowing.Api.Modules.Energy.McfcCarbonCapturePolygeneration;
using Xunit;

namespace SGrowing.Tests.Energy;

public sealed class McfcCarbonCapturePolygenerationTests
{
    private readonly McfcCarbonCapturePolygenerationEngine _engine = new();

    [Fact]
    public void AnalyzeCarbonCaptureCell_NominalConditions_ReturnsElectrochemicalCaptureOptimal()
    {
        var telemetry = new McfcCaptureTelemetry(
            PlantStackId: "MCFC-TURBINE-EXHAUST-01",
            CellOperatingTemperatureCelsius: 650.0,
            FlueGasInletCo2MoleFraction: 0.09,
            FlueGasInletFlowRateNm3PerHour: 1500.0,
            CurrentDensityMilliampsPerCm2: 140.0,
            MeasuredCellVoltageVolts: 0.76,
            AnodeCo2PurityPercent: 82.0,
            ElectrolyteMatrixCorrosionRateMicronsPerYear: 4.5,
            AnodeTailGasCatalyticOxidizerActive: true
        );

        var result = _engine.AnalyzeCarbonCaptureCell(telemetry);

        result.PolygenerationRegime.Should().Be("ELECTROCHEMICAL_CAPTURE_OPTIMAL");
        result.IsStackElectrochemicalSound.Should().BeTrue();
        result.CarbonCaptureRatePercent.Should().BeGreaterThan(75.0);
        result.NetElectricPowerOutputMegawatts.Should().BeGreaterThan(50.0);
        result.ElectrochemicalCo2FluxMolesPerSecond.Should().BeGreaterThan(1.0);
    }

    [Fact]
    public void AnalyzeCarbonCaptureCell_OverheatingHighCorrosion_ReturnsCorrosionHazard()
    {
        var telemetry = new McfcCaptureTelemetry(
            PlantStackId: "MCFC-CORRODED-02",
            CellOperatingTemperatureCelsius: 705.0, // High temperature > 685C
            FlueGasInletCo2MoleFraction: 0.08,
            FlueGasInletFlowRateNm3PerHour: 1800.0,
            CurrentDensityMilliampsPerCm2: 120.0,
            MeasuredCellVoltageVolts: 0.65,
            AnodeCo2PurityPercent: 78.0,
            ElectrolyteMatrixCorrosionRateMicronsPerYear: 16.5, // High corrosion > 12 um/yr
            AnodeTailGasCatalyticOxidizerActive: true
        );

        var result = _engine.AnalyzeCarbonCaptureCell(telemetry);

        result.PolygenerationRegime.Should().Be("ELECTROLYTE_MATRIX_CORROSION_HAZARD");
        result.IsStackElectrochemicalSound.Should().BeFalse();
        result.StackBalancingAction.Should().ContainEquivalentOf("molten carbonate matrix dissolution");
    }

    [Fact]
    public void AnalyzeCarbonCaptureCell_LowAnodeCo2Purity_ReturnsDilutionUnacceptable()
    {
        var telemetry = new McfcCaptureTelemetry(
            PlantStackId: "MCFC-SLIPPAGE-03",
            CellOperatingTemperatureCelsius: 650.0,
            FlueGasInletCo2MoleFraction: 0.08,
            FlueGasInletFlowRateNm3PerHour: 1600.0,
            CurrentDensityMilliampsPerCm2: 130.0,
            MeasuredCellVoltageVolts: 0.72,
            AnodeCo2PurityPercent: 62.0, // Low purity < 70%
            ElectrolyteMatrixCorrosionRateMicronsPerYear: 5.0,
            AnodeTailGasCatalyticOxidizerActive: false
        );

        var result = _engine.AnalyzeCarbonCaptureCell(telemetry);

        result.PolygenerationRegime.Should().Be("ANODE_CO2_DILUTION_UNACCEPTABLE");
        result.IsStackElectrochemicalSound.Should().BeFalse();
        result.StackBalancingAction.Should().Contain("sequestration threshold");
    }

    [Fact]
    public void AnalyzeCarbonCaptureCell_LeanFlueGas_ReturnsCathodeStarvation()
    {
        var telemetry = new McfcCaptureTelemetry(
            PlantStackId: "MCFC-LEAN-04",
            CellOperatingTemperatureCelsius: 645.0,
            FlueGasInletCo2MoleFraction: 0.022, // Lean CO2 < 0.035
            FlueGasInletFlowRateNm3PerHour: 3000.0,
            CurrentDensityMilliampsPerCm2: 80.0,
            MeasuredCellVoltageVolts: 0.68,
            AnodeCo2PurityPercent: 74.0,
            ElectrolyteMatrixCorrosionRateMicronsPerYear: 3.8,
            AnodeTailGasCatalyticOxidizerActive: true
        );

        var result = _engine.AnalyzeCarbonCaptureCell(telemetry);

        result.PolygenerationRegime.Should().Be("CATHODE_CO2_STARVATION_DEGRADED");
        result.IsStackElectrochemicalSound.Should().BeFalse();
        result.StackBalancingAction.Should().Contain("Recirculate cathode exhaust");
    }
}
