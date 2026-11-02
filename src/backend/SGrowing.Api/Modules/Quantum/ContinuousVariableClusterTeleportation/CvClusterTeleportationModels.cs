namespace SGrowing.Api.Modules.Quantum.ContinuousVariableClusterTeleportation;

public record CvClusterTeleportationTelemetry
{
    public required string ClusterNodeId { get; init; }
    public string OpticalFrequencyBand { get; init; } = "Telecom_1550nm"; // 1550 nm or 850 nm
    public double SqueezingParameterR { get; init; } = 1.25; // r >= 1.15 (~10.8 dB)
    public double SqueezingDecibels { get; init; } = 10.85; // dB = 8.686 * r
    public double HomodyneDetectorQuantumEfficiencyPercent { get; init; } = 94.5; // >= 92.0%
    public double OpticalFiberPropagationLossDbPerKm { get; init; } = 0.18; // <= 0.25 dB/km
    public double ClusterGraphNullifierVariance { get; init; } = 0.082; // <= 0.15 (classical > 0.5)
    public double CoincidentPhotonPumpPowerMilliWatts { get; init; } = 35.0;
    public double LocalOscillatorPhaseJitterDegrees { get; init; } = 0.85; // <= 2.0 deg
}

public record CvClusterTeleportationEvaluation
{
    public required string ClusterNodeId { get; init; }
    public double CalculatedTeleportationFidelityPercent { get; init; }
    public double CalculatedSqueezingDecibels { get; init; }
    public double ClusterEntanglementWitnessMetric { get; init; }
    public bool IsSqueezingSufficientForTeleportation { get; init; }
    public bool IsHomodyneDetectionOptimal { get; init; }
    public bool IsClusterNullifierVarianceBound { get; init; }
    public bool IsUniversalMbqcReady { get; init; }
    public List<string> ActiveRegimes { get; init; } = new();
    public List<string> RecommendedMitigations { get; init; } = new();
    public DateTime EvaluatedAtUtc { get; init; } = DateTime.UtcNow;
}
