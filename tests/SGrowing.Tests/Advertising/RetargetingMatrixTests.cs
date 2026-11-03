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
}
