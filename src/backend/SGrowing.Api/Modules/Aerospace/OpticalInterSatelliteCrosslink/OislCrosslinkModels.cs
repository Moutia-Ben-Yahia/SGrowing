namespace SGrowing.Api.Modules.Aerospace.OpticalInterSatelliteCrosslink;

public record OislCrosslinkTelemetry
{
    public required string OpticalTerminalPairId { get; init; }
    public string OrbitRegime { get; init; } = "LowEarthOrbit_LEO"; // LEO, MEO, GEO
    public double CrosslinkRangeKilometers { get; init; } = 2850.0; // 1500 - 6500 km
    public double OpticalLinkMarginDecibels { get; init; } = 5.8; // Target >= 4.5 dB
    public double FastSteeringMirrorJitterMicroRadians { get; init; } = 0.62; // Target <= 0.85 urad
    public double BeaconAcquisitionLockTimeSeconds { get; init; } = 8.4; // Target <= 12.0 s
    public double RelativeDopplerFrequencyShiftGigaHertz { get; init; } = 6.4; // +/- 12 GHz
    public double DopplerFrequencySlewRateMegaHertzPerSecond { get; init; } = 8.2; // Target <= 15 MHz/s
    public double CoherentBitErrorRate { get; init; } = 1.2e-9; // Target <= 1e-7
    public double DataThroughputGigaBitsPerSecond { get; init; } = 100.0; // 100+ Gbps
}

public record OislCrosslinkEvaluation
{
    public required string OpticalTerminalPairId { get; init; }
    public double ThreeSigmaPointingJitterMicroRadians { get; init; }
    public double BitErrorRateLogMargin { get; init; }
    public bool IsLinkMarginAdequate { get; init; }
    public bool IsPointingJitterTolerated { get; init; }
    public bool IsDopplerTrackingLocked { get; init; }
    public bool IsCrosslinkPatGoForTransmission { get; init; }
    public List<string> ActiveRegimes { get; init; } = new();
    public List<string> RecommendedMitigations { get; init; } = new();
    public DateTime EvaluatedAtUtc { get; init; } = DateTime.UtcNow;
}
