using FluentAssertions;
using SGrowing.Api.Modules.AI.SpikingRetinaGazeArbiter;
using Xunit;

namespace SGrowing.Tests.AI;

public class SpikingRetinaGazeArbiterTests
{
    private readonly SpikingRetinaGazeArbiterEngine _engine = new();

    [Fact]
    public void EvaluateSpikeStream_EmptySpikes_ReturnsFixationIdle()
    {
        var sensor = new NeuromorphicSensorState("DVS-CAMERA-01", ResolutionWidth: 1280, ResolutionHeight: 720, Spikes: []);
        var config = new SaccadicGazeConfig();
        var request = new SpikingRetinaEvaluationRequest(sensor, config);

        var decision = _engine.EvaluateSpikeStream(request);

        decision.SensorId.Should().Be("DVS-CAMERA-01");
        decision.TotalProcessedEvents.Should().Be(0);
        decision.SaccadeCommandIssued.Should().BeFalse();
        decision.GazeState.Should().Be("FIXATION_IDLE");
        decision.TargetFoveationX.Should().Be(640);
        decision.TargetFoveationY.Should().Be(360);
    }

    [Fact]
    public void EvaluateSpikeStream_HighDensitySaliencyCluster_TriggersBallisticSaccade()
    {
        // 20 spikes concentrated in upper-right quadrant (X ~ 950, Y ~ 180)
        var spikes = new List<RetinaSpikePacket>();
        long baseTs = 1000000;
        for (int i = 0; i < 20; i++)
        {
            spikes.Add(new RetinaSpikePacket(
                X: 950 + (i % 5),
                Y: 180 + (i / 5),
                TimestampMicroseconds: baseTs + (i * 200),
                Polarity: i % 2 == 0 ? 1 : -1
            ));
        }

        var sensor = new NeuromorphicSensorState("DVS-HIGH-SPEED-ROBOT", Spikes: spikes);
        var config = new SaccadicGazeConfig(MinSpikesToTriggerSaccade: 15);
        var request = new SpikingRetinaEvaluationRequest(sensor, config);

        var decision = _engine.EvaluateSpikeStream(request);

        decision.TotalProcessedEvents.Should().Be(20);
        decision.OnEventsCount.Should().Be(10);
        decision.OffEventsCount.Should().Be(10);
        decision.SaccadeCommandIssued.Should().BeTrue();
        decision.GazeState.Should().Be("BALLISTIC_SACCADE_ENGAGED");
        decision.TargetFoveationX.Should().BeInRange(945, 955);
        decision.TargetFoveationY.Should().BeInRange(175, 185);
        decision.RecommendedGazeDirective.Should().Contain("EXECUTE_SACCADE");
    }

    [Fact]
    public void EvaluateSpikeStream_SparseSpikes_EngagesSmoothPursuitMicrosaccade()
    {
        // 5 spikes (less than 15 min threshold for full ballistic saccade)
        var spikes = new List<RetinaSpikePacket>
        {
            new(100, 100, 10000, 1),
            new(105, 102, 11000, 1),
            new(110, 104, 12000, 1),
            new(115, 106, 13000, -1),
            new(120, 108, 14000, -1)
        };

        var sensor = new NeuromorphicSensorState("DVS-MICROSACCADE", Spikes: spikes);
        var config = new SaccadicGazeConfig(MinSpikesToTriggerSaccade: 15);
        var request = new SpikingRetinaEvaluationRequest(sensor, config);

        var decision = _engine.EvaluateSpikeStream(request);

        decision.SaccadeCommandIssued.Should().BeFalse();
        decision.GazeState.Should().Be("SMOOTH_PURSUIT_MICROSACCADE");
        decision.MeanOpticalFlowVelocityPixelsPerMicrosec.Should().BeGreaterThan(0.0);
    }

    [Fact]
    public void EvaluateSpikeStream_NullSpikes_HandlesGracefullyWithoutThrowing()
    {
        var sensor = new NeuromorphicSensorState("DVS-NULL-STREAMS", Spikes: null);
        var request = new SpikingRetinaEvaluationRequest(sensor, new SaccadicGazeConfig());

        var decision = _engine.EvaluateSpikeStream(request);

        decision.TotalProcessedEvents.Should().Be(0);
        decision.GazeState.Should().Be("FIXATION_IDLE");
    }
}
