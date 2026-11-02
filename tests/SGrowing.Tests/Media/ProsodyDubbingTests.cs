using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using SGrowing.Api.Modules.Media.ProsodyDubbing;
using Xunit;

namespace SGrowing.Tests.Media;

public sealed class ProsodyDubbingTests
{
    private readonly ProsodyDubbingAlignmentEngine _engine = new();

    [Fact]
    public void AlignDubbingProsody_AcceptableTimeScaleWarp_PreservesProsodyAndEnforcesEbuR128()
    {
        // Arrange: Spanish dubbed speech is 5000ms, scene video window is 4200ms (warp factor ~0.84, within 0.75-1.30 limit)
        var phonemes = new List<PhonemeTimingSegment>
        {
            new("B", 0, 100, 120.0, 0.8),
            new("w", 100, 250, 130.0, 0.9),
            new("e", 250, 400, 125.0, 0.95),
            new("n", 400, 500, 115.0, 0.7)
        };

        var lipAnchors = new List<VideoSceneLipAnchor>
        {
            new("SCENE-01", 0, 4200, 0.75)
        };

        var request = new AlignProsodyDubbingRequest(
            ProjectId: "PRJ-DUB-ES",
            SourceLanguage: "en-US",
            TargetLanguage: "es-ES",
            VideoSceneDurationMs: 4200,
            SynthesizedSpeechDurationMs: 5000,
            MeasuredLoudnessLufs: -28.5, // 5.5 dB too quiet
            Phonemes: phonemes,
            LipAnchors: lipAnchors
        );

        // Act
        var plan = _engine.AlignDubbingProsody(request);

        // Assert
        plan.Should().NotBeNull();
        plan.TimeScaleWarpFactor.Should().BeApproximately(0.84, 0.01);
        plan.AdjustedDurationMs.Should().Be(4200);
        plan.IsLipSyncFeasible.Should().BeTrue();
        plan.RequiresTextRewrite.Should().BeFalse();
        plan.LoudnessGainDb.Should().BeApproximately(5.5, 0.01);
        plan.TargetLoudnessLufs.Should().Be(-23.0);
        plan.SynchronizedFrames.Should().HaveCount(4);
        plan.SynchronizedFrames[0].AdjustedStartMs.Should().Be(0);
        plan.SynchronizedFrames[3].AdjustedEndMs.Should().Be(420); // 500ms * 0.84 = 420ms
    }

    [Fact]
    public void AlignDubbingProsody_ExcessiveSpeechLength_SuggestsTextCondensation()
    {
        // Arrange: Audio is 6000ms but video scene cut occurs at 3000ms (warp factor 0.50, exceeds 0.75 limit)
        var request = new AlignProsodyDubbingRequest(
            ProjectId: "PRJ-DUB-DE",
            SourceLanguage: "en-US",
            TargetLanguage: "de-DE",
            VideoSceneDurationMs: 3000,
            SynthesizedSpeechDurationMs: 6000,
            MeasuredLoudnessLufs: -23.0,
            Phonemes: [],
            LipAnchors: []
        );

        // Act
        var plan = _engine.AlignDubbingProsody(request);

        // Assert
        plan.IsLipSyncFeasible.Should().BeFalse();
        plan.RequiresTextRewrite.Should().BeTrue();
        plan.SuggestedRewriteAction.Should().StartWith("TEXT CONDENSATION REQUIRED");
    }

    [Fact]
    public void AlignDubbingProsody_ExcessiveShortSpeech_SuggestsTextElaboration()
    {
        // Arrange: Audio is 2000ms but video scene is 4000ms (warp factor 2.0, exceeds 1.30 limit)
        var request = new AlignProsodyDubbingRequest(
            ProjectId: "PRJ-DUB-JA",
            SourceLanguage: "en-US",
            TargetLanguage: "ja-JP",
            VideoSceneDurationMs: 4000,
            SynthesizedSpeechDurationMs: 2000,
            MeasuredLoudnessLufs: -23.0,
            Phonemes: [],
            LipAnchors: []
        );

        // Act
        var plan = _engine.AlignDubbingProsody(request);

        // Assert
        plan.IsLipSyncFeasible.Should().BeFalse();
        plan.RequiresTextRewrite.Should().BeTrue();
        plan.SuggestedRewriteAction.Should().StartWith("TEXT ELABORATION REQUIRED");
    }

    [Fact]
    public void AddProsodyDubbingServices_RegistersSingleton()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddProsodyDubbingServices();

        // Assert - Rule: NEVER call services.BuildServiceProvider()
        services.Should().ContainSingle(s => s.ServiceType == typeof(ProsodyDubbingAlignmentEngine) && s.Lifetime == ServiceLifetime.Singleton);
    }
}
