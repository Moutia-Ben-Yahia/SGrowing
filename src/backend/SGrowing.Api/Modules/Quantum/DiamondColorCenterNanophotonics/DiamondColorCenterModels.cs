namespace SGrowing.Api.Modules.Quantum.DiamondColorCenterNanophotonics;

public sealed record DiamondColorCenterTelemetry
{
    public required string CavityId { get; init; }
    public string DefectType { get; init; } = "SnV-"; // e.g. "SiV-", "SnV-", "PbV-"
    public double CavityQualityFactorQ { get; init; } = 35000.0; // Q-factor
    public double ModeVolumeVLambdaOverNCubed { get; init; } = 0.55; // V in (lambda/n)^3
    public double ZplWavelengthNm { get; init; } = 619.0; // ZPL wavelength (SnV- ~ 619 nm, SiV- ~ 737 nm)
    public double DetuningGhz { get; init; } = 1.2; // Delta omega / (2*pi)
    public double DipoleOrientationAlignmentFactor { get; init; } = 0.94; // |E * mu|^2 / (|E|^2 |mu|^2)
    public double IntrinsicDebyeWallerFactor { get; init; } = 0.55; // DW0 (fraction into ZPL)
    public double OperatingTemperatureKelvin { get; init; } = 2.0; // Kelvin
    public double GroundStateSplittingGhz { get; init; } = 850.0; // Delta_gs (SnV- ~ 850 GHz, SiV- ~ 50 GHz)
}

public sealed record DiamondColorCenterAnalysisResult
{
    public required string CavityId { get; init; }
    public required double PurcellFactorFp { get; init; }
    public required double GuidedModeBranchingRatioBetaZpl { get; init; }
    public required double SpontaneousEmissionRateEnhancement { get; init; }
    public required double PhononOrbitalDephasingRateKhz { get; init; }
    public required double SpinCoherenceTimeEstimateMs { get; init; }
    public required bool IsPurcellEnhancementSufficient { get; init; }
    public required bool IsGuidedBranchingRatioOptimal { get; init; }
    public required bool IsOrbitalDephasingSuppressed { get; init; }
    public required string OpticalInterfaceRegime { get; init; }
    public required string NanophotonicRecommendation { get; init; }
}
