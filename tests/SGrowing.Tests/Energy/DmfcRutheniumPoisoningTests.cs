using FluentAssertions;
using SGrowing.Api.Modules.Energy.DmfcRutheniumPoisoning;
using Xunit;

namespace SGrowing.Tests.Energy;

public class DmfcRutheniumPoisoningTests
{
    private readonly DmfcPoisoningEngine _engine = new();

    [Fact]
    public void EvaluateCellHealth_NominalBifunctionalState_YieldsHighPowerDensity()
    {
        var tel = new DmfcCellTelemetry(
            StackId: "DMFC-MICROPOWER-01",
            MethanolFeedConcentrationMolar: 1.0,
            OperatingTemperatureCelsius: 60.0,
            AnodePotentialVoltsVsRhe: 0.40,
            CarbonMonoxideCoverageFraction: 0.35, // Low CO coverage
            MethanolCrossoverCurrentDensityMilliAmpsPerCm2: 45.0,
            CellOperatingVoltageVolts: 0.50,
            CurrentDensityMilliAmpsPerCm2: 140.0
        );

        var request = new DmfcEvaluationRequest(tel, new DmfcProtectionThresholds());
        var plan = _engine.EvaluateCellHealth(request);

        plan.StackId.Should().Be("DMFC-MICROPOWER-01");
        plan.AnodeCoPoisoningCriticalDetected.Should().BeFalse();
        plan.ExcessiveMethanolCrossoverDetected.Should().BeFalse();
        plan.CatalyticRegime.Should().Be("BIFUNCTIONAL_CATALYSIS_OPTIMAL");
        plan.StackElectricalPowerDensityMilliWattsPerCm2.Should().Be(70.0);
        plan.FuelUtilizationEfficiencyPercent.Should().BeGreaterThan(70.0);
        plan.RecommendedElectrochemicalDirective.Should().Contain("NOMINAL_CELL_OPERATION");
    }

    [Fact]
    public void EvaluateCellHealth_SevereCoPoisoning_RecommendsElectrochemicalStrippingPulse()
    {
        // 75% CO coverage exceeds 65% limit
        var tel = new DmfcCellTelemetry(
            StackId: "DMFC-CO-POISONED",
            CarbonMonoxideCoverageFraction: 0.75, // Poisoned!
            MethanolCrossoverCurrentDensityMilliAmpsPerCm2: 40.0,
            CellOperatingVoltageVolts: 0.35,
            CurrentDensityMilliAmpsPerCm2: 60.0
        );

        var thresh = new DmfcProtectionThresholds(MaxAllowableCoCoverageFraction: 0.65);
        var request = new DmfcEvaluationRequest(tel, thresh);
        var plan = _engine.EvaluateCellHealth(request);

        plan.AnodeCoPoisoningCriticalDetected.Should().BeTrue();
        plan.CatalyticRegime.Should().Be("ANODE_CO_POISONING_STARVATION");
        plan.RecommendedElectrochemicalDirective.Should().Contain("APPLY_STRIPPING_PULSE");
    }

    [Fact]
    public void EvaluateCellHealth_HighMethanolCrossover_CommandsConcentrationReduction()
    {
        // 135 mA/cm2 crossover exceeds 110 mA/cm2 limit
        var tel = new DmfcCellTelemetry(
            StackId: "DMFC-CROSSOVER-STORM",
            MethanolFeedConcentrationMolar: 2.0,
            CarbonMonoxideCoverageFraction: 0.40,
            MethanolCrossoverCurrentDensityMilliAmpsPerCm2: 135.0, // High crossover!
            CellOperatingVoltageVolts: 0.38,
            CurrentDensityMilliAmpsPerCm2: 100.0
        );

        var thresh = new DmfcProtectionThresholds(MaxAllowableCrossoverCurrentMilliAmpsPerCm2: 110.0);
        var request = new DmfcEvaluationRequest(tel, thresh);
        var plan = _engine.EvaluateCellHealth(request);

        plan.ExcessiveMethanolCrossoverDetected.Should().BeTrue();
        plan.CatalyticRegime.Should().Be("CATHODE_CROSSOVER_DEPOLARIZATION");
        plan.RecommendedElectrochemicalDirective.Should().Contain("DILUTE_METHANOL_FEED");
    }

    [Fact]
    public void EvaluateCellHealth_CalculatesFuelUtilizationAccurately()
    {
        var tel = new DmfcCellTelemetry("DMFC-CALC-CHECK", CurrentDensityMilliAmpsPerCm2: 100.0, MethanolCrossoverCurrentDensityMilliAmpsPerCm2: 100.0);
        var request = new DmfcEvaluationRequest(tel, new DmfcProtectionThresholds());
        var plan = _engine.EvaluateCellHealth(request);

        // 100 / (100 + 100) = 50%
        plan.FuelUtilizationEfficiencyPercent.Should().Be(50.0);
    }
}
