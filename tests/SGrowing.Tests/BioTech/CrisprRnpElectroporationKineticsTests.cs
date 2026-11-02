namespace SGrowing.Tests.BioTech;

using System;
using FluentAssertions;
using SGrowing.Api.Modules.BioTech.CrisprRnpElectroporationKinetics;
using Xunit;

public class CrisprRnpElectroporationKineticsTests
{
    private readonly CrisprRnpElectroporationKineticsEngine _engine = new();

    [Fact]
    public void AnalyzeElectroporation_NominalPrimaryTCells_ReturnsOptimalRegime()
    {
        var telemetry = new CrisprRnpElectroporationTelemetry(
            BatchId: "RNP-TCELL-01",
            TargetCellType: "PrimaryHumanCD4TCells",
            ElectricPulseFieldStrengthVoltsPerCm: 1600.0,
            PulseDurationMilliseconds: 10.0,
            IntracellularRnpMolarConcentrationMicromolar: 2.5,
            CellSuspensionDensityMillionsPerMl: 5.0,
            PostPulseIncubationHours: 24.0,
            IntracellularRnpHalfLifeHours: 16.0,
            HighSequenceHomologyOffTargetSitesCount: 4.0
        );

        var result = _engine.AnalyzeElectroporation(telemetry);

        result.BatchId.Should().Be("RNP-TCELL-01");
        result.ElectroporationKineticsRegime.Should().Be("OPTIMAL_HIGH_VIABILITY_RNP_DELIVERY_REGIME");
        result.IsElectroporationSuccessful.Should().BeTrue();
        result.PredictedMembranePermeabilizationFractionPercent.Should().BeGreaterThan(60.0);
        result.PredictedPostPulseCellViabilityPercent.Should().BeGreaterThan(75.0);
        result.OnTargetEditingEfficiencyPercent.Should().BeGreaterThan(50.0);
        result.CumulativeOffTargetCleavageRatePercent.Should().BeLessThan(1.0);
        result.CleavageSpecificityRatio.Should().BeGreaterThan(50.0);
        result.DeliveryOptimizationGuidance.Should().ContainEquivalentOf("Rapid RNP clearance restricts off-target activity");
    }

    [Fact]
    public void AnalyzeElectroporation_ExcessiveVoltage_ReturnsIrreversibleLysis()
    {
        var telemetry = new CrisprRnpElectroporationTelemetry(
            BatchId: "RNP-TCELL-02",
            TargetCellType: "PrimaryHumanCD4TCells",
            ElectricPulseFieldStrengthVoltsPerCm: 2800.0, // Lethal field
            PulseDurationMilliseconds: 25.0, // Long pulse
            IntracellularRnpMolarConcentrationMicromolar: 2.0,
            CellSuspensionDensityMillionsPerMl: 5.0,
            PostPulseIncubationHours: 24.0,
            IntracellularRnpHalfLifeHours: 16.0,
            HighSequenceHomologyOffTargetSitesCount: 3.0
        );

        var result = _engine.AnalyzeElectroporation(telemetry);

        result.ElectroporationKineticsRegime.Should().Be("IRREVERSIBLE_MEMBRANE_LYSIS_HAZARD");
        result.IsElectroporationSuccessful.Should().BeFalse();
        result.PredictedPostPulseCellViabilityPercent.Should().BeLessThan(60.0);
        result.DeliveryOptimizationGuidance.Should().ContainEquivalentOf("irreversible membrane pore expansion");
    }

    [Fact]
    public void AnalyzeElectroporation_InsufficientField_ReturnsDeliveryFailure()
    {
        var telemetry = new CrisprRnpElectroporationTelemetry(
            BatchId: "RNP-TCELL-03",
            TargetCellType: "HSC_CD34Plus",
            ElectricPulseFieldStrengthVoltsPerCm: 950.0, // Below permeabilization threshold
            PulseDurationMilliseconds: 5.0,
            IntracellularRnpMolarConcentrationMicromolar: 2.0,
            CellSuspensionDensityMillionsPerMl: 5.0,
            PostPulseIncubationHours: 24.0,
            IntracellularRnpHalfLifeHours: 16.0,
            HighSequenceHomologyOffTargetSitesCount: 2.0
        );

        var result = _engine.AnalyzeElectroporation(telemetry);

        result.ElectroporationKineticsRegime.Should().Be("INSUFFICIENT_VOLTAGE_DELIVERY_FAILURE");
        result.IsElectroporationSuccessful.Should().BeFalse();
        result.PredictedMembranePermeabilizationFractionPercent.Should().BeLessThan(50.0);
        result.DeliveryOptimizationGuidance.Should().ContainEquivalentOf("Increase pulse voltage");
    }

    [Fact]
    public void AnalyzeElectroporation_HighOffTargetDensity_ReturnsElevatedOffTargetWarning()
    {
        var telemetry = new CrisprRnpElectroporationTelemetry(
            BatchId: "RNP-TCELL-04",
            TargetCellType: "PrimaryHumanCD4TCells",
            ElectricPulseFieldStrengthVoltsPerCm: 1550.0,
            PulseDurationMilliseconds: 8.0,
            IntracellularRnpMolarConcentrationMicromolar: 5.0, // High RNP concentration
            CellSuspensionDensityMillionsPerMl: 5.0,
            PostPulseIncubationHours: 24.0,
            IntracellularRnpHalfLifeHours: 36.0, // Long half-life
            HighSequenceHomologyOffTargetSitesCount: 16.0 // Many off-target loci -> cleavage > 2.5%
        );

        var result = _engine.AnalyzeElectroporation(telemetry);

        result.ElectroporationKineticsRegime.Should().Be("ELEVATED_OFF_TARGET_CLEAVAGE_WARNING");
        result.IsElectroporationSuccessful.Should().BeFalse();
        result.CumulativeOffTargetCleavageRatePercent.Should().BeGreaterThan(2.5);
        result.DeliveryOptimizationGuidance.Should().ContainEquivalentOf("high-fidelity Cas9-HF1");
    }

    [Fact]
    public void AnalyzeElectroporation_NullTelemetry_ThrowsArgumentNullException()
    {
        var act = () => _engine.AnalyzeElectroporation(null!);
        act.Should().Throw<ArgumentNullException>();
    }
}
