using SGrowing.Api.Modules.Voice.DialectSynthesis;
using Xunit;

namespace SGrowing.Tests.Voice;

public class DialectSynthesisTests
{
    private readonly DialectSynthesisEngine _engine = new();

    [Fact]
    public void SeededProfiles_IncludeAcousticPitchAndCadenceParameters()
    {
        var profiles = _engine.GetProfiles();
        Assert.NotEmpty(profiles);

        var south = profiles.FirstOrDefault(p => p.DialectCode == "EN-US-SOUTH");
        Assert.NotNull(south);
        Assert.NotEmpty(south.CharacteristicIdioms);
        Assert.True(south.SpeechCadenceMultiplier < 1.0);
    }

    [Fact]
    public void DetectDialect_IdentifiesVernacularTokens()
    {
        var request = new DetectDialectRequest(
            CallId: "CALL-DIALECT-001",
            CallerRawTranscript: "Well howdy there, I reckon y'all can help me with this problem.",
            DetectedPitchHz: 165.0,
            DetectedCadenceRate: 0.94
        );

        var result = _engine.DetectDialect(request);

        Assert.NotNull(result);
        Assert.Equal("EN-US-SOUTH", result.DetectedDialectCode);
        Assert.True(result.ConfidenceScore >= 90);
        Assert.Contains("y'all", result.ExtractedVernacularTokens);
    }

    [Fact]
    public void SynthesizeDialect_AdaptsColloquialPhrasingUnderTargetLatency()
    {
        var request = new SynthesizeDialectRequest(
            CallId: "CALL-DIALECT-002",
            TargetDialectCode: "EN-GB-SCOTTISH",
            StandardResponseText: "There is a small delay, but yes we will solve it with no problem."
        );

        var utterance = _engine.SynthesizeDialect(request);

        Assert.NotNull(utterance);
        Assert.Contains("wee", utterance.DialectAdaptedText);
        Assert.Contains("nae bother", utterance.DialectAdaptedText);
        Assert.True(utterance.SynthesisLatencyMs <= 45); // Sub-45ms real-time target
    }
}
