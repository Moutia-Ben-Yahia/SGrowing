using SGrowing.Api.Modules.Commerce.FraudFirewall;
using Xunit;

namespace SGrowing.Tests.Commerce;

public sealed class FraudFirewallTests
{
    private readonly FraudFirewallEngine _engine = new();

    [Fact]
    public void GetStats_ReturnsValidInitialStats()
    {
        var stats = _engine.GetStats();

        Assert.NotNull(stats);
        Assert.True(stats.TotalEvaluated >= 3);
        Assert.True(stats.AllowedCount >= 1);
        Assert.True(stats.AverageRiskScore > 0);
    }

    [Fact]
    public void EvaluateTransaction_FlagsHighRiskWhenGeoMismatchAndHighValue()
    {
        var request = new EvaluateTransactionRequest(
            TransactionId: "txn_eval_99",
            AmountUsd: 12000m,
            Currency: "USD",
            CustomerEmail: "suspicious@tempmail.com",
            IpAddress: "198.51.100.99",
            CardCountry: "US",
            BillingCountry: "NG",
            DeviceFingerprint: "fp_981249"
        );

        var eval = _engine.EvaluateTransaction(request);

        Assert.NotNull(eval);
        Assert.True(eval.RiskScore >= 75);
        Assert.Equal("Block", eval.Decision);
        Assert.NotEmpty(eval.TriggeredRules);
    }

    [Fact]
    public void ToggleRule_DisablesRuleEvaluation()
    {
        var toggled = _engine.ToggleRule("rule_temp_email", false);
        Assert.True(toggled);

        var rules = _engine.ListRules();
        var tempRule = rules.FirstOrDefault(r => r.Id == "rule_temp_email");
        Assert.NotNull(tempRule);
        Assert.False(tempRule.IsActive);
    }
}
