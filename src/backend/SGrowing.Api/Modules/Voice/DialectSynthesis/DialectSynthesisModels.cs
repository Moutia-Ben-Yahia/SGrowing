namespace SGrowing.Api.Modules.Voice.DialectSynthesis;

public record DialectProfile(
    string DialectCode,
    string DialectName,
    string Region,
    List<string> CharacteristicIdioms,
    double PitchBaselineOffsetHz,
    double SpeechCadenceMultiplier,
    string FormalityLevel,
    DateTime CreatedAtUtc
);

public record DetectDialectRequest(
    string CallId,
    string CallerRawTranscript,
    double DetectedPitchHz,
    double DetectedCadenceRate
);

public record DetectedDialectResult(
    string CallId,
    string DetectedDialectCode,
    string DetectedDialectName,
    int ConfidenceScore,
    List<string> ExtractedVernacularTokens,
    string CulturalPersonaAlignment,
    DateTime EvaluatedAtUtc
);

public record SynthesizeDialectRequest(
    string CallId,
    string TargetDialectCode,
    string StandardResponseText
);

public record SynthesizedUtterance(
    string UtteranceId,
    string CallId,
    string TargetDialectCode,
    string OriginalText,
    string DialectAdaptedText,
    int SynthesisLatencyMs,
    double PitchModulationHz,
    double CadenceMultiplier,
    DateTime SynthesizedAtUtc
);
