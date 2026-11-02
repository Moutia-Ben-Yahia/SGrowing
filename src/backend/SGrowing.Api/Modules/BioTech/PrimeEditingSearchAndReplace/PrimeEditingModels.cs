namespace SGrowing.Api.Modules.BioTech.PrimeEditingSearchAndReplace;

public sealed record PrimeEditingTelemetry(
    string EditTargetLocusId,
    int PrimerBindingSiteLengthNucleotides, // PBS length: e.g. 13-16 nt
    int ReverseTranscriptionTemplateLengthNucleotides, // RTT length: e.g. 10-34 nt
    double PbsMeltingTemperatureCelsius, // Tm of PBS
    double RttGcContentPercent, // GC% of RTT
    bool IsPe3NonEditedStrandNickApplied, // PE3 vs PE2
    double NonEditedStrandNickDistanceBasePairs, // e.g. +40 to +90 bp
    double SecondaryStructureFreeEnergyDeltaG, // kcal/mol (more negative = hairpin inhibition)
    double TargetGenomicLocusAccessibilityScore // ATAC-seq accessibility [0, 1]
);

public sealed record PrimeEditingAnalysisResult(
    string EditTargetLocusId,
    double PredictedOnTargetEditingEfficiencyPercent,
    double PredictedIndelFormationRatePercent,
    double EditToIndelPrecisionRatio,
    string PrimeEditingSystemType,
    string PrimeEditingRegime,
    string PegRnaOptimizationGuidance,
    bool IsEditingPrecisionHigh
);

public interface IPrimeEditingSearchAndReplaceEngine
{
    PrimeEditingAnalysisResult AnalyzePrimeEditing(PrimeEditingTelemetry telemetry);
}
