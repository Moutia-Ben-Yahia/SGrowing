namespace SGrowing.Api.Modules.BioTech.DropletMicrofluidicSingleCell;

public record DropletMicrofluidicTelemetry
{
    public required string MicrofluidicChipRunId { get; init; }
    public string SurfactantChemistry { get; init; } = "Krytox_PEG_Fluorosurfactant_2wtPct";
    public double DropletGenerationFrequencyHertz { get; init; } = 3200.0; // 1500 - 5000 Hz
    public double DropletDiameterMicrons { get; init; } = 62.0; // 50 - 70 um
    public double CellPoissonLambda { get; init; } = 0.08; // Target 0.05 - 0.10
    public double MeasuredDoubletRatePercent { get; init; } = 1.65; // Target <= 2.5%
    public double BarcodedBeadLoadingFractionPercent { get; init; } = 88.5; // Target >= 85.0%
    public double EmulsionCoalescenceRatePercent { get; init; } = 0.045; // Target <= 0.10%
    public double InputCellConcentrationCellsPerMicroliter { get; init; } = 850.0; // Target <= 1200
    public double PostThermocyclingRnaRecoveryPercent { get; init; } = 91.2; // Target >= 85.0%
}

public record DropletMicrofluidicEvaluation
{
    public required string MicrofluidicChipRunId { get; init; }
    public double DropletVolumePicoLiters { get; init; }
    public double SingletPurityIndex { get; init; }
    public bool IsEmulsionPhysicallyStable { get; init; }
    public bool IsPoissonLoadingOptimal { get; init; }
    public bool IsBeadOccupancySufficient { get; init; }
    public bool IsSingleCellPartitioningCertified { get; init; }
    public List<string> ActiveRegimes { get; init; } = new();
    public List<string> RecommendedMitigations { get; init; } = new();
    public DateTime EvaluatedAtUtc { get; init; } = DateTime.UtcNow;
}
