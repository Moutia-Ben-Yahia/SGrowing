using FluentAssertions;
using SGrowing.Api.Modules.Voice.SentimentDeescalation;
using Xunit;

namespace SGrowing.Tests.Voice;

public sealed class VoiceSentimentDeescalationTests
{
    private readonly AcousticSentimentClassifierEngine _classifier = new();
    private readonly AutonomousVoiceDeescalationCoordinator _coordinator = new();

    #region Acoustic Sentiment Classification Tests

    [Fact]
    public void ClassifyFrame_CalmSteadyCadence_ReturnsDelightedOrNeutral()
    {
        var frame = new VoiceCallAcousticFrame(
            CallId: "CALL-CALM-01",
            TimestampUtc: DateTime.UtcNow,
            PitchF0Hz: 125.0,
            BaselineF0Hz: 120.0,
            SpeechRateWordsPerMinute: 135.0,
            VolumeDb: 62.0,
            VocalJitterPercent: 0.8,
            NegativeSentimentKeywordCount: 0
        );

        var eval = _classifier.ClassifyFrame(frame);

        eval.Should().NotBeNull();
        eval.State.Should().BeOneOf(CallerEmotionalState.Delighted, CallerEmotionalState.NeutralCalm);
        eval.AgitationIndex.Should().BeLessThan(3.0);
        eval.SpeechTempoAdjustmentRatio.Should().BeInRange(0.95, 1.0);
        eval.RecommendedIntervention.Should().BeOneOf(DeescalationInterventionType.MaintainStandardPacing, DeescalationInterventionType.SlowDownTempoAndSoftenTone);
    }

    [Fact]
    public void ClassifyFrame_SeverePitchSpikeAndShouting_ClassifiesSevereAngerAndRecommendsSupervisor()
    {
        var frame = new VoiceCallAcousticFrame(
            CallId: "CALL-ANGRY-02",
            TimestampUtc: DateTime.UtcNow,
            PitchF0Hz: 260.0, // Significant pitch spike
            BaselineF0Hz: 110.0,
            SpeechRateWordsPerMinute: 210.0, // High verbal velocity
            VolumeDb: 84.0, // Loud shouting into mic
            VocalJitterPercent: 4.5,
            NegativeSentimentKeywordCount: 3 // "unacceptable", "furious", "refund"
        );

        var eval = _classifier.ClassifyFrame(frame);

        eval.State.Should().Be(CallerEmotionalState.SevereAngerEscalation);
        eval.AgitationIndex.Should().BeGreaterThanOrEqualTo(7.0);
        eval.RecommendedIntervention.Should().Be(DeescalationInterventionType.ImmediateSupervisorTakeover);
        eval.SpeechTempoAdjustmentRatio.Should().BeLessThan(0.85, "Must slow down speech rate to calm caller.");
        eval.PitchModulationSemitones.Should().BeLessThan(0.0, "Must drop agent pitch register.");
        eval.SuggestedGoodwillCreditEur.Should().Be(25.00m);
    }

    #endregion

    #region De-escalation Dispatch Coordinator Tests

    [Fact]
    public void ExecuteIntervention_SupervisorTakeover_DispatchesTransferEvent()
    {
        var eval = new EmotionalStateEvaluation(
            CallId: "CALL-CRITICAL-99",
            EvaluatedAtUtc: DateTime.UtcNow,
            State: CallerEmotionalState.SevereAngerEscalation,
            AgitationIndex: 8.5,
            RecommendedIntervention: DeescalationInterventionType.ImmediateSupervisorTakeover,
            SpeechTempoAdjustmentRatio: 0.80,
            PitchModulationSemitones: -2.5,
            SuggestedGoodwillCreditEur: 25.00m,
            Rationale: "Severe vocal agitation detected."
        );

        var dispatch = _coordinator.ExecuteIntervention(eval);

        dispatch.Should().NotBeNull();
        dispatch.CallId.Should().Be("CALL-CRITICAL-99");
        dispatch.TransferredToHumanSupervisor.Should().BeTrue();
        dispatch.ActionSummary.Should().Contain("Retention Supervisor");

        _coordinator.GetLastDispatch("CALL-CRITICAL-99").Should().NotBeNull();
    }

    #endregion
}
