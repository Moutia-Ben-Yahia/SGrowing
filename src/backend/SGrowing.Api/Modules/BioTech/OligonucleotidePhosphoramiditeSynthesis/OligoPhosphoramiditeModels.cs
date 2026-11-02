namespace SGrowing.Api.Modules.BioTech.OligonucleotidePhosphoramiditeSynthesis;

public sealed record OligoPhosphoramiditeTelemetry
{
    public required string SynthesisRunId { get; init; }
    public string TargetSequenceName { get; init; } = "Cas9-sgRNA-Targeting";
    public int OligoLengthNtds { get; init; } = 100; // total length (nt)
    public double StepwiseCouplingYieldPercent { get; init; } = 99.4; // SCY (%)
    public double InLineTritylAbsorbance498NmRatio { get; init; } = 0.994; // A(n) / A(n-1)
    public double AcetonitrileMoisturePpm { get; init; } = 18.0; // Karl Fischer H2O ppm (target < 30 ppm)
    public double CappingEfficiencyPercent { get; init; } = 98.8; // Capping efficiency (%)
    public double ActivatorCouplingTimeSeconds { get; init; } = 90.0; // coupling wait time (s)
    public double OxidationIodineConcentrationMolar { get; init; } = 0.02; // Iodine oxidizer molarity
    public double SynthesisScaleMicromoles { get; init; } = 1.0; // scale in umol
}

public sealed record OligoPhosphoramiditeAnalysisResult
{
    public required string SynthesisRunId { get; init; }
    public required double CalculatedStepwiseCouplingYieldPercent { get; init; }
    public required double CumulativeFullLengthProductPurityPercent { get; init; }
    public required double EstimatedNMinus1DeletionPurityLossPercent { get; init; }
    public required double MoistureHydrolysisInactivationRiskPercent { get; init; }
    public required bool IsStepwiseCouplingYieldOptimal { get; init; }
    public required bool IsSolventMoistureWithinSpecification { get; init; }
    public required bool IsCappingEfficiencySufficient { get; init; }
    public required bool IsFullLengthPurityTargetMet { get; init; }
    public required string SynthesisIntegrityRegime { get; init; }
    public required string SynthesisOptimizationGuidance { get; init; }
}
