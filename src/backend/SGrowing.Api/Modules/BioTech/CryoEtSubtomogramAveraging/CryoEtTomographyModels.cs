namespace SGrowing.Api.Modules.BioTech.CryoEtSubtomogramAveraging;

public record CryoEtTomographyTelemetry
{
    public required string TiltSeriesDatasetId { get; init; }
    public string TargetMacromolecule { get; init; } = "Ribosome_70S_InSitu"; // Ribosome, NuclearPore, Microtubule
    public double MaxTiltAngleDegrees { get; init; } = 60.0; // +/- 60 deg
    public double TiltStepAngleDegrees { get; init; } = 2.0; // 2 - 3 deg
    public double CumulativeElectronDoseElectronsPerAngstrom2 { get; init; } = 95.0; // Target <= 120 e-/A^2
    public double CtfDefocusMicrons { get; init; } = 2.1; // 1.2 - 3.5 um
    public double BeamInducedDriftRateAngstromsPerFrame { get; init; } = 1.15; // <= 2.0 A/frame
    public double CrystallineIceDiffractionPeakIntensityArbitraryUnits { get; init; } = 0.04; // Vitreous <= 0.10
    public int SubtomogramParticlesCount { get; init; } = 14500;
    public double TargetFscCutoffResolutionAngstroms { get; init; } = 3.8; // Target <= 4.5 A
}

public record CryoEtTomographyEvaluation
{
    public required string TiltSeriesDatasetId { get; init; }
    public double CalculatedGlobalFscResolutionAngstroms { get; init; }
    public double MissingWedgeFourierFractionPercent { get; init; }
    public double DoseFractionationPreservationScore { get; init; }
    public bool IsSpecimenVitreousIceConfirmed { get; init; }
    public bool IsRadiationDamageControlled { get; init; }
    public bool IsBeamInducedDriftCompensated { get; init; }
    public bool IsHighResolutionReconstructionReady { get; init; }
    public List<string> ActiveRegimes { get; init; } = new();
    public List<string> RecommendedMitigations { get; init; } = new();
    public DateTime EvaluatedAtUtc { get; init; } = DateTime.UtcNow;
}
