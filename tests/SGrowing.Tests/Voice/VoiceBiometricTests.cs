using SGrowing.Api.Modules.Voice.VoiceBiometric;
using Xunit;

namespace SGrowing.Tests.Voice;

public class VoiceBiometricTests
{
    [Fact]
    public void GetOverview_ReportsEnrolledProfilesAndBlockedDeepfakes()
    {
        var engine = new VoiceBiometricDefenseEngine();

        var overview = engine.GetOverview();

        Assert.NotNull(overview);
        Assert.True(overview.TotalEnrolledProfilesCount >= 2);
        Assert.True(overview.ActiveScannedCallsCount >= 2);
        Assert.True(overview.DeepfakeAttacksBlockedCount >= 1);
        Assert.True(overview.DeepfakeDefenseAccuracyPct >= 99.0);
        Assert.NotEmpty(overview.EnrolledProfiles);
        Assert.NotEmpty(overview.RecentSecurityAudits);
    }

    [Fact]
    public void VerifyAudio_DetectsPhaseDiscontinuityAndQuarantinesSyntheticDeepfake()
    {
        var engine = new VoiceBiometricDefenseEngine();
        var request = new VerifyAudioFrameRequest(
            "CALL-ATTACK-991",
            "+1-800-UNKNOWN-CALLER",
            1.24,
            2450.0,
            true // synthetic phase discontinuity detected
        );

        var audit = engine.VerifyAudio(request);

        Assert.NotNull(audit);
        Assert.StartsWith("AUD-SEC-", audit.AuditId);
        Assert.True(audit.SyntheticDeepfakeProbability >= 0.80);
        Assert.Equal("TERMINATED_QUARANTINED", audit.DefenseActionTaken);
        Assert.Equal("VOCODER_DIFFUSION_RESIDUAL_ARTIFACT", audit.VocoderArtifactSignature);

        var overview = engine.GetOverview();
        Assert.Contains(overview.RecentSecurityAudits, a => a.AuditId == audit.AuditId);
    }

    [Fact]
    public void VerifyAudio_AllowsCleanOrganicSpeechStream()
    {
        var engine = new VoiceBiometricDefenseEngine();
        var request = new VerifyAudioFrameRequest(
            "CALL-ORGANIC-102",
            "+1-415-555-8901",
            0.61,
            1418.0,
            false // organic continuous audio
        );

        var audit = engine.VerifyAudio(request);

        Assert.NotNull(audit);
        Assert.True(audit.SyntheticDeepfakeProbability < 0.20);
        Assert.Equal("VERIFIED_ALLOWED", audit.DefenseActionTaken);
        Assert.Equal("CLEAN_ORGANIC_VOCAL_TRACT", audit.VocoderArtifactSignature);
    }
}
