namespace SGrowing.Tests.Automotive;

using FluentAssertions;
using SGrowing.Api.Modules.Automotive.TeleoperationFoveatedStream;
using Xunit;

public class TeleoperationFoveatedStreamTests
{
    private readonly TeleoperationFoveatedStreamEngine _engine = new();

    [Fact]
    public void TranscodeFoveatedFrame_HighQualityNetwork_DeliversWithinLatencyBudget()
    {
        var frame = new VideoFrameMetadata("AV-ROBOTAXI-01", 1920, 1080, BaseQuantizationParameter: 20, TargetFrameRateFps: 60.0);
        var gaze = new GazePoint2D(0.50, 0.50, GazeConfidence: 0.98); // Focused at center
        var qos = new NetworkChannelQoS(RttMilliseconds: 25.0, JitterMilliseconds: 4.0, PacketLossPercent: 0.1, AvailableBandwidthMbps: 45.0);

        var request = new FoveatedTranscodeRequest(frame, gaze, qos, FovealRadiusNormalized: 0.20);
        var result = _engine.TranscodeFoveatedFrame(request);

        result.VehicleId.Should().Be("AV-ROBOTAXI-01");
        result.FovealQp.Should().Be(20);
        result.PeripheralQp.Should().BeGreaterThan(25);
        result.WithinLatencyBudget.Should().BeTrue();
        result.EstimatedEncodingLatencyMs.Should().BeLessThan(80.0);
        result.TileProfiles.Should().HaveCount(64); // 8x8 grid
        result.BitrateAdaptationStrategy.Should().Contain("Ultra-Low Latency");
    }

    [Fact]
    public void TranscodeFoveatedFrame_Severe5GJitterAndLoss_AdaptsPeripheralCompression()
    {
        var frame = new VideoFrameMetadata("AV-SHUTTLE-02", 1920, 1080, 22, 30.0);
        var gaze = new GazePoint2D(0.20, 0.80, 0.90);
        // Severely degraded network: RTT 90ms, Jitter 25ms, Bandwidth 5 Mbps
        var degradedQos = new NetworkChannelQoS(RttMilliseconds: 90.0, JitterMilliseconds: 25.0, PacketLossPercent: 4.5, AvailableBandwidthMbps: 5.0);

        var request = new FoveatedTranscodeRequest(frame, gaze, degradedQos);
        var result = _engine.TranscodeFoveatedFrame(request);

        result.WithinLatencyBudget.Should().BeFalse();
        result.EstimatedEncodingLatencyMs.Should().BeGreaterThan(80.0);
        result.BitrateAdaptationStrategy.Should().Contain("Bandwidth Backpressure");
    }

    [Fact]
    public void TranscodeFoveatedFrame_TilesNearGazeCenter_AssignedFovealQp()
    {
        var frame = new VideoFrameMetadata("AV-TEST-03", 1280, 720, 18, 30.0);
        var gaze = new GazePoint2D(0.50, 0.50, 1.0); // Exact center (Row 3/4, Col 3/4)
        var qos = new NetworkChannelQoS(20.0, 2.0, 0.0, 50.0);

        var request = new FoveatedTranscodeRequest(frame, gaze, qos, FovealRadiusNormalized: 0.15);
        var result = _engine.TranscodeFoveatedFrame(request);

        // Center tiles (e.g. Row 3, Col 3) should have exact BaseQP (18)
        var centerTile = result.TileProfiles.First(t => t.TileRow == 3 && t.TileCol == 3);
        centerTile.AssignedQp.Should().Be(18);

        // Corner tile (Row 0, Col 0) should have high peripheral QP
        var cornerTile = result.TileProfiles.First(t => t.TileRow == 0 && t.TileCol == 0);
        cornerTile.AssignedQp.Should().BeGreaterThan(18);
    }

    [Fact]
    public void TranscodeFoveatedFrame_ZeroBandwidth_HandlesSafelyWithoutException()
    {
        var frame = new VideoFrameMetadata("AV-ZERO-04", 1920, 1080, 24, 30.0);
        var gaze = new GazePoint2D(0.5, 0.5, 0.5);
        var deadQos = new NetworkChannelQoS(200.0, 50.0, 20.0, 0.0);

        var request = new FoveatedTranscodeRequest(frame, gaze, deadQos);
        var result = _engine.TranscodeFoveatedFrame(request);

        result.WithinLatencyBudget.Should().BeFalse();
    }
}
