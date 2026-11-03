using System.Collections.Concurrent;

namespace SGrowing.Api.Modules.Sales.ValueRealization;

public class ValueRealizationEngine
{
    private readonly ConcurrentDictionary<string, CustomerValueMetric> _metrics = new();
    private readonly ConcurrentBag<QbrExecutiveDeck> _decks = new();

    public ValueRealizationEngine()
    {
        SeedDemoData();
    }

    private void SeedDemoData()
    {
        var m1 = new CustomerValueMetric(
            "VAL-MET-001",
            "Apex Global Financial",
            "COST_REDUCTION",
            1250000.00m,
            420000.00m,
            830000.00m,
            3.2,
            "AUTOMATED_LEDGER_AUDIT_TELEMETRY"
        );

        var m2 = new CustomerValueMetric(
            "VAL-MET-002",
            "Vanguard Logistics Cloud",
            "CYCLE_TIME_SPEEDUP",
            48.00m, // 48 hours order-to-cash
            6.50m,  // 6.5 hours order-to-cash
            1450000.00m,
            4.8,
            "EDI_FULFILLMENT_PIPELINE_TIMESTAMPS"
        );

        _metrics[m1.MetricId] = m1;
        _metrics[m2.MetricId] = m2;

        var sampleSlides = new List<QbrExecutiveSlide>
        {
            new(1, "Executive Summary & Realized ROI", "Delivered $2.28M verified financial impact across FY26.", "KPI_SCORECARD", new List<string> { "Cost reduction: $830k", "Cycle time speedup: 7.4x", "Target ROI multiplier: 3.2x" }),
            new(2, "Operational Velocity & Efficiency Gains", "Order fulfillment latency reduced from 48h to 6.5h.", "ROI_WATERFALL", new List<string> { "86% automation rate in dispatch", "Zero SLA breach incidents" }),
            new(3, "Next Horizon Expansion Proposal", "Unlock Autonomous Multi-Cloud WireGuard Mesh for EMEA.", "EXPANSION_PROPOSAL", new List<string> { "Proposed Expansion ARR: $420,000", "Estimated additional savings: $1.1M" })
        };

        _decks.Add(new QbrExecutiveDeck(
            "QBR-DECK-001",
            "Apex Global Financial",
            "2026-Q2",
            2280000.00m,
            96.5,
            420000.00m,
            sampleSlides,
            DateTime.UtcNow.AddDays(-6)
        ));
    }

    public ValueRealizationOverview GetOverview()
    {
        var metricsList = _metrics.Values.ToList();
        var decksList = _decks.ToList();

        decimal totalRoi = metricsList.Sum(m => m.AnnualizedRoiDollars);
        double avgHealth = decksList.Count > 0 ? decksList.Average(d => d.OverallAccountHealthScore) : 95.0;

        return new ValueRealizationOverview(
            metricsList.Select(m => m.CustomerAccountName).Distinct().Count(),
            totalRoi,
            Math.Round(avgHealth, 1),
            decksList.Count,
            metricsList,
            decksList
        );
    }

    public QbrExecutiveDeck GenerateQbrDeck(GenerateQbrDeckRequest request)
    {
        var deckId = $"QBR-DECK-{Guid.NewGuid():N}"[..12].ToUpperInvariant();
        var slides = new List<QbrExecutiveSlide>
        {
            new(1, $"Executive Summary — {request.FiscalQuarter}", $"Total realized client value: ${request.TotalRealizedRoiDollars:N0}.", "KPI_SCORECARD", new List<string> { $"Target realized value: ${request.TotalRealizedRoiDollars:N0}", $"Health Score: {request.OverallAccountHealthScore}%" }),
            new(2, "Peer Benchmark Comparison", "Leading upper quartile across B2B growth operations.", "PEER_BENCHMARK", new List<string> { "Top 5% latency efficiency", "Verified audit compliance" }),
            new(3, "Strategic Expansion & Roadmap", $"Recommended expansion: ${request.ProposedExpansionArrDollars:N0} ARR.", "EXPANSION_PROPOSAL", new List<string> { "Multi-Region Anycast failover", "Voice biometric deepfake defense" })
        };

        var deck = new QbrExecutiveDeck(
            deckId,
            request.CustomerAccountName,
            request.FiscalQuarter,
            request.TotalRealizedRoiDollars,
            request.OverallAccountHealthScore,
            request.ProposedExpansionArrDollars,
            slides,
            DateTime.UtcNow
        );

        _decks.Add(deck);
        return deck;
    }

    public CustomerValueMetric AttestRoi(AttestRoiRequest request)
    {
        var metricId = $"VAL-MET-{Guid.NewGuid():N}"[..12].ToUpperInvariant();
        decimal savings = Math.Max(0m, request.BaselineValue - request.RealizedCurrentValue);
        double mult = request.RealizedCurrentValue > 0 ? (double)(request.BaselineValue / request.RealizedCurrentValue) : 1.0;

        var metric = new CustomerValueMetric(
            metricId,
            request.CustomerAccountName,
            request.ObjectiveCategory,
            request.BaselineValue,
            request.RealizedCurrentValue,
            savings,
            Math.Round(mult, 1),
            request.VerificationSource
        );

        _metrics[metricId] = metric;
        return metric;
    }
}
