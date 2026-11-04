using SGrowing.Api.Modules.Support.SentimentDeescalation;
using Xunit;

namespace SGrowing.Tests.Support;

public class SentimentDeescalationTests
{
    private readonly SentimentDeescalationEngine _engine = new();

    [Fact]
    public void SeededSubmissions_ContainsUrgentAlertForFrustratedCustomer()
    {
        var alerts = _engine.GetDeescalationAlerts();

        Assert.NotEmpty(alerts);
        var alert = alerts[0];
        Assert.Equal("cust_fintech_88", alert.CustomerId);
        Assert.Equal(SentimentLevel.FrustratedAtRisk, alert.EvaluatedSentiment);
        Assert.True(alert.RequiresUrgentTriage);
    }

    [Fact]
    public void SubmitCsat_DelightedCustomer_DoesNotRequireUrgentTriage()
    {
        var request = new SubmitCsatRequest(
            "cust_happy",
            "happy@user.com",
            5,
            "Flawless setup and incredible speed!"
        );

        var result = _engine.SubmitCsat(request);

        Assert.Equal(SentimentLevel.Delighted, result.EvaluatedSentiment);
        Assert.False(result.RequiresUrgentTriage);
    }

    [Fact]
    public void IssueGoodwill_ResolvesAlertAndAttachesCompensation()
    {
        var alerts = _engine.GetDeescalationAlerts();
        var target = alerts[0];

        var request = new IssueGoodwillRequest(target.SubmissionId, 500m, "Free 1-Month Enterprise SLA + $500 Platform Credit");
        var updated = _engine.IssueGoodwill(request);

        Assert.NotNull(updated);
        Assert.NotNull(updated.AppliedGoodwill);
        Assert.Equal(500m, updated.AppliedGoodwill.AccountCreditDollars);
        Assert.False(updated.RequiresUrgentTriage);
    }

    [Fact]
    public void SubmitCsat_PassiveNeutralRating_ClassifiesCorrectly()
    {
        var request = new SubmitCsatRequest(
            "cust_neutral",
            "neutral@user.com",
            3,
            "Service was average, nothing special."
        );

        var result = _engine.SubmitCsat(request);

        Assert.Equal(SentimentLevel.PassiveNeutral, result.EvaluatedSentiment);
        Assert.False(result.RequiresUrgentTriage);
    }

    [Theory]
    [InlineData("This is unacceptable service")]
    [InlineData("I am canceling my account")]
    [InlineData("Terrible experience overall")]
    [InlineData("Please escalate to manager")]
    public void SubmitCsat_KeywordTrigger_TriggersUrgentTriageEvenForHighRating(string feedback)
    {
        var request = new SubmitCsatRequest("cust_keyword", "user@test.com", 4, feedback);
        var result = _engine.SubmitCsat(request);

        Assert.Equal(SentimentLevel.FrustratedAtRisk, result.EvaluatedSentiment);
        Assert.True(result.RequiresUrgentTriage);
    }

    [Fact]
    public void IssueGoodwill_WhenSubmissionNotFound_ReturnsNull()
    {
        var result = _engine.IssueGoodwill(new IssueGoodwillRequest("nonexistent_sub_123", 100m, "Credit"));
        Assert.Null(result);
    }

    [Fact]
    public void SubmitCsat_ClampsRatingScoreBetweenOneAndFive()
    {
        var reqLow = new SubmitCsatRequest("c1", "a@b.com", -5, "Text");
        var resLow = _engine.SubmitCsat(reqLow);
        Assert.Equal(1, resLow.RatingScore);

        var reqHigh = new SubmitCsatRequest("c2", "a@b.com", 15, "Text");
        var resHigh = _engine.SubmitCsat(reqHigh);
        Assert.Equal(5, resHigh.RatingScore);
    }

    [Fact]
    public void SubmitCsat_RatingFourWithoutKeywordTrigger_ReturnsSatisfiedSentiment()
    {
        var request = new SubmitCsatRequest("cust_satisfied", "satisfied@user.com", 4, "Great platform and easy integration.");
        var result = _engine.SubmitCsat(request);

        Assert.Equal(SentimentLevel.Satisfied, result.EvaluatedSentiment);
        Assert.False(result.RequiresUrgentTriage);
    }
}
