using System;
using System.Collections.Generic;

namespace SGrowing.Api.Modules.Intelligence.DeAnonymization;

public enum NetworkType
{
    Corporate,
    ISP,
    Education,
    HostingOrCloud,
    Unknown
}

public enum IntentStage
{
    Cold,
    Awareness,
    Consideration,
    HighIntentDecision,
    ReadyToBuy
}

public record DeAnonymizeRequest(
    string IpAddress,
    string? UserAgent = null,
    string? PageUrl = null,
    string? Referrer = null,
    int TimeOnPageSeconds = 0
);

public record DeAnonymizeResult(
    string IpAddress,
    NetworkType NetworkType,
    string? Domain,
    string? CompanyName,
    string? Industry,
    string? EmployeeRange,
    string? Country,
    string? City,
    int IntentScore,
    IntentStage IntentStage,
    List<string> HighIntentTopics,
    bool ShouldAlertAccountExec,
    DateTime DetectedAtUtc
);

public record AccountIntentRecord(
    string AccountId,
    string Domain,
    string CompanyName,
    int TotalVisitsPast7Days,
    int ActiveScore,
    IntentStage Stage,
    List<string> VisitedPages,
    string? AssignedRepEmail,
    DateTime LastSeenUtc
);

public record DispatchAlertRequest(
    string Domain,
    string RecipientEmail,
    string Channel = "Slack" // Slack, Email, WhatsApp
);

public record DispatchAlertResult(
    bool Dispatched,
    string AlertId,
    string MessagePreview,
    DateTime DispatchedAtUtc
);
