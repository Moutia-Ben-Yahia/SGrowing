using System;
using System.Linq;
using SGrowing.Api.Modules.Voice.VoiceDialect;
using Xunit;

namespace SGrowing.Tests.Voice;

public sealed class VoiceDialectTests
{
    [Fact]
    public void GetOverview_ReturnsInitialProfilesAndAdaptations()
    {
        var engine = new VoiceDialectEngine();
        var overview = engine.GetOverview();

        Assert.NotNull(overview);
        Assert.True(overview.SupportedDialectsCount >= 2);
        Assert.True(overview.AverageNaturalnessScore > 0.9);
        Assert.NotEmpty(overview.Profiles);
        Assert.NotEmpty(overview.RecentAdaptations);
    }

    [Fact]
    public void AdaptPhrase_AppliesRegionalIdiom()
    {
        var engine = new VoiceDialectEngine();
        var request = new AdaptPhraseRequest(
            "en-US-TEXAS",
            "We will schedule your deployment tomorrow",
            "Sales Closing"
        );

        var result = engine.AdaptPhrase(request);

        Assert.NotNull(result);
        Assert.StartsWith("ADP-", result.AdaptationId);
        Assert.Equal("en-US-TEXAS", result.DialectCode);
        Assert.Contains("y'all", result.CulturallyAdaptedPhrase);
        Assert.True(result.NaturalnessScore > 0.95);
    }

    [Fact]
    public void RegisterProfile_AppendsNewRegionalCadence()
    {
        var engine = new VoiceDialectEngine();
        var request = new RegisterDialectProfileRequest(
            "es-MX-NORTE",
            "Northern Mexico Commercial",
            "Direct energetic rhythm with business courtesy",
            0.0,
            1.05,
            new[] { "De acuerdo -> Enterado, con gusto" }
        );

        var profile = engine.RegisterProfile(request);

        Assert.NotNull(profile);
        Assert.Equal("es-MX-NORTE", profile.DialectCode);

        var overview = engine.GetOverview();
        Assert.Contains(overview.Profiles, p => p.DialectCode == "es-MX-NORTE");
    }
}
