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
}
