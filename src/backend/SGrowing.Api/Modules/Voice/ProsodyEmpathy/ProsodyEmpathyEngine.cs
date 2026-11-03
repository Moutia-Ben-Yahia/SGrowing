using System.Collections.Concurrent;

namespace SGrowing.Api.Modules.Voice.ProsodyEmpathy;

public interface IProsodyEmpathyEngine
{
    ProsodyEmpathyOverview GetOverview();
    ToneShifterEvent ShiftToneForCaller(string callSessionId, CallerEmotionState callerEmotion, double confidence);
    CallerEmotionState ClassifyUtteranceEmotion(string transcriptSnippet, double speechEnergyRms);
}

public sealed class ProsodyEmpathyEngine : IProsodyEmpathyEngine
{
    private readonly ConcurrentDictionary<string, ToneShifterEvent> _events = new();
    private readonly List<ProsodyModulationProfile> _profiles = [];

    public ProsodyEmpathyEngine()
    {
        SeedProfiles();
        SeedInitialEvents();
    }

    private void SeedProfiles()
    {
        _profiles.Add(new ProsodyModulationProfile(
            "PROF-CALMING-01",
            "VOICE-NEURAL-EMILY",
            PitchF0ModulationSemitones: -1.5,
            SpeechRateMultiplier: 0.88,
            WarmthHarmonicBoostDb: 3.5,
            TargetEmotionResponse: CallerEmotionState.FrustratedAngry
        ));

        _profiles.Add(new ProsodyModulationProfile(
            "PROF-REASSURING-01",
            "VOICE-NEURAL-MARCUS",
            PitchF0ModulationSemitones: -0.8,
            SpeechRateMultiplier: 0.92,
            WarmthHarmonicBoostDb: 2.8,
            TargetEmotionResponse: CallerEmotionState.AnxiousDistressed
        ));
    }

    private void SeedInitialEvents()
    {
        var evt1 = new ToneShifterEvent(
            "TONE-EVT-001",
            "CALL-SIP-7740",
            CallerEmotionState.FrustratedAngry,
            0.96,
            AppliedPitchShiftSemitones: -1.5,
            AppliedTempoAdjustmentRatio: 0.88,
            AppliedTimbreFilterName: "NeuralCalmingWarmthFilter",
            DateTime.UtcNow.AddMinutes(-4)
        );
        _events.TryAdd(evt1.EventId, evt1);
    }

    public ProsodyEmpathyOverview GetOverview()
    {
        var events = _events.Values.OrderByDescending(e => e.ShiftedAtUtc).Take(10).ToList();

        return new ProsodyEmpathyOverview(
            ActiveCallsWithToneShiftingCount: 28,
            CustomerDeEscalationSuccessRatePct: 88.4,
            AverageResponseLatencyMs: 34.2,
            RecentToneEvents: events,
            ActiveProfiles: _profiles
        );
    }

    public ToneShifterEvent ShiftToneForCaller(string callSessionId, CallerEmotionState callerEmotion, double confidence)
    {
        double pitchShift;
        double tempoRatio;
        string filterName;

        switch (callerEmotion)
        {
            case CallerEmotionState.FrustratedAngry:
                pitchShift = -1.5;
                tempoRatio = 0.88;
                filterName = "DeepCalmingEmpathyFilter";
                break;
            case CallerEmotionState.AnxiousDistressed:
                pitchShift = -0.8;
                tempoRatio = 0.92;
                filterName = "GentleReassuranceFilter";
                break;
            case CallerEmotionState.UrgentCritical:
                pitchShift = 0.0;
                tempoRatio = 1.05;
                filterName = "CrispDirectClarityFilter";
                break;
            default:
                pitchShift = 0.0;
                tempoRatio = 1.00;
                filterName = "NaturalConversationalFilter";
                break;
        }

        var evt = new ToneShifterEvent(
            $"TONE-EVT-{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}",
            callSessionId,
            callerEmotion,
            confidence,
            pitchShift,
            tempoRatio,
            filterName,
            DateTime.UtcNow
        );

        _events.TryAdd(evt.EventId, evt);
        return evt;
    }

    public CallerEmotionState ClassifyUtteranceEmotion(string transcriptSnippet, double speechEnergyRms)
    {
        var lower = transcriptSnippet.ToLowerInvariant();
        if (lower.Contains("ridiculous") || lower.Contains("unacceptable") || lower.Contains("cancel") || speechEnergyRms > 0.80)
        {
            return CallerEmotionState.FrustratedAngry;
        }
        if (lower.Contains("worried") || lower.Contains("afraid") || lower.Contains("lost") || lower.Contains("help me"))
        {
            return CallerEmotionState.AnxiousDistressed;
        }
        if (lower.Contains("immediately") || lower.Contains("emergency") || lower.Contains("asap"))
        {
            return CallerEmotionState.UrgentCritical;
        }
        if (lower.Contains("great") || lower.Contains("thank you") || lower.Contains("wonderful"))
        {
            return CallerEmotionState.SatisfiedDelighted;
        }

        return CallerEmotionState.NeutralBalanced;
    }
}
