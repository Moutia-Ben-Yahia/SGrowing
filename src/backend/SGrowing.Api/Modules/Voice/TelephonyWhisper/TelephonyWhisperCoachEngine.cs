using System.Collections.Concurrent;

namespace SGrowing.Api.Modules.Voice.TelephonyWhisper;

public class TelephonyWhisperCoachEngine
{
    private readonly ConcurrentDictionary<string, TelephonyCallSession> _sessions = new();
    private readonly ConcurrentBag<WhisperPromptRecord> _whispers = new();

    public TelephonyWhisperCoachEngine()
    {
        SeedDemoData();
    }

    private void SeedDemoData()
    {
        var s1 = new TelephonyCallSession(
            "CALL-WHISP-001",
            "+1-415-555-8910",
            "Jordan Reed (Mid-Market AE)",
            "Elena Rostova (Sales Director)",
            "WHISPERING",
            82.4,
            "CLEAN",
            DateTime.UtcNow.AddMinutes(-8),
            ["Highlight ISO27001 and SOC2 Type II audit attestations", "Stand firm on multi-year 15% discount cap"]
        );

        var s2 = new TelephonyCallSession(
            "CALL-WHISP-002",
            "+44-20-7946-0922",
            "David Sterling (Enterprise Rep)",
            "Elena Rostova (Sales Director)",
            "MONITORING",
            64.1,
            "CLEAN",
            DateTime.UtcNow.AddMinutes(-14),
            ["Mention UK sovereign cloud hosting option in London"]
        );

        _sessions[s1.CallSessionId] = s1;
        _sessions[s2.CallSessionId] = s2;

        _whispers.Add(new WhisperPromptRecord(
            "WSP-001",
            "CALL-WHISP-001",
            "Highlight ISO27001 and SOC2 Type II audit attestations",
            "TECH_SPEC",
            "Elena Rostova (Sales Director)",
            DateTime.UtcNow.AddMinutes(-4),
            true
        ));

        _whispers.Add(new WhisperPromptRecord(
            "WSP-002",
            "CALL-WHISP-001",
            "Stand firm on multi-year 15% discount cap",
            "PRICING_CONCESSION",
            "Elena Rostova (Sales Director)",
            DateTime.UtcNow.AddMinutes(-2),
            true
        ));
    }

    public TelephonyWhisperOverview GetOverview()
    {
        var sessionsList = _sessions.Values.ToList();
        var whispersList = _whispers.ToList();

        double avgLatency = sessionsList.Count > 0
            ? Math.Round(sessionsList.Average(s => s.RoundTripLatencyMs), 1)
            : 75.0;

        int takeovers = sessionsList.Count(s => s.ActiveSupervisionMode is "BARGE_IN" or "FULL_TAKEOVER");

        return new TelephonyWhisperOverview(
            sessionsList.Count,
            avgLatency,
            whispersList.Count,
            takeovers,
            sessionsList,
            whispersList
        );
    }

    public WhisperPromptRecord InjectWhisper(InjectWhisperRequest request)
    {
        var whisperId = $"WSP-{Guid.NewGuid():N}"[..10].ToUpperInvariant();
        var record = new WhisperPromptRecord(
            whisperId,
            request.CallSessionId,
            request.PromptText,
            request.ContextCategory,
            request.SupervisorName,
            DateTime.UtcNow,
            false
        );

        _whispers.Add(record);

        if (_sessions.TryGetValue(request.CallSessionId, out var session))
        {
            var updatedList = new List<string>(session.InjectedWhispers) { request.PromptText };
            var updatedSession = session with
            {
                InjectedWhispers = updatedList,
                ActiveSupervisionMode = "WHISPERING"
            };
            _sessions[request.CallSessionId] = updatedSession;
        }

        return record;
    }

    public TelephonyCallSession TransitionSupervisionMode(TakeoverCallRequest request)
    {
        if (!_sessions.TryGetValue(request.CallSessionId, out var session))
        {
            session = new TelephonyCallSession(
                request.CallSessionId,
                "+1-800-ENTERPRISE",
                "Assigned Representative",
                request.SupervisorName,
                request.NewSupervisionMode,
                68.0,
                "ESCALATED",
                DateTime.UtcNow,
                []
            );
        }

        var updatedSession = session with
        {
            ActiveSupervisionMode = request.NewSupervisionMode,
            SupervisorName = request.SupervisorName,
            ComplianceStatus = request.NewSupervisionMode == "FULL_TAKEOVER" ? "ESCALATED" : session.ComplianceStatus
        };

        _sessions[request.CallSessionId] = updatedSession;
        return updatedSession;
    }
}
