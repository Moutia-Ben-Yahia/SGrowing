using FluentAssertions;
using SGrowing.Api.Modules.BioTech.MicrobialCrisprCascade;
using Xunit;

namespace SGrowing.Tests.BioTech;

public class MicrobialCrisprCascadeTests
{
    private readonly MicrobialCrisprCascadeEngine _engine = new();

    [Fact]
    public void EvaluateCascade_PostSwitchBiomass_TriggersDcas9Redirection()
    {
        var state = new BioreactorState(
            BioreactorId: "BIOREACTOR-PRECISION-01",
            HostOrganism: "Escherichia_coli_BL21",
            OpticalDensity600: 14.5, // Well past the 8.0 trigger
            DissolvedOxygenPercent: 42.0,
            GlucoseConcentrationGramsPerLiter: 5.0,
            AcetateConcentrationGramsPerLiter: 0.5,
            TargetMetaboliteGramsPerLiter: 8.2,
            ActiveSgRnaGuideCount: 5
        );

        var thresh = new MetabolicSwitchThresholds(CriticalOd600Trigger: 8.0);
        var request = new MicrobialCascadeEvaluationRequest(state, thresh);
        var assessment = _engine.EvaluateCascade(request);

        assessment.BioreactorId.Should().Be("BIOREACTOR-PRECISION-01");
        assessment.Dcas9CascadeTriggered.Should().BeTrue();
        assessment.FermentationPhase.Should().Be("STATIONARY_BIOSYNTHESIS_CASCADE");
        assessment.CarbonFluxRedirectionPercent.Should().BeGreaterThan(70.0);
        assessment.PredictedFinalYieldGramsPerLiter.Should().BeGreaterThan(state.TargetMetaboliteGramsPerLiter);
        assessment.RecommendedCascadeDirective.Should().Contain("CASCADE_ACTIVE");
    }

    [Fact]
    public void EvaluateCascade_PreSwitchEarlyPhase_AccumulatesBiomassWithoutInduction()
    {
        // OD600 is 3.5, below 8.0 switch
        var state = new BioreactorState(
            BioreactorId: "BIOREACTOR-EARLY-SEED",
            OpticalDensity600: 3.5,
            DissolvedOxygenPercent: 65.0,
            GlucoseConcentrationGramsPerLiter: 15.0,
            AcetateConcentrationGramsPerLiter: 0.1,
            TargetMetaboliteGramsPerLiter: 0.2,
            ActiveSgRnaGuideCount: 3
        );

        var thresh = new MetabolicSwitchThresholds(CriticalOd600Trigger: 8.0);
        var request = new MicrobialCascadeEvaluationRequest(state, thresh);
        var assessment = _engine.EvaluateCascade(request);

        assessment.Dcas9CascadeTriggered.Should().BeFalse();
        assessment.FermentationPhase.Should().Be("EXPONENTIAL_BIOMASS_EXPANSION");
        assessment.CarbonFluxRedirectionPercent.Should().Be(15.0);
        assessment.RecommendedCascadeDirective.Should().Contain("ACCUMULATE_BIOMASS");
    }

    [Fact]
    public void EvaluateCascade_AcetateOverflowToxicity_CommandsPathwayRepression()
    {
        // Acetate reaches 2.8 g/L (exceeds 2.0 g/L threshold)
        var state = new BioreactorState(
            BioreactorId: "BIOREACTOR-ACETATE-SPIKE",
            OpticalDensity600: 16.0,
            DissolvedOxygenPercent: 25.0,
            GlucoseConcentrationGramsPerLiter: 22.0,
            AcetateConcentrationGramsPerLiter: 2.8, // Toxic overflow!
            TargetMetaboliteGramsPerLiter: 5.0,
            ActiveSgRnaGuideCount: 4
        );

        var thresh = new MetabolicSwitchThresholds(MaxAllowableAcetateGramsPerLiter: 2.0);
        var request = new MicrobialCascadeEvaluationRequest(state, thresh);
        var assessment = _engine.EvaluateCascade(request);

        assessment.Dcas9CascadeTriggered.Should().BeFalse();
        assessment.FermentationPhase.Should().Be("ACETATE_OVERFLOW_TOXICITY");
        assessment.RecommendedCascadeDirective.Should().Contain("REPRESS_PTA_ACKA");
    }

    [Fact]
    public void EvaluateCascade_CalculatesMetabolicBurdenIndexSensibly()
    {
        var state = new BioreactorState("BIOREACTOR-BURDEN-TEST", OpticalDensity600: 10.0, ActiveSgRnaGuideCount: 6);
        var request = new MicrobialCascadeEvaluationRequest(state, new MetabolicSwitchThresholds());
        var assessment = _engine.EvaluateCascade(request);

        assessment.MetabolicBurdenIndex.Should().BeGreaterThan(0.1);
        assessment.MetabolicBurdenIndex.Should().BeLessThan(0.9);
    }
}
