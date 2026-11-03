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
}
