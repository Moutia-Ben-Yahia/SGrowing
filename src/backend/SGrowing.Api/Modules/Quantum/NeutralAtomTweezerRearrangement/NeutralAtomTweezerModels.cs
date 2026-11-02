namespace SGrowing.Api.Modules.Quantum.NeutralAtomTweezerRearrangement;

public sealed record NeutralAtomTweezerTelemetry
{
    public required string RegisterId { get; init; }
    public string AtomicSpecies { get; init; } = "Rb-87"; // e.g. Rb-87 or Yb-171
    public int TargetSitesCount { get; init; } = 100;
    public int InitialLoadedAtomsCount { get; init; } = 160;
    public double TrapWaistUm { get; init; } = 1.05; // beam waist in microns
    public double TrapFrequencyKhz { get; init; } = 120.0; // radial trap frequency in kHz
    public double MaxShuttleVelocityMmPerSec { get; init; } = 55.0; // mm/s
    public double MaxShuttleAccelerationMPerSec2 { get; init; } = 450.0; // m/s^2
    public double MinInterAtomClearanceUm { get; init; } = 4.2; // minimum distance to static traps
    public double TotalSortingDurationMs { get; init; } = 45.0; // ms
    public double VacuumLifetimeSec { get; init; } = 25.0; // background gas collision lifetime
    public int UnfilledDefectVacanciesCount { get; init; } = 0;
}

public sealed record NeutralAtomTweezerAnalysisResult
{
    public required string RegisterId { get; init; }
    public required double InitialLoadingFraction { get; init; }
    public required double AdiabaticityParameterGamma { get; init; }
    public required double MotionalHeatingRateNbarPerSec { get; init; }
    public required double BackgroundVacuumSurvivalProbability { get; init; }
    public required double TotalWavepacketSurvivalProbability { get; init; }
    public required double MinimumClearanceSeparationUm { get; init; }
    public required double VacancyDefectRatePercent { get; init; }
    public required bool IsCollisionlessSeparationMaintained { get; init; }
    public required bool IsAdiabaticTransportPreserved { get; init; }
    public required bool IsDefectFreeRegisterReady { get; init; }
    public required string RearrangementRegime { get; init; }
    public required string MitigationRecommendation { get; init; }
}
