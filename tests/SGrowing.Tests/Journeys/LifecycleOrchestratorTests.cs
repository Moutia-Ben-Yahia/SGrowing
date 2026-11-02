using SGrowing.Api.Modules.Journeys.LifecycleOrchestrator;

namespace SGrowing.Tests.Journeys;

public class LifecycleOrchestratorTests
{
    private readonly IJourneyEngine _engine = new JourneyEngine();

    [Fact]
    public void ListJourneys_ReturnsSeededLifecycleJourneyWithStages()
    {
        var journeys = _engine.ListJourneys();

        Assert.NotEmpty(journeys);
        Assert.Equal("jrn_enterprise_plo", journeys[0].JourneyId);
        Assert.Equal(4, journeys[0].Stages.Count);
        Assert.NotNull(journeys[0].Stages[0].WaitCondition);
        Assert.True(journeys[0].OverallConversionRate > 40.0);
    }

    [Fact]
    public void EnrollContact_AddsNewEnrollmentAndIncrementsCounter()
    {
        var initial = _engine.GetJourney("jrn_enterprise_plo")!.EnrolledCount;

        var enrollment = _engine.EnrollContact(new EnrollContactRequest(
            JourneyId: "jrn_enterprise_plo",
            ContactEmail: "founder@hypergrowth.tech"
        ));

        Assert.NotNull(enrollment);
        Assert.Equal("founder@hypergrowth.tech", enrollment.ContactEmail);
        Assert.Equal(LifecycleStage.LeadCapture, enrollment.CurrentStage);

        var updated = _engine.GetJourney("jrn_enterprise_plo");
        Assert.Equal(initial + 1, updated!.EnrolledCount);
    }

    [Fact]
    public void EvaluateFatigue_EnforcesCooldownOrWeeklyLimit()
    {
        var result = _engine.EvaluateFatigue(new EvaluateFatigueRequest(
            ContactEmail: "cto@acme.corp",
            ProposedChannel: "WhatsApp"
        ));

        Assert.NotNull(result);
        Assert.False(result.CanSend);
        Assert.Contains("cooldown spacing", result.Reason);
    }

    [Fact]
    public void EvaluateFatigue_ApprovesFreshContact()
    {
        var result = _engine.EvaluateFatigue(new EvaluateFatigueRequest(
            ContactEmail: "fresh_lead@enterprise.corp",
            ProposedChannel: "Email"
        ));

        Assert.NotNull(result);
        Assert.True(result.CanSend);
    }
}
