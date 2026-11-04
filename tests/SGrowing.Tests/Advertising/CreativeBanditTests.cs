using System.Collections.Generic;
using System.Linq;
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
    public void GetCampaignReport_Should_Return_Null_When_Campaign_Not_Found()
    {
        var report = _engine.GetCampaignReport("non_existent_campaign_99");

        Assert.Null(report);
    }

    [Fact]
    public void GenerateVariants_Should_Create_Variants_With_Requested_Angles()
    {
        var campaignId = "camp_saas_scale_2026";
        var request = new GenerateVariantsRequest(
            campaignId,
            "Autonomous SDR",
            new List<HookAngle>
            {
                HookAngle.PainPointAgitation,
                HookAngle.ContrarianCuriosity
            }
        );

        var report = _engine.GenerateVariants(request);

        Assert.NotNull(report);
        Assert.Equal(2, report.Variants.Count);
        Assert.Equal(50.0, report.Variants[0].AllocatedShare);
        Assert.Contains(report.Variants, v => v.Angle == HookAngle.PainPointAgitation);
        Assert.Contains(report.Variants, v => v.Angle == HookAngle.ContrarianCuriosity);
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
    public void AllocateTraffic_Should_Auto_Generate_Campaign_If_Not_Present()
    {
        var newCampaignId = "camp_auto_generated_01";
        var allocated = _engine.AllocateTraffic(new AllocateTrafficRequest(newCampaignId, 3000));

        Assert.NotNull(allocated);
        Assert.Equal(newCampaignId, allocated.CampaignId);
        Assert.NotEmpty(allocated.Variants);
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

    [Fact]
    public void SynthesizeMutant_Should_Fallback_To_Default_When_Parent_Not_Found()
    {
        var mutant = _engine.SynthesizeMutant("camp_growth_q1", "non_existent_var");

        Assert.NotNull(mutant);
        Assert.StartsWith("[AI-Evolved]", mutant.Headline);
    }
}
