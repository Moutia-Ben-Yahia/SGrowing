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

    [Theory]
    [InlineData(null, "")]
    [InlineData("", "")]
    [InlineData("   ", "")]
    public void HashSha256_Should_Return_Empty_On_Null_Or_Whitespace(string? input, string expected)
    {
        var result = ServerConversionMeshService.HashSha256(input);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void HashSha256_Should_Normalize_Case_And_Whitespace_Deterministically()
    {
        var hashed1 = ServerConversionMeshService.HashSha256("  User.Test@Domain.COM ");
        var hashed2 = ServerConversionMeshService.HashSha256("user.test@domain.com");

        Assert.Equal(64, hashed1.Length);
        Assert.Equal(hashed1, hashed2);
    }

    [Fact]
    public void CalculateEventMatchQualityScore_Should_Return_Zero_For_Empty_Event()
    {
        var db = CreateDbContext();
        var mesh = new ServerConversionMeshService(db);
        var ev = new ServerConversionEvent();

        var score = mesh.CalculateEventMatchQualityScore(ev);

        Assert.Equal(0.0, score);
    }

    [Fact]
    public void CalculateEventMatchQualityScore_Should_Calculate_Partial_Scores_Accurately()
    {
        var db = CreateDbContext();
        var mesh = new ServerConversionMeshService(db);

        var phoneOnly = new ServerConversionEvent { HashedPhone = "abc" };
        Assert.Equal(3.5, mesh.CalculateEventMatchQualityScore(phoneOnly));

        var emailOnly = new ServerConversionEvent { HashedEmail = "def" };
        Assert.Equal(2.5, mesh.CalculateEventMatchQualityScore(emailOnly));

        var cookiesOnly = new ServerConversionEvent { Fbp = "fbp.1", Fbc = "fbc.1" };
        Assert.Equal(2.5, mesh.CalculateEventMatchQualityScore(cookiesOnly)); // 1.5 + 1.0

        var networkOnly = new ServerConversionEvent { ClientUserAgent = "curl", ClientIpAddress = "127.0.0.1" };
        Assert.Equal(1.0, mesh.CalculateEventMatchQualityScore(networkOnly)); // 0.5 + 0.5
    }

    [Fact]
    public async Task TrackConversionAsync_Should_Respect_Custom_EventId_And_Uppercase_Currency()
    {
        var db = CreateDbContext();
        var mesh = new ServerConversionMeshService(db);
        var tenantId = Guid.NewGuid();
        var customEventId = "ORDER_ORD_998877_WEB";

        var ev = await mesh.TrackConversionAsync(
            tenantId,
            AdPlatform.TikTokEvents,
            ConversionEventType.InitiateCheckout,
            valueAmount: 250.50m,
            currency: "eur",
            eventId: customEventId,
            customDataJson: "{\"cart_size\":3}"
        );

        Assert.Equal(customEventId, ev.EventId);
        Assert.Equal("EUR", ev.Currency);
        Assert.Equal("{\"cart_size\":3}", ev.CustomDataJson);
        Assert.Contains("fbtrace_id", ev.ApiResponseJson);
    }

    [Fact]
    public async Task GetRecentConversionsAsync_Should_Enforce_Limit_And_Tenant_Isolation()
    {
        var db = CreateDbContext();
        var mesh = new ServerConversionMeshService(db);
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();

        for (int i = 0; i < 5; i++)
        {
            await mesh.TrackConversionAsync(tenantA, AdPlatform.GoogleAdsOffline, ConversionEventType.AddToCart, i * 10, "USD");
        }

        await mesh.TrackConversionAsync(tenantB, AdPlatform.TikTokEvents, ConversionEventType.Lead, 0, "USD");

        var recentA = await mesh.GetRecentConversionsAsync(tenantA, limit: 3);
        Assert.Equal(3, recentA.Count);
        Assert.All(recentA, e => Assert.Equal(tenantA, e.TenantId));

        var recentB = await mesh.GetRecentConversionsAsync(tenantB, limit: 10);
        Assert.Single(recentB);
        Assert.Equal(tenantB, recentB[0].TenantId);
    }
}
