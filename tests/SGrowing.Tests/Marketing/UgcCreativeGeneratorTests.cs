using Microsoft.EntityFrameworkCore;
using SGrowing.Api.Data;
using SGrowing.Api.Modules.Marketing;
using SGrowing.Core.Entities;
using Xunit;

namespace SGrowing.Tests.Marketing;

public class UgcCreativeGeneratorTests
{
    private readonly SGrowingDbContext _db;
    private readonly UgcCreativeGeneratorService _service;

    public UgcCreativeGeneratorTests()
    {
        var options = new DbContextOptionsBuilder<SGrowingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _db = new SGrowingDbContext(options);
        _service = new UgcCreativeGeneratorService(_db);
    }

    [Fact]
    public async Task GenerateScriptsForProduct_GeneratesFivePsychologicalAnglesWithScenesAndVoiceover()
    {
        var tenantId = Guid.NewGuid();
        var sku = "CREAM-COLLAGEN-ANTIAGING";
        var productName = "Hydro-Collagen Rejuvenating Serum";

        var scripts = await _service.GenerateScriptsForProductAsync(tenantId, sku, productName, "Darija", "Women 25-45");

        Assert.NotNull(scripts);
        Assert.Equal(5, scripts.Count);

        // Check Angle 1: Problem Agitator
        Assert.Contains(scripts, s => s.HookAngle == UgcHookAngle.ProblemAgitator);
        // Check Angle 3: Cultural COD Reassurance
        Assert.Contains(scripts, s => s.HookAngle == UgcHookAngle.CulturalCodReassurance);
        // Check Angle 4: Unboxing ASMR
        Assert.Contains(scripts, s => s.HookAngle == UgcHookAngle.UnboxingDemonstration);

        var codScript = scripts.First(s => s.HookAngle == UgcHookAngle.CulturalCodReassurance);
        Assert.Contains("livraison", codScript.HookText.ToLower() + codScript.ScenesJson.ToLower());
        Assert.Contains("Yalidine", codScript.ScenesJson);

        var fetched = await _service.GetScriptsByProductAsync(tenantId, sku);
        Assert.Equal(5, fetched.Count);
    }

    [Fact]
    public async Task GenerateScriptsForProduct_In_French_Produces_French_Hooks_And_Ctas()
    {
        var tenantId = Guid.NewGuid();
        var sku = "ROBOT-ASPIRATEUR-PRO";
        var productName = "Robot Aspirateur Intelligent Pro";

        var scripts = await _service.GenerateScriptsForProductAsync(tenantId, sku, productName, "French", "Homeowners 25-60");

        Assert.NotNull(scripts);
        Assert.Equal(5, scripts.Count);

        var problemScript = scripts.First(s => s.HookAngle == UgcHookAngle.ProblemAgitator);
        Assert.Equal("Arrêtez d'acheter n'importe quoi !", problemScript.HookText);
        Assert.Contains("Cliquez sur le lien ci-dessous avant rupture de stock !", problemScript.CallToAction);

        var unboxingScript = scripts.First(s => s.HookAngle == UgcHookAngle.UnboxingDemonstration);
        Assert.Equal("Ce déballage va vous laisser bouche bée !", unboxingScript.HookText);

        var scarcityScript = scripts.First(s => s.HookAngle == UgcHookAngle.ScarcityFlashDeal);
        Assert.Equal("Dernier appel pour l'offre flash !", scarcityScript.HookText);
    }

    [Fact]
    public async Task GenerateScriptsForProduct_Should_Return_Existing_Scripts_When_Already_Generated()
    {
        var tenantId = Guid.NewGuid();
        var sku = "SMARTWATCH-ULTRA-MAX";
        var productName = "Ultra Max Smartwatch";

        // First generation
        var firstRun = await _service.GenerateScriptsForProductAsync(tenantId, sku, productName, "Darija");
        Assert.Equal(5, firstRun.Count);

        // Second generation call should return the exact existing records without inserting duplicates
        var secondRun = await _service.GenerateScriptsForProductAsync(tenantId, sku, productName, "Darija");
        Assert.Equal(5, secondRun.Count);

        var totalInDb = await _db.UgcVideoScripts.Where(s => s.TenantId == tenantId && s.ProductSku == sku).CountAsync();
        Assert.Equal(5, totalInDb);
    }

    [Fact]
    public async Task GetScriptsByProduct_Should_Filter_By_Sku_And_Tenant()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var skuA = "PRODUCT-SKU-ALPHA";
        var skuB = "PRODUCT-SKU-BETA";

        await _service.GenerateScriptsForProductAsync(tenantA, skuA, "Product Alpha");
        await _service.GenerateScriptsForProductAsync(tenantB, skuB, "Product Beta");

        var resultsA = await _service.GetScriptsByProductAsync(tenantA, skuA);
        Assert.Equal(5, resultsA.Count);
        Assert.All(resultsA, s => Assert.Equal(tenantA, s.TenantId));

        var resultsB = await _service.GetScriptsByProductAsync(tenantB, skuA);
        Assert.Empty(resultsB);
    }
}
