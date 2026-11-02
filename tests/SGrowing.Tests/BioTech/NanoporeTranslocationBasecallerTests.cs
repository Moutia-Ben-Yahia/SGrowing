using FluentAssertions;
using SGrowing.Api.Modules.BioTech.NanoporeTranslocationBasecaller;
using Xunit;

namespace SGrowing.Tests.BioTech;

public class NanoporeTranslocationBasecallerTests
{
    private readonly NanoporeTranslocationEngine _engine = new();

    [Fact]
    public void EvaluateTranslocation_NominalTranslocation_ReturnsActiveSequencing()
    {
        var samples = new List<IonicCurrentSample>();
        for (int i = 0; i < 30; i++)
        {
            samples.Add(new IonicCurrentSample(i, CurrentPicoAmperes: 85.0 + (i % 8), DwellTimeMicroseconds: 2200.0));
        }

        var tel = new NanoporeChannelTelemetry(
            ChannelId: "PORE-R10.4.1-01",
            OpenPoreCurrentPicoAmperes: 220.0,
            CurrentSignal: samples
        );

        var request = new NanoporeEvaluationRequest(tel, new BasecallerThresholds());
        var decision = _engine.EvaluateTranslocation(request);

        decision.ChannelId.Should().Be("PORE-R10.4.1-01");
        decision.TotalKmersTranslocated.Should().Be(30);
        decision.PoreStallEventDetected.Should().BeFalse();
        decision.ChannelState.Should().Be("SEQUENCING_TRANSLOCATION_ACTIVE");
        decision.MeanTranslocationSpeedBps.Should().BeInRange(400.0, 500.0);
        decision.RecommendedPoreControlDirective.Should().Contain("CONTINUE_BASECALLING");
    }

    [Fact]
    public void EvaluateTranslocation_LongDwellStall_TriggersReverseUnblockPulse()
    {
        // One sample has 65,000 us (65 ms) dwell time, exceeding 50 ms limit
        var samples = new List<IonicCurrentSample>
        {
            new(0, CurrentPicoAmperes: 90.0, DwellTimeMicroseconds: 2000.0),
            new(1, CurrentPicoAmperes: 45.0, DwellTimeMicroseconds: 65000.0), // Stalled!
            new(2, CurrentPicoAmperes: 90.0, DwellTimeMicroseconds: 2000.0)
        };

        var tel = new NanoporeChannelTelemetry(ChannelId: "PORE-STALLED", CurrentSignal: samples);
        var request = new NanoporeEvaluationRequest(tel, new BasecallerThresholds(MaxStallDwellTimeMicroseconds: 50000.0));
        var decision = _engine.EvaluateTranslocation(request);

        decision.PoreStallEventDetected.Should().BeTrue();
        decision.ChannelState.Should().Be("MOLECULAR_PORE_STALL");
        decision.RecommendedPoreControlDirective.Should().Contain("TRIGGER_REVERSE_PULSE");
    }

    [Fact]
    public void EvaluateTranslocation_PolyATailHomopolymer_DetectsTailLength()
    {
        // 25 samples with current level matching poly-A fraction (~61.6 pA)
        var samples = new List<IonicCurrentSample>();
        for (int i = 0; i < 25; i++)
        {
            samples.Add(new IonicCurrentSample(i, CurrentPicoAmperes: 61.6, DwellTimeMicroseconds: 2400.0));
        }

        var tel = new NanoporeChannelTelemetry(
            ChannelId: "PORE-POLYA-RUN",
            OpenPoreCurrentPicoAmperes: 220.0,
            CurrentSignal: samples
        );

        var request = new NanoporeEvaluationRequest(tel, new BasecallerThresholds(PolyACurrentLevelFraction: 0.28));
        var decision = _engine.EvaluateTranslocation(request);

        decision.EstimatedPolyATailLengthNucleotides.Should().Be(25);
        decision.ChannelState.Should().Be("POLYA_HOMOPOLYMER_TRANSLOCATING");
        decision.RecommendedPoreControlDirective.Should().Contain("SEQUENCE_POLYA");
    }

    [Fact]
    public void EvaluateTranslocation_EmptySignal_ReturnsOpenPoreState()
    {
        var tel = new NanoporeChannelTelemetry("PORE-EMPTY", CurrentSignal: []);
        var request = new NanoporeEvaluationRequest(tel, new BasecallerThresholds());
        var decision = _engine.EvaluateTranslocation(request);

        decision.TotalKmersTranslocated.Should().Be(0);
        decision.PoreStallEventDetected.Should().BeFalse();
        decision.ChannelState.Should().Be("OPEN_PORE_AWAITING_CAPTURE");
    }
}
