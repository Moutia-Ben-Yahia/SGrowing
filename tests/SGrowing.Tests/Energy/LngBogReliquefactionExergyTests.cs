using System;
using SGrowing.Api.Modules.Energy.LngBogReliquefactionExergy;
using Xunit;

namespace SGrowing.Tests.Energy;

public sealed class LngBogReliquefactionExergyTests
{
    private readonly LngBogReliquefactionExergyEngine _engine = new();

    [Fact]
    public void AnalyzeReliquefactionEfficiency_NominalCondensation_MaintainsClosedLoopSteadyState()
    {
        var telemetry = new LngBogReliquefactionTelemetry(
            CarrierTankId: "Q-MAX-LNG-CARRIER-01",
            TankUllagePressureBar: 1.10,
            MeasuredBogGenerationRateKgPerHour: 2400.0,
            NitrogenRefrigerantMassFlowKgPerSec: 18.0,
            CompressorPowerConsumptionKilowatts: 1700.0,
            ReliquefactionColdBoxTempInletCelsius: -115.0,
            ReliquefactionSubcoolerTempOutletCelsius: -163.0,
            MaxDesignReliquefactionCapacityKgPerHour: 3000.0,
            DualFuelBoilerAvailableCapacityKw: 4500.0);

        var result = _engine.AnalyzeReliquefactionEfficiency(telemetry);

        Assert.Equal("RELIQUEFACTION_NOMINAL", result.ReliquefactionOperatingState);
        Assert.Equal("MAINTAIN_CLOSED_LOOP_NITROGEN_BRAYTON_STEADY_STATE", result.OptimalPlantControlAction);
        Assert.True(result.IsFlaringSuppressed);
        Assert.True(result.SecondLawExergyEfficiencyPercent >= 38.0);
    }

    [Fact]
    public void AnalyzeReliquefactionEfficiency_TankPressureSurge_EngagesDualFuelSupplement()
    {
        var telemetry = new LngBogReliquefactionTelemetry(
            CarrierTankId: "Q-MAX-LNG-CARRIER-02",
            TankUllagePressureBar: 1.38, // Exceeds 1.30 bar
            MeasuredBogGenerationRateKgPerHour: 2800.0,
            NitrogenRefrigerantMassFlowKgPerSec: 20.0,
            CompressorPowerConsumptionKilowatts: 2200.0,
            ReliquefactionColdBoxTempInletCelsius: -105.0,
            ReliquefactionSubcoolerTempOutletCelsius: -160.0,
            MaxDesignReliquefactionCapacityKgPerHour: 3000.0,
            DualFuelBoilerAvailableCapacityKw: 4500.0);

        var result = _engine.AnalyzeReliquefactionEfficiency(telemetry);

        Assert.Equal("EXCESS_BOG_PRESSURE_SURGE", result.ReliquefactionOperatingState);
        Assert.Equal("MAXIMIZE_COMPRESSOR_THROUGHPUT_AND_ENGAGE_DUAL_FUEL_BOILER_SUPPLEMENT", result.OptimalPlantControlAction);
        Assert.False(result.IsFlaringSuppressed);
    }

    [Fact]
    public void AnalyzeReliquefactionEfficiency_ExcessBogGenerationOverCapacity_TriggersSurgeMode()
    {
        var telemetry = new LngBogReliquefactionTelemetry(
            CarrierTankId: "Q-MAX-LNG-CARRIER-03",
            TankUllagePressureBar: 1.15,
            MeasuredBogGenerationRateKgPerHour: 3400.0, // Exceeds 3000.0 kg/hr
            NitrogenRefrigerantMassFlowKgPerSec: 22.0,
            CompressorPowerConsumptionKilowatts: 2400.0,
            ReliquefactionColdBoxTempInletCelsius: -110.0,
            ReliquefactionSubcoolerTempOutletCelsius: -162.0,
            MaxDesignReliquefactionCapacityKgPerHour: 3000.0,
            DualFuelBoilerAvailableCapacityKw: 5000.0);

        var result = _engine.AnalyzeReliquefactionEfficiency(telemetry);

        Assert.Equal("EXCESS_BOG_PRESSURE_SURGE", result.ReliquefactionOperatingState);
        Assert.False(result.IsFlaringSuppressed);
    }

    [Fact]
    public void AnalyzeReliquefactionEfficiency_DegradedExergyEfficiency_TriggersJtModulation()
    {
        // Power 1600 kW for 1500 kg/hr -> Exergy eff = 26.69% < 32.0%
        var telemetry = new LngBogReliquefactionTelemetry(
            CarrierTankId: "Q-MAX-LNG-CARRIER-04",
            TankUllagePressureBar: 1.12,
            MeasuredBogGenerationRateKgPerHour: 1500.0,
            NitrogenRefrigerantMassFlowKgPerSec: 15.0,
            CompressorPowerConsumptionKilowatts: 1600.0,
            ReliquefactionColdBoxTempInletCelsius: -95.0,
            ReliquefactionSubcoolerTempOutletCelsius: -155.0,
            MaxDesignReliquefactionCapacityKgPerHour: 3000.0,
            DualFuelBoilerAvailableCapacityKw: 4500.0);

        var result = _engine.AnalyzeReliquefactionEfficiency(telemetry);

        Assert.Equal("EXERGY_EFFICIENCY_DEGRADED", result.ReliquefactionOperatingState);
        Assert.Equal("MODULATE_JOULE_THOMSON_VALVE_EXPANSION_RATIO_AND_DEFROST_COLDBOX", result.OptimalPlantControlAction);
        Assert.True(result.IsFlaringSuppressed);
        Assert.True(result.SecondLawExergyEfficiencyPercent < 32.0);
    }
}
