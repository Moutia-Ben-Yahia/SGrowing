using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;

namespace SGrowing.Api.Modules.Voice.VoiceBiometric;

public class VoiceBiometricDefenseEngine
{
    private readonly ConcurrentDictionary<string, VoiceBiometricProfile> _profiles = new();
    private readonly ConcurrentBag<CallSecurityAuditRecord> _audits = new();

    public VoiceBiometricDefenseEngine()
    {
        SeedDemoData();
    }

    private void SeedDemoData()
    {
        var p1 = new VoiceBiometricProfile(
            "BIO-PROF-001",
            "+1-415-555-8901",
            "Alexander Vance (CEO)",
            0.62,
            1420.5,
            "VP-SHA256-48BA192F01C",
            DateTime.UtcNow.AddMonths(-6),
            "ACTIVE_VERIFIED"
        );

        var p2 = new VoiceBiometricProfile(
            "BIO-PROF-002",
            "+1-212-555-4422",
            "Beatrice Sterling (CFO)",
            0.58,
            1680.2,
            "VP-SHA256-99DF021C88B",
            DateTime.UtcNow.AddMonths(-4),
            "ACTIVE_VERIFIED"
        );

        _profiles[p1.ProfileId] = p1;
        _profiles[p2.ProfileId] = p2;

        _audits.Add(new CallSecurityAuditRecord(
            "AUD-SEC-001",
            "CALL-SESSION-890",
            "+1-415-555-8901",
            0.94,
            0.02,
            "CLEAN_ORGANIC_VOCAL_TRACT",
            "VERIFIED_ALLOWED",
            DateTime.UtcNow.AddMinutes(-35)
        ));

        _audits.Add(new CallSecurityAuditRecord(
            "AUD-SEC-002",
            "CALL-SESSION-912",
            "+1-800-SUSPICIOUS",
            0.42,
            0.96,
            "VOCODER_DIFFUSION_RESIDUAL_ARTIFACT",
            "TERMINATED_QUARANTINED",
            DateTime.UtcNow.AddMinutes(-10)
        ));
    }

    public VoiceBiometricOverview GetOverview()
    {
        var profilesList = _profiles.Values.ToList();
        var auditsList = _audits.ToList();

        int blockedCount = auditsList.Count(a => a.DefenseActionTaken == "TERMINATED_QUARANTINED");

        return new VoiceBiometricOverview(
            profilesList.Count,
            auditsList.Count,
            blockedCount,
            99.4,
            profilesList,
            auditsList
        );
    }

    public CallSecurityAuditRecord VerifyAudio(VerifyAudioFrameRequest request)
    {
        var auditId = $"AUD-SEC-{Guid.NewGuid():N}"[..12].ToUpperInvariant();
        double deepfakeProb = request.ContainsPhaseDiscontinuity ? 0.94 : 0.05;
        double biometricMatch = request.ContainsPhaseDiscontinuity ? 0.38 : 0.92;

        string action = deepfakeProb switch
        {
            >= 0.80 => "TERMINATED_QUARANTINED",
            >= 0.40 => "CHALLENGE_STEP_UP",
            _ => "VERIFIED_ALLOWED"
        };

        string artifact = request.ContainsPhaseDiscontinuity
            ? "VOCODER_DIFFUSION_RESIDUAL_ARTIFACT"
            : "CLEAN_ORGANIC_VOCAL_TRACT";

        var record = new CallSecurityAuditRecord(
            auditId,
            request.CallSessionId,
            request.CallerIdentifier,
            biometricMatch,
            deepfakeProb,
            artifact,
            action,
            DateTime.UtcNow
        );

        _audits.Add(record);
        return record;
    }

    public CallSecurityAuditRecord QuarantineCall(QuarantineCallRequest request)
    {
        var auditId = $"AUD-SEC-{Guid.NewGuid():N}"[..12].ToUpperInvariant();
        var record = new CallSecurityAuditRecord(
            auditId,
            request.CallSessionId,
            "MANUAL_TRIGGER",
            0.0,
            1.0,
            $"SUPERVISOR_QUARANTINE_OVERRIDE ({request.Reason})",
            "TERMINATED_QUARANTINED",
            DateTime.UtcNow
        );

        _audits.Add(record);
        return record;
    }
}
