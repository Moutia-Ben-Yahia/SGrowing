namespace SGrowing.Api.Modules.Voice.VocalTremor;

public record VoiceAcousticFrame(
    string FrameId,
    string CallId,
    double EnergyDbfs,
    double FundamentalFrequencyHz,
    double JitterLocalPercentage,
    double ShimmerLocalPercentage,
    double MicroTremorModulationHz,
    bool IsWhisperDetected,
    DateTime TimestampUtc
);

public record WhisperEnhancementResult(
    string FrameId,
    string CallId,
    bool WhisperDetected,
    double OriginalGainDb,
    double EnhancedGainDb,
    double FormantBoostRatio,
    string StatusDescription,
    DateTime ProcessedAtUtc
);

public record VocalTremorMetrics(
    string CallId,
    double AverageTremorModulationHz,
    double JitterVariance,
    int AcuteStressScore, // 0 to 100
    string DuressLevel, // Normal, MildStress, SevereDuress
    bool SupervisorAlertTriggered,
    DateTime EvaluatedAtUtc
);

public record AnalyzeAcousticsRequest(
    string CallId,
    string CallerId,
    double EnergyDbfs,
    double FundamentalFrequencyHz,
    double JitterPercentage,
    double ShimmerPercentage,
    double ModulationFrequencyHz
);

public record VocalTremorSessionSummary(
    string CallId,
    string CallerId,
    int TotalFramesAnalyzed,
    int WhisperFramesDetected,
    int PeakStressScore,
    string FinalStressClassification,
    bool DeEscalationRecommended,
    DateTime SessionStartTimeUtc
);
