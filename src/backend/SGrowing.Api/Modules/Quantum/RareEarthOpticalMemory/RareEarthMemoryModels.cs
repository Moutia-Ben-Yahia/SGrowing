namespace SGrowing.Api.Modules.Quantum.RareEarthOpticalMemory;

public record RareEarthMemoryTelemetry
{
    public required string CrystalId { get; init; }
    public string DopedIonType { get; init; } = "Eu3+:Y2SiO5"; // or "Pr3+:Y2SiO5"
    public double CryogenicTemperatureKelvin { get; init; } = 2.1; // Optimal < 3.5 K
    public double OpticalDepth { get; init; } = 2.5; // d = alpha * L
    public double UnburnedBackgroundAbsorptionDepth { get; init; } = 0.08; // d0 < 0.2
    public double CombToothSpacingMhz { get; init; } = 1.0; // Delta in MHz -> tau = 1/Delta
    public double CombFinesse { get; init; } = 4.2; // F = Delta / gamma >= 3.5
    public double ToothJitterPercent { get; init; } = 2.5; // <= 6.0%
    public double OpticalHomogeneousLinewidthKhz { get; init; } = 2.2; // Gamma_h <= 5.0 kHz
    public double SpinWaveStorageTimeSeconds { get; init; } = 1.2; // With dynamical decoupling
    public double InputSinglePhotonSnr { get; init; } = 28.5; // >= 20.0
}

public record RareEarthMemoryEvaluation
{
    public required string CrystalId { get; init; }
    public double CalculatedAfcEfficiencyPercent { get; init; }
    public double StorageDelayTimeMicroseconds { get; init; }
    public double OutputPhotonFidelityPercent { get; init; }
    public double SpinWaveRetentionPercent { get; init; }
    public bool IsFinesseOptimal { get; init; }
    public bool IsBackgroundLossSuppressed { get; init; }
    public bool IsCryogenicTemperatureSafe { get; init; }
    public bool IsQuantumRepeaterReady { get; init; }
    public List<string> ActiveRegimes { get; init; } = new();
    public List<string> RecommendedMitigations { get; init; } = new();
    public DateTime EvaluatedAtUtc { get; init; } = DateTime.UtcNow;
}
