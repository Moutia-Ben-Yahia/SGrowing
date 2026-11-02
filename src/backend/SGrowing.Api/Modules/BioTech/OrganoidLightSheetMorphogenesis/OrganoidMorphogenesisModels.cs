namespace SGrowing.Api.Modules.BioTech.OrganoidLightSheetMorphogenesis;

public record OrganoidMorphogenesisTelemetry
{
    public required string OrganoidCultureBatchId { get; init; }
    public string OrganoidLineage { get; init; } = "HumanCerebralCortical_BrainOrganoid"; // Cerebral, Hepatic, Intestinal
    public int CultureDayPostDifferentiation { get; init; } = 45; // Day 30 - 120
    public double OrganoidRadiusMicrons { get; init; } = 420.0; // 200 - 800 um
    public double NecroticCoreRadiusMicrons { get; init; } = 48.0; // Target <= 0.15 * R
    public double SphericityIndex { get; init; } = 0.89; // Target >= 0.85
    public double FractalBranchingDimension { get; init; } = 1.62; // 1.45 - 1.85
    public int TerminalBudMorphogenesisCount { get; init; } = 18; // Target >= 12
    public double DeepTissueCellViabilityPercent { get; init; } = 92.4; // Target >= 88.0%
    public double LightSheetPhotobleachingApoptosisPercent { get; init; } = 1.8; // Target <= 3.0%
}

public record OrganoidMorphogenesisEvaluation
{
    public required string OrganoidCultureBatchId { get; init; }
    public double NecroticCoreFractionRatio { get; init; }
    public double MorphologicalIntegrityScore { get; init; }
    public bool IsNecroticCoreHypoxiaBounded { get; init; }
    public bool IsBranchingMorphogenesisActive { get; init; }
    public bool IsPhototoxicityControlled { get; init; }
    public bool IsOrganoidBatchQualityCertified { get; init; }
    public List<string> ActiveRegimes { get; init; } = new();
    public List<string> RecommendedMitigations { get; init; } = new();
    public DateTime EvaluatedAtUtc { get; init; } = DateTime.UtcNow;
}
