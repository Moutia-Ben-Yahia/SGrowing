namespace SGrowing.Api.Modules.Journeys.LifecycleOrchestrator;

public enum LifecycleStage
{
    LeadCapture,
    TrialOnboarding,
    ValueActivation,
    AccountExpansion,
    RenewalSafeguard,
    ChurnWinback
}

public record WaitUntilCondition(
    string ConditionKey,
    int MaxWaitHours,
    string TargetEvent,
    string FallbackAction
);

public record JourneyStageNode(
    string StageId,
    LifecycleStage Stage,
    string Name,
    string Description,
    WaitUntilCondition? WaitCondition,
    string? NextStageId,
    double DropoffRatePercent
);

public record FatiguePolicy(
    int MaxTouchesPerWeek,
    int MinHoursBetweenTouches,
    List<string> RestrictedChannels
);

public record CustomerJourney(
    string JourneyId,
    string Name,
    string TargetAudience,
    List<JourneyStageNode> Stages,
    FatiguePolicy Fatigue,
    int EnrolledCount,
    double OverallConversionRate,
    bool IsActive,
    DateTime CreatedAtUtc
);

public record JourneyEnrollment(
    string EnrollmentId,
    string JourneyId,
    string ContactEmail,
    LifecycleStage CurrentStage,
    int TouchesThisWeek,
    DateTime EnrolledAtUtc,
    DateTime? LastTouchAtUtc
);

public record EnrollContactRequest(
    string JourneyId,
    string ContactEmail
);

public record EvaluateFatigueRequest(
    string ContactEmail,
    string ProposedChannel
);

public record FatigueEvaluationResult(
    string ContactEmail,
    string ProposedChannel,
    bool CanSend,
    string Reason,
    int TouchesThisWeek,
    int MaxTouchesAllowed
);
