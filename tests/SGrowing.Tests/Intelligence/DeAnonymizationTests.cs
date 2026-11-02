using System.Threading.Tasks;
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
    public void DispatchAccountAlert_GeneratesValidAlertPreview()
    {
        // Arrange
        var request = new DispatchAlertRequest(
            Domain: "stripe.com",
            RecipientEmail: "rep@enterprise.corp",
            Channel: "Slack"
        );

        // Act
        var result = _engine.DispatchAccountAlert(request);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Dispatched);
        Assert.StartsWith("alt_", result.AlertId);
        Assert.Contains("stripe.com", result.MessagePreview);
    }
}
