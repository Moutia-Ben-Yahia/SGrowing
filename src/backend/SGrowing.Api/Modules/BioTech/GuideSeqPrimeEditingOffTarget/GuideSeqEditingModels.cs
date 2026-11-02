namespace SGrowing.Api.Modules.BioTech.GuideSeqPrimeEditingOffTarget;

public record GuideSeqEditingTelemetry
{
    public required string AssayExperimentId { get; init; }
    public string EditorArchitecture { get; init; } = "PEmax_PrimeEditor"; // PEmax, ABE8e, BE4max
    public double OnTargetEditingEfficiencyPercent { get; init; } = 74.5; // Target >= 70.0%
    public double OffTargetCleavageFrequencyPercent { get; init; } = 0.045; // Target <= 0.08%
    public double UnintendedIndelFrequencyPercent { get; init; } = 1.15; // Target <= 1.5%
    public int ConfirmedGenomeWideOffTargetLociCount { get; init; } = 1; // Target <= 2
    public double PamProximalMismatchDiscriminationScore { get; init; } = 9.2; // 0 - 10 (Target >= 8.0)
    public double TranscriptomeRnaDeaminaseOffTargetPercent { get; init; } = 0.025; // <= 0.04%
    public int DsbTagCaptureSequencingDepthReads { get; init; } = 2500000;
}

public record GuideSeqEditingEvaluation
{
    public required string AssayExperimentId { get; init; }
    public double EditToIndelSelectivityRatio { get; init; }
    public double PrecisionEditingTherapeuticSafetyIndex { get; init; }
    public bool IsOnTargetEfficiencyAcceptable { get; init; }
    public bool IsOffTargetCleavageSuppressed { get; init; }
    public bool IsIndelFormationControlled { get; init; }
    public bool IsGenomeWidePrecisionClinicalReady { get; init; }
    public List<string> ActiveRegimes { get; init; } = new();
    public List<string> RecommendedMitigations { get; init; } = new();
    public DateTime EvaluatedAtUtc { get; init; } = DateTime.UtcNow;
}
