using System.Collections.Generic;
using SGrowing.Api.Modules.Advertising.CreativeBandit;
using Xunit;

namespace SGrowing.Tests.Advertising;

public class CreativeBanditTests
{
    private readonly CreativeBanditEngine _engine = new();

    [Fact]
    public void SeededCampaign_ReturnsVariantsWithAllocatedShares()
    {
        var report = _engine.GetCampaignReport("camp_growth_q1");

        Assert.NotNull(report);
        Assert.Equal("camp_growth_q1", report.CampaignId);
        Assert.Equal(3, report.Variants.Count);
        Assert.True(report.TotalConversions > 0);
        Assert.Contains(report.Variants, v => v.IsFatigued);
    }

    [Fact]
    public void AllocateTraffic_UpdatesImpressionsAndParameters()
    {
        var initial = _engine.GetCampaignReport("camp_growth_q1");
        var initialConversions = initial!.TotalConversions;

        var allocated = _engine.AllocateTraffic(new AllocateTrafficRequest("camp_growth_q1", 5000));

        Assert.NotNull(allocated);
        Assert.True(allocated.TotalConversions >= initialConversions);
    }

    [Fact]
    public void SynthesizeMutant_AddsNewEvolvedVariant()
    {
        var mutant = _engine.SynthesizeMutant("camp_growth_q1", "var_101");

        Assert.NotNull(mutant);
        Assert.StartsWith("[AI-Evolved]", mutant.Headline);
        Assert.False(mutant.IsFatigued);

        var report = _engine.GetCampaignReport("camp_growth_q1");
        Assert.Equal(4, report!.Variants.Count);
    }
}
