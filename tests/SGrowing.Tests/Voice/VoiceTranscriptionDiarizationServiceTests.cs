using System.Collections.Concurrent;
using FluentAssertions;
using SGrowing.Api.Modules.Voice;
using Xunit;

namespace SGrowing.Tests.Voice;

public class VoiceTranscriptionDiarizationServiceTests
{
    private readonly VoiceTranscriptionDiarizationService _service;

    public VoiceTranscriptionDiarizationServiceTests()
    {
        _service = new VoiceTranscriptionDiarizationService();
    }

    [Fact]
    public async Task Diarization_SpeakerPartitioning_CorrectlySeparatesAgentAndCaller()
    {
        var callId = "CALL-TEST-DIAR-01";
        
        await _service.StreamUtteranceAsync(new StreamUtteranceRequest(
            CallId: callId,
            Speaker: SpeakerType.Agent,
            Text: "Hello, this is Marcus from customer support."
        ));

        await _service.StreamUtteranceAsync(new StreamUtteranceRequest(
            CallId: callId,
            Speaker: SpeakerType.Caller,
            Text: "Hi, I have a question regarding our API rate limits."
        ));

        var call = await _service.GetCallByIdAsync(callId);
        call.Should().NotBeNull();
        call!.Utterances.Should().HaveCount(2);

        call.Utterances[0].Speaker.Should().Be(SpeakerType.Agent);
        call.Utterances[1].Speaker.Should().Be(SpeakerType.Caller);
    }

    [Fact]
    public async Task UtteranceDuration_CalculatesAccuratelyFromStartAndEndOffsets()
    {
        var callId = "CALL-TEST-DUR-01";
        var utterance = await _service.StreamUtteranceAsync(new StreamUtteranceRequest(
            CallId: callId,
            Speaker: SpeakerType.Agent,
            Text: "Testing duration calculations.",
            StartOffsetMs: 5000,
            EndOffsetMs: 8500
        ));

        utterance.StartOffsetMs.Should().Be(5000);
        utterance.EndOffsetMs.Should().Be(8500);
        utterance.DurationMs.Should().Be(3500);
    }

    [Fact]
    public async Task WordConfidenceTokens_GeneratedAndBoundedWithinRealisticRange()
    {
        var callId = "CALL-TEST-WORDS-01";
        var utterance = await _service.StreamUtteranceAsync(new StreamUtteranceRequest(
            CallId: callId,
            Speaker: SpeakerType.Caller,
            Text: "Warehouse automation is working smoothly",
            StartOffsetMs: 0,
            EndOffsetMs: 3000,
            ConfidenceScore: 0.98
        ));

        utterance.Words.Should().NotBeEmpty();
        utterance.Words.Count.Should().Be(5);
        foreach (var word in utterance.Words)
        {
            word.Confidence.Should().BeInRange(0.70, 1.0);
            word.EndMs.Should().BeGreaterThan(word.StartMs);
        }
    }

    [Fact]
    public void LexicalSentiment_PositiveText_YieldsPositiveScore()
    {
        var text = "We are delighted with this fantastic and wonderful resolution. Thank you!";
        var score = _service.CalculateLexicalSentiment(text);

        score.Should().BeGreaterThan(0.5);
    }

    [Fact]
    public void LexicalSentiment_NegativeText_YieldsNegativeScore()
    {
        var text = "This is a terrible disaster and complete outage. We are furious.";
        var score = _service.CalculateLexicalSentiment(text);

        score.Should().BeLessThan(-0.5);
    }

    [Fact]
    public void LexicalSentiment_NegationWords_InvertsSentimentPolarity()
    {
        var positiveText = "We are happy and satisfied.";
        var negatedText = "We are not happy and not satisfied.";

        var posScore = _service.CalculateLexicalSentiment(positiveText);
        var negScore = _service.CalculateLexicalSentiment(negatedText);

        posScore.Should().BeGreaterThan(0.3);
        negScore.Should().BeLessThan(0.0);
    }

    [Fact]
    public void LexicalSentiment_Intensifiers_BoostsSentimentScoreMagnitude()
    {
        var standard = "This service is good.";
        var intensified = "This service is extremely good and really wonderful.";

        var standardScore = _service.CalculateLexicalSentiment(standard);
        var intensifiedScore = _service.CalculateLexicalSentiment(intensified);

        intensifiedScore.Should().BeGreaterThan(standardScore);
    }

