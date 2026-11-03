namespace SGrowing.Api.Modules.Sales.RepRamp;

public enum RampStage
{
    Onboarding,
    Shadowing,
    SoloRamping,
    FullyRamped
}

public record RepCompetency(
    string CompetencyName,
    int Score,
    int TargetScore,
    string Status
);

public record RepProfile(
    string RepId,
    string Name,
    string Role,
    RampStage Stage,
    int WeeksInRole,
    int TargetWeeksToRamp,
    List<RepCompetency> Competencies,
    decimal QuotaAttainmentPercentage,
    int DealsClosed,
    DateTime CreatedAtUtc
);

public record CreateRepProfileRequest(
    string Name,
    string Role,
    int TargetWeeksToRamp
);

public record ShadowCallGuidanceRequest(
    string RepId,
    string CallId,
    string CustomerPersona,
    string LiveTranscriptSegment,
    string CurrentTopic
);

public record ShadowCallGuidanceResult(
    string GuidanceId,
    string RepId,
    string CallId,
    string DetectedIntent,
    string MissedCue,
    string RecommendedWhisperPrompt,
    string MicroCoachingTip,
    int ConfidenceScore,
    DateTime TimestampUtc
);

public record RampTrajectoryProjection(
    string RepId,
    int CurrentWeeks,
    int ProjectedWeeksToFirstDeal,
    int ProjectedWeeksToFullQuota,
    decimal ProjectedRunRateArr,
    string VelocityAssessment,
    List<string> RecommendedCurriculumModules,
    DateTime EvaluatedAtUtc
);
