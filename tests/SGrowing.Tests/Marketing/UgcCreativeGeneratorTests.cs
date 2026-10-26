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
}
