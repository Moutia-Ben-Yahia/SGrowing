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
}
