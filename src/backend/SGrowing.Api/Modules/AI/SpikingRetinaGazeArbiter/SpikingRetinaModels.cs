namespace SGrowing.Api.Modules.AI.SpikingRetinaGazeArbiter;

public record RetinaSpikePacket(
    int X,
    int Y,
    long TimestampMicroseconds,
    int Polarity // +1 for ON event (brightness increase), -1 for OFF event (brightness decrease)
);

public record NeuromorphicSensorState(
    string SensorId,
    int ResolutionWidth = 1280,
    int ResolutionHeight = 720,
    double BackgroundSpikeRateEventsPerSecond = 50000.0,
    double TemporalContrastThresholdLog = 0.15,
    IReadOnlyList<RetinaSpikePacket>? Spikes = null
);

public record SaccadicGazeConfig(
    double MaxSaccadeVelocityDegPerSec = 900.0,
    double SaliencyClusterRadiusPixels = 45.0,
    int MinSpikesToTriggerSaccade = 15
);

public record SpikingRetinaEvaluationRequest(
    NeuromorphicSensorState SensorState,
    SaccadicGazeConfig GazeConfig
);

public record SaccadicGazeDecision(
    string SensorId,
    int TotalProcessedEvents,
    int OnEventsCount,
    int OffEventsCount,
    double MeanOpticalFlowAngleRadians,
    double MeanOpticalFlowVelocityPixelsPerMicrosec,
    bool SaccadeCommandIssued,
    int TargetFoveationX,
    int TargetFoveationY,
    string GazeState,
    string RecommendedGazeDirective
);
