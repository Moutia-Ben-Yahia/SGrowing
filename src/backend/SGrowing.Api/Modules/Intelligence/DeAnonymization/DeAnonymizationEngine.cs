using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace SGrowing.Api.Modules.Intelligence.DeAnonymization;

public interface IDeAnonymizationEngine
{
    DeAnonymizeResult DeAnonymizeVisitor(DeAnonymizeRequest request);
    IReadOnlyList<AccountIntentRecord> GetActiveIntentFeed();
    DispatchAlertResult DispatchAccountAlert(DispatchAlertRequest request);
}

public class DeAnonymizationEngine : IDeAnonymizationEngine
{
    private readonly ConcurrentDictionary<string, AccountIntentRecord> _accounts = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentBag<DispatchAlertResult> _alerts = new();

    // Known corporate IP mapping database simulation
    private static readonly Dictionary<string, (string Domain, string Name, string Industry, string Range, string Country, string City)> _knownCorpRanges = new()
    {
        { "198.51.100.24", ("stripe.com", "Stripe Inc.", "FinTech / Payments", "5000+", "United States", "San Francisco") },
        { "198.51.100.42", ("shopify.com", "Shopify Inc.", "E-Commerce Infrastructure", "10000+", "Canada", "Ottawa") },
        { "198.51.100.88", ("databricks.com", "Databricks", "Data & AI", "5000+", "United States", "San Francisco") },
        { "203.0.113.15", ("siemens.com", "Siemens AG", "Industrial Automation", "10000+", "Germany", "Munich") },
        { "203.0.113.99", ("snowflake.com", "Snowflake Inc.", "Cloud Data Platform", "5000+", "United States", "Bozeman") }
    };

    public DeAnonymizationEngine()
    {
        // Seed default intent accounts
        _accounts["stripe.com"] = new AccountIntentRecord(
            AccountId: "acc_stripe",
            Domain: "stripe.com",
            CompanyName: "Stripe Inc.",
            TotalVisitsPast7Days: 14,
            ActiveScore: 92,
            Stage: IntentStage.ReadyToBuy,
            VisitedPages: new List<string> { "/pricing", "/enterprise-sla", "/docs/api/growth-os" },
            AssignedRepEmail: "sarah.cro@sgrowing.io",
            LastSeenUtc: DateTime.UtcNow.AddMinutes(-12)
        );

        _accounts["shopify.com"] = new AccountIntentRecord(
            AccountId: "acc_shopify",
            Domain: "shopify.com",
            CompanyName: "Shopify Inc.",
            TotalVisitsPast7Days: 8,
            ActiveScore: 78,
            Stage: IntentStage.HighIntentDecision,
            VisitedPages: new List<string> { "/integrations/commerce", "/pricing/volume" },
            AssignedRepEmail: "alex.ae@sgrowing.io",
            LastSeenUtc: DateTime.UtcNow.AddHours(-1)
        );
    }

    public DeAnonymizeResult DeAnonymizeVisitor(DeAnonymizeRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var ip = string.IsNullOrWhiteSpace(request.IpAddress) ? "198.51.100.24" : request.IpAddress.Trim();

        if (_knownCorpRanges.TryGetValue(ip, out var corp))
        {
            var highIntentPages = new List<string>();
            var intentScore = 40;

            if (!string.IsNullOrEmpty(request.PageUrl))
            {
                highIntentPages.Add(request.PageUrl);
                if (request.PageUrl.Contains("pricing", StringComparison.OrdinalIgnoreCase) ||
                    request.PageUrl.Contains("enterprise", StringComparison.OrdinalIgnoreCase) ||
                    request.PageUrl.Contains("demo", StringComparison.OrdinalIgnoreCase))
                {
                    intentScore += 45;
                }
                else
                {
                    intentScore += 15;
                }
            }

            if (request.TimeOnPageSeconds > 60)
            {
                intentScore += 15;
            }

            intentScore = Math.Min(100, intentScore);

            var stage = intentScore switch
            {
                >= 85 => IntentStage.ReadyToBuy,
                >= 70 => IntentStage.HighIntentDecision,
                >= 50 => IntentStage.Consideration,
                >= 25 => IntentStage.Awareness,
                _ => IntentStage.Cold
            };

            var topics = new List<string> { "Enterprise Growth OS", "High-Throughput CDP", "AI SDR Automation" };
            var shouldAlert = intentScore >= 75;

            // Update or add account record
            _accounts.AddOrUpdate(
                corp.Domain,
                _ => new AccountIntentRecord(
                    $"acc_{Guid.NewGuid():N}"[..12],
                    corp.Domain,
                    corp.Name,
                    1,
                    intentScore,
                    stage,
                    highIntentPages,
                    "inbound-sdr@sgrowing.io",
                    DateTime.UtcNow
                ),
                (_, existing) => existing with
                {
                    TotalVisitsPast7Days = existing.TotalVisitsPast7Days + 1,
                    ActiveScore = Math.Max(existing.ActiveScore, intentScore),
                    Stage = stage,
                    LastSeenUtc = DateTime.UtcNow
                }
            );

            return new DeAnonymizeResult(
                IpAddress: ip,
                NetworkType: NetworkType.Corporate,
                Domain: corp.Domain,
                CompanyName: corp.Name,
                Industry: corp.Industry,
                EmployeeRange: corp.Range,
                Country: corp.Country,
                City: corp.City,
                IntentScore: intentScore,
                IntentStage: stage,
                HighIntentTopics: topics,
                ShouldAlertAccountExec: shouldAlert,
                DetectedAtUtc: DateTime.UtcNow
            );
        }

        // Fallback for ISP or unknown IP
        return new DeAnonymizeResult(
            IpAddress: ip,
            NetworkType: NetworkType.ISP,
            Domain: null,
            CompanyName: "Residential / Mobile ISP User",
            Industry: "Consumer / Retail",
            EmployeeRange: null,
            Country: "United States",
            City: "Ashburn",
            IntentScore: 15,
            IntentStage: IntentStage.Cold,
            HighIntentTopics: new List<string> { "General Overview" },
            ShouldAlertAccountExec: false,
            DetectedAtUtc: DateTime.UtcNow
        );
    }

    public IReadOnlyList<AccountIntentRecord> GetActiveIntentFeed()
    {
        return _accounts.Values.OrderByDescending(a => a.ActiveScore).ToList();
    }

    public DispatchAlertResult DispatchAccountAlert(DispatchAlertRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var domain = request.Domain.Trim().ToLowerInvariant();
        var alertId = $"alt_{Guid.NewGuid():N}"[..10];
        var preview = $"🔥 High-Intent Account Active: {domain} visited /pricing. Buying readiness >= 85. Dispatched to {request.RecipientEmail} via {request.Channel}.";

        var result = new DispatchAlertResult(
            Dispatched: true,
            AlertId: alertId,
            MessagePreview: preview,
            DispatchedAtUtc: DateTime.UtcNow
        );

        _alerts.Add(result);
        return result;
    }
}
