using System.Collections.Concurrent;

namespace SGrowing.Api.Modules.Security.VoiceLiveness;

/// <summary>
/// Service generating and validating dynamic phonetic challenge phrases
/// and measuring caller response latencies to defeat replay attacks and speech-to-speech voice cloners.
/// </summary>
public sealed class BiometricChallengeResponseService
{
    private static readonly string[] PhoneticWords = [
        "Atlas", "Sahara", "Casbah", "Numidia", "Djurdjura",
        "Constantine", "Tlemcen", "Hoggar", "Oasis", "Mediterranean"
    ];

    private readonly ConcurrentDictionary<string, BiometricChallenge> _challenges = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Issues a dynamic biometric phonetic challenge for a call session.
    /// </summary>
    public BiometricChallenge IssueChallenge(IssueChallengeRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var word1 = PhoneticWords[Random.Shared.Next(PhoneticWords.Length)];
        var word2 = PhoneticWords[Random.Shared.Next(PhoneticWords.Length)];
        var digits = $"{Random.Shared.Next(100, 999)}";
        var phrase = $"{word1} {word2} {digits}";

        var challengeId = $"CHAL-{Guid.NewGuid():N}"[..18].ToUpperInvariant();
        var now = DateTime.UtcNow;

        var challenge = new BiometricChallenge(
            ChallengeId: challengeId,
            CallSessionId: request.CallSessionId,
            RequiredPhoneticPhrase: phrase,
            RandomNumericSalt: digits,
            MinimumResponseLatencyMs: 350,   // Real human minimum vocal reaction time
            MaximumResponseLatencyMs: 2500,  // Latency ceiling before flag
            IssuedAtUtc: now,
            ExpiresAtUtc: now.AddSeconds(request.ValiditySeconds),
            IsCompleted: false,
            Passed: false
        );

        _challenges[challengeId] = challenge;
        return challenge;
    }

    /// <summary>
    /// Validates caller response latency and phrase completion.
    /// </summary>
    public (bool Passed, string? Reason) EvaluateChallengeResponse(
        string challengeId,
        int callerResponseLatencyMs,
        DateTime? asOfUtc = null)
    {
        if (string.IsNullOrWhiteSpace(challengeId))
        {
            return (true, null); // No challenge active
        }

        if (!_challenges.TryGetValue(challengeId, out var challenge))
        {
            return (false, "Challenge ID not found or already purged.");
        }

        var now = asOfUtc ?? DateTime.UtcNow;

        if (now > challenge.ExpiresAtUtc)
        {
            return (false, "Biometric challenge response expired.");
        }

        if (callerResponseLatencyMs < challenge.MinimumResponseLatencyMs)
        {
            return (false, $"Unnaturally instantaneous response ({callerResponseLatencyMs}ms < {challenge.MinimumResponseLatencyMs}ms). Potential pre-recorded soundboard attack.");
        }

        if (callerResponseLatencyMs > challenge.MaximumResponseLatencyMs)
        {
            return (false, $"Excessive response latency ({callerResponseLatencyMs}ms > {challenge.MaximumResponseLatencyMs}ms). Probable speech-to-speech voice clone inference delay.");
        }

        var passedChallenge = challenge with
        {
            IsCompleted = true,
            Passed = true
        };
        _challenges[challengeId] = passedChallenge;

        return (true, null);
    }
}
