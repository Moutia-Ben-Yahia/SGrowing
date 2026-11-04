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

    [Fact]
    public void EvaluateTransaction_LowRiskTransaction_ReturnsAllowDecision()
    {
        var request = new EvaluateTransactionRequest(
            TransactionId: "txn_safe_101",
            AmountUsd: 150m,
            Currency: "USD",
            CustomerEmail: "trusted.buyer@company.com",
            IpAddress: "10.0.0.1",
            CardCountry: "US",
            BillingCountry: "US",
            DeviceFingerprint: "fp_safe_device"
        );

        var eval = _engine.EvaluateTransaction(request);

        Assert.NotNull(eval);
        Assert.Equal(0, eval.RiskScore);
        Assert.Equal("Allow", eval.Decision);
        Assert.Empty(eval.TriggeredRules);
    }

    [Fact]
    public void ToggleRule_NonExistentRule_ReturnsFalse()
    {
        var toggled = _engine.ToggleRule("rule_ghost_unknown", false);
        Assert.False(toggled);
    }

    [Fact]
    public void ListRecentEvaluations_ContainsNewlyEvaluatedTransactions()
    {
        var request = new EvaluateTransactionRequest(
            TransactionId: "txn_audit_tracked",
            AmountUsd: 6500m, // triggers high value rule
            Currency: "USD",
            CustomerEmail: "regular@gmail.com",
            IpAddress: "12.34.56.78",
            CardCountry: "GB",
            BillingCountry: "GB",
            DeviceFingerprint: "fp_gb_device"
        );

        var eval = _engine.EvaluateTransaction(request);
        var recent = _engine.ListRecentEvaluations();

        Assert.Contains(recent, e => e.TransactionId == "txn_audit_tracked");
        Assert.True(eval.RiskScore > 0);
    }

    [Fact]
    public void ListRules_ReturnsAllConfiguredFraudRules()
    {
        var rules = _engine.ListRules();

        Assert.NotNull(rules);
        Assert.True(rules.Count >= 4);
        Assert.Contains(rules, r => r.Id == "rule_geo_mismatch");
        Assert.Contains(rules, r => r.Id == "rule_high_value");
        Assert.Contains(rules, r => r.Id == "rule_ip_velocity");
    }

    [Fact]
    public void EvaluateTransaction_ModerateRisk_RequiresChallenge3DS()
    {
        var request = new EvaluateTransactionRequest(
            TransactionId: "txn_challenge_3ds",
            AmountUsd: 6000m, // High value: 25 pts
            Currency: "USD",
            CustomerEmail: "buyer@legit.com", // Normal email
            IpAddress: "10.0.0.5", // Normal IP
            CardCountry: "US", // Geo mismatch: 35 pts
            BillingCountry: "FR",
            DeviceFingerprint: "fp_challenge"
        );

        var eval = _engine.EvaluateTransaction(request);

        Assert.NotNull(eval);
        Assert.Equal(60, eval.RiskScore);
        Assert.Equal("Challenge3DS", eval.Decision);
        Assert.Equal(2, eval.TriggeredRules.Count);
    }
}
