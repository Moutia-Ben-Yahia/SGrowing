using SGrowing.Api.Modules.Voice.VoiceIntentFastPath;
using Xunit;

namespace SGrowing.Tests.Voice;

public class VoiceIntentFastPathTests
{
    [Fact]
    public void GetOverview_ReportsFastPathBenchmarkAndPredictions()
    {
        var engine = new VoiceIntentFastPathEngine();

        var overview = engine.GetOverview();

        Assert.NotNull(overview);
        Assert.True(overview.TotalInFlightPredictionsCount >= 2);
        Assert.True(overview.AverageFastPathLatencyMs < 65.0);
        Assert.True(overview.IntentClassificationAccuracyPct > 90.0);
        Assert.NotNull(overview.Benchmark);
        Assert.True(overview.Benchmark.LatencyReductionPct > 80.0);
        Assert.NotEmpty(overview.RecentPredictions);
    }

    [Fact]
    public void ClassifyIntent_PredictsIntentSub65Ms()
    {
        var engine = new VoiceIntentFastPathEngine();
        var request = new ClassifyIntentRequest(
            "CALL-SESSION-8841",
            "ACOUSTIC_FEAT_HASH_77BA91",
            "SOUTHERN_US"
        );

        var prediction = engine.ClassifyIntent(request);

        Assert.NotNull(prediction);
        Assert.StartsWith("PRED-INT-", prediction.PredictionId);
        Assert.Equal("CALL-SESSION-8841", prediction.CallSessionId);
        Assert.Equal("PRICING_CONCESSION", prediction.RecognizedIntent);
        Assert.True(prediction.ConfidenceScore >= 0.90);
        Assert.True(prediction.ProcessingLatencyMs < 65.0);
        Assert.Equal("SOUTHERN_US", prediction.AcousticDialectNormalized);

        var overview = engine.GetOverview();
        Assert.Contains(overview.RecentPredictions, p => p.PredictionId == prediction.PredictionId);
    }

    [Fact]
    public void TuneThreshold_ClampsAndReturnsNewCutoff()
    {
        var engine = new VoiceIntentFastPathEngine();
        var request = new TuneThresholdRequest(0.92);

        var cutoff = engine.TuneThreshold(request);

        Assert.Equal(0.92, cutoff);
    }

    [Fact]
    public void ClassifyIntent_NonSouthernDialect_PredictsOrderStatusIntent()
    {
        var engine = new VoiceIntentFastPathEngine();
        var request = new ClassifyIntentRequest(
            "CALL-SESSION-MIDWEST-01",
            "FEAT_HASH_MIDWEST_992",
            "US_MIDWEST"
        );

        var prediction = engine.ClassifyIntent(request);

        Assert.NotNull(prediction);
        Assert.Equal("ORDER_STATUS", prediction.RecognizedIntent);
        Assert.Equal("ROUTER_DISPATCH_FASTPATH_ORDER_STATUS", prediction.RecommendedActionRoute);
        Assert.Equal("US_MIDWEST", prediction.AcousticDialectNormalized);
    }

    [Theory]
    [InlineData(0.10, 0.50)]  // Clamped at lower bound 0.50
    [InlineData(1.50, 0.99)]  // Clamped at upper bound 0.99
    [InlineData(0.85, 0.85)]  // Within valid range
    public void TuneThreshold_ClampsValuesOutsidePermittedRange(double inputThreshold, double expectedCutoff)
    {
        var engine = new VoiceIntentFastPathEngine();
        var actualCutoff = engine.TuneThreshold(new TuneThresholdRequest(inputThreshold));

        Assert.Equal(expectedCutoff, actualCutoff);
    }

    [Fact]
    public void TuneThreshold_HigherCutoff_IncreasesEscalationCountInOverview()
    {
        var engine = new VoiceIntentFastPathEngine();

        // Baseline threshold 0.85: seeded predictions have confidence 0.96 and 0.92, so 0 escalations
        var initialOverview = engine.GetOverview();
        Assert.Equal(0, initialOverview.EscalationsToDeepLlmCount);

        // Raise threshold to 0.95 -> prediction with 0.92 is now below threshold
        engine.TuneThreshold(new TuneThresholdRequest(0.95));
        var updatedOverview = engine.GetOverview();
        Assert.Equal(1, updatedOverview.EscalationsToDeepLlmCount);
    }

    [Fact]
    public void ClassifyIntent_GeneratesUniquePredictionIds()
    {
        var engine = new VoiceIntentFastPathEngine();
        var p1 = engine.ClassifyIntent(new ClassifyIntentRequest("SES-1", "HASH-1", "GENERAL_US"));
        var p2 = engine.ClassifyIntent(new ClassifyIntentRequest("SES-2", "HASH-2", "GENERAL_US"));

        Assert.StartsWith("PRED-INT-", p1.PredictionId);
        Assert.StartsWith("PRED-INT-", p2.PredictionId);
        Assert.NotEqual(p1.PredictionId, p2.PredictionId);
    }

    [Fact]
    public void GetOverview_LatencyReductionPct_CalculatedCorrectlyAgainstBaseBenchmark()
    {
        var engine = new VoiceIntentFastPathEngine();
        var overview = engine.GetOverview();

        var avgLatency = overview.AverageFastPathLatencyMs;
        var expectedReduction = System.Math.Round((1.0 - (avgLatency / 520.0)) * 100.0, 1);

        Assert.Equal(expectedReduction, overview.Benchmark.LatencyReductionPct);
    }
}
