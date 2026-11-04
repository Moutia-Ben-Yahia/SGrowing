using System;
using System.Linq;
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
    public void EnrichLead_ThrowsOnNullRequest()
    {
        Assert.Throws<ArgumentNullException>(() => _engine.EnrichLead(null!));
    }

    [Fact]
    public void EnrichLead_EmailQuery_ParsesContactAndDomainAccurately()
    {
        var request = new WaterfallEnrichRequest("alex.chen@databricks.com", "Alex Chen");
        var result = _engine.EnrichLead(request);

        Assert.NotNull(result);
        Assert.NotNull(result.Company);
        Assert.NotNull(result.Contact);
        Assert.Equal("databricks.com", result.Company.Domain);
        Assert.Equal("alex.chen@databricks.com", result.Contact.Email);
        Assert.Equal("Alex Chen", result.Contact.FullName);
    }

    [Fact]
    public void EnrichLead_CachesResultForIdenticalQueries()
    {
        var query = "cache-test-corp.com";
        var first = _engine.EnrichLead(new WaterfallEnrichRequest(query));
        var second = _engine.EnrichLead(new WaterfallEnrichRequest(query));

        Assert.Same(first, second);
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
        Assert.True(metrics.First().HistoricalMatchRate >= metrics.Last().HistoricalMatchRate); // Sorted descending
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

    [Fact]
    public void VerifyDeliverability_InvalidDomainOrFormat_ReturnsBounced()
    {
        var invalidDomainResult = _engine.VerifyDeliverability(new VerifyDeliverabilityRequest("user@domain.invalid"));
        Assert.False(invalidDomainResult.CanSendWithoutBounceRisk);
        Assert.Equal("Bounced", invalidDomainResult.Status);

        var badFormatResult = _engine.VerifyDeliverability(new VerifyDeliverabilityRequest("not-an-email"));
        Assert.False(badFormatResult.CanSendWithoutBounceRisk);
        Assert.Equal("Bounced", badFormatResult.Status);
    }

    [Fact]
    public void VerifyDeliverability_ThrowsOnNullRequest()
    {
        Assert.Throws<ArgumentNullException>(() => _engine.VerifyDeliverability(null!));
    }
}
