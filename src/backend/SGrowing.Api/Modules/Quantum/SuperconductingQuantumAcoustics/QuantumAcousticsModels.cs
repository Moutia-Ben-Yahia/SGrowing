namespace SGrowing.Api.Modules.Quantum.SuperconductingQuantumAcoustics;

public record QuantumAcousticsTelemetry
{
    public required string ResonatorUnitId { get; init; }
    public string PiezoelectricSubstrate { get; init; } = "LithiumNiobate_LiNbO3"; // or "Sapphire_Al2O3"
    public double ResonatorFrequencyGigaHertz { get; init; } = 4.85; // 3 - 6 GHz
    public double MechanicalQualityFactor { get; init; } = 6.2e6; // Target >= 5.0e6
    public double PhononCoherenceLifetimeMicroseconds { get; init; } = 92.0; // Target >= 85 us
    public double TransmonAcousticCouplingMhz { get; init; } = 3.2; // g_m / 2pi >= 2.5 MHz
    public double CryostatPlateTemperatureMilliKelvin { get; init; } = 12.0; // <= 20 mK
    public double MeasuredWignerOriginValue { get; init; } = -0.52; // W(0,0) <= -0.45 for Fock |1>
    public int TargetPhononFockNumber { get; init; } = 1; // |1> state
    public double SinglePhononGenerationFidelityPercent { get; init; } = 95.8; // >= 94.0%
}

public record QuantumAcousticsEvaluation
{
    public required string ResonatorUnitId { get; init; }
    public double CalculatedThermalPhononOccupancy { get; init; }
    public double AcousticWignerNegativityMargin { get; init; }
    public double TransmonPhononCooperativityC { get; init; }
    public bool IsMechanicalQualityOptimal { get; init; }
    public bool IsThermalPhononBathSuppressed { get; init; }
    public bool IsNonClassicalWignerConfirmed { get; init; }
    public bool IsPhononFockStateSynthesisReady { get; init; }
    public List<string> ActiveRegimes { get; init; } = new();
    public List<string> RecommendedMitigations { get; init; } = new();
    public DateTime EvaluatedAtUtc { get; init; } = DateTime.UtcNow;
}
