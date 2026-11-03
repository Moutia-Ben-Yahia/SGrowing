namespace SGrowing.Api.Modules.Voice.EchoCancellation;

public record AudioDspFrameMetric(
    string FrameId,
    string CallSessionId,
    double ResidualEchoDecibels, // target < -50 dB
    double NearEndSpeechEnergyDb,
    double FarEndReferenceEnergyDb,
    bool DoubleTalkDetected,
    double HumanBanterPreservationIndex, // 0.0 to 1.0 (e.g. 0.98)
    string FilterState, // CONVERGED, ADAPTING, REINITIALIZING
    DateTime ProcessedAt
);

public record EchoFilterProfile(
    string ProfileId,
    string CodecType, // OPUS_48KHZ, G711_8KHZ, G722_16KHZ
    int FilterLengthTaps, // e.g. 1024 or 2048 taps
    double StepSizeMu,
    double MaxEchoReturnLossDb
);

public record EchoCancellationOverview(
    int ActiveWebRtcSessionsCount,
    double AverageEchoSuppressionDb,
    double DoubleTalkAccuracyPct,
    double DuplexSpeechClarityScore,
    List<AudioDspFrameMetric> RecentFrames,
    List<EchoFilterProfile> Profiles
);

public record ProcessAudioFrameRequest(
    string CallSessionId,
    double NearEndSpeechEnergyDb,
    double FarEndReferenceEnergyDb,
    bool ForceDoubleTalkEvaluation
);

public record TuneSuppressionRequest(
    string ProfileId,
    int FilterLengthTaps,
    double StepSizeMu
);
