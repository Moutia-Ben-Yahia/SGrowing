using Microsoft.EntityFrameworkCore;
using SGrowing.Api.Data;
using SGrowing.Api.Modules.Tenancy;
using SGrowing.Core.Entities;
using Xunit;

namespace SGrowing.Tests.Tenancy;

public class StorefrontDomainTests
{
    private SGrowingDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<SGrowingDbContext>()
            .UseInMemoryDatabase(databaseName: $"StorefrontDomainTestDb_{Guid.NewGuid()}")
            .Options;
        return new SGrowingDbContext(options);
    }

    [Fact]
    public async Task StorefrontDomain_Should_Register_Verify_And_Serve_Public_Theme()
    {
        var db = CreateDbContext();
        var service = new StorefrontDomainService(db);
        var tenantId = Guid.NewGuid();

        // 1. Register custom domain
        var domain = await service.RegisterCustomDomainAsync(tenantId, "boutique.carthage-parfums.tn");
        Assert.NotNull(domain);
        Assert.Equal("boutique.carthage-parfums.tn", domain.Hostname);
        Assert.Equal(DomainValidationStatus.PendingDns, domain.ValidationStatus);
        Assert.Equal("cname.sgrowing.com", domain.TargetCname);

        // 2. Verify DNS and issue SSL
        var verifiedDomain = await service.VerifyDomainDnsAsync(tenantId, domain.Id, simulateValidDns: true);
        Assert.Equal(DomainValidationStatus.Active, verifiedDomain.ValidationStatus);
        Assert.Equal(SslCertificateStatus.Issued, verifiedDomain.SslStatus);
        Assert.NotNull(verifiedDomain.VerifiedAtUtc);

        // 3. Configure Brand Theme Tokens
        var theme = new BrandThemeTokens
        {
            StorefrontSlug = "carthage",
            BrandName = "Carthage Parfums Prestige",
            PrimaryColorHex = "#D97706",
            SecondaryColorHex = "#B45309",
            FontFamily = "Playfair Display, serif",
            LogoUrl = "https://cdn.carthage.tn/logo.svg",
            FaviconUrl = "https://cdn.carthage.tn/favicon.ico",
            SupportEmail = "contact@carthage-parfums.tn",
            SupportPhone = "+21671889900",
            SmsSenderId = "CARTHAGE",
            WhatsAppSenderPhone = "+21620001122"
        };
        await service.UpsertBrandThemeTokensAsync(tenantId, theme);

        // 4. Retrieve Public Headless Config by Custom Domain
        var publicConfig = await service.GetPublicStorefrontConfigAsync("boutique.carthage-parfums.tn");
        Assert.NotNull(publicConfig);
        Assert.Equal("Carthage Parfums Prestige", publicConfig.BrandName);
        Assert.Equal("#D97706", publicConfig.PrimaryColorHex);
        Assert.Equal("Playfair Display, serif", publicConfig.FontFamily);
        Assert.Equal("CARTHAGE", theme.SmsSenderId);
    }

    [Fact]
    public async Task RegisterCustomDomainAsync_ExistingHostname_ReturnsExistingWithoutDuplicate()
    {
        var db = CreateDbContext();
        var service = new StorefrontDomainService(db);
        var tenantId = Guid.NewGuid();

        var first = await service.RegisterCustomDomainAsync(tenantId, "shop.brand.tn");
        var second = await service.RegisterCustomDomainAsync(tenantId, "shop.brand.tn");

        Assert.Equal(first.Id, second.Id);
        Assert.Equal(1, await db.StorefrontDomains.CountAsync());
    }

    [Fact]
    public async Task RegisterCustomDomainAsync_NormalizesWhitespaceAndCase()
    {
        var db = CreateDbContext();
        var service = new StorefrontDomainService(db);
        var tenantId = Guid.NewGuid();

        var domain = await service.RegisterCustomDomainAsync(tenantId, "   BOUTIQUE.Luxury-Store.COM   ");

        Assert.Equal("boutique.luxury-store.com", domain.Hostname);
    }

    [Fact]
    public async Task VerifyDomainDnsAsync_InvalidDomainId_ThrowsInvalidOperationException()
    {
        var db = CreateDbContext();
        var service = new StorefrontDomainService(db);
        var tenantId = Guid.NewGuid();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.VerifyDomainDnsAsync(tenantId, Guid.NewGuid())
        );
    }

    [Fact]
    public async Task VerifyDomainDnsAsync_FailedDns_SetsFailedAndProvisioning()
    {
        var db = CreateDbContext();
        var service = new StorefrontDomainService(db);
        var tenantId = Guid.NewGuid();

        var domain = await service.RegisterCustomDomainAsync(tenantId, "dns-fail.store.com");
        var verified = await service.VerifyDomainDnsAsync(tenantId, domain.Id, simulateValidDns: false);

        Assert.Equal(DomainValidationStatus.Failed, verified.ValidationStatus);
        Assert.Equal(SslCertificateStatus.Provisioning, verified.SslStatus);
        Assert.Null(verified.VerifiedAtUtc);
    }

    [Fact]
    public async Task UpsertBrandThemeTokensAsync_UpdatesExistingThemeTokens()
    {
        var db = CreateDbContext();
        var service = new StorefrontDomainService(db);
        var tenantId = Guid.NewGuid();

        var initial = new BrandThemeTokens
        {
            StorefrontSlug = "myshop",
            BrandName = "My Shop V1",
            PrimaryColorHex = "#111111",
            SecondaryColorHex = "#222222",
            FontFamily = "Arial"
        };
        await service.UpsertBrandThemeTokensAsync(tenantId, initial);

        var updated = new BrandThemeTokens
        {
            StorefrontSlug = "myshop",
            BrandName = "My Shop V2 Rebranded",
            PrimaryColorHex = "#FF5500",
            SecondaryColorHex = "#FFAA00",
            FontFamily = "Roboto"
        };
        var result = await service.UpsertBrandThemeTokensAsync(tenantId, updated);

        Assert.Equal("My Shop V2 Rebranded", result.BrandName);
        Assert.Equal("#FF5500", result.PrimaryColorHex);
        Assert.Equal(1, await db.BrandThemeTokens.CountAsync());
    }

    [Fact]
    public async Task GetPublicStorefrontConfigAsync_UnknownSlugOrDomain_ReturnsDefaultOfficialStoreConfig()
    {
        var db = CreateDbContext();
        var service = new StorefrontDomainService(db);

        var fallbackConfig = await service.GetPublicStorefrontConfigAsync("completely-unknown-store-1234");

        Assert.NotNull(fallbackConfig);
        Assert.Equal("default", fallbackConfig.StorefrontSlug);
        Assert.Equal("SGrowing Official Store", fallbackConfig.BrandName);
        Assert.Equal("#4F46E5", fallbackConfig.PrimaryColorHex);
        Assert.Equal("TND", fallbackConfig.Currency);
        Assert.True(fallbackConfig.CodEnabled);
    }

    [Fact]
    public async Task GetPublicStorefrontConfigAsync_ResolvesDirectlyBySlug()
    {
        var db = CreateDbContext();
        var service = new StorefrontDomainService(db);
        var tenantId = Guid.NewGuid();

        var theme = new BrandThemeTokens
        {
            StorefrontSlug = "direct-slug-store",
            BrandName = "Direct Slug Brand",
            PrimaryColorHex = "#00AA55",
            SecondaryColorHex = "#005522",
            FontFamily = "Geist"
        };
        await service.UpsertBrandThemeTokensAsync(tenantId, theme);

        var config = await service.GetPublicStorefrontConfigAsync("direct-slug-store");

        Assert.NotNull(config);
        Assert.Equal("direct-slug-store", config.StorefrontSlug);
        Assert.Equal("Direct Slug Brand", config.BrandName);
        Assert.Equal("#00AA55", config.PrimaryColorHex);
    }
}
