using System.Collections.Concurrent;

namespace SGrowing.Api.Modules.Security.ZtnaContinuousPosture;

/// <summary>
/// Continuous Zero-Trust Network Access (ZTNA) behavioral posture attestation engine.
/// Evaluates continuous device telemetry and geovelocity signals, dynamically adjusting
/// trust scores and revoking compromised sessions instantly.
/// </summary>
public sealed class ContinuousTrustScoringEngine
{
    private const double HighTrustCutoff = 0.80;
    private const double ZeroTrustRevocationCutoff = 0.40;

    private readonly GeovelocityAnomalyDetectorService _geovelocityDetector;
    private readonly ConcurrentDictionary<string, double> _sessionScores = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, bool> _blacklistedSessions = new(StringComparer.OrdinalIgnoreCase);

    public ContinuousTrustScoringEngine(GeovelocityAnomalyDetectorService geovelocityDetector)
    {
        _geovelocityDetector = geovelocityDetector;
    }

    public TrustAssessmentResult EvaluateSessionAccess(SessionAccessTelemetry telemetry)
    {
        ArgumentNullException.ThrowIfNull(telemetry);

        if (_blacklistedSessions.ContainsKey(telemetry.SessionId))
        {
            return new TrustAssessmentResult(
                SessionId: telemetry.SessionId,
                UserId: telemetry.UserId,
                TrustScore: 0.0,
                TrustLevel: ZtnaTrustLevel.ZeroTrustRevoked,
                IsSessionRevoked: true,
                SecurityAnomaliesDetected: ["Session blacklisted due to prior critical zero-trust breach."],
                PolicyVerdict: "DENY: Connection terminated. Immediate re-authentication required.",
                EvaluatedAtUtc: DateTime.UtcNow
            );
        }

        var anomalies = new List<string>();
        var penalty = 0.0;

        // 1. Check Geovelocity
        var geoResult = _geovelocityDetector.CheckVelocity(
            telemetry.SessionId,
            telemetry.Latitude,
            telemetry.Longitude,
            telemetry.TimestampUtc
        );

        if (geoResult.IsImpossibleTravel)
        {
            anomalies.Add(geoResult.Message);
            penalty += 0.60;
        }

        // 2. Check Device Posture
        if (telemetry.DevicePosture.IsJailbrokenOrRooted)
        {
            anomalies.Add("CRITICAL POSTURE: Device operating system is rooted or jailbroken.");
            penalty += 0.50;
        }

        if (!telemetry.DevicePosture.EndpointProtectionActive)
        {
            anomalies.Add("WARNING: Host endpoint detection & response (EDR) agent is disabled.");
            penalty += 0.25;
        }

        if (!telemetry.DevicePosture.HasActiveTpmEnclave)
        {
            anomalies.Add("WARNING: Hardware TPM 2.0 security enclave not detected.");
            penalty += 0.15;
        }

        // 3. Check Request Rate Spikes (DDoS or scraping bot behavior)
        if (telemetry.RequestsPerMinute > 300)
        {
            anomalies.Add($"ANOMALOUS VELOCITY: Request rate ({telemetry.RequestsPerMinute} req/min) exceeds interactive threshold.");
            penalty += 0.35;
        }

        // Continuous Bayesian EWMA score update:
        // Current raw signal confidence = max(0.0, 1.0 - penalty)
        var signalConfidence = Math.Max(0.0, 1.0 - penalty);
        var currentScore = _sessionScores.GetOrAdd(telemetry.SessionId, 0.95); // Starts with high initial trust upon login
        var newScore = (0.60 * currentScore) + (0.40 * signalConfidence);

        // Critical posture override: Severe zero-trust breaches (rooted/jailbroken device or impossible travel) bypass inertia
        if (telemetry.DevicePosture.IsJailbrokenOrRooted || geoResult.IsImpossibleTravel)
        {
            newScore = Math.Min(newScore, signalConfidence);
        }

        _sessionScores[telemetry.SessionId] = Math.Round(newScore, 3);

        ZtnaTrustLevel level;
        bool isRevoked;
        string verdict;

        if (newScore < ZeroTrustRevocationCutoff)
        {
            level = ZtnaTrustLevel.ZeroTrustRevoked;
            isRevoked = true;
            _blacklistedSessions[telemetry.SessionId] = true;
            verdict = $"TERMINATE: Trust score ({newScore:F2} < {ZeroTrustRevocationCutoff}) breached zero-trust floor. Session immediately revoked.";
        }
        else if (newScore < HighTrustCutoff)
        {
            level = ZtnaTrustLevel.ConditionalTrust;
            isRevoked = false;
            verdict = $"STEP-UP REQUIRED: Trust score degraded to {newScore:F2}. Step-up MFA challenge or biometric re-attestation requested.";
        }
        else
        {
            level = ZtnaTrustLevel.HighTrust;
            isRevoked = false;
            verdict = $"PERMIT: High continuous trust ({newScore:F2} >= {HighTrustCutoff}). All sovereign microservices authorized.";
        }

        return new TrustAssessmentResult(
            SessionId: telemetry.SessionId,
            UserId: telemetry.UserId,
            TrustScore: Math.Round(newScore, 3),
            TrustLevel: level,
            IsSessionRevoked: isRevoked,
            SecurityAnomaliesDetected: anomalies,
            PolicyVerdict: verdict,
            EvaluatedAtUtc: DateTime.UtcNow
        );
    }

    public bool IsSessionRevoked(string sessionId) =>
        _blacklistedSessions.ContainsKey(sessionId);
}
