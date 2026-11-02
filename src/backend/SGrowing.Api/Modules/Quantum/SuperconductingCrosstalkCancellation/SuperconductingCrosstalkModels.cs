namespace SGrowing.Api.Modules.Quantum.SuperconductingCrosstalkCancellation;

public sealed record SuperconductingCrosstalkTelemetry
{
    public required string ProcessorId { get; init; }
    public int ActiveQubitsCount { get; init; } = 54;
    public double MaxMutualFluxCrosstalkCoefficient { get; init; } = 0.045; // max off-diagonal M_ij
    public double ResidualStaticZzCouplingKhz { get; init; } = 14.5; // zeta_ZZ (kHz)
    public double TwoQubitExchangeCouplingMhz { get; init; } = 12.0; // J_12 (MHz)
    public double QubitAnharmonicityMhz { get; init; } = -225.0; // alpha (MHz)
    public double QubitQubitDetuningMhz { get; init; } = 180.0; // Delta_12 (MHz)
    public double SkinDepthFluxPulseDistortionPercent { get; init; } = 1.2; // pulse overshoot/tail (%)
    public double TargetCzGateFidelityPercent { get; init; } = 99.85; // F_CZ target
}

public sealed record SuperconductingCrosstalkAnalysisResult
{
    public required string ProcessorId { get; init; }
    public required double ConditionNumberOfMutualMatrix { get; init; }
    public required double CalculatedResidualZzCouplingKhz { get; init; }
    public required double ProjectedTwoQubitCzFidelityPercent { get; init; }
    public required double PulseDistortionInversionErrorBps { get; init; }
    public required bool IsFluxMatrixWellConditioned { get; init; }
    public required bool IsResidualZzCouplingSuppressed { get; init; }
    public required bool IsCzGateFidelityTargetMet { get; init; }
    public required string CrosstalkSuppressionRegime { get; init; }
    public required string CalibrationRecommendation { get; init; }
}
