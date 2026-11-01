using System;

namespace SGrowing.Api.Modules.Voice.Synthesis;

/// <summary>
/// Service contract for real-time customer voice sentiment analysis and de-escalating prosody modulation.
/// </summary>
public interface IAcousticSentimentControllerService
{
    /// <summary>
    /// Analyzes acoustic voice stream characteristics to detect agitation, distress, or hesitation.
    /// </summary>
    AcousticSentimentResult AnalyzeAcousticSentiment(AcousticSentimentInput input);

    /// <summary>
    /// Computes tailored prosody modulation parameters to calm agitated callers or support hesitant users.
    /// </summary>
    ProsodyModulationParams ComputeAdaptiveProsody(AcousticSentimentInput input);
}

/// <summary>
/// Production service for customer voice sentiment detection and dynamic prosody modulation.
/// Implements acoustic signal heuristics for agitation, hesitation, and de-escalation tier classification.
/// </summary>
public sealed class AcousticSentimentControllerService : IAcousticSentimentControllerService
{
    // Agitation thresholds
    private const double HighRmsThresholdDb = -12.0;
    private const double HighPitchThresholdHz = 280.0;
    private const double HighCadenceThresholdSyllablesPerSec = 6.0;

    // Moderate thresholds
    private const double ModerateRmsThresholdDb = -16.0;
    private const double ModeratePitchThresholdHz = 240.0;
    private const double ModerateCadenceThresholdSyllablesPerSec = 5.2;

    // Hesitation threshold
    private const double HesitationThreshold = 0.35;

    /// <inheritdoc />
    public AcousticSentimentResult AnalyzeAcousticSentiment(AcousticSentimentInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        double agitationScore = CalculateAgitationScore(input);
        double hesitationScore = Math.Clamp(input.HesitationPauseRatio / 0.50, 0.0, 1.0);

        string tier = DetermineCalmnessTier(input, agitationScore);
        bool isAgitated = tier is "HighEmpathy" or "DeEscalation";

        string sentimentTier = tier;
        if (!isAgitated && input.HesitationPauseRatio >= HesitationThreshold)
        {
            sentimentTier = "Hesitant";
        }

        ProsodyModulationParams prosody = BuildProsodyParams(tier);

        string summary = BuildAnalysisSummary(input, sentimentTier, agitationScore, hesitationScore);

        return new AcousticSentimentResult(
            SentimentTier: sentimentTier,
            IsAgitated: isAgitated,
            AgitationScore: Math.Round(agitationScore, 3),
            HesitationScore: Math.Round(hesitationScore, 3),
            RecommendedProsody: prosody,
            AnalysisSummary: summary
        );
    }

    /// <inheritdoc />
    public ProsodyModulationParams ComputeAdaptiveProsody(AcousticSentimentInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        double agitationScore = CalculateAgitationScore(input);
        string tier = DetermineCalmnessTier(input, agitationScore);

        return BuildProsodyParams(tier);
    }

    private static string DetermineCalmnessTier(AcousticSentimentInput input, double agitationScore)
    {
        int highViolations = (input.AudioRmsLevelDb > HighRmsThresholdDb ? 1 : 0) +
                             (input.FundamentalFrequencyHz > HighPitchThresholdHz ? 1 : 0) +
                             (input.SpeechCadenceSyllablesPerSecond > HighCadenceThresholdSyllablesPerSec ? 1 : 0);

        int moderateViolations = (input.AudioRmsLevelDb > ModerateRmsThresholdDb ? 1 : 0) +
                                 (input.FundamentalFrequencyHz > ModeratePitchThresholdHz ? 1 : 0) +
                                 (input.SpeechCadenceSyllablesPerSecond > ModerateCadenceThresholdSyllablesPerSec ? 1 : 0);

        if (highViolations >= 2 || agitationScore >= 0.65)
        {
            return "HighEmpathy";
        }

        if (highViolations >= 1 || moderateViolations >= 2 || agitationScore >= 0.35)
        {
            return "DeEscalation";
        }

        return "Normal";
    }

    private static ProsodyModulationParams BuildProsodyParams(string tier) => tier switch
    {
        "HighEmpathy" => new ProsodyModulationParams(
            PitchAdjustmentSemitones: -2.0,
            SpeakingRateMultiplier: 0.85,
            WarmthGainDb: 3.0,
            EmpathyPauseExpansionFactor: 1.4,
            CalmnessTier: "HighEmpathy"
        ),
        "DeEscalation" => new ProsodyModulationParams(
            PitchAdjustmentSemitones: -1.0,
            SpeakingRateMultiplier: 0.92,
            WarmthGainDb: 1.5,
            EmpathyPauseExpansionFactor: 1.2,
            CalmnessTier: "DeEscalation"
        ),
        _ => new ProsodyModulationParams(
            PitchAdjustmentSemitones: 0.0,
            SpeakingRateMultiplier: 1.0,
            WarmthGainDb: 0.0,
            EmpathyPauseExpansionFactor: 1.0,
            CalmnessTier: "Normal"
        )
    };

    private static double CalculateAgitationScore(AcousticSentimentInput input)
    {
        // RMS factor (-24 dB baseline to -8 dB clipping limit)
        double rmsNorm = Math.Clamp((input.AudioRmsLevelDb - (-24.0)) / ((-8.0) - (-24.0)), 0.0, 1.0);

        // Pitch factor (180 Hz conversational to 340 Hz screech/stress)
        double pitchNorm = Math.Clamp((input.FundamentalFrequencyHz - 180.0) / (340.0 - 180.0), 0.0, 1.0);

        // Cadence factor (3.8 syllables/sec steady to 7.0 rapid outburst)
        double cadenceNorm = Math.Clamp((input.SpeechCadenceSyllablesPerSecond - 3.8) / (7.0 - 3.8), 0.0, 1.0);

        return Math.Clamp((0.40 * rmsNorm) + (0.35 * pitchNorm) + (0.25 * cadenceNorm), 0.0, 1.0);
    }

    private static string BuildAnalysisSummary(AcousticSentimentInput input, string tier, double agitation, double hesitation)
    {
        return tier switch
        {
            "HighEmpathy" => $"Severe caller agitation detected (score: {agitation:F2}, RMS: {input.AudioRmsLevelDb:F1} dB, Pitch: {input.FundamentalFrequencyHz:F0} Hz, Cadence: {input.SpeechCadenceSyllablesPerSecond:F1} syl/s). High empathy and soothing vocal response triggered.",
            "DeEscalation" => $"Elevated caller tension detected (score: {agitation:F2}, RMS: {input.AudioRmsLevelDb:F1} dB, Pitch: {input.FundamentalFrequencyHz:F0} Hz). Mild de-escalation modulation engaged.",
            "Hesitant" => $"Caller hesitation or uncertainty observed (pause ratio: {input.HesitationPauseRatio:F2}, score: {hesitation:F2}). Patient pacing and clarity pauses recommended.",
            _ => $"Caller acoustic metrics within normal conversational bounds (agitation: {agitation:F2}, pause ratio: {input.HesitationPauseRatio:F2}). Standard voice prosody applied."
        };
    }
}
