using System.Collections.Concurrent;

namespace SGrowing.Api.Modules.Commerce.FraudFirewall;

public interface IFraudFirewallEngine
{
    TransactionRiskEvaluation EvaluateTransaction(EvaluateTransactionRequest request);
    FraudFirewallStats GetStats();
    IReadOnlyList<TransactionRiskEvaluation> ListRecentEvaluations();
    IReadOnlyList<FraudRule> ListRules();
    bool ToggleRule(string ruleId, bool isActive);
}

public sealed class FraudFirewallEngine : IFraudFirewallEngine
{
    private readonly ConcurrentDictionary<string, FraudRule> _rules = new();
    private readonly ConcurrentBag<TransactionRiskEvaluation> _evaluations = new();

    public FraudFirewallEngine()
    {
        SeedDefaultRules();
        SeedDefaultEvaluations();
    }

    public TransactionRiskEvaluation EvaluateTransaction(EvaluateTransactionRequest request)
    {
        var triggered = new List<string>();
        var totalScore = 0;

        if (_rules.TryGetValue("rule_geo_mismatch", out var rGeo) && rGeo.IsActive)
        {
            if (!string.Equals(request.CardCountry, request.BillingCountry, StringComparison.OrdinalIgnoreCase))
            {
                triggered.Add($"Geographic Mismatch: Card ({request.CardCountry}) != Billing ({request.BillingCountry})");
                totalScore += rGeo.RiskWeight;
            }
        }

        if (_rules.TryGetValue("rule_high_value", out var rVal) && rVal.IsActive)
        {
            if (request.AmountUsd >= 5000m)
            {
                triggered.Add($"High-Value Transaction: ${request.AmountUsd:F2} exceeds $5,000 threshold");
                totalScore += rVal.RiskWeight;
            }
        }

        if (_rules.TryGetValue("rule_temp_email", out var rEmail) && rEmail.IsActive)
        {
            if (request.CustomerEmail.EndsWith("@mailinator.com", StringComparison.OrdinalIgnoreCase) ||
                request.CustomerEmail.EndsWith("@tempmail.com", StringComparison.OrdinalIgnoreCase) ||
                request.CustomerEmail.EndsWith("@guerrillamail.com", StringComparison.OrdinalIgnoreCase))
            {
                triggered.Add($"Disposable Domain: {request.CustomerEmail}");
                totalScore += rEmail.RiskWeight;
            }
        }

        if (_rules.TryGetValue("rule_ip_velocity", out var rVel) && rVel.IsActive)
        {
            if (request.IpAddress.StartsWith("198.51.", StringComparison.Ordinal) || request.IpAddress.StartsWith("203.0.", StringComparison.Ordinal))
            {
                triggered.Add($"High Velocity IP Cluster: {request.IpAddress}");
                totalScore += rVel.RiskWeight;
            }
        }

        totalScore = Math.Min(totalScore, 100);

        string decision = totalScore switch
        {
            >= 75 => "Block",
            >= 50 => "Challenge3DS",
            >= 30 => "ManualReview",
            _ => "Allow"
        };

        var evaluation = new TransactionRiskEvaluation(
            TransactionId: request.TransactionId,
            AmountUsd: request.AmountUsd,
            CustomerEmail: request.CustomerEmail,
            RiskScore: totalScore,
            Decision: decision,
            TriggeredRules: triggered,
            EvaluatedAtUtc: DateTime.UtcNow
        );

        _evaluations.Add(evaluation);
        return evaluation;
    }

    public FraudFirewallStats GetStats()
    {
        var evals = _evaluations.ToList();
        var total = evals.Count;
        if (total == 0)
        {
            return new FraudFirewallStats(0, 0, 0, 0, 0.0, 0.0);
        }

        var blocked = evals.Count(e => e.Decision == "Block");
        var challenged = evals.Count(e => e.Decision == "Challenge3DS");
        var allowed = evals.Count(e => e.Decision == "Allow" || e.Decision == "ManualReview");
        var blockRate = Math.Round((double)blocked / total * 100, 2);
        var avgScore = Math.Round(evals.Average(e => e.RiskScore), 1);

        return new FraudFirewallStats(total, allowed, challenged, blocked, blockRate, avgScore);
    }

    public IReadOnlyList<TransactionRiskEvaluation> ListRecentEvaluations()
    {
        return _evaluations.OrderByDescending(e => e.EvaluatedAtUtc).Take(50).ToList();
    }

    public IReadOnlyList<FraudRule> ListRules()
    {
        return _rules.Values.ToList();
    }

    public bool ToggleRule(string ruleId, bool isActive)
    {
        if (_rules.TryGetValue(ruleId, out var rule))
        {
            _rules[ruleId] = rule with { IsActive = isActive };
            return true;
        }
        return false;
    }

    private void SeedDefaultRules()
    {
        var r1 = new FraudRule("rule_geo_mismatch", "Cross-Border Card vs Billing Mismatch", "Triggers when card issuing country diverges from buyer billing address", 35, "IpGeoMismatch", true);
        var r2 = new FraudRule("rule_high_value", "High-Value Transaction Spike", "Flags transactions exceeding $5,000 for elevated verification", 25, "HighValueFirstOrder", true);
        var r3 = new FraudRule("rule_temp_email", "Disposable Email Shield", "Detects throwaway and burner email addresses", 40, "DisposableEmail", true);
        var r4 = new FraudRule("rule_ip_velocity", "IP Rapid Velocity Cluster", "Detects rapid multi-card testing across the same subnet", 30, "VelocityCheck", true);

        _rules[r1.Id] = r1;
        _rules[r2.Id] = r2;
        _rules[r3.Id] = r3;
        _rules[r4.Id] = r4;
    }

    private void SeedDefaultEvaluations()
    {
        _evaluations.Add(new TransactionRiskEvaluation("txn_89210", 1250.00m, "cfo@acme.corp", 15, "Allow", Array.Empty<string>(), DateTime.UtcNow.AddMinutes(-12)));
        _evaluations.Add(new TransactionRiskEvaluation("txn_89211", 7500.00m, "buyer@anonymous.com", 60, "Challenge3DS", new[] { "High-Value Transaction: $7,500.00 exceeds $5,000 threshold", "Geographic Mismatch: Card (US) != Billing (GB)" }, DateTime.UtcNow.AddMinutes(-5)));
        _evaluations.Add(new TransactionRiskEvaluation("txn_89212", 240.00m, "bot99@tempmail.com", 70, "Challenge3DS", new[] { "Disposable Domain: bot99@tempmail.com", "High Velocity IP Cluster: 198.51.100.4" }, DateTime.UtcNow.AddMinutes(-2)));
    }
}
