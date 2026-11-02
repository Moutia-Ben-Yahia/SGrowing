using FluentAssertions;
using SGrowing.Api.Modules.BioTech.CarTCellExhaustionKinetics;
using Xunit;

namespace SGrowing.Tests.BioTech;

public class CarTCellExhaustionKineticsTests
{
    private readonly CarTCellExhaustionEngine _engine = new();

    [Fact]
    public void EvaluateExhaustion_StemCellMemoryEnriched_ReturnsPotentPersistentState()
    {
        var tel = new CarTCellPhenotypeTelemetry(
            BatchId: "CART-CD19-STEM-01",
            TargetAntigen: "CD19",
            Pd1PositiveFraction: 0.15,
            Lag3PositiveFraction: 0.10,
            Tim3PositiveFraction: 0.08,
            StemCellMemoryFraction: 0.42, // High T_SCM!
            DaysPostInfusion: 10.0,
            Il2SecretionPicogramsPerMl: 650.0,
            ChronicAntigenLoadIndex: 0.20
        );

        var request = new CarTExhaustionEvaluationRequest(tel, new ExhaustionThresholdLimits());
        var assessment = _engine.EvaluateExhaustion(request);

        assessment.BatchId.Should().Be("CART-CD19-STEM-01");
        assessment.TerminalExhaustionRiskDetected.Should().BeFalse();
        assessment.ImmunologicalPhenotypeState.Should().Be("POTENT_PERSISTENT_MEMORY");
        assessment.ExhaustionHazardIndex.Should().BeLessThan(0.20);
        assessment.EstimatedPersistenceHalfLifeDays.Should().BeGreaterThan(30.0);
        assessment.RecommendedCytokineInterventionDirective.Should().Contain("MAINTAIN_SURVEILLANCE");
    }

    [Fact]
    public void EvaluateExhaustion_HighTripleCheckpointCoExpression_TriggersTerminalExhaustionAlert()
    {
        // High PD-1 (0.85), LAG-3 (0.75), and TIM-3 (0.65) with depleted T_SCM (0.05)
        var tel = new CarTCellPhenotypeTelemetry(
            BatchId: "CART-EXHAUSTED-BATCH",
            TargetAntigen: "BCMA",
            Pd1PositiveFraction: 0.85,
            Lag3PositiveFraction: 0.75,
            Tim3PositiveFraction: 0.65,
            StemCellMemoryFraction: 0.05, // Depleted memory!
            DaysPostInfusion: 45.0,
            ChronicAntigenLoadIndex: 0.85
        );

        var limits = new ExhaustionThresholdLimits(MaxAllowableTerminalExhaustionIndex: 0.55);
        var request = new CarTExhaustionEvaluationRequest(tel, limits);
        var assessment = _engine.EvaluateExhaustion(request);

        assessment.TerminalExhaustionRiskDetected.Should().BeTrue();
        assessment.ImmunologicalPhenotypeState.Should().Be("TERMINAL_EXHAUSTION_DYSFUNCTION");
        assessment.ExhaustionHazardIndex.Should().BeGreaterThan(0.60);
        assessment.RecommendedCytokineInterventionDirective.Should().Contain("INTERVENE_IL7_IL15");
    }

    [Fact]
    public void EvaluateExhaustion_IntermediateExhaustion_FlagsTransitionalWarning()
    {
        var tel = new CarTCellPhenotypeTelemetry(
            BatchId: "CART-TRANSITIONAL",
            Pd1PositiveFraction: 0.45,
            Lag3PositiveFraction: 0.40,
            Tim3PositiveFraction: 0.35,
            StemCellMemoryFraction: 0.20,
            ChronicAntigenLoadIndex: 0.40
        );

        var request = new CarTExhaustionEvaluationRequest(tel, new ExhaustionThresholdLimits());
        var assessment = _engine.EvaluateExhaustion(request);

        assessment.TerminalExhaustionRiskDetected.Should().BeFalse();
        assessment.ImmunologicalPhenotypeState.Should().Be("EARLY_TRANSITIONAL_EXHAUSTION");
        assessment.RecommendedCytokineInterventionDirective.Should().Contain("MONITOR_CYTOKINES");
    }

    [Fact]
    public void EvaluateExhaustion_CalculatesPersistenceHalfLifeSensibly()
    {
        var tel = new CarTCellPhenotypeTelemetry("CART-PERSISTENCE-TEST", StemCellMemoryFraction: 0.30, Pd1PositiveFraction: 0.20);
        var request = new CarTExhaustionEvaluationRequest(tel, new ExhaustionThresholdLimits());
        var assessment = _engine.EvaluateExhaustion(request);

        assessment.EstimatedPersistenceHalfLifeDays.Should().BeGreaterThan(10.0);
        assessment.EstimatedPersistenceHalfLifeDays.Should().BeLessThan(100.0);
    }
}
