namespace SGrowing.Api.Modules.BioTech.CellFreeProteinSynthesisExpression;

public sealed record CellFreeProteinSynthesisTelemetry
{
    public required string BatchId { get; init; }
    public string TargetProteinName { get; init; } = "Nanobody-Fc";
    public double LinearDnaConcentrationNm { get; init; } = 10.0; // nM
    public double GamSInhibitorConcentrationUm { get; init; } = 3.5; // uM GamS exonuclease inhibitor
    public double RareCodonStallingIndex { get; init; } = 1.2; // cumulative rare codon penalty
    public double RbsHairpinFreeEnergyKcalPerMol { get; init; } = -8.5; // dG_RBS (kcal/mol)
    public double MagnesiumConcentrationMm { get; init; } = 12.0; // mM Mg2+ (optimum 10-14 mM)
    public double PotassiumConcentrationMm { get; init; } = 150.0; // mM K+ (optimum 120-180 mM)
    public double ReactionDurationHours { get; init; } = 8.0; // hours
    public double UninhibitedDnaDegradationConstantPerMin { get; init; } = 0.055; // min^-1
}

public sealed record CellFreeProteinSynthesisAnalysisResult
{
    public required string BatchId { get; init; }
    public required double DnaDegradationHalfLifeMinutes { get; init; }
    public required double TranslationInitiationEfficiencyPercent { get; init; }
    public required double RibosomeElongationStallPenalty { get; init; }
    public required double IonicCationFitnessFactor { get; init; }
    public required double ProjectedProteinYieldMgPerMl { get; init; }
    public required bool IsDnaSufficientlyProtected { get; init; }
    public required bool IsRbsFreeOfStrongSecondaryStructures { get; init; }
    public required bool IsYieldTargetAchieved { get; init; }
    public required string ExpressionRegime { get; init; }
    public required string OptimizationRecommendation { get; init; }
}
