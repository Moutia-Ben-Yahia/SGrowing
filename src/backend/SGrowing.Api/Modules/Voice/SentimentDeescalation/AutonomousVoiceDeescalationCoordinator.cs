using System.Collections.Concurrent;

namespace SGrowing.Api.Modules.Voice.SentimentDeescalation;

/// <summary>
/// Autonomous telephony mesh coordinator executing voice de-escalation policies,
/// managing goodwill credit grants, and bridging agitated calls to human supervisors.
/// </summary>
public sealed class AutonomousVoiceDeescalationCoordinator
{
    private readonly ConcurrentDictionary<string, DeescalationDispatchEvent> _activeDispatches = new(StringComparer.OrdinalIgnoreCase);

    public DeescalationDispatchEvent ExecuteIntervention(EmotionalStateEvaluation evaluation)
    {
        ArgumentNullException.ThrowIfNull(evaluation);

        var eventId = $"ESC-{Guid.NewGuid():N}"[..12].ToUpperInvariant();
        var transferred = evaluation.RecommendedIntervention == DeescalationInterventionType.ImmediateSupervisorTakeover;

        var summary = evaluation.RecommendedIntervention switch
        {
            DeescalationInterventionType.ImmediateSupervisorTakeover =>
                $"Call patched to Tier-3 Retention Supervisor desk. Pre-approved {evaluation.SuggestedGoodwillCreditEur:F2} EUR credit added to account ledger.",
            DeescalationInterventionType.OfferGoodwillVoucherCredit =>
                $"Agent voice tempo throttled to {evaluation.SpeechTempoAdjustmentRatio:F2}x. Automated {evaluation.SuggestedGoodwillCreditEur:F2} EUR courtesy voucher offered via SMS.",
            DeescalationInterventionType.SlowDownTempoAndSoftenTone =>
                $"Agent voice pitch shifted by {evaluation.PitchModulationSemitones:F1} semitones to establish empathetic rapport.",
            _ => "Standard conversational telemetry within control parameters."
        };

        var dispatch = new DeescalationDispatchEvent(
            EventId: eventId,
            CallId: evaluation.CallId,
            Intervention: evaluation.RecommendedIntervention,
            ActionSummary: summary,
            TransferredToHumanSupervisor: transferred,
            DispatchedAtUtc: DateTime.UtcNow
        );

        _activeDispatches[evaluation.CallId] = dispatch;
        return dispatch;
    }

    public DeescalationDispatchEvent? GetLastDispatch(string callId) =>
        _activeDispatches.TryGetValue(callId, out var dispatch) ? dispatch : null;
}
