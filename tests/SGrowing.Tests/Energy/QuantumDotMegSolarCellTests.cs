using FluentAssertions;
using SGrowing.Api.Modules.Energy.QuantumDotMegSolarCell;
using Xunit;

namespace SGrowing.Tests.Energy;

public class QuantumDotMegSolarCellTests
{
    private readonly QuantumDotMegEngine _engine = new();

    [Fact]
    public void EvaluateMegPerformance_HighEnergyPhotons_ProducesMultipleExcitons()
    {
        // 3.1 eV photon hitting 0.95 eV bandgap PbSe quantum dots (ratio 3.26 > 2.5)
        var parameters = new QuantumDotParameters(
            CellId: "QD-PBSE-HIGH-EFF-01",
            CoreMaterial: "PbSe",
            QuantumDotDiameterNanometers: 4.0,
            BandgapEnergyElectronVolts: 0.95,
            IncidentPhotonEnergyElectronVolts: 3.10,
            CarrierCollectionEfficiency: 0.90,
            AugerRecombinationRatePicoseconds: 50.0
        );

        var request = new QuantumDotMegEvaluationRequest(parameters, new MegOperationalThresholds());
        var assessment = _engine.EvaluateMegPerformance(request);

        assessment.CellId.Should().Be("QD-PBSE-HIGH-EFF-01");
        assessment.MegThresholdExceeded.Should().BeTrue();
        assessment.CarrierMultiplicationQuantumYield.Should().BeGreaterThan(1.20);
        assessment.SolarCellEfficiencyRegime.Should().Be("MULTIPLE_EXCITON_ACTIVE_HIGH_YIELD");
        assessment.PowerConversionEfficiencyPercent.Should().BeGreaterThan(30.0);
        assessment.OptimizationDirective.Should().Contain("BYPASS_SHOCKLEY_QUEISSER");
    }

    [Fact]
    public void EvaluateMegPerformance_BelowMegThreshold_ReturnsSingleExcitonRegime()
    {
        // 1.5 eV photon on 1.0 eV bandgap (ratio 1.5 < 2.5)
        var parameters = new QuantumDotParameters(
            CellId: "QD-SUB-THRESHOLD",
            BandgapEnergyElectronVolts: 1.0,
            IncidentPhotonEnergyElectronVolts: 1.50
        );

        var request = new QuantumDotMegEvaluationRequest(parameters, new MegOperationalThresholds());
        var assessment = _engine.EvaluateMegPerformance(request);

        assessment.MegThresholdExceeded.Should().BeFalse();
        assessment.CarrierMultiplicationQuantumYield.Should().Be(1.0);
        assessment.SolarCellEfficiencyRegime.Should().Be("STANDARD_SINGLE_EXCITON");
        assessment.AugerRecombinationPenaltyPercent.Should().Be(0.0);
    }

    [Fact]
    public void EvaluateMegPerformance_FastAugerRecombination_CalculatesPenaltyAccurately()
    {
        // 15 ps fast Auger lifetime increases recombination penalty
        var parameters = new QuantumDotParameters(
            CellId: "QD-FAST-AUGER",
            BandgapEnergyElectronVolts: 0.90,
            IncidentPhotonEnergyElectronVolts: 2.70, // 3.0x Eg
            AugerRecombinationRatePicoseconds: 15.0
        );

        var request = new QuantumDotMegEvaluationRequest(parameters, new MegOperationalThresholds());
        var assessment = _engine.EvaluateMegPerformance(request);

        assessment.MegThresholdExceeded.Should().BeTrue();
        assessment.AugerRecombinationPenaltyPercent.Should().BeGreaterThan(4.0);
    }

    [Fact]
    public void EvaluateMegPerformance_NearThresholdOnset_FlagsTuningDirective()
    {
        // Ratio is exactly 2.55 (just above 2.5 threshold)
        var parameters = new QuantumDotParameters(
            CellId: "QD-ONSET-EDGE",
            BandgapEnergyElectronVolts: 1.0,
            IncidentPhotonEnergyElectronVolts: 2.55
        );

        var request = new QuantumDotMegEvaluationRequest(parameters, new MegOperationalThresholds());
        var assessment = _engine.EvaluateMegPerformance(request);

        assessment.MegThresholdExceeded.Should().BeTrue();
        assessment.SolarCellEfficiencyRegime.Should().Be("MARGINAL_MEG_ONSET");
        assessment.OptimizationDirective.Should().Contain("TUNE_DOT_DIAMETER");
    }
}
