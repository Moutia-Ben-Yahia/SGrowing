namespace SGrowing.Api.Modules.BioTech.HighMolecularWeightDnaExtraction;

public record HmwDnaExtractionTelemetry
{
    public required string ExtractionBatchId { get; init; }
    public string SampleType { get; init; } = "MammalianWholeBlood"; // CellCulture, PlantTissue, Blood
    public double DnaConcentrationNgPerUl { get; init; } = 85.0; // >= 50.0 ng/uL
    public double AbsorbanceRatio260_280 { get; init; } = 1.86; // Ideal 1.80 - 2.00
    public double AbsorbanceRatio260_230 { get; init; } = 2.15; // Ideal >= 2.00
    public double FragmentLengthN50Kbp { get; init; } = 52.4; // Target >= 40.0 kbp
    public double UltraLongFractionAbove100KbpPercent { get; init; } = 28.5; // Target >= 20.0%
    public double PipettingHydrodynamicShearRateSecInv { get; init; } = 120.0; // Low-shear wide-bore tips < 250 s^-1
    public double ActiveNanoporePoreFractionPercent { get; init; } = 88.0; // Target >= 80%
    public double TranslocationSpeedBasesPerSecond { get; init; } = 425.0; // ~400-450 bps
}

public record HmwDnaExtractionEvaluation
{
    public required string ExtractionBatchId { get; init; }
    public double CalculatedSequencingReadinessScorePercent { get; init; }
    public double EstimatedPoreBlockingHazardIndex { get; init; }
    public bool IsLengthProfileUltraLongReady { get; init; }
    public bool IsChemicalPurityCompliant { get; init; }
    public bool IsHydrodynamicShearControlled { get; init; }
    public bool IsNanoporeTranslocationOptimal { get; init; }
    public List<string> ActiveRegimes { get; init; } = new();
    public List<string> RecommendedMitigations { get; init; } = new();
    public DateTime EvaluatedAtUtc { get; init; } = DateTime.UtcNow;
}
