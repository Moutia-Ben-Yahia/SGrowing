namespace SGrowing.Api.Modules.Security.CvQkdCoherentState;

public record QuantumChannelParameters(
    string LinkId,
    double FiberLengthKm = 25.0,
    double FiberAttenuationDbPerKm = 0.20,
    double ExcessNoiseSnu = 0.005, // Shot noise units
    double DetectionQuantumEfficiency = 0.65,
    double ElectronicNoiseSnu = 0.015,
    double ModulationVarianceSnu = 4.5
);

public record CvQkdSecurityThresholds(
    double MinAllowableSecretKeyRateBitsPerPulse = 0.001,
    double MaxAllowableExcessNoiseSnu = 0.012,
    double ReverseReconciliationEfficiency = 0.95
);

public record CvQkdEvaluationRequest(
    QuantumChannelParameters Channel,
    CvQkdSecurityThresholds Thresholds
);

public record CvQkdSecurityAssessment(
    string LinkId,
    double ChannelTransmissionFraction,
    double MutualInformationAliceBobBits,
    double HolevoBoundEveBits,
    double AsymptoticSecretKeyRateBitsPerPulse,
    bool QuantumKeyExchangeSecure,
    string SecurityState,
    string RecommendedProtocolDirective
);
