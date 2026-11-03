using System;
using System.Linq;
using SGrowing.Api.Modules.Finance.SovereignCbam;
using Xunit;

namespace SGrowing.Tests.Finance;

public sealed class SovereignCbamTests
{
    [Fact]
    public void GetOverview_ReturnsInitialDeclarationsAndFactors()
    {
        var engine = new SovereignCbamEngine();
        var overview = engine.GetOverview();

        Assert.NotNull(overview);
        Assert.True(overview.TotalDeclarationsCount >= 2);
        Assert.True(overview.TotalEmbeddedEmissionsTco2e > 0);
        Assert.True(overview.TotalCarbonTaxLiabilityEur > 0);
        Assert.NotEmpty(overview.RecentDeclarations);
        Assert.NotEmpty(overview.BenchmarkFactors);
    }

    [Fact]
    public void GenerateDeclaration_CalculatesEmbeddedEmissionsAndTax()
    {
        var engine = new SovereignCbamEngine();
        var request = new GenerateCbamDeclarationRequest(
            "DE812345678",
            "CN",
            "STEEL",
            200.00m
        );

        var declaration = engine.GenerateDeclaration(request);

        Assert.NotNull(declaration);
        Assert.StartsWith("CBAM-DEC-", declaration.DeclarationId);
        Assert.Equal("CN", declaration.CountryOfOrigin);
        Assert.Equal("STEEL", declaration.GoodsCategory);
        Assert.True(declaration.EmbeddedDirectEmissionsTco2e > 0);
        Assert.True(declaration.StatutoryCarbonTaxLiabilityEur > 0);
        Assert.Equal("CLEARED_BY_CUSTOMS", declaration.Status);
    }

    [Fact]
    public void OffsetEmissions_UpdatesStatusToSettled()
    {
        var engine = new SovereignCbamEngine();
        var request = new OffsetEmissionsRequest(
            "CBAM-DEC-2026-001",
            1075.00m,
            "EU ETS Compliance Carbon Registry"
        );

        var updated = engine.OffsetEmissions(request);

        Assert.NotNull(updated);
        Assert.Equal("CBAM-DEC-2026-001", updated.DeclarationId);
        Assert.Equal("OFFSET_SETTLED", updated.Status);

        var overview = engine.GetOverview();
        var dec = overview.RecentDeclarations.FirstOrDefault(d => d.DeclarationId == "CBAM-DEC-2026-001");
        Assert.NotNull(dec);
        Assert.Equal("OFFSET_SETTLED", dec.Status);
    }
}
