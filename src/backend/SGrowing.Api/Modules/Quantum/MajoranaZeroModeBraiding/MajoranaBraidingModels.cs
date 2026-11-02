namespace SGrowing.Api.Modules.Quantum.MajoranaZeroModeBraiding;

public record MajoranaBraidingTelemetry
{
    public required string NanowireDeviceUid { get; init; }
    public string SemiconductorHeterostructure { get; init; } = "InAs_Al_EpitaxialNanowire"; // or "InSb_Al"
    public double InPlaneZeemanFieldTesla { get; init; } = 0.85; // 0.6 - 1.2 T
    public double ZeroBiasConductanceMicroSiemens { get; init; } = 76.8; // Target ~ 2e^2/h = 77.48 uS
    public double TopologicalSuperconductingGapMicroEv { get; init; } = 42.5; // Target >= 35.0 ueV
    public double QuasiparticlePoisoningRateKiloHertz { get; init; } = 0.85; // Target <= 1.2 kHz
    public double MajoranaBraidingPhaseErrorDegrees { get; init; } = 0.95; // Target <= 1.8 deg
    public double BraidingOperationFidelityPercent { get; init; } = 99.4; // Target >= 99.0%
    public double NanowireLengthMicrons { get; init; } = 1.8; // > 1.2 um to suppress MZM overlap
    public double TopologicalProtectionGapRatio { get; init; } = 0.38; // Delta_top / Delta_0 >= 0.25
}

public record MajoranaBraidingEvaluation
{
    public required string NanowireDeviceUid { get; init; }
    public double ConductanceQuantizationErrorPercent { get; init; }
    public double ParityLifetimeMicroseconds { get; init; }
    public bool IsZeroBiasPeakQuantized { get; init; }
    public bool IsTopologicalGapOpen { get; init; }
    public bool IsParityLifetimeProtected { get; init; }
    public bool IsNonAbelianBraidingVerified { get; init; }
    public List<string> ActiveRegimes { get; init; } = new();
    public List<string> RecommendedMitigations { get; init; } = new();
    public DateTime EvaluatedAtUtc { get; init; } = DateTime.UtcNow;
}
