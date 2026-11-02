namespace SGrowing.Api.Modules.Quantum.DualSpeciesNeutralAtomTweezers;

public record DualSpeciesTweezersTelemetry
{
    public required string TweezerArrayUnitId { get; init; }
    public string SpeciesPair { get; init; } = "Rb87_Cs133_DualSpecies"; // or "Rb87_Yb171"
    public double InteratomicDistanceMicrons { get; init; } = 4.2; // Target < R_blockade
    public double RydbergBlockadeRadiusMicrons { get; init; } = 6.8; // Target >= 6.5 um
    public double InterSpeciesCnotGateFidelityPercent { get; init; } = 99.45; // Target >= 99.2%
    public double AtomLossRatePerTweezerHopPercent { get; init; } = 0.012; // Target <= 0.015%
    public double MidCircuitImagingCrosstalkScatterPercent { get; init; } = 0.035; // Target <= 0.05%
    public int TotalTargetArrayAtomCount { get; init; } = 256;
    public double DefectFreeAssemblySuccessRatePercent { get; init; } = 98.2;
}

public record DualSpeciesTweezersEvaluation
{
    public required string TweezerArrayUnitId { get; init; }
    public double BlockadeRadiusMarginRatio { get; init; }
    public double ShufflingSurvivalProbabilityPercent { get; init; }
    public bool IsRydbergBlockadeEnforced { get; init; }
    public bool IsShufflingLossAcceptable { get; init; }
    public bool IsSpectatorCrosstalkSuppressed { get; init; }
    public bool IsDualSpeciesQubitArrayReady { get; init; }
    public List<string> ActiveRegimes { get; init; } = new();
    public List<string> RecommendedMitigations { get; init; } = new();
    public DateTime EvaluatedAtUtc { get; init; } = DateTime.UtcNow;
}