    [Fact]
    public void LexicalSentiment_ExtremeNegative_ClampsAtNegativeOne()
    {
        var extremeText = "Fraud scam disaster unacceptable terrible horrible furious broken hate useless fail outage!";
        var score = _service.CalculateLexicalSentiment(extremeText);

        score.Should().BeInRange(-1.0, -0.7);
    }

    [Fact]
    public void LexicalSentiment_ExtremePositive_ClampsAtPositiveOne()
    {
        var extremeText = "Delighted fantastic wonderful perfect awesome love great seamless resolved impressed solved!";
        var score = _service.CalculateLexicalSentiment(extremeText);

        score.Should().BeInRange(0.7, 1.0);
    }

    [Fact]
    public void LexicalSentiment_NeutralText_ReturnsZero()
    {
        var neutral = "The webhook payload was transmitted over HTTP port 443.";
        var score = _service.CalculateLexicalSentiment(neutral);

        score.Should().Be(0.0);
    }

    [Fact]
    public void SentimentVelocity_CalculatesPositiveVelocity_WhenSentimentImproves()
    {
        double prevSentiment = -0.50;
        double currentSentiment = 0.50;
        double deltaSeconds = 10.0;

        var velocity = _service.CalculateSentimentVelocity(currentSentiment, prevSentiment, deltaSeconds);

        velocity.Should().Be(0.10); // (0.50 - (-0.50)) / 10 = 0.10
    }

    [Fact]
    public void SentimentVelocity_CalculatesNegativeVelocity_WhenSentimentDrops()
    {
        double prevSentiment = 0.40;
        double currentSentiment = -0.60;
        double deltaSeconds = 5.0;

        var velocity = _service.CalculateSentimentVelocity(currentSentiment, prevSentiment, deltaSeconds);

        velocity.Should().Be(-0.20); // (-0.60 - 0.40) / 5 = -0.20
    }

    [Fact]
    public void SentimentVelocity_WithZeroOrNegativeDeltaSeconds_UsesSafeFloor()
    {
        double prevSentiment = 0.20;
        double currentSentiment = 0.80;

        var velocityZero = _service.CalculateSentimentVelocity(currentSentiment, prevSentiment, 0.0);
        var velocityNegative = _service.CalculateSentimentVelocity(currentSentiment, prevSentiment, -2.5);

        // Max(deltaSeconds, 1.0) => denominator is 1.0
        velocityZero.Should().Be(0.60);
        velocityNegative.Should().Be(0.60);
    }

    [Fact]
    public void SentimentVelocity_WithNullPreviousSentiment_ReturnsZero()
    {
        var velocity = _service.CalculateSentimentVelocity(0.75, null, 15.0);
        velocity.Should().Be(0.0);
    }

    [Fact]
    public void SentimentTrajectory_ConsecutivePositiveTurns_ClassifiesAsImproving()
    {
        var utterances = new List<TranscribedUtterance>
        {
            new() { Speaker = SpeakerType.Caller, SentimentScore = -0.40 },
            new() { Speaker = SpeakerType.Agent, SentimentScore = 0.20 },
            new() { Speaker = SpeakerType.Caller, SentimentScore = 0.10 },
            new() { Speaker = SpeakerType.Caller, SentimentScore = 0.80 }
        };

        var trajectory = _service.DetermineTrajectory(utterances);
        trajectory.Should().Be(SentimentTrajectory.Improving);
    }

    [Fact]
    public void SentimentTrajectory_DecliningSentiment_ClassifiesAsDeteriorating()
    {
        var utterances = new List<TranscribedUtterance>
        {
            new() { Speaker = SpeakerType.Caller, SentimentScore = 0.40 },
            new() { Speaker = SpeakerType.Caller, SentimentScore = -0.10 },
            new() { Speaker = SpeakerType.Caller, SentimentScore = -0.70 }
        };

        var trajectory = _service.DetermineTrajectory(utterances);
        trajectory.Should().Be(SentimentTrajectory.Deteriorating);
    }

