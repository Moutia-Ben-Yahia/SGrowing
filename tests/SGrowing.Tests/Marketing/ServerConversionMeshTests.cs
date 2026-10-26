using Microsoft.EntityFrameworkCore;
using SGrowing.Api.Data;
using SGrowing.Api.Modules.Marketing;
using SGrowing.Core.Entities;
using Xunit;

namespace SGrowing.Tests.Marketing;

public class ServerConversionMeshTests
{
    private SGrowingDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<SGrowingDbContext>()
            .UseInMemoryDatabase(databaseName: $"ConversionTestDb_{Guid.NewGuid()}")
            .Options;
        return new SGrowingDbContext(options);
    }

    [Fact]
    public async Task TrackConversion_Should_Hash_Pii_And_Calculate_High_Emq_Score()
    {
        var db = CreateDbContext();
        var mesh = new ServerConversionMeshService(db);
        var tenantId = Guid.NewGuid();

        var ev = await mesh.TrackConversionAsync(
            tenantId,
            AdPlatform.MetaCapi,
            ConversionEventType.Purchase,
            valueAmount: 180.0m,
            currency: "TND",
            email: "client.vip@gmail.com",
            phone: "+21698123456",
            firstName: "Sarra",
            lastName: "Gharbi",
            city: "Tunis",
            country: "TN",
            fbc: "fb.1.1689000000.IwAR23456789",
            fbp: "fb.1.1689000000.9876543210",
            userAgent: "Mozilla/5.0 (iPhone; CPU iPhone OS 17_0 like Mac OS X)",
            ipAddress: "197.1.2.3");

        Assert.NotNull(ev);
        Assert.Equal(ConversionDispatchStatus.Dispatched, ev.Status);

        // Verify SHA-256 hashing (64 hex characters)
        Assert.Equal(64, ev.HashedEmail!.Length);
        Assert.Equal(64, ev.HashedPhone!.Length);
        Assert.DoesNotContain("client.vip@gmail.com", ev.HashedEmail);
        Assert.DoesNotContain("98123456", ev.HashedPhone);

        // Verify Event Match Quality (EMQ) calculation
        var emq = mesh.CalculateEventMatchQualityScore(ev);
        Assert.True(emq >= 9.0, $"Expected EMQ >= 9.0, got {emq}");
    }

    [Fact]
    public async Task GetRecentConversions_Should_Return_Logged_Events()
    {
        var db = CreateDbContext();
        var mesh = new ServerConversionMeshService(db);
        var tenantId = Guid.NewGuid();

        await mesh.TrackConversionAsync(tenantId, AdPlatform.MetaCapi, ConversionEventType.Lead, 0, "TND", phone: "+21622334455");
        await mesh.TrackConversionAsync(tenantId, AdPlatform.TikTokEvents, ConversionEventType.AddToCart, 75m, "TND", phone: "+21655112233");

        var recent = await mesh.GetRecentConversionsAsync(tenantId);
        Assert.Equal(2, recent.Count);
    }
}
