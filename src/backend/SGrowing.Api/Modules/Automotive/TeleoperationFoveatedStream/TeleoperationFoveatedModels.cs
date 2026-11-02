namespace SGrowing.Api.Modules.Automotive.TeleoperationFoveatedStream;

public record GazePoint2D(
    double NormalizedX,
    double NormalizedY,
    double GazeConfidence);

public record NetworkChannelQoS(
    double RttMilliseconds,
    double JitterMilliseconds,
    double PacketLossPercent,
    double AvailableBandwidthMbps);

public record VideoFrameMetadata(
    string VehicleId,
    int FrameWidth,
    int FrameHeight,
    int BaseQuantizationParameter,
    double TargetFrameRateFps);

public record FoveatedTranscodeRequest(
    VideoFrameMetadata Frame,
    GazePoint2D OperatorGaze,
    NetworkChannelQoS NetworkQoS,
    double FovealRadiusNormalized = 0.20);

public record VideoTileCompressionProfile(
    int TileRow,
    int TileCol,
    double DistanceFromGaze,
    int AssignedQp,
    double BitrateAllocatedKbps);

public record FoveatedTranscodeResult(
    string VehicleId,
    int FovealQp,
    int PeripheralQp,
    double TotalFrameSizeKilobytes,
    double EstimatedEncodingLatencyMs,
    bool WithinLatencyBudget,
    List<VideoTileCompressionProfile> TileProfiles,
    string BitrateAdaptationStrategy);
