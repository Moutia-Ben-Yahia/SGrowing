using System;
using SGrowing.Api.Modules.BioTech.CellFreeProteinTranslation;
using Xunit;

namespace SGrowing.Tests.BioTech;

public sealed class CellFreeProteinTranslationTests
{
    private readonly CellFreeProteinTranslationEngine _engine = new();

    [Fact]
    public void AnalyzeBioreactorKinetics_OptimalSubstrates_MaintainsSteadyState()
    {
        var telemetry = new CellFreeBioreactorTelemetry(
            BioreactorRunId: "CFPS-BIO-RUN-701",
            TargetProteinName: "Trastuzumab-scFv",
            AtpConcentrationMillimolar: 2.2,
            GtpConcentrationMillimolar: 1.5,
            SecondaryEnergySubstrateConcentrationMillimolar: 30.0,
            InorganicPhosphateConcentrationMillimolar: 12.0,
            AminoAcidsAverageConcentrationMillimolar: 2.0,
            ActiveRibosomeConcentrationMicromolar: 1.8,
            ReactionTemperatureCelsius: 37.0,
            ElapsedSynthesisHours: 4.0);

        var result = _engine.AnalyzeBioreactorKinetics(telemetry);

        Assert.Equal("TX_TL_STEADY_STATE_OPTIMAL", result.TranslationKineticsRegime);
        Assert.Equal("MAINTAIN_STEADY_STATE_INCUBATION_AT_37_CELSIUS", result.BioreactorFeedRecommendation);
        Assert.True(result.IsReactionHealthy);
        Assert.True(result.RibosomalElongationRateAaPerSec >= 1.5);
        Assert.True(result.InstantaneousSynthesisYieldMicrogramsPerMlPerHour > 500.0);
    }

    [Fact]
    public void AnalyzeBioreactorKinetics_HighPhosphateAccumulation_TriggersDialysisPurge()
    {
        var telemetry = new CellFreeBioreactorTelemetry(
            BioreactorRunId: "CFPS-BIO-RUN-702",
            TargetProteinName: "Cas9-Endonuclease",
            AtpConcentrationMillimolar: 1.8,
            GtpConcentrationMillimolar: 1.2,
            SecondaryEnergySubstrateConcentrationMillimolar: 20.0,
            InorganicPhosphateConcentrationMillimolar: 48.0, // >= 45.0 mM
            AminoAcidsAverageConcentrationMillimolar: 1.5,
            ActiveRibosomeConcentrationMicromolar: 1.5,
            ReactionTemperatureCelsius: 37.0,
            ElapsedSynthesisHours: 8.0);

        var result = _engine.AnalyzeBioreactorKinetics(telemetry);

        Assert.Equal("PHOSPHATE_POISONING_IMMINENT", result.TranslationKineticsRegime);
        Assert.Equal("TRIGGER_DIALYSIS_CONTINUOUS_EXCHANGE_BUFFER_PURGE", result.BioreactorFeedRecommendation);
        Assert.False(result.IsReactionHealthy);
    }

    [Fact]
    public void AnalyzeBioreactorKinetics_LowAtpConcentration_IssuesSubstrateDepletionWarning()
    {
        var telemetry = new CellFreeBioreactorTelemetry(
            BioreactorRunId: "CFPS-BIO-RUN-703",
            TargetProteinName: "Luciferase-Reporter",
            AtpConcentrationMillimolar: 0.35, // < 0.50 mM
            GtpConcentrationMillimolar: 1.0,
            SecondaryEnergySubstrateConcentrationMillimolar: 15.0,
            InorganicPhosphateConcentrationMillimolar: 15.0,
            AminoAcidsAverageConcentrationMillimolar: 1.5,
            ActiveRibosomeConcentrationMicromolar: 1.2,
            ReactionTemperatureCelsius: 37.0,
            ElapsedSynthesisHours: 2.5);

        var result = _engine.AnalyzeBioreactorKinetics(telemetry);

        Assert.Equal("SUBSTRATE_DEPLETION_WARNING", result.TranslationKineticsRegime);
        Assert.Equal("FEED_CONCENTRATED_NUCLEOTIDE_AND_AMINO_ACID_COCKTAIL", result.BioreactorFeedRecommendation);
        Assert.False(result.IsReactionHealthy);
    }

    [Fact]
    public void AnalyzeBioreactorKinetics_ExhaustedEnergySecondarySubstrate_IssuesDepletionWarning()
    {
        var telemetry = new CellFreeBioreactorTelemetry(
            BioreactorRunId: "CFPS-BIO-RUN-704",
            TargetProteinName: "GFP-Positive-Control",
            AtpConcentrationMillimolar: 1.2,
            GtpConcentrationMillimolar: 1.0,
            SecondaryEnergySubstrateConcentrationMillimolar: 3.2, // < 5.0 mM
            InorganicPhosphateConcentrationMillimolar: 18.0,
            AminoAcidsAverageConcentrationMillimolar: 1.5,
            ActiveRibosomeConcentrationMicromolar: 1.5,
            ReactionTemperatureCelsius: 37.0,
            ElapsedSynthesisHours: 5.0);

        var result = _engine.AnalyzeBioreactorKinetics(telemetry);

        Assert.Equal("SUBSTRATE_DEPLETION_WARNING", result.TranslationKineticsRegime);
        Assert.False(result.IsReactionHealthy);
    }
}