    [Fact]
    public void SentimentTrajectory_FlatSentiment_ClassifiesAsStable()
    {
        var utterances = new List<TranscribedUtterance>
        {
            new() { Speaker = SpeakerType.Caller, SentimentScore = 0.30 },
            new() { Speaker = SpeakerType.Caller, SentimentScore = 0.35 },
            new() { Speaker = SpeakerType.Caller, SentimentScore = 0.28 }
        };

        var trajectory = _service.DetermineTrajectory(utterances);
        trajectory.Should().Be(SentimentTrajectory.Stable);
    }

    [Fact]
    public void SentimentTrajectory_SingleUtterance_DefaultsToStable()
    {
        var utterances = new List<TranscribedUtterance>
        {
            new() { Speaker = SpeakerType.Caller, SentimentScore = -0.80 }
        };

        var trajectory = _service.DetermineTrajectory(utterances);
        trajectory.Should().Be(SentimentTrajectory.Stable);
    }

    [Fact]
    public void FrustrationAlert_TriggersOnHighRiskKeyword_Cancel()
    {
        var utterances = new List<TranscribedUtterance>
        {
            new() { Speaker = SpeakerType.Caller, SentimentScore = -0.40 }
        };

        var alert = _service.EvaluateFrustrationAndChurn(utterances, "I want to cancel our enterprise agreement immediately!");

        alert.IsActive.Should().BeTrue();
        alert.Severity.Should().Be(ChurnAlertSeverity.High);
        alert.TriggerKeywords.Should().Contain("cancel");
    }

    [Fact]
    public void FrustrationAlert_TriggersOnHighRiskKeywords_RefundAndUnacceptable()
    {
        var utterances = new List<TranscribedUtterance>();
        var alert = _service.EvaluateFrustrationAndChurn(utterances, "This unexpected charge is unacceptable and I demand an immediate refund.");

        alert.IsActive.Should().BeTrue();
        alert.TriggerKeywords.Should().Contain(new[] { "unacceptable", "refund" });
        alert.Severity.Should().Be(ChurnAlertSeverity.High);
    }

    [Fact]
    public void FrustrationAlert_TriggersOnTwoConsecutiveTurnsBelowThreshold()
    {
        var utterances = new List<TranscribedUtterance>
        {
            new() { Speaker = SpeakerType.Caller, SentimentScore = -0.65 },
            new() { Speaker = SpeakerType.Caller, SentimentScore = -0.72 }
        };

        // Text without explicit high risk keyword
        var alert = _service.EvaluateFrustrationAndChurn(utterances, "Your system has broken down again.");

        alert.IsActive.Should().BeTrue();
        alert.Severity.Should().Be(ChurnAlertSeverity.High);
        alert.TriggerReason.Should().Contain("consecutive turns");
    }

    [Fact]
    public void FrustrationAlert_CriticalSeverity_WhenBothKeywordsAndConsecutiveLowSentimentOccur()
    {
        var utterances = new List<TranscribedUtterance>
        {
            new() { Speaker = SpeakerType.Caller, SentimentScore = -0.70 },
            new() { Speaker = SpeakerType.Caller, SentimentScore = -0.85 }
        };

        var alert = _service.EvaluateFrustrationAndChurn(utterances, "This is unacceptable fraud and I need to speak to your supervisor!");

        alert.IsActive.Should().BeTrue();
        alert.Severity.Should().Be(ChurnAlertSeverity.Critical);
        alert.TriggerKeywords.Should().Contain(new[] { "unacceptable", "fraud", "supervisor" });
    }

    [Fact]
    public void FrustrationAlert_GeneratesActionableEmpathyGuidance_ForBillingAndCancellation()
    {
        var utterances = new List<TranscribedUtterance>();
        var alert = _service.EvaluateFrustrationAndChurn(utterances, "Cancel my subscription and issue a refund.");

        alert.EmpathyGuidanceTalkingPoints.Should().NotBeEmpty();
        alert.EmpathyGuidanceTalkingPoints.Any(p => p.Contains("credit", StringComparison.OrdinalIgnoreCase) || p.Contains("billing", StringComparison.OrdinalIgnoreCase)).Should().BeTrue();
    }

    [Fact]
    public void FrustrationAlert_GeneratesActionableEmpathyGuidance_ForSupervisorEscalation()
    {
        var utterances = new List<TranscribedUtterance>();
        var alert = _service.EvaluateFrustrationAndChurn(utterances, "Connect me with a supervisor right now.");

        alert.EmpathyGuidanceTalkingPoints.Should().NotBeEmpty();
        alert.EmpathyGuidanceTalkingPoints.Any(p => p.Contains("escalating", StringComparison.OrdinalIgnoreCase) || p.Contains("VP", StringComparison.OrdinalIgnoreCase)).Should().BeTrue();
    }

