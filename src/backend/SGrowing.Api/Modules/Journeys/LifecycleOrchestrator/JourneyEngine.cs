using System.Collections.Concurrent;

namespace SGrowing.Api.Modules.Journeys.LifecycleOrchestrator;

public interface IJourneyEngine
{
    List<CustomerJourney> ListJourneys();
    CustomerJourney? GetJourney(string journeyId);
    JourneyEnrollment EnrollContact(EnrollContactRequest request);
    FatigueEvaluationResult EvaluateFatigue(EvaluateFatigueRequest request);
    List<JourneyEnrollment> GetEnrollments(string journeyId);
}

public class JourneyEngine : IJourneyEngine
{
    private readonly ConcurrentDictionary<string, CustomerJourney> _journeys = new();
    private readonly ConcurrentBag<JourneyEnrollment> _enrollments = new();

    public JourneyEngine()
    {
        SeedDefaultLifecycleJourney();
    }

    private void SeedDefaultLifecycleJourney()
    {
        var stages = new List<JourneyStageNode>
        {
            new(
                StageId: "stg_1_capture",
                Stage: LifecycleStage.LeadCapture,
                Name: "Inbound Lead Qualification",
                Description: "Validate email deliverability, check company ICP fit, and trigger personalized welcome sequence.",
                WaitCondition: new WaitUntilCondition("wait_verify_email", 24, "UserEmailVerified", "SendReminderEmail"),
                NextStageId: "stg_2_onboarding",
                DropoffRatePercent: 12.5
            ),
            new(
                StageId: "stg_2_onboarding",
                Stage: LifecycleStage.TrialOnboarding,
                Name: "First AI Agent Deployment",
                Description: "Guide customer to configure their first autonomous voice SDR or WhatsApp broadcast template.",
                WaitCondition: new WaitUntilCondition("wait_first_agent", 48, "AgentFirstRunCompleted", "TriggerInAppConciergeVideo"),
                NextStageId: "stg_3_activation",
                DropoffRatePercent: 18.2
            ),
            new(
                StageId: "stg_3_activation",
                Stage: LifecycleStage.ValueActivation,
                Name: "Team Seeding & Expansion",
                Description: "Invite team members, configure CRM two-way sync, and review attribution ROI dashboard.",
                WaitCondition: new WaitUntilCondition("wait_team_invites", 72, "FiveTeamMembersInvited", "ScheduleCustomerSuccessReview"),
                NextStageId: "stg_4_expansion",
                DropoffRatePercent: 14.0
            ),
            new(
                StageId: "stg_4_expansion",
                Stage: LifecycleStage.AccountExpansion,
                Name: "Annual Enterprise Contract Upgrade",
                Description: "Autonomous RevOps Deal Copilot presents custom CPQ quote with 90-day ROI guarantee.",
                WaitCondition: null,
                NextStageId: null,
                DropoffRatePercent: 8.5
            )
        };

        var journey = new CustomerJourney(
            JourneyId: "jrn_enterprise_plo",
            Name: "Enterprise Product-Led Onboarding & Expansion",
            TargetAudience: "B2B SaaS Founders & RevOps Leaders",
            Stages: stages,
            Fatigue: new FatiguePolicy(
                MaxTouchesPerWeek: 3,
                MinHoursBetweenTouches: 24,
                RestrictedChannels: new List<string> { "Voice", "WhatsApp", "Email" }
            ),
            EnrolledCount: 1420,
            OverallConversionRate: 46.8,
            IsActive: true,
            CreatedAtUtc: DateTime.UtcNow.AddDays(-14)
        );

        _journeys[journey.JourneyId] = journey;

        // Sample enrollments
        _enrollments.Add(new JourneyEnrollment(
            EnrollmentId: "enr_1",
            JourneyId: journey.JourneyId,
            ContactEmail: "cto@acme.corp",
            CurrentStage: LifecycleStage.TrialOnboarding,
            TouchesThisWeek: 2,
            EnrolledAtUtc: DateTime.UtcNow.AddDays(-2),
            LastTouchAtUtc: DateTime.UtcNow.AddHours(-18)
        ));

        _enrollments.Add(new JourneyEnrollment(
            EnrollmentId: "enr_2",
            JourneyId: journey.JourneyId,
            ContactEmail: "vp_sales@growthly.io",
            CurrentStage: LifecycleStage.ValueActivation,
            TouchesThisWeek: 1,
            EnrolledAtUtc: DateTime.UtcNow.AddDays(-5),
            LastTouchAtUtc: DateTime.UtcNow.AddHours(-36)
        ));
    }

    public List<CustomerJourney> ListJourneys() =>
        _journeys.Values.OrderByDescending(j => j.CreatedAtUtc).ToList();

    public CustomerJourney? GetJourney(string journeyId) =>
        _journeys.TryGetValue(journeyId, out var journey) ? journey : null;

    public JourneyEnrollment EnrollContact(EnrollContactRequest request)
    {
        if (!_journeys.TryGetValue(request.JourneyId, out var journey))
        {
            throw new KeyNotFoundException($"Customer journey '{request.JourneyId}' not found.");
        }

        var enrollment = new JourneyEnrollment(
            EnrollmentId: "enr_" + Guid.NewGuid().ToString("N")[..8],
            JourneyId: request.JourneyId,
            ContactEmail: request.ContactEmail,
            CurrentStage: journey.Stages.First().Stage,
            TouchesThisWeek: 0,
            EnrolledAtUtc: DateTime.UtcNow,
            LastTouchAtUtc: null
        );

        _enrollments.Add(enrollment);

        // Update journey enrolled counter
        _journeys[request.JourneyId] = journey with
        {
            EnrolledCount = journey.EnrolledCount + 1
        };

        return enrollment;
    }

    public FatigueEvaluationResult EvaluateFatigue(EvaluateFatigueRequest request)
    {
        var active = _enrollments.FirstOrDefault(e => e.ContactEmail.Equals(request.ContactEmail, StringComparison.OrdinalIgnoreCase));
        var touches = active?.TouchesThisWeek ?? 1;
        const int maxTouches = 3;

        if (touches >= maxTouches)
        {
            return new FatigueEvaluationResult(
                ContactEmail: request.ContactEmail,
                ProposedChannel: request.ProposedChannel,
                CanSend: false,
                Reason: $"Weekly frequency cap reached ({touches}/{maxTouches} touches). Outbound message suppressed to prevent unsubscribe churn.",
                TouchesThisWeek: touches,
                MaxTouchesAllowed: maxTouches
            );
        }

        if (active?.LastTouchAtUtc != null && (DateTime.UtcNow - active.LastTouchAtUtc.Value).TotalHours < 24)
        {
            var remainingHours = Math.Round(24 - (DateTime.UtcNow - active.LastTouchAtUtc.Value).TotalHours, 1);
            return new FatigueEvaluationResult(
                ContactEmail: request.ContactEmail,
                ProposedChannel: request.ProposedChannel,
                CanSend: false,
                Reason: $"Minimum cooldown spacing active. Please wait {remainingHours} more hours before sending another touch.",
                TouchesThisWeek: touches,
                MaxTouchesAllowed: maxTouches
            );
        }

        return new FatigueEvaluationResult(
            ContactEmail: request.ContactEmail,
            ProposedChannel: request.ProposedChannel,
            CanSend: true,
            Reason: "Contact is eligible for outreach within frequency and spacing policies.",
            TouchesThisWeek: touches,
            MaxTouchesAllowed: maxTouches
        );
    }

    public List<JourneyEnrollment> GetEnrollments(string journeyId) =>
        _enrollments.Where(e => e.JourneyId == journeyId).ToList();
}
