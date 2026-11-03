using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace SGrowing.Api.Modules.Personalization.ContentPersonalization;

public sealed class ContentPersonalizationEngine
{
    private readonly ConcurrentDictionary<string, FirmographicSegment> _segments = new();
    private readonly ConcurrentDictionary<string, PersonalizationRule> _rules = new();
    private readonly ConcurrentBag<ConversionReceipt> _conversionHistory = new();

    public ContentPersonalizationEngine()
    {
        InitializeDefaultData();
    }

    private void InitializeDefaultData()
    {
        var segEnterprise = new FirmographicSegment("SEG-ENT-01", "Global Enterprise Fintech", "Financial Services", "1,000-10,000+", "$100M+", 420);
        var segHealth = new FirmographicSegment("SEG-HLT-02", "HealthTech & Life Sciences", "Healthcare", "250-1,000", "$25M-$100M", 280);
        var segSaaS = new FirmographicSegment("SEG-SAS-03", "Hypergrowth B2B SaaS", "Technology", "50-250", "$10M-$50M", 650);

        _segments[segEnterprise.SegmentId] = segEnterprise;
        _segments[segHealth.SegmentId] = segHealth;
        _segments[segSaaS.SegmentId] = segSaaS;

        var ruleEnt = new PersonalizationRule(
            "RULE-FIN-01",
            "Fintech SOC2 High-Volume Messaging",
            segEnterprise.SegmentId,
            "/enterprise",
            "Sovereign Multi-Jurisdiction Growth OS for Global Financial Institutions",
            "Schedule Executive Architecture Review",
            14.8,
            3450
        );

        var ruleHlt = new PersonalizationRule(
            "RULE-HLT-02",
            "HIPAA Compliant Care Journey Engine",
            segHealth.SegmentId,
            "/solutions/healthcare",
            "Zero-Trust Patient Acquisition & Omnichannel Life Sciences Engine",
            "Explore HIPAA Compliant Blueprints",
            12.2,
            1890
        );

        var ruleSaaS = new PersonalizationRule(
            "RULE-SAS-03",
            "SaaS PLG to Enterprise Bridge",
            segSaaS.SegmentId,
            "/pricing",
            "Scale from Seed to $100M ARR with Autonomous AI Growth Swarms",
            "Start 14-Day Enterprise Sandbox",
            18.5,
            5200
        );

        _rules[ruleEnt.RuleId] = ruleEnt;
        _rules[ruleHlt.RuleId] = ruleHlt;
        _rules[ruleSaaS.RuleId] = ruleSaaS;
    }

    public PersonalizationOverview GetOverview()
    {
        var segmentsList = _segments.Values.ToList();
        var rulesList = _rules.Values.ToList();
        var avgLift = rulesList.Count > 0 ? rulesList.Average(r => r.ConversionRatePct) : 15.0;

        return new PersonalizationOverview(
            rulesList.Count,
            segmentsList.Count,
            Math.Round(avgLift, 2),
            2450000,
            segmentsList,
            rulesList
        );
    }

    public PersonalizationEvaluationResult EvaluatePagePersonalization(EvaluatePersonalizationRequest request)
    {
        var segment = _segments.Values.FirstOrDefault(s =>
            string.Equals(s.TargetIndustry, request.Industry, StringComparison.OrdinalIgnoreCase))
            ?? _segments.Values.First();

        var matchedRule = _rules.Values.FirstOrDefault(r => r.SegmentId == segment.SegmentId)
            ?? _rules.Values.First();

        var logos = segment.SegmentId switch
        {
            "SEG-ENT-01" => new[] { "Stripe", "Adyen", "JPMorgan", "Brex" },
            "SEG-HLT-02" => new[] { "Pfizer", "Moderna", "MayoClinic", "Novartis" },
            _ => new[] { "Linear", "Vercel", "Datadog", "Ramp" }
        };

        var pricingTier = request.EstimatedEmployees > 500 ? "Enterprise Sovereign Custom" : "Growth Scale Tier";

        return new PersonalizationEvaluationResult(
            $"DEC-{Guid.NewGuid():N}"[..12].ToUpperInvariant(),
            segment.SegmentId,
            matchedRule.SelectedHeroHeadline,
            $"Automated firmographic match for {request.VisitorDomain ?? "enterprise visitors"} in {request.Industry} with {request.EstimatedEmployees}+ headcount.",
            matchedRule.CallToActionText,
            $"/get-started?segment={segment.SegmentId}&ip={request.VisitorIp}",
            logos,
            pricingTier,
            0.965,
            14
        );
    }

    public ConversionReceipt RecordConversion(RecordConversionRequest request)
    {
        var receipt = new ConversionReceipt(
            $"REC-{Guid.NewGuid():N}"[..10].ToUpperInvariant(),
            request.DecisionId,
            request.RuleId,
            request.ConversionType,
            request.AttributedValueUsd,
            0.985,
            DateTime.UtcNow
        );

        _conversionHistory.Add(receipt);
        return receipt;
    }
}
