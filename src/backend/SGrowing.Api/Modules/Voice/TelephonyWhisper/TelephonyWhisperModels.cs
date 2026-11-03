namespace SGrowing.Api.Modules.Voice.TelephonyWhisper;

public record TelephonyCallSession(
    string CallSessionId,
    string CustomerPhoneNumber,
    string AgentName,
    string SupervisorName,
    string ActiveSupervisionMode, // MONITORING, WHISPERING, BARGE_IN, FULL_TAKEOVER
    double RoundTripLatencyMs, // e.g. 78.4ms
    string ComplianceStatus, // CLEAN, CAUTION, ESCALATED
    DateTime StartedAt,
    List<string> InjectedWhispers
);

public record WhisperPromptRecord(
    string WhisperId,
    string CallSessionId,
    string PromptText,
    string ContextCategory, // OBJECTION_HANDLING, COMPLIANCE_WARNING, PRICING_CONCESSION, TECH_SPEC
    string SupervisorName,
    DateTime Timestamp,
    bool AgentAcknowledged
);

public record TelephonyWhisperOverview(
    int ActiveSupervisedCallsCount,
    double AverageDualChannelLatencyMs,
    int TotalWhispersInjectedCount,
    int TakeoverEscalationsCount,
    List<TelephonyCallSession> ActiveSessions,
    List<WhisperPromptRecord> RecentWhispers
);

public record InjectWhisperRequest(
    string CallSessionId,
    string PromptText,
    string ContextCategory,
    string SupervisorName
);

public record TakeoverCallRequest(
    string CallSessionId,
    string NewSupervisionMode, // BARGE_IN, FULL_TAKEOVER
    string SupervisorName,
    string Justification
);
