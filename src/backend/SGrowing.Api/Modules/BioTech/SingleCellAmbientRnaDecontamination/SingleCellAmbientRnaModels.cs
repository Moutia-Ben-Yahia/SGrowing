namespace SGrowing.Api.Modules.BioTech.SingleCellAmbientRnaDecontamination;

public sealed record SingleCellAmbientRnaTelemetry
{
    public required string SampleRunId { get; init; }
    public string TissueType { get; init; } = "Peripheral-Blood-Mononuclear-Cells"; // PBMC, Brain Cortex, Tumor
    public int TotalDropletsAnalyzedCount { get; init; } = 8500;
    public double EstimatedAmbientSoupContaminationFraction { get; init; } = 0.12; // rho_c ~ 12%
    public double EmptyDropletMeanUmiCount { get; init; } = 45.0; // background RNA in empty GEMs
    public double CellularDropletMedianUmiCount { get; init; } = 4200.0; // cell UMIs
    public double MitochondrialTranscriptFractionPercent { get; init; } = 5.8; // MT% (target < 15%)
    public double HemoglobinContaminationFractionPercent { get; init; } = 1.8; // HBA/HBB leakage
    public double FalsePositiveMarkerSpecificityBps { get; init; } = 85.0; // specificity metric
}

public sealed record SingleCellAmbientRnaAnalysisResult
{
    public required string SampleRunId { get; init; }
    public required double CellularPurityScorePercent { get; init; }
    public required double DecontaminatedSoupFractionPercent { get; init; }
    public required double SignalToAmbientBackgroundRatio { get; init; }
    public required double MitochondrialStressFlagRatio { get; init; }
    public required bool IsAmbientSoupContaminationAcceptable { get; init; }
    public required bool IsMitochondrialQualityPassing { get; init; }
    public required bool IsSingleCellDatasetDecontaminated { get; init; }
    public required string DecontaminationRegime { get; init; }
    public required string BioinformaticCurationGuidance { get; init; }
}
