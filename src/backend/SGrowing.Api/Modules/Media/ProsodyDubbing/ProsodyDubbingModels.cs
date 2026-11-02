namespace SGrowing.Api.Modules.Media.ProsodyDubbing;

public sealed record PhonemeTimingSegment(
    string Phoneme,
    int StartMs,
    int EndMs,
    double PitchHz,
    double Energy
);

public sealed record VideoSceneLipAnchor(
    string SceneId,
    int StartMs,
    int EndMs,
    double MouthOpennessRatio // 0.0 to 1.0
);

public sealed record SynchronizedAudioFrame(
    string SegmentId,
    string Phoneme,
    int AdjustedStartMs,
    int AdjustedEndMs,
    double SpeedFactor
);

public sealed record AlignProsodyDubbingRequest(
    string ProjectId,
    string SourceLanguage,
    string TargetLanguage,
    int VideoSceneDurationMs,
    int SynthesizedSpeechDurationMs,
    double MeasuredLoudnessLufs,
    IReadOnlyList<PhonemeTimingSegment> Phonemes,
    IReadOnlyList<VideoSceneLipAnchor> LipAnchors
);

public sealed record ProsodyDubbingPlan(
    string DubbingPlanId,
    string ProjectId,
    double TimeScaleWarpFactor,
    int OriginalDurationMs,
    int AdjustedDurationMs,
    bool IsLipSyncFeasible,
    bool RequiresTextRewrite,
    string SuggestedRewriteAction,
    double LoudnessGainDb,
    double TargetLoudnessLufs,
    IReadOnlyList<SynchronizedAudioFrame> SynchronizedFrames,
    DateTime GeneratedAtUtc
);
