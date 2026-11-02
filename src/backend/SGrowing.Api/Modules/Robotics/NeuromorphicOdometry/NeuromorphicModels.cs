namespace SGrowing.Api.Modules.Robotics.NeuromorphicOdometry;

public record DvsEvent(
    double TimestampMicroseconds,
    int X,
    int Y,
    int Polarity);

public record NeuromorphicOdometryRequest(
    string SensorId,
    double TimeWindowMicroseconds,
    List<DvsEvent> Events,
    double PixelPitchMicrons = 15.0,
    double FocalLengthMm = 4.5);

public record OpticalFlowVector(
    double MeanVxPixelsPerSec,
    double MeanVyPixelsPerSec,
    double Magnitude);

public record PoseIncrement3D(
    double DeltaXMeters,
    double DeltaYMeters,
    double DeltaZMeters,
    double DeltaYawDegrees);

public record NeuromorphicOdometryResult(
    string SensorId,
    int TotalEventsProcessed,
    int SpikeCount,
    OpticalFlowVector OpticalFlow,
    PoseIncrement3D EstimatedPoseIncrement,
    double ConfidenceScore,
    string MotionState);
