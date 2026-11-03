using System.Collections.Concurrent;

namespace SGrowing.Api.Modules.Voice.VoiceIntentFastPath;

public class VoiceIntentFastPathEngine
{
    private readonly ConcurrentBag<FastPathIntentPrediction> _predictions = new();
    private double _cutoffThreshold = 0.85;

    public VoiceIntentFastPathEngine()
    {
        SeedDemoData();
    }

    private void SeedDemoData()
    {
        _predictions.Add(new FastPathIntentPrediction(
            "PRED-INT-001",
            "CALL-FAST-890",
            "PRICING_CONCESSION",
            0.96,
            46.8,
            "SOUTHERN_US",
            "ROUTER_DISPATCH_TO_RETENTION_DESK",
            DateTime.UtcNow.AddMinutes(-12)
        ));

        _predictions.Add(new FastPathIntentPrediction(
            "PRED-INT-002",
            "CALL-FAST-891",
            "TECH_SUPPORT_ESCALATE",
            0.92,
            51.4,
            "UK_SCOTTISH",
            "ROUTER_DISPATCH_TO_TIER2_INFRA",
            DateTime.UtcNow.AddMinutes(-5)
        ));
    }

    public VoiceIntentOverview GetOverview()
    {
        var predsList = _predictions.ToList();
        double avgLatency = predsList.Count > 0
            ? Math.Round(predsList.Average(p => p.ProcessingLatencyMs), 1)
            : 48.0;

        int escalations = predsList.Count(p => p.ConfidenceScore < _cutoffThreshold);

        var benchmark = new FastPathLatencyBenchmark(
            "BMK-FASTPATH-01",
            avgLatency,
            520.0,
            Math.Round((1.0 - (avgLatency / 520.0)) * 100.0, 1)
        );

        return new VoiceIntentOverview(
            predsList.Count,
            avgLatency,
            98.2,
            escalations,
            predsList,
            benchmark
        );
    }

    public FastPathIntentPrediction ClassifyIntent(ClassifyIntentRequest request)
    {
        var predId = $"PRED-INT-{Guid.NewGuid():N}"[..12].ToUpperInvariant();
        string intent = request.InputDialectTag.Contains("SOUTHERN") ? "PRICING_CONCESSION" : "ORDER_STATUS";
        double confidence = 0.94;
        double latency = 44.5; // sub-65ms target

        var prediction = new FastPathIntentPrediction(
            predId,
            request.CallSessionId,
            intent,
            confidence,
            latency,
            request.InputDialectTag,
            $"ROUTER_DISPATCH_FASTPATH_{intent}",
            DateTime.UtcNow
        );

        _predictions.Add(prediction);
        return prediction;
    }

    public double TuneThreshold(TuneThresholdRequest request)
    {
        _cutoffThreshold = Math.Clamp(request.FastPathCutoffThreshold, 0.50, 0.99);
        return _cutoffThreshold;
    }
}
