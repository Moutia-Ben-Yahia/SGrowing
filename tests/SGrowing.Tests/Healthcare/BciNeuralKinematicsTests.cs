namespace SGrowing.Tests.Healthcare;

using FluentAssertions;
using SGrowing.Api.Modules.Healthcare.BciNeuralKinematics;
using Xunit;

public class BciNeuralKinematicsTests
{
    private readonly BciNeuralKinematicsEngine _engine = new();

    [Fact]
    public void DecodeKinematics_QuiescentRest_DecaysVelocityToZero()
    {
        // Zero spikes in population bin
        var packets = new List<NeuralChannelPacket>
        {
            new(1, MeanVoltageMicrovolts: -1.2, SpikeCountInBin: 0, SignalToNoiseRatio: 1.5),
            new(2, MeanVoltageMicrovolts: 0.5, SpikeCountInBin: 0, SignalToNoiseRatio: 1.8)
        };

        var request = new BciKinematicsDecodeRequest(
            SubjectId: "PATIENT-M1-042",
            ChannelPackets: packets,
            PreviousVx: 20.0,
            PreviousVy: -10.0,
            PreviousVz: 5.0
        );

        var result = _engine.DecodeKinematics(request);

        result.SubjectId.Should().Be("PATIENT-M1-042");
        result.TotalSpikesCount.Should().Be(0);
        result.ActiveChannelsCount.Should().Be(0);
        result.Kinematics.DominantDirection.Should().Be("IDLE_REST");
        result.MotorIntentState.Should().Contain("QUIESCENT_REST");
        result.Kinematics.VelocityX.Should().BeLessThan(20.0); // Decayed
    }

    [Fact]
    public void DecodeKinematics_ActiveFiring_DecodesNonZeroVelocityVector()
    {
        // Coordinated population spikes on channels 0-5
        var packets = new List<NeuralChannelPacket>();
        for (int ch = 0; ch < 12; ch++)
        {
            packets.Add(new NeuralChannelPacket(ch, -45.0, SpikeCountInBin: 6, SignalToNoiseRatio: 4.2));
        }

        var request = new BciKinematicsDecodeRequest(
            SubjectId: "PATIENT-M1-042",
            ChannelPackets: packets,
            PreviousVx: 0.0,
            PreviousVy: 0.0,
            PreviousVz: 0.0
        );

        var result = _engine.DecodeKinematics(request);

        result.TotalSpikesCount.Should().Be(72);
        result.ActiveChannelsCount.Should().Be(12);
        result.Kinematics.SpeedMagnitudeMmPerSec.Should().BeGreaterThan(5.0);
        result.DecodingConfidence.Should().BeGreaterThan(0.70);
        result.MotorIntentState.Should().Contain("ACTIVE_REACH");
    }

    [Fact]
    public void DecodeKinematics_HighConfidence_ReflectsChannelPopulationDensity()
    {
        var sparsePackets = new List<NeuralChannelPacket>
        {
            new(1, -25.0, 2, 3.0),
            new(2, -30.0, 3, 3.2)
        };

        var densePackets = new List<NeuralChannelPacket>();
        for (int i = 0; i < 20; i++)
        {
            densePackets.Add(new NeuralChannelPacket(i, -40.0, 5, 4.0));
        }

        var sparseResult = _engine.DecodeKinematics(new BciKinematicsDecodeRequest("SUBJ", sparsePackets));
        var denseResult = _engine.DecodeKinematics(new BciKinematicsDecodeRequest("SUBJ", densePackets));

        denseResult.DecodingConfidence.Should().BeGreaterThan(sparseResult.DecodingConfidence);
    }

    [Fact]
    public void DecodeKinematics_SmoothTrajectory_PreservesAutoregressiveMomentum()
    {
        var packets = new List<NeuralChannelPacket>
        {
            new(0, -40.0, 8, 4.5)
        };

        // First decode from rest
        var step1 = _engine.DecodeKinematics(new BciKinematicsDecodeRequest("SUBJ", packets, PreviousVx: 0.0));
        
        // Second decode carrying previous momentum
        var step2 = _engine.DecodeKinematics(new BciKinematicsDecodeRequest("SUBJ", packets, PreviousVx: step1.Kinematics.VelocityX));

        step2.Kinematics.VelocityX.Should().BeGreaterThan(step1.Kinematics.VelocityX);
    }
}
