namespace SGrowing.Api.Modules.Telecom.Terahertz6gBeamSquint;

public record UmMimoArrayConfig(
    string ArrayId,
    int TotalAntennaElements = 1024,
    double CarrierFrequencyGigaHertz = 220.0,
    double TransmissionBandwidthGigaHertz = 20.0,
    double TargetSteeringAngleDegrees = 45.0,
    int TrueTimeDelayUnitsCount = 16,
    double SubcarrierFrequencyGigaHertz = 230.0 // Edge subcarrier
);

public record BeamSquintToleranceLimits(
    double MaxAllowableBeamDriftDegrees = 2.5,
    double MaxAllowableArrayGainLossDb = 3.0
);

public record BeamSquintEvaluationRequest(
    UmMimoArrayConfig ArrayConfig,
    BeamSquintToleranceLimits Limits
);

public record BeamSquintMitigationPlan(
    string ArrayId,
    double UncompensatedBeamDriftDegrees,
    double EdgeSubcarrierArrayGainLossDb,
    double CompensatedBeamDriftDegrees,
    bool BeamSquintExceedsTolerance,
    int OptimalTtdSubarraysCount,
    string PrecodingArchitectureState,
    string RecommendedPrecodingDirective
);
