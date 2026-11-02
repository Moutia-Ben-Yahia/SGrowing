namespace SGrowing.Api.Modules.Voice.SentimentDeescalation;

/// <summary>
/// Real-time acoustic pitch, speech velocity, and lexical sentiment classifier
/// evaluating caller emotional agitation and prescribing de-escalation pacing adjustments.
/// </summary>
public sealed class AcousticSentimentClassifierEngine
{
    public EmotionalStateEvaluation ClassifyFrame(VoiceCallAcousticFrame frame)
    {
        ArgumentNullException.ThrowIfNull(frame);

        // 1. Pitch Delta Component (High pitch spike above baseline indicates emotional stress/screaming)
        var deltaPitch = Math.Max(0.0, frame.PitchF0Hz - frame.BaselineF0Hz);
        var pitchScore = Math.Min(3.0, (deltaPitch / 40.0) * 2.0);

        // 2. Speech Velocity Component (Rapid speaking > 160 WPM indicates agitation)
        var excessWpm = Math.Max(0.0, frame.SpeechRateWordsPerMinute - 160.0);
        var wpmScore = Math.Min(2.5, (excessWpm / 30.0) * 2.0);

        // 3. Decibel Energy Spurt (> 72 dB is shouting over phone microphone)
        var excessDb = Math.Max(0.0, frame.VolumeDb - 72.0);
        var dbScore = Math.Min(2.5, (excessDb / 8.0) * 2.0);

        // 4. Lexical Negative Keyword Component
        var keywordScore = Math.Min(2.0, frame.NegativeSentimentKeywordCount * 0.7);

        var totalAgitation = Math.Clamp(pitchScore + wpmScore + dbScore + keywordScore, 0.0, 10.0);

        CallerEmotionalState state;
        DeescalationInterventionType intervention;
        double tempoAdj = 1.0;
        double pitchMod = 0.0;
        decimal? goodwillCredit = null;
        string rationale;

        if (totalAgitation >= 7.0 || frame.NegativeSentimentKeywordCount >= 3)
        {
            state = CallerEmotionalState.SevereAngerEscalation;
            intervention = DeescalationInterventionType.ImmediateSupervisorTakeover;
            tempoAdj = 0.80; // Slow down substantially
            pitchMod = -2.5; // Deeper reassuring voice register
            goodwillCredit = 25.00m; // Instant 25 EUR courtesy credit
            rationale = $"CRITICAL AGITATION ({totalAgitation:F1}/10): High vocal pitch ({frame.PitchF0Hz:F0}Hz) and volume shouting. Immediate supervisor transfer initiated with courtesy credit.";
        }
        else if (totalAgitation >= 4.0)
        {
            state = CallerEmotionalState.MildFrustration;
            intervention = DeescalationInterventionType.OfferGoodwillVoucherCredit;
            tempoAdj = 0.88; // 12% slower cadence
            pitchMod = -1.5;
            goodwillCredit = 10.00m;
            rationale = $"MILD FRUSTRATION ({totalAgitation:F1}/10): Accelerated speech ({frame.SpeechRateWordsPerMinute:F0} WPM). Soften voice prosody and present 10 EUR resolution voucher.";
        }
        else if (totalAgitation >= 2.0)
        {
            state = CallerEmotionalState.NeutralCalm;
            intervention = DeescalationInterventionType.SlowDownTempoAndSoftenTone;
            tempoAdj = 0.95;
            pitchMod = -0.5;
            rationale = $"NOMINAL CALM ({totalAgitation:F1}/10): Minor conversational variation. Maintain active listening tempo.";
        }
        else
        {
            state = CallerEmotionalState.Delighted;
            intervention = DeescalationInterventionType.MaintainStandardPacing;
            tempoAdj = 1.0;
            pitchMod = 0.0;
            rationale = "DELIGHTED: Relaxed pitch and steady cadence. Standard conversational pacing maintained.";
        }

        return new EmotionalStateEvaluation(
            CallId: frame.CallId,
            EvaluatedAtUtc: DateTime.UtcNow,
            State: state,
            AgitationIndex: Math.Round(totalAgitation, 2),
            RecommendedIntervention: intervention,
            SpeechTempoAdjustmentRatio: Math.Round(tempoAdj, 2),
            PitchModulationSemitones: Math.Round(pitchMod, 1),
            SuggestedGoodwillCreditEur: goodwillCredit,
            Rationale: rationale
        );
    }
}
