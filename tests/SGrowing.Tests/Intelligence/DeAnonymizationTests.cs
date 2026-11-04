using System;
using System.Linq;
using SGrowing.Api.Modules.Intelligence.DeAnonymization;
using Xunit;

namespace SGrowing.Tests.Intelligence;

public class DeAnonymizationTests
{
    private readonly DeAnonymizationEngine _engine = new();

    [Fact]
    public void DeAnonymizeVisitor_KnownCorporateIp_ResolvesFirmographicsAndIntent()
    {
        // Arrange
        var request = new DeAnonymizeRequest(
            IpAddress: "198.51.100.24",
            UserAgent: "Mozilla/5.0 Chrome/130",
            PageUrl: "/enterprise/pricing",
            Referrer: "https://google.com",
            TimeOnPageSeconds: 95
        );

        // Act
        var result = _engine.DeAnonymizeVisitor(request);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(NetworkType.Corporate, result.NetworkType);
        Assert.Equal("stripe.com", result.Domain);
        Assert.Equal("Stripe Inc.", result.CompanyName);
        Assert.True(result.IntentScore >= 85);
        Assert.Equal(IntentStage.ReadyToBuy, result.IntentStage);
        Assert.True(result.ShouldAlertAccountExec);
    }

    [Fact]
    public void DeAnonymizeVisitor_ThrowsOnNullRequest()
    {
        Assert.Throws<ArgumentNullException>(() => _engine.DeAnonymizeVisitor(null!));
    }

    [Fact]
    public void DeAnonymizeVisitor_UnknownIp_ReturnsIspNetworkType()
    {
        // Arrange
        var request = new DeAnonymizeRequest(
            IpAddress: "8.8.8.8",
            PageUrl: "/blog/post-1",
            TimeOnPageSeconds: 10
        );

        // Act
        var result = _engine.DeAnonymizeVisitor(request);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(NetworkType.ISP, result.NetworkType);
        Assert.Null(result.Domain);
        Assert.False(result.ShouldAlertAccountExec);
    }

    [Fact]
    public void DeAnonymizeVisitor_NonPricingPage_CalculatesModerateIntent()
    {
        var request = new DeAnonymizeRequest(
            IpAddress: "198.51.100.42", // shopify.com
            PageUrl: "/features/general-overview",
            TimeOnPageSeconds: 30
        );

        var result = _engine.DeAnonymizeVisitor(request);

        Assert.Equal("shopify.com", result.Domain);
        // Base 40 + non-pricing 15 = 55
        Assert.Equal(55, result.IntentScore);
        Assert.Equal(IntentStage.Consideration, result.IntentStage);
        Assert.False(result.ShouldAlertAccountExec);
    }

    [Fact]
    public void GetActiveIntentFeed_ReturnsAccountsSortedByActiveScore()
    {
        var feed = _engine.GetActiveIntentFeed();

        Assert.NotNull(feed);
        Assert.NotEmpty(feed);
        Assert.True(feed.First().ActiveScore >= feed.Last().ActiveScore);
    }

    [Fact]
    public void DispatchAccountAlert_GeneratesValidAlertPreview()
    {
        // Arrange
        var request = new DispatchAlertRequest(
            Domain: "stripe.com",
            RecipientEmail: "sarah.cro@sgrowing.io",
            Channel: "Slack"
        );

        // Act
        var result = _engine.DispatchAccountAlert(request);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Dispatched);
        Assert.StartsWith("alt_", result.AlertId);
        Assert.Contains("stripe.com", result.MessagePreview);
        Assert.Contains("Slack", result.MessagePreview);
    }

    [Fact]
    public void DispatchAccountAlert_ThrowsOnNullRequest()
    {
        Assert.Throws<ArgumentNullException>(() => _engine.DispatchAccountAlert(null!));
    }
}
