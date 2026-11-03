using System;
using System.Collections.Generic;

namespace SGrowing.Api.Modules.Voice.PitchTuning;

public enum AcousticSentiment
{
    Defensive = 0,
    Skeptical = 1,
    Neutral = 2,
    Curious = 3,
    Enthusiastic = 4
}

public enum TonePersona
{
    Consultative = 0,
    Empathic = 1,
    Assertive = 2,
    Urgent = 3
}

public record LiveCallTelemetry(
    string CallId,
    string ContactName,
    string CompanyName,
    string PhoneNumber,
    AcousticSentiment CurrentSentiment,
    double ConfidenceScore,
    double FundamentalFrequencyHz, // e.g. 115 Hz - 145 Hz
    int SpeechRateWpm,            // Words per minute: e.g. 125 - 165
    TonePersona CurrentPersona,
    string RecommendedPivotAngle,
    int DurationSeconds
);

public record TunePitchCommand(
    string CallId,
    double TargetPitchShiftHz,
    int TargetSpeechRateWpm,
    TonePersona TargetPersona,
    string TacticalPitchAngle
);

public record PitchTuningResult(
    string CallId,
    double AppliedPitchShiftHz,
    int AppliedSpeechRateWpm,
    TonePersona AppliedPersona,
    string PitchAngle,
    double ProjectedDeescalationLikelihood,
    string TuningLog
);

public record PitchTuningOverview(
    int ActiveCallsCount,
    double AverageCallSentimentScore,
    double ConversionLiftPercentage,
    List<LiveCallTelemetry> ActiveCalls
);

public record SimulateSentimentShiftRequest(
    AcousticSentiment NewSentiment
);
