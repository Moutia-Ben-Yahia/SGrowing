namespace SGrowing.Api.Modules.Security.ZtnaContinuousPosture;

public enum ZtnaTrustLevel
{
    HighTrust,
    ConditionalTrust,
    ZeroTrustRevoked
}

public sealed record DevicePostureSignal(
    string DeviceId,
    bool IsCompliantOsVersion,
    bool HasActiveTpmEnclave,
    bool IsJailbrokenOrRooted,
    bool EndpointProtectionActive
);

public sealed record SessionAccessTelemetry(
    string SessionId,
    string UserId,
    string TenantId,
    DateTime TimestampUtc,
    double Latitude,
    double Longitude,
    string IpAddress,
    int RequestsPerMinute,
    DevicePostureSignal DevicePosture
);

public sealed record TrustAssessmentResult(
    string SessionId,
    string UserId,
    double TrustScore, // 0.0 to 1.0
    ZtnaTrustLevel TrustLevel,
    bool IsSessionRevoked,
    IReadOnlyList<string> SecurityAnomaliesDetected,
    string PolicyVerdict,
    DateTime EvaluatedAtUtc
);

public sealed record GeovelocityCheckResult(
    bool IsImpossibleTravel,
    double DistanceKm,
    TimeSpan TimeElapsed,
    double ImpliedVelocityKmH,
    string Message
);