    [Fact]
    public async Task GeneratePostCallSummary_SynthesizesKeyDiscussionPointsAndAgreedActionItems()
    {
        var call = await _service.GetCallByIdAsync("CALL-8921-EXP");
        call.Should().NotBeNull();

        var summary = await _service.GeneratePostCallSummaryAsync("CALL-8921-EXP");

        summary.Should().NotBeNull();
        summary.KeyDiscussionPoints.Should().NotBeEmpty();
        summary.AgreedActionItems.Should().NotBeEmpty();
        summary.CustomerSentimentTrajectory.Should().Be(SentimentTrajectory.Improving);
        summary.CrmDealStage.Should().Be("Closed Won");
        summary.RecommendedNextStep.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task GeneratePostCallSummary_AssignsAtRisk_ForDeterioratingCallWithAlert()
    {
        var summary = await _service.GeneratePostCallSummaryAsync("CALL-5120-OUT");

        summary.CustomerSentimentTrajectory.Should().Be(SentimentTrajectory.Deteriorating);
        summary.CrmDealStage.Should().Be("At Risk");
        summary.ObjectionsRaised.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GeneratePostCallSummary_NonExistentCallId_ThrowsKeyNotFoundException()
    {
        var act = async () => await _service.GeneratePostCallSummaryAsync("NON-EXISTENT-CALL-ID");
        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task ListCallsAsync_FiltersByTenantIdAndSearchQuery()
    {
        var allCalls = await _service.ListCallsAsync();
        allCalls.Count.Should().BeGreaterThanOrEqualTo(4);

        var searchFiltered = await _service.ListCallsAsync(search: "Sarah Chen");
        searchFiltered.Should().HaveCount(1);
        searchFiltered[0].CallerName.Should().Be("Sarah Chen");

        var topicFiltered = await _service.ListCallsAsync(search: "Dispute");
        topicFiltered.Should().HaveCount(1);
        topicFiltered[0].CallId.Should().Be("CALL-7419-DIS");
    }

    [Fact]
    public async Task GetCallByIdAsync_ReturnsSeededCall_WhenIdMatches()
    {
        var call = await _service.GetCallByIdAsync("CALL-3382-STB");

        call.Should().NotBeNull();
        call!.CallerName.Should().Be("Robert Sterling");
        call.SentimentTrajectory.Should().Be(SentimentTrajectory.Stable);
        call.Status.Should().Be("Completed");
    }

    [Fact]
    public async Task StreamUtteranceAsync_AppendsUtterances_AndUpdatesOverallSentiment()
    {
        var callId = "CALL-TEST-STREAM-01";

        var u1 = await _service.StreamUtteranceAsync(new StreamUtteranceRequest(
            CallId: callId,
            Speaker: SpeakerType.Caller,
            Text: "We are pleased with this helpful API update."
        ));

        u1.SentimentScore.Should().BeGreaterThan(0.20);

        var call = await _service.GetCallByIdAsync(callId);
        call.Should().NotBeNull();
        call!.Utterances.Should().HaveCount(1);
        call.OverallSentimentScore.Should().Be(u1.SentimentScore);
    }

    [Fact]
    public async Task StreamUtteranceAsync_ThreadSafety_HandlesConcurrentStreamsCorrectly()
    {
        var callId = "CALL-CONCURRENT-01";
        int concurrentTurns = 20;

        var tasks = Enumerable.Range(0, concurrentTurns).Select(i =>
        {
            var speaker = (i % 2 == 0) ? SpeakerType.Agent : SpeakerType.Caller;
            return Task.Run(async () =>
            {
                await _service.StreamUtteranceAsync(new StreamUtteranceRequest(
                    CallId: callId,
                    Speaker: speaker,
                    Text: $"Turn index {i} in the voice call session."
                ));
            });
        });

        await Task.WhenAll(tasks);

        var call = await _service.GetCallByIdAsync(callId);
        call.Should().NotBeNull();
        call!.Utterances.Should().HaveCount(concurrentTurns);
    }
}
