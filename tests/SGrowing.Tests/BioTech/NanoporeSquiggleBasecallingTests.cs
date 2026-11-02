using SGrowing.Api.Modules.BioTech.NanoporeSquiggleBasecalling;
using Xunit;

namespace SGrowing.Tests.BioTech;

public class NanoporeSquiggleBasecallingTests
{
    private readonly NanoporeSquiggleEngine _engine = new();

    [Fact]
    public void AnalyzeSquiggle_NominalSequencing_ReturnsHighAccuracyReady()
    {
        var telemetry = new NanoporeSquiggleTelemetry
        {
            ChannelPoreId = "PORE-R10-CH102",
            PoreChemistryType = "R10.4.1_CsgG",
            OpenPoreBaselineCurrentPicoAmps = 104.0,
            CurrentBaselineDriftPicoAmpsPerMin = 0.65,
            MeanTranslocationVelocityBps = 415.0,
            HelicaseDwellTimeVarianceRatio = 1.12,
            RawSignalSignalToNoiseRatio = 15.2,
            MeanPhredQualityScore = 22.0,
            Epigenetic5mCPredictionConfidencePercent = 95.0,
            HomopolymerCompressionArtifactRatePercent = 1.2
        };

        var result = _engine.AnalyzeSquiggle(telemetry);

        Assert.Equal("PORE-R10-CH102", result.ChannelPoreId);
        Assert.True(result.IsHighAccuracyBasecallingReady);
        Assert.True(result.IsCurrentBaselineStable);
        Assert.True(result.IsHelicaseTranslocationRegular);
        Assert.True(result.IsEpigeneticCallingConfident);
        Assert.True(result.CalculatedBasecallingAccuracyPercent >= 99.0);
        Assert.True(result.EstimatedHelicaseProcessivityScore >= 8.0);
        Assert.Empty(result.ActiveRegimes);
    }

    [Fact]
    public void AnalyzeSquiggle_ExcessiveBaselineDrift_TriggersBaselineAlert()
    {
        var telemetry = new NanoporeSquiggleTelemetry
        {
            ChannelPoreId = "PORE-DRIFT-ALERT",
            CurrentBaselineDriftPicoAmpsPerMin = 4.8 // Exceeds 2.5 pA/min
        };

        var result = _engine.AnalyzeSquiggle(telemetry);

        Assert.False(result.IsCurrentBaselineStable);
        Assert.False(result.IsHighAccuracyBasecallingReady);
        Assert.Contains("CURRENT_BASELINE_DRIFT_SATURATION", result.ActiveRegimes);
        Assert.NotEmpty(result.RecommendedMitigations);
    }

    [Fact]
    public void AnalyzeSquiggle_HelicaseIrregularity_TriggersStallingAlert()
    {
        var telemetry = new NanoporeSquiggleTelemetry
        {
            ChannelPoreId = "PORE-HELICASE-STALL",
            HelicaseDwellTimeVarianceRatio = 1.95, // Above 1.50
            MeanTranslocationVelocityBps = 240.0 // Stalled
        };

        var result = _engine.AnalyzeSquiggle(telemetry);

        Assert.False(result.IsHelicaseTranslocationRegular);
        Assert.Contains("HELICASE_STALLING_OR_BACKSTEPPING", result.ActiveRegimes);
    }

    [Fact]
    public void AnalyzeSquiggle_LowPhredScore_TriggersConfidenceCollapse()
    {
        var telemetry = new NanoporeSquiggleTelemetry
        {
            ChannelPoreId = "PORE-LOW-Q",
            MeanPhredQualityScore = 13.5 // Below 17.0
        };

        var result = _engine.AnalyzeSquiggle(telemetry);

        Assert.False(result.IsHighAccuracyBasecallingReady);
        Assert.Contains("BASECALLING_CONFIDENCE_COLLAPSE", result.ActiveRegimes);
    }

    [Fact]
    public void AnalyzeSquiggle_BasecallingAccuracy_CorrelatesWithPhredScore()
    {
        var highQTelemetry = new NanoporeSquiggleTelemetry
        {
            ChannelPoreId = "PORE-HIGH-Q",
            MeanPhredQualityScore = 25.0
        };

        var lowQTelemetry = new NanoporeSquiggleTelemetry
        {
            ChannelPoreId = "PORE-LOW-Q",
            MeanPhredQualityScore = 12.0
        };

        var highRes = _engine.AnalyzeSquiggle(highQTelemetry);
        var lowRes = _engine.AnalyzeSquiggle(lowQTelemetry);

        Assert.True(highRes.CalculatedBasecallingAccuracyPercent > lowRes.CalculatedBasecallingAccuracyPercent);
    }
}
