using System;

namespace SGrowing.Api.Modules.BioTech.CellFreeProteinTranslation;

public sealed record CellFreeBioreactorTelemetry(
    string BioreactorRunId,
    string TargetProteinName,
    double AtpConcentrationMillimolar,
    double GtpConcentrationMillimolar,
    double SecondaryEnergySubstrateConcentrationMillimolar,
    double InorganicPhosphateConcentrationMillimolar,
    double AminoAcidsAverageConcentrationMillimolar,
    double ActiveRibosomeConcentrationMicromolar,
    double ReactionTemperatureCelsius,
    double ElapsedSynthesisHours);

public sealed record CellFreeBioreactorAnalysisResult(
    string BioreactorRunId,
    string TargetProteinName,
    double RibosomalElongationRateAaPerSec,
    double InstantaneousSynthesisYieldMicrogramsPerMlPerHour,
    double EstimatedCumulativeProteinYieldMicrogramsPerMl,
    string TranslationKineticsRegime,
    string BioreactorFeedRecommendation,
    bool IsReactionHealthy,
    DateTimeOffset Timestamp);
