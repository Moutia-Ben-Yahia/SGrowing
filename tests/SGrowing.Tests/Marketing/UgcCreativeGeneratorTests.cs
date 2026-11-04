using System;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
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

        scripts.Should().NotBeNull();
        scripts.Should().HaveCount(5);

        // Check Angle 1: Problem Agitator
        scripts.Should().Contain(s => s.HookAngle == UgcHookAngle.ProblemAgitator);
        // Check Angle 3: Cultural COD Reassurance
        scripts.Should().Contain(s => s.HookAngle == UgcHookAngle.CulturalCodReassurance);
        // Check Angle 4: Unboxing ASMR
        scripts.Should().Contain(s => s.HookAngle == UgcHookAngle.UnboxingDemonstration);

        var codScript = scripts.First(s => s.HookAngle == UgcHookAngle.CulturalCodReassurance);
        (codScript.HookText.ToLower() + codScript.ScenesJson.ToLower()).Should().Contain("livraison");
        codScript.ScenesJson.Should().Contain("Yalidine");

        var fetched = await _service.GetScriptsByProductAsync(tenantId, sku);
        fetched.Should().HaveCount(5);
    }

    [Fact]
    public async Task GenerateScriptsForProduct_In_French_Produces_French_Hooks_And_Ctas()
    {
        var tenantId = Guid.NewGuid();
        var sku = "ROBOT-ASPIRATEUR-PRO";
        var productName = "Robot Aspirateur Intelligent Pro";

        var scripts = await _service.GenerateScriptsForProductAsync(tenantId, sku, productName, "French", "Homeowners 25-60");

        scripts.Should().NotBeNull();
        scripts.Should().HaveCount(5);

        var problemScript = scripts.First(s => s.HookAngle == UgcHookAngle.ProblemAgitator);
        problemScript.HookText.Should().Be("Arrêtez d'acheter n'importe quoi !");
        problemScript.CallToAction.Should().Contain("Cliquez sur le lien ci-dessous avant rupture de stock !");

        var unboxingScript = scripts.First(s => s.HookAngle == UgcHookAngle.UnboxingDemonstration);
        unboxingScript.HookText.Should().Be("Ce déballage va vous laisser bouche bée !");

        var scarcityScript = scripts.First(s => s.HookAngle == UgcHookAngle.ScarcityFlashDeal);
        scarcityScript.HookText.Should().Be("Dernier appel pour l'offre flash !");
    }

    [Fact]
    public async Task GenerateScriptsForProduct_GeneratesCompetitorCalloutAngleWithScenes()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var sku = "BLENDER-TITANIUM-PRO";
        var productName = "Titanium Vortex Blender";

        // Act
        var scripts = await _service.GenerateScriptsForProductAsync(tenantId, sku, productName, "French");
        var competitorScript = scripts.FirstOrDefault(s => s.HookAngle == UgcHookAngle.CompetitorCallout);

        // Assert
        competitorScript.Should().NotBeNull();
        competitorScript!.HookText.Should().Contain("Ne vous faites plus avoir");
        competitorScript.ScenesJson.Should().Contain("Marque X");
        competitorScript.CallToAction.Should().Contain("Profitez de l'offre spéciale");
    }

    [Fact]
    public async Task GenerateScriptsForProduct_PreservesTargetAudienceAndLanguageMetadata()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var sku = "WIRELESS-EARBUDS-ANC";
        var productName = "ProSound ANC Earbuds";
        var audience = "Gamers & Tech Enthusiasts 18-28";

        // Act
        var scripts = await _service.GenerateScriptsForProductAsync(tenantId, sku, productName, "Darija", audience);

        // Assert
        scripts.Should().AllSatisfy(s =>
        {
            s.Language.Should().Be("Darija");
            s.TargetAudience.Should().Be(audience);
            s.ProductSku.Should().Be(sku);
            s.ProductName.Should().Be(productName);
        });
    }

    [Fact]
    public async Task GenerateScriptsForProduct_Should_Return_Existing_Scripts_When_Already_Generated()
    {
        var tenantId = Guid.NewGuid();
        var sku = "SMARTWATCH-ULTRA-MAX";
        var productName = "Ultra Max Smartwatch";

        // First generation
        var firstRun = await _service.GenerateScriptsForProductAsync(tenantId, sku, productName, "Darija");
        firstRun.Should().HaveCount(5);

        // Second generation call should return the exact existing records without inserting duplicates
        var secondRun = await _service.GenerateScriptsForProductAsync(tenantId, sku, productName, "Darija");
        secondRun.Should().HaveCount(5);

        var totalInDb = await _db.UgcVideoScripts.Where(s => s.TenantId == tenantId && s.ProductSku == sku).CountAsync();
        totalInDb.Should().Be(5);
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
        resultsA.Should().HaveCount(5);
        resultsA.Should().AllSatisfy(s => s.TenantId.Should().Be(tenantA));

        var resultsB = await _service.GetScriptsByProductAsync(tenantB, skuA);
        resultsB.Should().BeEmpty();
    }

    [Fact]
    public async Task GetScriptsByProduct_WhenNoScriptsExist_ReturnsEmptyList()
    {
        // Arrange
        var tenantId = Guid.NewGuid();

        // Act
        var results = await _service.GetScriptsByProductAsync(tenantId, "NONEXISTENT-SKU");

        // Assert
        results.Should().BeEmpty();
    }

    [Fact]
    public async Task GenerateScriptsForProduct_GeneratesUniqueScriptIds()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var sku = "LED-RING-LIGHT";

        // Act
        var scripts = await _service.GenerateScriptsForProductAsync(tenantId, sku, "Ring Light Pro");

        // Assert
        var ids = scripts.Select(s => s.Id).Distinct().ToList();
        ids.Should().HaveCount(5);
    }
}
