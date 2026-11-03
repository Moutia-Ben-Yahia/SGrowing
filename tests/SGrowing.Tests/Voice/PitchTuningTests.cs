using System;
using System.Collections.Generic;
using FluentAssertions;
using SGrowing.Api.Modules.Voice.PitchTuning;
using Xunit;

namespace SGrowing.Tests.Voice;

public class PitchTuningTests
{
    private readonly PitchTuningEngine _engine = new();

    [Fact]
    public void GetOverview_ReturnsActiveTelephonyCalls()
    {
        var overview = _engine.GetOverview();

        overview.Should().NotBeNull();
        overview.ActiveCallsCount.Should().Be(3);
        overview.ConversionLiftPercentage.Should().BeGreaterThan(20.0);
        overview.ActiveCalls.Should().NotBeEmpty();
    }

    [Fact]
    public void TunePitch_AppliesAcousticModulation()
    {
        var command = new TunePitchCommand(
            CallId: "CALL-VOICE-9801",
            TargetPitchShiftHz: -4.5,
            TargetSpeechRateWpm: 125,
            TargetPersona: TonePersona.Empathic,
            TacticalPitchAngle: "De-escalate pricing friction with deferred payment terms"
        );

        var result = _engine.TunePitch(command);

        result.Should().NotBeNull();
        result.CallId.Should().Be("CALL-VOICE-9801");
        result.AppliedPitchShiftHz.Should().Be(-4.5);
        result.AppliedSpeechRateWpm.Should().Be(125);
        result.AppliedPersona.Should().Be(TonePersona.Empathic);
        result.ProjectedDeescalationLikelihood.Should().BeGreaterThan(0.85);
        result.TuningLog.Should().Contain("Real-Time Speech DSP");
    }

    [Fact]
    public void SimulateSentimentShift_AdaptsPersonaAndPivotAngle()
    {
        var updated = _engine.SimulateSentimentShift("CALL-VOICE-9801", AcousticSentiment.Enthusiastic);

        updated.Should().NotBeNull();
        updated.CurrentSentiment.Should().Be(AcousticSentiment.Enthusiastic);
        updated.CurrentPersona.Should().Be(TonePersona.Assertive);
        updated.SpeechRateWpm.Should().Be(160);
    }
}
