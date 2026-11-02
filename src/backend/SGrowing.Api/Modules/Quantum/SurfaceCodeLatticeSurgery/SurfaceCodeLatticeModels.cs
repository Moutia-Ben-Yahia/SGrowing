namespace SGrowing.Api.Modules.Quantum.SurfaceCodeLatticeSurgery;

public record SurfaceCodeLatticeTelemetry
{
    public required string LogicalQubitPatchUid { get; init; }
    public int CodeDistance { get; init; } = 5; // d = 3, 5, 7
    public double PhysicalQubitErrorRate { get; init; } = 0.00065; // Target <= 0.0010
    public double DecoderLatencyMicroseconds { get; init; } = 0.78; // Target <= 1.0 us
    public double StabilizerSyndromeFidelityPercent { get; init; } = 99.82; // Target >= 99.5%
    public double DefectChainWeightSpanRatio { get; init; } = 0.42; // Target <= 0.50 (defect span < d/2)
    public double LatticeSurgeryMergeFidelityPercent { get; init; } = 99.45; // Target >= 99.0%
    public double LogicalErrorRatePerRound { get; init; } = 1.2e-8; // Target <= 1e-7 for d=5
    public bool IsBoundaryCommutationVerified { get; init; } = true;
}

public record SurfaceCodeLatticeEvaluation
{
    public required string LogicalQubitPatchUid { get; init; }
    public double LogicalSuppressionFactorLambda { get; init; }
    public double DecoderHeadroomMicroseconds { get; init; }
    public bool IsPhysicalErrorBelowThreshold { get; init; }
    public bool IsDecodingLatencyRealTimeBounded { get; init; }
    public bool IsDefectChainTolerated { get; init; }
    public bool IsLatticeSurgeryFaultTolerant { get; init; }
    public List<string> ActiveRegimes { get; init; } = new();
    public List<string> RecommendedMitigations { get; init; } = new();
    public DateTime EvaluatedAtUtc { get; init; } = DateTime.UtcNow;
}
