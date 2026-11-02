using SGrowing.Api.Modules.Intelligence.WaterfallEnrichment;
using Xunit;

namespace SGrowing.Tests.Intelligence;

public class WaterfallEnrichmentTests
{
    private readonly WaterfallEnrichmentEngine _engine = new();

    [Fact]
    public void EnrichLead_DomainQuery_CascadesAndReturnsFullProfile()
    {
        // Arrange
        var request = new WaterfallEnrichRequest("stripe.com", "John Doe");

        // Act
        var result = _engine.EnrichLead(request);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.Equal(EnrichmentProvider.Clearbit, result.WinningProvider);
        Assert.NotNull(result.Company);
        Assert.Equal("stripe.com", result.Company.Domain);
        Assert.NotEmpty(result.Company.TechnologiesUsed);
        Assert.NotEmpty(result.WaterfallSteps);
    }

    [Fact]
    public void GetProviderMetrics_ReturnsAllFourMajorProviders()
    {
        // Act
        var metrics = _engine.GetProviderMetrics();

        // Assert
        Assert.NotNull(metrics);
        Assert.True(metrics.Count >= 4);
        Assert.Contains(metrics, m => m.Provider == EnrichmentProvider.Clearbit && m.IsActive);
        Assert.Contains(metrics, m => m.Provider == EnrichmentProvider.Apollo && m.IsActive);
    }

    [Fact]
    public void VerifyDeliverability_ValidEmail_ReturnsDeliverableWithMx()
    {
        // Arrange
        var request = new VerifyDeliverabilityRequest("sarah@enterprisebrand.corp");

        // Act
        var result = _engine.VerifyDeliverability(request);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.CanSendWithoutBounceRisk);
        Assert.Equal("DeliverableWithMx", result.Status);
    }
}
