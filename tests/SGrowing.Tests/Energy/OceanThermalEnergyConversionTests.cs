namespace SGrowing.Tests.Energy;

using System;
using FluentAssertions;
using SGrowing.Api.Modules.Energy.OceanThermalEnergyConversion;
using Xunit;

public class OceanThermalEnergyConversionTests
{
    private readonly OceanThermalEnergyConversionEngine _engine = new();

    [Fact]
    public void AnalyzeOtecPlant_NominalTropicalSeawater_ReturnsOptimalBaseload()
    {
        var telemetry = new OtecTelemetry(
            PlantId: "OTEC-HAWAII-01",
            SurfaceSeawaterTemperatureCelsius: 26.5,
            DeepSeawaterTemperatureCelsius: 4.5, // DeltaT = 22.0 C
            WarmSeawaterVolumetricFlowRateM3PerSec: 32.0,
            ColdSeawaterVolumetricFlowRateM3PerSec: 18.0,
            DeepSeawaterPipeLengthMeters: 1000.0,
            DeepSeawaterPipeDiameterMeters: 2.8,
            WorkingFluidAmmoniaMassFlowRateKgPerSec: 240.0,
            TurbineGeneratorIsentropicEfficiencyPercent: 86.0,
            SeawaterPumpingStationMechanicalEfficiencyPercent: 80.0
        );

        var result = _engine.AnalyzeOtecPlant(telemetry);

        result.PlantId.Should().Be("OTEC-HAWAII-01");
        result.OtecOperatingRegime.Should().Be("OPTIMAL_BASELOAD_OCEAN_THERMAL_POWER_REGIME");
        result.IsOtecNetPositive.Should().BeTrue();
        result.OceanTemperatureDifferentialDeltaTCelsius.Should().Be(22.0);
        result.GrossTurbineElectricPowerMegaWatts.Should().BeGreaterThan(8.0);
        result.ParasiticSeawaterPumpingPowerMegaWatts.Should().BeLessThan(result.GrossTurbineElectricPowerMegaWatts);
        result.NetExportElectricPowerMegaWatts.Should().BeGreaterThan(5.0);
        result.PlantThermodynamicOptimizationGuidance.Should().ContainEquivalentOf("Continuous baseload 24/7 marine solar thermal");
    }

    [Fact]
    public void AnalyzeOtecPlant_LowThermalGradient_ReturnsShutdownRegime()
    {
        var telemetry = new OtecTelemetry(
            PlantId: "OTEC-SUBTROPICAL-02",
            SurfaceSeawaterTemperatureCelsius: 21.0,
            DeepSeawaterTemperatureCelsius: 6.0, // DeltaT = 15.0 C < 18 C
            WarmSeawaterVolumetricFlowRateM3PerSec: 30.0,
            ColdSeawaterVolumetricFlowRateM3PerSec: 18.0,
            DeepSeawaterPipeLengthMeters: 1000.0,
            DeepSeawaterPipeDiameterMeters: 2.8,
            WorkingFluidAmmoniaMassFlowRateKgPerSec: 200.0,
            TurbineGeneratorIsentropicEfficiencyPercent: 85.0,
            SeawaterPumpingStationMechanicalEfficiencyPercent: 80.0
        );

        var result = _engine.AnalyzeOtecPlant(telemetry);

        result.OtecOperatingRegime.Should().Be("INSUFFICIENT_THERMAL_GRADIENT_SHUTDOWN");
        result.IsOtecNetPositive.Should().BeFalse();
        result.PlantThermodynamicOptimizationGuidance.Should().ContainEquivalentOf("Carnot potential insufficient");
    }

    [Fact]
    public void AnalyzeOtecPlant_NarrowPipeHighFriction_ReturnsParasiticLossDeficit()
    {
        var telemetry = new OtecTelemetry(
            PlantId: "OTEC-DEEP-03",
            SurfaceSeawaterTemperatureCelsius: 26.0,
            DeepSeawaterTemperatureCelsius: 5.0, // DeltaT = 21.0 C
            WarmSeawaterVolumetricFlowRateM3PerSec: 10.0,
            ColdSeawaterVolumetricFlowRateM3PerSec: 35.0, // Huge flow rate
            DeepSeawaterPipeLengthMeters: 1200.0,
            DeepSeawaterPipeDiameterMeters: 1.2, // Narrow pipe -> massive friction head
            WorkingFluidAmmoniaMassFlowRateKgPerSec: 100.0,
            TurbineGeneratorIsentropicEfficiencyPercent: 80.0,
            SeawaterPumpingStationMechanicalEfficiencyPercent: 60.0
        );

        var result = _engine.AnalyzeOtecPlant(telemetry);

        result.OtecOperatingRegime.Should().Be("PARASITIC_PUMPING_LOSS_DEFICIT");
        result.IsOtecNetPositive.Should().BeFalse();
        result.PlantThermodynamicOptimizationGuidance.Should().ContainEquivalentOf("Negative net electricity balance");
    }

    [Fact]
    public void AnalyzeOtecPlant_LowWarmFlow_ReturnsMarginalNetPowerWarning()
    {
        var telemetry = new OtecTelemetry(
            PlantId: "OTEC-PILOT-04",
            SurfaceSeawaterTemperatureCelsius: 25.5,
            DeepSeawaterTemperatureCelsius: 5.5, // DeltaT = 20.0 C
            WarmSeawaterVolumetricFlowRateM3PerSec: 4.0, // Small flow rate -> Net power < 1.0 MW
            ColdSeawaterVolumetricFlowRateM3PerSec: 5.0,
            DeepSeawaterPipeLengthMeters: 800.0,
            DeepSeawaterPipeDiameterMeters: 1.8,
            WorkingFluidAmmoniaMassFlowRateKgPerSec: 50.0,
            TurbineGeneratorIsentropicEfficiencyPercent: 75.0,
            SeawaterPumpingStationMechanicalEfficiencyPercent: 75.0
        );

        var result = _engine.AnalyzeOtecPlant(telemetry);

        result.OtecOperatingRegime.Should().Be("MARGINAL_NET_POWER_WARNING");
        result.IsOtecNetPositive.Should().BeFalse();
        result.NetExportElectricPowerMegaWatts.Should().BeLessThan(1.0);
        result.PlantThermodynamicOptimizationGuidance.Should().ContainEquivalentOf("Clean evaporator plate heat exchangers");
    }

    [Fact]
    public void AnalyzeOtecPlant_NullTelemetry_ThrowsArgumentNullException()
    {
        var act = () => _engine.AnalyzeOtecPlant(null!);
        act.Should().Throw<ArgumentNullException>();
    }
}
