using System;
using SGrowing.Api.Modules.BioTech.EngineeredExosomeCargoLoading;
using Xunit;

namespace SGrowing.Tests.BioTech;

public sealed class EngineeredExosomeCargoLoadingTests
{
    private readonly EngineeredExosomeCargoLoadingEngine _engine = new();

    [Fact]
    public void AnalyzeExosomeBatch_NullTelemetry_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => _engine.AnalyzeExosomeBatch(null!));
    }

    [Fact]
    public void AnalyzeExosomeBatch_NegativeDiameter_ThrowsArgumentException()
    {
        var telemetry = new EngineeredExosomeTelemetry(
            ExosomeBatchId: "EXO-BATCH-01",
            MeanHydrodynamicDiameterNanometers: -50.0, // Negative!
            PolydispersityIndex: 0.15,
            ZetaPotentialMillivolts: -20.0,
            NanobodiesPerExosomeEstimate: 50.0,
            RnaCargoEncapsulationEfficiencyPercent: 60.0,
            EndosomalEscapeEfficiencyPercent: 25.0,
            TotalParticleConcentrationParticlesPerMl: 1e11
        );

        Assert.Throws<ArgumentException>(() => _engine.AnalyzeExosomeBatch(telemetry));
    }

    [Fact]
    public void AnalyzeExosomeBatch_HighPotencyBatch_ReturnsHighPotencyRegime()
    {
        var telemetry = new EngineeredExosomeTelemetry(
            ExosomeBatchId: "EXO-VHH-HER2-RNA",
            MeanHydrodynamicDiameterNanometers: 102.0,
            PolydispersityIndex: 0.11,
            ZetaPotentialMillivolts: -26.0,
            NanobodiesPerExosomeEstimate: 90.0, // 1.8x
            RnaCargoEncapsulationEfficiencyPercent: 72.0, // 1.2x
            EndosomalEscapeEfficiencyPercent: 35.0, // 1.4x -> Potency = 3.02 >= 1.5
            TotalParticleConcentrationParticlesPerMl: 3.5e11
        );

        var result = _engine.AnalyzeExosomeBatch(telemetry);

        Assert.NotNull(result);
        Assert.Equal("EXO-VHH-HER2-RNA", result.ExosomeBatchId);
        Assert.True(result.CompositeDeliveryPotencyIndex >= 1.5);
        Assert.True(result.IsBatchClinicallyPotent);
        Assert.Equal("HIGH_POTENCY_TARGETED_EXOSOME_DELIVERY_REGIME", result.ExosomeQualityRegime);
        Assert.Contains("Exceptional targeted delivery profile", result.BioprocessFormulationGuidance);
    }

    [Fact]
    public void AnalyzeExosomeBatch_HighPdiAggregation_ReturnsColloidalAggregationHazard()
    {
        var telemetry = new EngineeredExosomeTelemetry(
            ExosomeBatchId: "EXO-AGGREGATE-02",
            MeanHydrodynamicDiameterNanometers: 210.0,
            PolydispersityIndex: 0.42, // PDI > 0.35!
            ZetaPotentialMillivolts: -6.0, // Zeta > -10 mV
            NanobodiesPerExosomeEstimate: 45.0,
            RnaCargoEncapsulationEfficiencyPercent: 50.0,
            EndosomalEscapeEfficiencyPercent: 20.0,
            TotalParticleConcentrationParticlesPerMl: 8e10
        );

        var result = _engine.AnalyzeExosomeBatch(telemetry);

        Assert.NotNull(result);
        Assert.Equal("COLLOIDAL_AGGREGATION_SIZE_HETEROGENEITY_HAZARD", result.ExosomeQualityRegime);
        Assert.False(result.IsBatchClinicallyPotent);
        Assert.Contains("tangential flow filtration", result.BioprocessFormulationGuidance);
    }

    [Fact]
    public void AnalyzeExosomeBatch_LowNanobodyDensity_ReturnsInsufficientLigandDensity()
    {
        var telemetry = new EngineeredExosomeTelemetry(
            ExosomeBatchId: "EXO-BALD-03",
            MeanHydrodynamicDiameterNanometers: 95.0,
            PolydispersityIndex: 0.14,
            ZetaPotentialMillivolts: -22.0,
            NanobodiesPerExosomeEstimate: 12.0, // < 20 copies/EV
            RnaCargoEncapsulationEfficiencyPercent: 65.0,
            EndosomalEscapeEfficiencyPercent: 28.0,
            TotalParticleConcentrationParticlesPerMl: 2e11
        );

        var result = _engine.AnalyzeExosomeBatch(telemetry);

        Assert.NotNull(result);
        Assert.Equal("INSUFFICIENT_SURFACE_HOMING_LIGAND_DENSITY", result.ExosomeQualityRegime);
        Assert.False(result.IsBatchClinicallyPotent);
        Assert.Contains("PTGFRN scaffold transfection", result.BioprocessFormulationGuidance);
    }
}
