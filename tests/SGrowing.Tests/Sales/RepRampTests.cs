using SGrowing.Api.Modules.Sales.RepRamp;
using Xunit;

namespace SGrowing.Tests.Sales;

public class RepRampTests
{
    private readonly RepRampEngine _engine = new();

    [Fact]
    public void SeededProfiles_ArePopulatedWithCompetencies()
    {
        var profiles = _engine.GetProfiles();
        Assert.NotEmpty(profiles);
        var alex = profiles.FirstOrDefault(p => p.RepId == "REP-RAMP-101");
        Assert.NotNull(alex);
        Assert.Equal("Enterprise Account Executive", alex.Role);
        Assert.NotEmpty(alex.Competencies);
    }

    [Fact]
    public void GenerateShadowGuidance_DetectsPricingConstraint()
    {
        var request = new ShadowCallGuidanceRequest(
            RepId: "REP-RAMP-101",
            CallId: "CALL-TEST-001",
            CustomerPersona: "CFO",
            LiveTranscriptSegment: "We love the product but your price is simply too high and over budget.",
            CurrentTopic: "Commercial Terms"
        );

        var guidance = _engine.GenerateShadowGuidance(request);

        Assert.NotNull(guidance);
        Assert.Contains("Budget", guidance.DetectedIntent);
        Assert.Contains("Net Value", guidance.RecommendedWhisperPrompt);
        Assert.True(guidance.ConfidenceScore >= 90);
    }

    [Fact]
    public void CalculateTrajectory_ReturnsVelocityAndModules()
    {
        var trajectory = _engine.GetRampTrajectory("REP-RAMP-101");

        Assert.NotNull(trajectory);
        Assert.Equal("REP-RAMP-101", trajectory.RepId);
        Assert.True(trajectory.ProjectedWeeksToFullQuota > 0);
        Assert.NotEmpty(trajectory.RecommendedCurriculumModules);
    }
}
