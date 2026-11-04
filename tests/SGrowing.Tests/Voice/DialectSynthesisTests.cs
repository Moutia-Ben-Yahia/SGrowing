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

    [Fact]
    public void GetProfile_KnownAndUnknownCodes_ReturnsExpected()
    {
        var aus = _engine.GetProfile("EN-AU-SYDNEY");
        Assert.NotNull(aus);
        Assert.Equal("Australian Colloquial", aus.DialectName);
        Assert.Equal(15.0, aus.PitchBaselineOffsetHz);
        Assert.Contains("g'day", aus.CharacteristicIdioms);

        var unknown = _engine.GetProfile("NON_EXISTENT_CODE");
        Assert.Null(unknown);
    }

    [Fact]
    public void DetectDialect_StandardEnglish_ReturnsStandardProfile()
    {
        var request = new DetectDialectRequest(
            CallId: "CALL-DIALECT-STD",
            CallerRawTranscript: "Good morning, I would like to inquire about enterprise licensing options.",
            DetectedPitchHz: 120.0,
            DetectedCadenceRate: 1.0
        );

        var result = _engine.DetectDialect(request);

        Assert.NotNull(result);
        Assert.Equal("EN-US-STANDARD", result.DetectedDialectCode);
        Assert.Equal(70, result.ConfidenceScore);
        Assert.Empty(result.ExtractedVernacularTokens);
    }

    [Fact]
    public void SynthesizeDialect_SouthernVernacular_ReplacesColloquialPhrases()
    {
        var request = new SynthesizeDialectRequest(
            CallId: "CALL-DIALECT-003",
            TargetDialectCode: "EN-US-SOUTH",
            StandardResponseText: "hello, do you all think this enterprise model fits?"
        );

        var utterance = _engine.SynthesizeDialect(request);

        Assert.NotNull(utterance);
        Assert.Equal("howdy, do y'all reckon this enterprise model fits?", utterance.DialectAdaptedText);
        Assert.Equal(-12.5, utterance.PitchModulationHz);
        Assert.Equal(0.92, utterance.CadenceMultiplier);
    }

    [Fact]
    public void SynthesizeDialect_UnknownDialect_PreservesOriginalTextWithDefaultAcoustics()
    {
        var request = new SynthesizeDialectRequest(
            CallId: "CALL-DIALECT-004",
            TargetDialectCode: "UNKNOWN-CODE",
            StandardResponseText: "This text should remain unaltered."
        );

        var utterance = _engine.SynthesizeDialect(request);

        Assert.NotNull(utterance);
        Assert.Equal("This text should remain unaltered.", utterance.DialectAdaptedText);
        Assert.Equal(0.0, utterance.PitchModulationHz);
        Assert.Equal(1.0, utterance.CadenceMultiplier);
    }

    [Fact]
    public void SynthesizeDialect_ScottishVernacular_ReplacesColloquialPhrases()
    {
        var request = new SynthesizeDialectRequest(
            CallId: "CALL-DIALECT-005",
            TargetDialectCode: "EN-GB-SCOTTISH",
            StandardResponseText: "yes, there is a small delay but no problem."
        );

        var utterance = _engine.SynthesizeDialect(request);

        Assert.NotNull(utterance);
        Assert.Equal("aye, there is a wee delay but nae bother.", utterance.DialectAdaptedText);
        Assert.Equal(8.0, utterance.PitchModulationHz);
        Assert.Equal(1.05, utterance.CadenceMultiplier);
    }
}
