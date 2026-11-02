namespace SGrowing.Api.Modules.Quantum.SuperconductingQubitGateArbiter;

public record QubitPhysicalParameters(
    string QubitId,
    double FrequencyGhz = 5.15,
    double AnharmonicityMhz = -310.0,
    double T1RelaxationMicroseconds = 85.0,
    double T2DephasingMicroseconds = 70.0,
    double ReadoutFidelityPercent = 99.1
);

public record InterQubitCoupling(
    string QubitA,
    string QubitB,
    double StaticZzCouplingKhz,
    double ExchangeCouplingJKhz
);

public record GateSynthesisRequest(
    QubitPhysicalParameters TargetQubit,
    string GateType = "X_PI", // "X_PI", "X_HALF_PI", "Y_PI", "HADAMARD"
    double GateDurationNs = 24.0,
    IReadOnlyList<InterQubitCoupling>? Couplings = null
);

public record SynthesizedPulseWaveform(
    double PeakAmplitudeMv,
    double GaussianSigmaNs,
    double DragCoefficientBeta,
    double LeakageSuppressionDb
);

public record QuantumGateArbiterResult(
    string QubitId,
    string GateType,
    SynthesizedPulseWaveform Pulse,
    double PredictedGateFidelityPercent,
    double ResidualZzPhaseErrorRadians,
    bool CalibrationApproved,
    string CalibrationAction
);
