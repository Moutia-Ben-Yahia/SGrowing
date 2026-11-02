using SGrowing.Api.Modules.Energy.DirectAirCaptureTvsaSorbent;
using Xunit;

namespace SGrowing.Tests.Energy;

public class DirectAirCaptureTvsaTests
{
    private readonly DirectAirCaptureTvsaEngine _engine = new();

    [Fact]
    public void AnalyzeDacTrain_NominalTvsaCycle_ReturnsDecarbonizationCertified()
    {
        var telemetry = new DirectAirCaptureTvsaTelemetry
        {
            ContactorTrainId = "DAC-TRAIN-001",
            SolidSorbentChemistry = "AmineFunctionalizedSilica_APTES",
            AmbientCo2Ppm = 422.0,
            AdsorptionCapacityMmolPerGram = 1.95,
            DesorptionResidualCapacityMmolPerGram = 0.35,
            DesorptionTemperatureCelsius = 95.0,
            DesorptionVacuumPressureBar = 0.15,
            OxygenConcentrationDuringDesorptionVolPercent = 0.15,
            ContactorBedPressureDropPascals = 125.0,
            SpecificThermalEnergyGigaJoulesPerTonCo2 = 4.85,
            SpecificElectricalEnergyGigaJoulesPerTonCo2 = 1.15,
            AmineDegradationRatePercentPerCycle = 0.035
        };

        var result = _engine.AnalyzeDacTrain(telemetry);

        Assert.NotNull(result);
        Assert.Equal("DAC-TRAIN-001", result.ContactorTrainId);
        Assert.Equal(1.60, result.WorkingCapacityMmolPerGram);
        Assert.Equal(6.0, result.TotalSpecificEnergyGigaJoulesPerTonCo2);
        Assert.True(result.IsWorkingCapacityAdequate);
        Assert.True(result.IsEnergeticPerformanceOptimal);
        Assert.True(result.IsOxidativeDegradationSuppressed);
        Assert.True(result.IsTvsaDecarbonizationCertified);
        Assert.Empty(result.ActiveRegimes);
        Assert.True(result.ProjectedSorbentCycleLifespan >= 500.0);
    }

    [Fact]
    public void AnalyzeDacTrain_HighTempOxygenExposure_TriggersOxidativeDegradationAlert()
    {
        var telemetry = new DirectAirCaptureTvsaTelemetry
        {
            ContactorTrainId = "DAC-TRAIN-002",
            DesorptionTemperatureCelsius = 115.0,
            OxygenConcentrationDuringDesorptionVolPercent = 1.8
        };

        var result = _engine.AnalyzeDacTrain(telemetry);

        Assert.False(result.IsOxidativeDegradationSuppressed);
        Assert.False(result.IsTvsaDecarbonizationCertified);
        Assert.Contains("AMINE_SORBENT_THERMAL_OXIDATIVE_DEGRADATION", result.ActiveRegimes);
    }

    [Fact]
    public void AnalyzeDacTrain_CloggedContactorBed_TriggersFanPressureDropAlert()
    {
        var telemetry = new DirectAirCaptureTvsaTelemetry
        {
            ContactorTrainId = "DAC-TRAIN-003",
            ContactorBedPressureDropPascals = 225.0
        };

        var result = _engine.AnalyzeDacTrain(telemetry);

        Assert.False(result.IsEnergeticPerformanceOptimal);
        Assert.False(result.IsTvsaDecarbonizationCertified);
        Assert.Contains("FAN_PRESSURE_DROP_PARASITIC_POWER_SPIKE", result.ActiveRegimes);
    }

    [Fact]
    public void AnalyzeDacTrain_DepletedWorkingCapacity_TriggersCapacityDepletionAlert()
    {
        var telemetry = new DirectAirCaptureTvsaTelemetry
        {
            ContactorTrainId = "DAC-TRAIN-004",
            AdsorptionCapacityMmolPerGram = 1.10,
            DesorptionResidualCapacityMmolPerGram = 0.40 // Working = 0.70 < 0.90
        };

        var result = _engine.AnalyzeDacTrain(telemetry);

        Assert.False(result.IsWorkingCapacityAdequate);
        Assert.False(result.IsTvsaDecarbonizationCertified);
        Assert.Contains("DESORPTION_WORKING_CAPACITY_DEPLETION", result.ActiveRegimes);
    }

    [Fact]
    public void AnalyzeDacTrain_ExcessiveThermalEnergy_FailsCertification()
    {
        var telemetry = new DirectAirCaptureTvsaTelemetry
        {
            ContactorTrainId = "DAC-TRAIN-005",
            SpecificThermalEnergyGigaJoulesPerTonCo2 = 6.95
        };

        var result = _engine.AnalyzeDacTrain(telemetry);

        Assert.False(result.IsEnergeticPerformanceOptimal);
        Assert.False(result.IsTvsaDecarbonizationCertified);
    }
}
