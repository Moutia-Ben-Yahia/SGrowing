using System.Collections.Concurrent;

namespace SGrowing.Api.Modules.Sales.RepRamp;

public interface IRepRampEngine
{
    RepProfile CreateOrUpdateRepProfile(CreateRepProfileRequest request);
    IReadOnlyList<RepProfile> GetProfiles();
    RepProfile? GetProfile(string repId);
    ShadowCallGuidanceResult GenerateShadowGuidance(ShadowCallGuidanceRequest request);
    RampTrajectoryProjection GetRampTrajectory(string repId);
}

public class RepRampEngine : IRepRampEngine
{
    private readonly ConcurrentDictionary<string, RepProfile> _profiles = new();
    private readonly ConcurrentBag<ShadowCallGuidanceResult> _guidanceLogs = new();

    public RepRampEngine()
    {
        // Seed standard ramping reps
        SeedInitialProfiles();
    }

    private void SeedInitialProfiles()
    {
        var rep1 = new RepProfile(
            RepId: "REP-RAMP-101",
            Name: "Alex Vance",
            Role: "Enterprise Account Executive",
            Stage: RampStage.Shadowing,
            WeeksInRole: 4,
            TargetWeeksToRamp: 12,
            Competencies: new List<RepCompetency>
            {
                new("Product Depth", 85, 90, "Progressing"),
                new("Economic Discovery", 62, 85, "AttentionNeeded"),
                new("Objection Handling", 78, 85, "OnTrack"),
                new("Pricing Negotiation", 55, 80, "CriticalGap"),
                new("Stage-Gate Progression", 70, 80, "Progressing")
            },
            QuotaAttainmentPercentage: 28.5m,
            DealsClosed: 1,
            CreatedAtUtc: DateTime.UtcNow.AddDays(-28)
        );

        var rep2 = new RepProfile(
            RepId: "REP-RAMP-102",
            Name: "Elena Rostova",
            Role: "Strategic SDR",
            Stage: RampStage.SoloRamping,
            WeeksInRole: 8,
            TargetWeeksToRamp: 10,
            Competencies: new List<RepCompetency>
            {
                new("Cold Outbound Prospecting", 92, 90, "Mastered"),
                new("ICP Qualification", 88, 85, "Mastered"),
                new("Objection Handling", 84, 85, "OnTrack"),
                new("Executive Hook Delivery", 79, 80, "OnTrack"),
                new("CRM Pipeline Hygiene", 95, 90, "Mastered")
            },
            QuotaAttainmentPercentage: 86.4m,
            DealsClosed: 14,
            CreatedAtUtc: DateTime.UtcNow.AddDays(-56)
        );

        _profiles[rep1.RepId] = rep1;
        _profiles[rep2.RepId] = rep2;
    }

    public RepProfile CreateOrUpdateRepProfile(CreateRepProfileRequest request)
    {
        var repId = $"REP-RAMP-{GuidanceSeed()}";
        var competencies = new List<RepCompetency>
        {
            new("Product Depth", 50, 90, "InTraining"),
            new("Economic Discovery", 45, 85, "InTraining"),
            new("Objection Handling", 40, 85, "InTraining"),
            new("Pricing Negotiation", 35, 80, "InTraining"),
            new("Stage-Gate Progression", 50, 80, "InTraining")
        };

        var profile = new RepProfile(
            RepId: repId,
            Name: request.Name,
            Role: request.Role,
            Stage: RampStage.Onboarding,
            WeeksInRole: 1,
            TargetWeeksToRamp: request.TargetWeeksToRamp > 0 ? request.TargetWeeksToRamp : 12,
            Competencies: competencies,
            QuotaAttainmentPercentage: 0m,
            DealsClosed: 0,
            CreatedAtUtc: DateTime.UtcNow
        );

        _profiles[repId] = profile;
        return profile;
    }

    public IReadOnlyList<RepProfile> GetProfiles()
    {
        return _profiles.Values.OrderByDescending(p => p.WeeksInRole).ToList();
    }

    public RepProfile? GetProfile(string repId)
    {
        _profiles.TryGetValue(repId, out var profile);
        return profile;
    }

