namespace SGrowing.Api.Modules.BioTech.CrisprCasCleavageKinetics;

public record CrisprGuideRna(
    string GuideId,
    string SpacerSequence20Nt,
    string TargetGeneSymbol,
    string ExpectedPam = "TTTA" // 5'-TTTV PAM for Cas12a
);

public record OffTargetLocus(
    string LocusId,
    string Chromosome,
    long PositionGrch38,
    string DnaProtospacer20Nt,
    string PamFound,
    string GeneAnnotation // e.g. "Exonic_TP53", "Intronic_EGFR", "Intergenic"
);

public record CrisprCleavageEvaluationRequest(
    CrisprGuideRna Guide,
    IReadOnlyList<OffTargetLocus> CandidateOffTargets
);

public record OffTargetCleavagePrediction(
    string LocusId,
    string GeneAnnotation,
    int SeedMismatchCount,
    int DistalMismatchCount,
    double DeltaDeltaGKcalMol,
    double CleavageProbabilityPercent,
    bool ExceedsSafetyThreshold
);

public record CrisprSafetyEvaluationResult(
    string GuideId,
    string TargetGeneSymbol,
    int TotalLociScreened,
    int HazardousOffTargetsCount,
    bool GuideApprovedForTherapy,
    string ClinicalSafetyClassification,
    IReadOnlyList<OffTargetCleavagePrediction> Predictions
);
