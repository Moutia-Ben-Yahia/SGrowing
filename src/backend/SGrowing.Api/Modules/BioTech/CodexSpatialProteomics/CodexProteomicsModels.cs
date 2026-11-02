namespace SGrowing.Api.Modules.BioTech.CodexSpatialProteomics;

public record CodexProteomicsTelemetry
{
    public required string TissueRunUid { get; init; }
    public string TissueType { get; init; } = "HumanTumorMicroenvironment_FFPE";
    public int MultiplexAntibodyPanelSize { get; init; } = 54; // 40 - 60 markers
    public int CompletedImagingCycles { get; init; } = 36; // 30 - 50 cycles
    public double IterativeReporterStrippingEfficiencyPercent { get; init; } = 99.1; // Target >= 98.8%
    public double ResidualBackgroundCarryoverRatio { get; init; } = 0.009; // Target <= 0.012
    public double TissueSectionDetachmentFractionPercent { get; init; } = 1.15; // Target <= 2.0%
    public double PhotobleachingDecayConstantSeconds { get; init; } = 52.0; // Target >= 45.0 s
    public double SingleCellSpatialSegmentationF1Score { get; init; } = 0.93; // Target >= 0.91
    public double AntibodySignalAttenuationOverRunPercent { get; init; } = 18.5; // Target <= 25.0%
}

public record CodexProteomicsEvaluation
{
    public required string TissueRunUid { get; init; }
    public double SignalToBackgroundDynamicRangeDb { get; init; }
    public double TissueIntegrityRetentionPercent { get; init; }
    public bool IsReporterStrippingComplete { get; init; }
    public bool IsTissueAdhesionPreserved { get; init; }
    public bool IsSegmentationQualityAccurate { get; init; }
    public bool IsCodexRunClinicallyValid { get; init; }
    public List<string> ActiveRegimes { get; init; } = new();
    public List<string> RecommendedMitigations { get; init; } = new();
    public DateTime EvaluatedAtUtc { get; init; } = DateTime.UtcNow;
}
