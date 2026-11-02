namespace SGrowing.Tests.Robotics;

using FluentAssertions;
using SGrowing.Api.Modules.Robotics.NeuromorphicOdometry;
using Xunit;

public class NeuromorphicOdometryTests
{
    private readonly NeuromorphicEventOdometryEngine _engine = new();

    [Fact]
    public void ProcessEventStream_AsynchronousEventBurst_ComputesOpticalFlowAndPositionalDelta()
    {
        var events = new List<DvsEvent>();
        for (int i = 0; i < 50; i++)
        {
            // Simulating rightward lateral drift: X increasing from 130 to 230, positive polarity
            events.Add(new DvsEvent(
                TimestampMicroseconds: 1000.0 + (i * 200.0),
                X: 130 + (i * 2),
                Y: 128,
                Polarity: 1
            ));
        }

        var request = new NeuromorphicOdometryRequest(
            SensorId: "DVS-PROPHESEE-01",
            TimeWindowMicroseconds: 10_000.0, // 10ms
            Events: events
        );

        var result = _engine.ProcessEventStream(request);

        result.SensorId.Should().Be("DVS-PROPHESEE-01");
        result.TotalEventsProcessed.Should().Be(50);
        result.SpikeCount.Should().BeGreaterThan(0);
        result.OpticalFlow.Magnitude.Should().BeGreaterThan(0.0);
        result.EstimatedPoseIncrement.DeltaXMeters.Should().NotBe(0.0);
        result.ConfidenceScore.Should().BeGreaterThan(0.50);
    }

    [Fact]
    public void ProcessEventStream_StationarySensorLowActivity_ClassifiesAsStationary()
    {
        // Very few events near center with long timestamps
        var events = new List<DvsEvent>
        {
            new(100.0, 128, 128, 1),
            new(900.0, 128, 129, -1)
        };

        var request = new NeuromorphicOdometryRequest("DVS-STATIONARY", 1_000_000.0, events);
        var result = _engine.ProcessEventStream(request);

        result.MotionState.Should().Be("Stationary");
        result.OpticalFlow.Magnitude.Should().BeLessThan(50.0);
    }

    [Fact]
    public void ProcessEventStream_EmptyEvents_ReturnsStationaryWithZeroDisplacement()
    {
        var request = new NeuromorphicOdometryRequest("DVS-EMPTY", 5000.0, []);
        var result = _engine.ProcessEventStream(request);

        result.TotalEventsProcessed.Should().Be(0);
        result.SpikeCount.Should().Be(0);
        result.MotionState.Should().Be("Stationary");
        result.EstimatedPoseIncrement.DeltaXMeters.Should().Be(0.0);
        result.EstimatedPoseIncrement.DeltaYMeters.Should().Be(0.0);
    }

    [Fact]
    public void ProcessEventStream_HighDensitySpikes_IncreasesConfidenceScore()
    {
        var events = new List<DvsEvent>();
        for (int i = 0; i < 100; i++)
        {
            // High concentration in single bin to trigger many LIF membrane resets
            events.Add(new DvsEvent(
                TimestampMicroseconds: 500.0 + (i * 10.0),
                X: 16,
                Y: 16,
                Polarity: 1
            ));
        }

        var request = new NeuromorphicOdometryRequest("DVS-BURST", 2000.0, events);
        var result = _engine.ProcessEventStream(request);

        result.SpikeCount.Should().BeGreaterThan(20);
        result.ConfidenceScore.Should().BeGreaterThan(0.70);
    }
}
