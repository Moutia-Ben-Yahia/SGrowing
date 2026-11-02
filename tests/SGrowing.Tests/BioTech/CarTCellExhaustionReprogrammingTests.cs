namespace SGrowing.Tests.BioTech;

using System;
using FluentAssertions;
using SGrowing.Api.Modules.BioTech.CarTCellExhaustionReprogramming;
using Xunit;

public class CarTCellExhaustionReprogrammingTests
{
    private readonly CarTCellExhaustionReprogrammingEngine _engine = new();

    [Fact]
    public void AnalyzeCarTExhaustion_ReprogrammedStemLike_ReturnsDurableRegime()
    {
        var telemetry = new CarTCellExhaustionTelemetry(
            CellBatchId: "CART-BATCH-001",
            ToxTranscriptionFactorRelativeExpression: 0.12, // CRISPR KO
            Nr4aTranscriptionFactorRelativeExpression: 0.85,
            Pd1CheckpointSurfaceDensityMfi: 18.0,
            Tim3CheckpointSurfaceDensityMfi: 14.0,
            Lag3CheckpointSurfaceDensityMfi: 12.0,
            StemCellMemoryFractionPercent: 32.0, // High stemness
            DegranulationCd107aFractionPercent: 68.0,
            IsToxCrisprKnockoutApplied: true,
            IsCJunOverexpressionApplied: true
        );

        var result = _engine.AnalyzeCarTExhaustion(telemetry);

        result.CellBatchId.Should().Be("CART-BATCH-001");
        result.ExhaustionRegime.Should().Be("EPIGENETICALLY_REPROGRAMMED_STEM_LIKE_DURABLE_REGIME");
        result.IsTherapeuticallyDurable.Should().BeTrue();
        result.CompositeExhaustionScore.Should().BeLessThan(1.0);
        result.MemoryStemnessRetentionIndex.Should().BeGreaterThan(40.0);
        result.FunctionalCytotoxicityIndex.Should().BeGreaterThan(60.0);
        result.TherapeuticReprogrammingGuidance.Should().ContainEquivalentOf("Robust self-renewing T_SCM");
    }

    [Fact]
    public void AnalyzeCarTExhaustion_TerminalExhaustion_ReturnsTerminalRegime()
    {
        var telemetry = new CarTCellExhaustionTelemetry(
            CellBatchId: "CART-BATCH-002",
            ToxTranscriptionFactorRelativeExpression: 5.2, // Massive TOX expression
            Nr4aTranscriptionFactorRelativeExpression: 4.8,
            Pd1CheckpointSurfaceDensityMfi: 140.0,
            Tim3CheckpointSurfaceDensityMfi: 110.0,
            Lag3CheckpointSurfaceDensityMfi: 95.0,
            StemCellMemoryFractionPercent: 6.5, // Depleted stemness
            DegranulationCd107aFractionPercent: 12.0,
            IsToxCrisprKnockoutApplied: false,
            IsCJunOverexpressionApplied: false
        );

        var result = _engine.AnalyzeCarTExhaustion(telemetry);

        result.ExhaustionRegime.Should().Be("TERMINAL_EPIGENETIC_CAR_T_EXHAUSTION");
        result.IsTherapeuticallyDurable.Should().BeFalse();
        result.CompositeExhaustionScore.Should().BeGreaterThan(3.0);
        result.TherapeuticReprogrammingGuidance.Should().ContainEquivalentOf("CRISPR-Cas9 TOX gene ablation");
    }

    [Fact]
    public void AnalyzeCarTExhaustion_HighCheckpoints_ReturnsProgressiveDysfunction()
    {
        var telemetry = new CarTCellExhaustionTelemetry(
            CellBatchId: "CART-BATCH-003",
            ToxTranscriptionFactorRelativeExpression: 1.8,
            Nr4aTranscriptionFactorRelativeExpression: 2.1,
            Pd1CheckpointSurfaceDensityMfi: 120.0,
            Tim3CheckpointSurfaceDensityMfi: 80.0,
            Lag3CheckpointSurfaceDensityMfi: 75.0,
            StemCellMemoryFractionPercent: 25.0, // Decent stemness (> 20 so not terminal)
            DegranulationCd107aFractionPercent: 35.0,
            IsToxCrisprKnockoutApplied: false,
            IsCJunOverexpressionApplied: false
        );

        var result = _engine.AnalyzeCarTExhaustion(telemetry);

        result.ExhaustionRegime.Should().Be("PROGRESSIVE_IMMUNE_CHECKPOINT_DYSFUNCTION");
        result.IsTherapeuticallyDurable.Should().BeFalse();
        result.TherapeuticReprogrammingGuidance.Should().ContainEquivalentOf("checkpoint blockade inhibitors");
    }

    [Fact]
    public void AnalyzeCarTExhaustion_LowStemness_ReturnsEffectorDifferentiationWarning()
    {
        var telemetry = new CarTCellExhaustionTelemetry(
            CellBatchId: "CART-BATCH-004",
            ToxTranscriptionFactorRelativeExpression: 0.9,
            Nr4aTranscriptionFactorRelativeExpression: 1.0,
            Pd1CheckpointSurfaceDensityMfi: 25.0,
            Tim3CheckpointSurfaceDensityMfi: 20.0,
            Lag3CheckpointSurfaceDensityMfi: 18.0,
            StemCellMemoryFractionPercent: 8.0, // Low stemness < 15
            DegranulationCd107aFractionPercent: 55.0,
            IsToxCrisprKnockoutApplied: false,
            IsCJunOverexpressionApplied: false
        );

        var result = _engine.AnalyzeCarTExhaustion(telemetry);

        result.ExhaustionRegime.Should().Be("SHORT_LIVED_EFFECTOR_DIFFERENTIATION_WARNING");
        result.IsTherapeuticallyDurable.Should().BeFalse();
        result.TherapeuticReprogrammingGuidance.Should().ContainEquivalentOf("Culture with IL-7/IL-15");
    }

    [Fact]
    public void AnalyzeCarTExhaustion_NullTelemetry_ThrowsArgumentNullException()
    {
        var act = () => _engine.AnalyzeCarTExhaustion(null!);
        act.Should().Throw<ArgumentNullException>();
    }
}
