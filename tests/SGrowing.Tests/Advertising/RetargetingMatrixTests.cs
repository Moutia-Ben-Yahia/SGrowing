using SGrowing.Api.Modules.Advertising.RetargetingMatrix;
using Xunit;

namespace SGrowing.Tests.Advertising;

public class RetargetingMatrixTests
{
    private readonly RetargetingMatrixEngine _engine = new();

    [Fact]
    public void EvaluateImpression_WithinCap_ApprovesAndIncrements()
    {
        var request = new EvaluateImpressionRequest("nexustech.io", "LinkedIn");
        var decision = _engine.EvaluateImpression(request);

        Assert.True(decision.ShouldServeImpression);
        Assert.Equal("3-Year TCO & 4.2 Month Payback ROI Deck", decision.SelectedCreativeTheme);
        Assert.Equal(2, decision.DailyImpressionsServed);
    }

    [Fact]
    public void EvaluateImpression_AtFrequencyCap_RejectsToPreventAdFatigue()
    {
        var request = new EvaluateImpressionRequest("capprofit.com", "Meta");
        var decision = _engine.EvaluateImpression(request);

        Assert.False(decision.ShouldServeImpression);
        Assert.Contains("Daily frequency cap reached", decision.Reason);
    }

    [Fact]
    public void SuppressedAccount_IsInstantlyBlocked()
    {
        var request = new EvaluateImpressionRequest("churnedlead.org", "GoogleDSP");
        var decision = _engine.EvaluateImpression(request);

        Assert.False(decision.ShouldServeImpression);
        Assert.Contains("suppressed", decision.Reason);
    }

    [Fact]
    public void EvaluateImpression_Auto_Registers_New_Account_Domain()
    {
        var newDomain = "freshlead.tech";
        var decision = _engine.EvaluateImpression(new EvaluateImpressionRequest(newDomain, "TwitterDSP"));

        Assert.True(decision.ShouldServeImpression);
        Assert.Equal(1, decision.DailyImpressionsServed);
        Assert.Equal("3-Year TCO & 4.2 Month Payback ROI Deck", decision.SelectedCreativeTheme);

        var profile = _engine.GetProfile(newDomain);
        Assert.NotNull(profile);
        Assert.Equal(RetargetingStage.PricingPageEvaluator, profile.Stage);
    }

    [Fact]
    public void EvaluateImpression_Serves_Specific_Themes_For_Different_Stages()
    {
        var demoDomain = "demo-prospect.io";
        _engine.UpdateStage(new UpdateBuyerStageRequest(demoDomain, RetargetingStage.DemoAttendee));

        var decisionDemo = _engine.EvaluateImpression(new EvaluateImpressionRequest(demoDomain, "LinkedIn"));
        Assert.True(decisionDemo.ShouldServeImpression);
        Assert.Equal("SOC2 Type II & Zero-Trust Architecture Brief", decisionDemo.SelectedCreativeTheme);

        var stalledDomain = "stalled-deal.io";
        _engine.UpdateStage(new UpdateBuyerStageRequest(stalledDomain, RetargetingStage.StalledProposal));

        var decisionStalled = _engine.EvaluateImpression(new EvaluateImpressionRequest(stalledDomain, "Meta"));
        Assert.True(decisionStalled.ShouldServeImpression);
        Assert.Equal("Verified Enterprise Video Testimonials Carousel", decisionStalled.SelectedCreativeTheme);
    }

    [Fact]
    public void UpdateStage_To_ClosedWonCustomer_Automatically_Suppresses_Future_Impressions()
    {
        var customerDomain = "enterprise-client.com";
        _engine.UpdateStage(new UpdateBuyerStageRequest(customerDomain, RetargetingStage.ClosedWonCustomer));

        var profile = _engine.GetProfile(customerDomain);
        Assert.NotNull(profile);
        Assert.True(profile.IsSuppressed);

        var decision = _engine.EvaluateImpression(new EvaluateImpressionRequest(customerDomain, "LinkedIn"));
        Assert.False(decision.ShouldServeImpression);
        Assert.Contains("suppressed", decision.Reason);
    }

    [Fact]
    public void GetProfile_Returns_Null_For_Unknown_Domain()
    {
        var profile = _engine.GetProfile("completely-unknown-domain-99.org");
        Assert.Null(profile);
    }
}
