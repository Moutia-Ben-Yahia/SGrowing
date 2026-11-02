namespace SGrowing.Api.Modules.Healthcare.BciNeuralKinematics;

public record NeuralChannelPacket(
    int ChannelId,
    double MeanVoltageMicrovolts,
    int SpikeCountInBin,
    double SignalToNoiseRatio
);

public record BciKinematicsDecodeRequest(
    string SubjectId,
    IReadOnlyList<NeuralChannelPacket> ChannelPackets,
    double PreviousVx = 0.0,
    double PreviousVy = 0.0,
    double PreviousVz = 0.0,
    double BinDurationMs = 50.0
);

public record DecodedKinematicVector(
    double VelocityX,
    double VelocityY,
    double VelocityZ,
    double SpeedMagnitudeMmPerSec,
    string DominantDirection
);

public record BciKinematicsDecodeResult(
    string SubjectId,
    int TotalSpikesCount,
    int ActiveChannelsCount,
    DecodedKinematicVector Kinematics,
    double DecodingConfidence,
    string MotorIntentState
);