    public ShadowCallGuidanceResult GenerateShadowGuidance(ShadowCallGuidanceRequest request)
    {
        var lower = request.LiveTranscriptSegment.ToLowerInvariant();
        string intent = "Discovery Inquiry";
        string missedCue = "No explicit cost-of-inaction quantified";
        string whisper = "Ask: 'What happens to your Q4 deliverables if this infrastructure problem remains unsolved?'";
        string microCoaching = "Pivot away from technical feature listing to executive business impact and ROI timeline.";
        int confidence = 88;

        if (lower.Contains("price") || lower.Contains("budget") || lower.Contains("expensive"))
        {
            intent = "Budget & Pricing Constraint";
            missedCue = "Prospect mentioned pricing pushback before value baseline was established";
            whisper = "Anchor on Net Value: 'Before we discuss tiers, can we confirm the $200k in downtime this prevents?'";
            microCoaching = "Defend margin: Never offer immediate discounting without trade-offs on commitment length or scope.";
            confidence = 94;
        }
        else if (lower.Contains("competitor") || lower.Contains("alternative") || lower.Contains("evaluating"))
        {
            intent = "Competitive Evaluation";
            missedCue = "Competitor mentioned; rep must position architectural differentiation rather than feature parities";
            whisper = "Pivot: 'Many clients evaluate Vendor X, but switch when they hit sub-100ms latency bottlenecks at scale.'";
            microCoaching = "Deploy the Symbiotic Augmentation battlecard instead of disparaging competitor.";
            confidence = 92;
        }
        else if (lower.Contains("timing") || lower.Contains("next quarter") || lower.Contains("delay"))
        {
            intent = "Urgency & Timing Stall";
            missedCue = "Prospect pushing timeline without addressing current compounding burn rate";
            whisper = "Urgency check: 'Understood. Is the cost of delaying 90 days acceptable to your VP of Engineering?'";
            microCoaching = "Quantify cost of delay: Highlight weekly churn/loss while waiting.";
            confidence = 90;
        }

        var result = new ShadowCallGuidanceResult(
            GuidanceId: $"GUIDE-{GuidanceSeed()}",
            RepId: request.RepId,
            CallId: string.IsNullOrWhiteSpace(request.CallId) ? $"CALL-{GuidanceSeed()}" : request.CallId,
            DetectedIntent: intent,
            MissedCue: missedCue,
            RecommendedWhisperPrompt: whisper,
            MicroCoachingTip: microCoaching,
            ConfidenceScore: confidence,
            TimestampUtc: DateTime.UtcNow
        );

        _guidanceLogs.Add(result);
        return result;
    }

    public RampTrajectoryProjection GetRampTrajectory(string repId)
    {
        var profile = GetProfile(repId) ?? new RepProfile(
            RepId: repId,
            Name: "Unknown Rep",
            Role: "Account Executive",
            Stage: RampStage.Onboarding,
            WeeksInRole: 2,
            TargetWeeksToRamp: 12,
            Competencies: new(),
            QuotaAttainmentPercentage: 10m,
            DealsClosed: 0,
            CreatedAtUtc: DateTime.UtcNow
        );

        int weeksLeft = Math.Max(1, profile.TargetWeeksToRamp - profile.WeeksInRole);
        int projectedFirstDeal = profile.DealsClosed > 0 ? profile.WeeksInRole : Math.Min(profile.WeeksInRole + 2, 6);
        int projectedFullQuota = profile.WeeksInRole >= profile.TargetWeeksToRamp ? profile.WeeksInRole : profile.TargetWeeksToRamp;
        decimal runRate = profile.QuotaAttainmentPercentage > 0
            ? Math.Round(profile.QuotaAttainmentPercentage * 12500m, 2)
            : 350000m;

        string velocity = profile.QuotaAttainmentPercentage switch
        {
            >= 80m => "Accelerated (Ahead of Cohort)",
            >= 40m => "On Track (Cohort Average)",
            _ => "Ramp Risk (Requires Shadow Support)"
        };

        var modules = new List<string>
        {
            "Advanced Multi-Stakeholder Discovery",
            "Executive Business Case Financial Modeling",
            "Competitive Objection Reframing Lab"
        };

        return new RampTrajectoryProjection(
            RepId: repId,
            CurrentWeeks: profile.WeeksInRole,
            ProjectedWeeksToFirstDeal: projectedFirstDeal,
            ProjectedWeeksToFullQuota: projectedFullQuota,
            ProjectedRunRateArr: runRate,
            VelocityAssessment: velocity,
            RecommendedCurriculumModules: modules,
            EvaluatedAtUtc: DateTime.UtcNow
        );
    }

    private static int _seedCounter = 3000;
    private static int GuidanceSeed() => Interlocked.Increment(ref _seedCounter);
}
