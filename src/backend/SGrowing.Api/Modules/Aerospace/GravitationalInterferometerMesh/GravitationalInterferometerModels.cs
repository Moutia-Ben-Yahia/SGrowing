namespace SGrowing.Api.Modules.Aerospace.GravitationalInterferometerMesh;

public record InterferometerArmMeasurement(
    string ArmId,
    int TransmitterSpacecraftId, // 1, 2, or 3
    int ReceiverSpacecraftId,    // 1, 2, or 3
    double PhaseMeasurementRadians,
    double PhotodiodeShotNoisePicoRadians
);

public record TdiSynthesisRequest(
    string ConstellationId,
    double ArmLengthMeters = 2.5e9, // 2.5 million kilometers
    double LaserCenterFrequencyHz = 281.6e12, // 1064nm Nd:YAG laser
    IReadOnlyList<InterferometerArmMeasurement>? ArmMeasurements = null
);

public record TdiCombinationChannel(
    string ChannelName, // "TDI_X", "TDI_Y", "TDI_Z", "Sagnac_alpha"
    double SynthesizedPhaseResidualRadians,
    double LaserPhaseNoiseCancellationDb
);

public record GravitationalStrainDetectionResult(
    string ConstellationId,
    double ReconstructedStrainAmplitudeH,
    double SignalToNoiseRatio,
    bool AstrophysicalSignalDetected,
    string SourceCandidateType,
    IReadOnlyList<TdiCombinationChannel> TdiChannels
);
