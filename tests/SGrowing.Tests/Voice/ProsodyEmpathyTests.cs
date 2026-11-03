using SGrowing.Api.Modules.Voice.ProsodyEmpathy;
using Xunit;

namespace SGrowing.Tests.Voice;

public sealed class ProsodyEmpathyTests
{
    private readonly ProsodyEmpathyEngine _engine = new();

    [Fact]
    public void GetOverview_ReturnsSeededProfilesAndMetrics()
    {
        var overview = _engine.GetOverview();

        Assert.NotNull(overview);
        Assert.True(overview.ActiveCallsWithToneShiftingCount >= 10);
        Assert.True(overview.CustomerDeEscalationSuccessRatePct > 80.0);
        Assert.NotEmpty(overview.RecentToneEvents);
        Assert.NotEmpty(overview.ActiveProfiles);
    }

    [Fact]
    public void ShiftToneForCaller_FrustratedCaller_AppliesCalmingDeceleration()
    {
        var evt = _engine.ShiftToneForCaller("CALL-SIP-9912", CallerEmotionState.FrustratedAngry, 0.98);

        Assert.NotNull(evt);
        Assert.StartsWith("TONE-EVT-", evt.EventId);
        Assert.Equal(-1.5, evt.AppliedPitchShiftSemitones);
        Assert.Equal(0.88, evt.AppliedTempoAdjustmentRatio); // Slower, calming tempo
        Assert.Equal("DeepCalmingEmpathyFilter", evt.AppliedTimbreFilterName);
    }

    [Fact]
    public void ClassifyUtteranceEmotion_AngryKeywords_ClassifiesFrustrated()
    {
        var emotion = _engine.ClassifyUtteranceEmotion("This billing mistake is completely unacceptable and ridiculous!", speechEnergyRms: 0.85);

        Assert.Equal(CallerEmotionState.FrustratedAngry, emotion);
    }
}
