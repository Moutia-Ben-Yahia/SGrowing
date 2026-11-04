using System;
using System.Collections.Generic;
using SGrowing.Api.Modules.Voice.ObjectionScoring;
using Xunit;

namespace SGrowing.Tests.Voice;

public class ObjectionScoringTests
{
    private readonly ObjectionScoringEngine _engine = new();

    [Theory]
    [InlineData("Your solution is way too expensive for our team right now.", ObjectionCategory.BudgetAndPricing)]
    [InlineData("We already use Salesforce and HubSpot for this.", ObjectionCategory.CompetitorEntrenchment)]
    [InlineData("We don't have time to look at this until next quarter.", ObjectionCategory.TimingAndPriority)]
    [InlineData("We need to confirm SOC2 and GDPR compliance before anything.", ObjectionCategory.ProcurementAndSecurity)]
    public void ClassifyAndRebut_VariousObjections_CorrectlyClassifiedWithRebuttal(string utterance, ObjectionCategory expectedCategory)
    {
        // Arrange
        var request = new ObjectionRebuttalRequest(utterance);

        // Act
        var result = _engine.ClassifyAndRebut(request);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(expectedCategory, result.DetectedCategory);
        Assert.False(string.IsNullOrWhiteSpace(result.RecommendedRebuttal));
        Assert.False(string.IsNullOrWhiteSpace(result.PsychologicalFraming));
        Assert.NotEmpty(result.SuggestedFollowUpQuestions);
    }

    [Fact]
    public void ScoreCall_ComprehensiveTranscript_CalculatesScoresAndRubric()
    {
        // Arrange
        var request = new ScoreCallRequest(
            CallId: "call_test_eval_101",
            LeadName: "Marcus Vance",
            CompanyName: "Apex Global Logistics",
            TranscriptTurns: new List<CallTurn>
            {
                new("Agent", "Hi Marcus, thanks for connecting. What prompted you to evaluate our Growth OS?", 4.2),
                new("Lead", "We are struggling with high response times on inbound leads.", 5.1),
                new("Agent", "Understood. Today our customers see response times drop from 45 minutes to 4 seconds.", 6.0),
                new("Lead", "Sounds great, but we already have HubSpot.", 3.5),
                new("Agent", "Growth OS connects directly into HubSpot so your reps don't have to change their workflow.", 5.5),
                new("Lead", "That makes sense. Can we schedule a technical walkthrough?", 4.0),
                new("Agent", "Absolutely, how does Thursday at 2 PM sound?", 3.2)
            }
        );

        // Act
        var result = _engine.ScoreCall(request);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("call_test_eval_101", result.CallId);
        Assert.True(result.OverallScorePercent >= 80.0);
        Assert.Equal("SalesQualifiedLead", result.OutcomeQualification);
        Assert.NotEmpty(result.StrengthPoints);
        Assert.NotEmpty(result.ImprovementCoachingNotes);
    }

    [Fact]
    public void ClassifyAndRebut_WithGeneralSkepticism_ClassifiesCorrectly()
    {
        var request = new ObjectionRebuttalRequest("I don't believe any AI tool can handle our nuanced sales conversations.");

        var result = _engine.ClassifyAndRebut(request);

        Assert.NotNull(result);
        Assert.Equal(ObjectionCategory.GeneralSkepticism, result.DetectedCategory);
        Assert.Contains("Social Proof Validation", result.PsychologicalFraming);
        Assert.Contains("skepticism is healthy", result.RecommendedRebuttal);
        Assert.NotEmpty(result.SuggestedFollowUpQuestions);
    }

    [Fact]
    public void ClassifyAndRebut_WithNullRequest_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => _engine.ClassifyAndRebut(null!));
    }

    [Fact]
    public void ScoreCall_WithNullRequest_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => _engine.ScoreCall(null!));
    }

    [Fact]
    public void ScoreCall_WithShortTranscript_CalculatesBaselineRubricScores()
    {
        var request = new ScoreCallRequest(
            CallId: "call_short_01",
            LeadName: "John Smith",
            CompanyName: "Acme Corp",
            TranscriptTurns: new List<CallTurn>
            {
                new("Agent", "Hello John", 1.5),
                new("Lead", "Not interested thanks", 1.0)
            }
        );

        var result = _engine.ScoreCall(request);

        Assert.NotNull(result);
        Assert.Equal("call_short_01", result.CallId);
        Assert.Equal(85.0, result.OverallScorePercent);
        Assert.Equal("SalesQualifiedLead", result.OutcomeQualification);
        Assert.Equal(20, result.DiscoveryDepthScore);
        Assert.Equal(21, result.ObjectionHandlingScore);
        Assert.Equal(21, result.RapportScore);
        Assert.Equal(23, result.ClosingEffectivenessScore);
    }

    [Fact]
    public void GetRecentScoredCalls_ShouldReturnCallsOrderedByScoredAtUtcDescending()
    {
        var callA = new ScoreCallRequest("call_order_a", "Alice", "Company A", new List<CallTurn>());
        var callB = new ScoreCallRequest("call_order_b", "Bob", "Company B", new List<CallTurn>());

        _engine.ScoreCall(callA);
        _engine.ScoreCall(callB);

        var recent = _engine.GetRecentScoredCalls();

        Assert.NotEmpty(recent);
        Assert.Contains(recent, c => c.CallId == "call_order_a");
        Assert.Contains(recent, c => c.CallId == "call_order_b");
    }
}
