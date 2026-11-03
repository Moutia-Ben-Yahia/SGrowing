using SGrowing.Api.Modules.Commerce.Tariffs;
using Xunit;

namespace SGrowing.Tests.Commerce;

public class TariffCalculatorTests
{
    private readonly TariffCalculatorEngine _engine = new();

    [Fact]
    public void ClassifyProduct_ClassifiesHardwareServerCorrectly()
    {
        var hs = _engine.ClassifyProduct("Enterprise 1U GPU Compute Server Hardware Appliance");

        Assert.Equal("8471.50", hs.HsCode);
        Assert.True(hs.ConfidencePercentage >= 90.0);
    }

    [Fact]
    public void CalculateLandedCost_CalculatesGermanMwStAndDdpTotal()
    {
        var request = new CalculateLandedCostRequest(
            "sku_server_rack",
            "Enterprise 1U Compute Server",
            "DE",
            2000m,
            100m
        );

        var breakdown = _engine.CalculateLandedCost(request);

        Assert.Equal("8471.50", breakdown.HsCode);
        Assert.Equal("DE", breakdown.DestinationCountry);
        Assert.Equal(19.0m, breakdown.DestinationVatRatePercentage);
        Assert.Equal("DDP", breakdown.Incoterm);
        Assert.True(breakdown.TotalLandedCost > 2400m);
    }

    [Fact]
    public void GetCountryVatRate_ReturnsAccurateStatutoryRates()
    {
        Assert.Equal(19.0m, _engine.GetCountryVatRate("DE"));
        Assert.Equal(20.0m, _engine.GetCountryVatRate("GB"));
        Assert.Equal(10.0m, _engine.GetCountryVatRate("JP"));
    }
}
