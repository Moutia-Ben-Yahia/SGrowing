using System;

namespace SGrowing.Api.Modules.Security.CvQkdExcessNoiseSecretKeyRate;

public sealed record CvQkdTelemetry(
    string FiberLinkId,
    double FiberDistanceKilometers,
    double FiberAttenuationDbPerKm,
    double AliceModulationVarianceSnu,
    double MeasuredTotalNoiseVarianceSnu,
    double DetectorQuantumEfficiency,
    double ElectronicNoiseVarianceSnu,
    double ReconciliationEfficiencyBeta,
    double PulseRepetitionRateMhz);

public sealed record CvQkdAnalysisResult(
    string FiberLinkId,
    double ChannelTransmittance,
    double ExcessNoiseSnu,
    double MutualInformationBits,
    double HolevoBoundEveInformationBits,
    double AsymptoticSecretKeyRateBitsPerPulse,
    double SecretKeyThroughputKbps,
    string ChannelSecurityStatus,
    string ProtocolActionRecommendation,
    bool IsKeyExchangeSecure,
    DateTimeOffset Timestamp);
