using FluentAssertions;
using SGrowing.Api.Modules.Tenancy;
using Xunit;

namespace SGrowing.Tests.Tenancy;

public class WhitelabelBrandingServiceTests
{
    private readonly WhitelabelBrandingService _service;

    public WhitelabelBrandingServiceTests()
    {
        _service = new WhitelabelBrandingService();
    }

    [Fact]
    public async Task GetAllTenants_ShouldReturnSeededRichAgencyProfiles()
    {
        // Act
        var tenants = await _service.GetAllTenantsAsync();

        // Assert
        tenants.Should().NotBeEmpty();
        tenants.Should().HaveCountGreaterThanOrEqualTo(4);

        var apex = tenants.FirstOrDefault(t => t.TenantId == WhitelabelBrandingService.ApexTenantId);
        apex.Should().NotBeNull();
        apex!.Profile.BrandName.Should().Be("Apex Growth Partners");
        apex.CustomDomains.Should().Contain(d => d.Hostname == "portal.apex-marketing.com" && d.SslStatus == WhitelabelSslStatus.Issued);

        var nordic = tenants.FirstOrDefault(t => t.TenantId == WhitelabelBrandingService.NordicTenantId);
        nordic.Should().NotBeNull();
        nordic!.Profile.BrandName.Should().Be("Nordic Scale Agency");
        nordic.CustomDomains.Should().Contain(d => d.Hostname == "app.nordicscale.se");

        var ecom = tenants.FirstOrDefault(t => t.TenantId == WhitelabelBrandingService.EcomTenantId);
        ecom.Should().NotBeNull();
        ecom!.Profile.BrandName.Should().Be("E-com Superchargers Ltd");
        ecom.CustomDomains.Should().Contain(d => d.Hostname == "growth.ecomsuperchargers.co.uk");
    }

    [Fact]
    public async Task ResolveHost_WithExactCustomDomain_ShouldReturnMatchedTenant()
    {
        // Act
        var result = await _service.ResolveHostAsync("portal.apex-marketing.com");

        // Assert
        result.Should().NotBeNull();
        result.ResolvedTenantId.Should().Be(WhitelabelBrandingService.ApexTenantId);
        result.ResolutionType.Should().Be(HostResolutionType.CustomDomain);
        result.MatchedDomain.Should().Be("portal.apex-marketing.com");
        result.BrandName.Should().Be("Apex Growth Partners");
        result.IsSslActive.Should().BeTrue();
    }

    [Fact]
    public async Task ResolveHost_WithPortAndProtocol_ShouldNormalizeAndMatchDomain()
    {
        // Act
        var resultWithPort = await _service.ResolveHostAsync("portal.apex-marketing.com:443");
        var resultWithHttps = await _service.ResolveHostAsync("https://app.nordicscale.se:8443/dashboard");

        // Assert
        resultWithPort.ResolvedTenantId.Should().Be(WhitelabelBrandingService.ApexTenantId);
        resultWithPort.ResolutionType.Should().Be(HostResolutionType.CustomDomain);

        resultWithHttps.ResolvedTenantId.Should().Be(WhitelabelBrandingService.NordicTenantId);
        resultWithHttps.MatchedDomain.Should().Be("app.nordicscale.se");
    }

    [Fact]
    public async Task ResolveHost_WithUnknownDomain_ShouldFallbackToMasterTenant()
    {
        // Act
        var result = await _service.ResolveHostAsync("unknown-client-domain.org");

        // Assert
        result.Should().NotBeNull();
        result.ResolvedTenantId.Should().Be(WhitelabelBrandingService.MasterTenantId);
        result.ResolutionType.Should().Be(HostResolutionType.FallbackMaster);
        result.BrandName.Should().Be("SGrowing Global Engine");
    }

    [Fact]
    public async Task ResolveHost_WithSubdomainPattern_ShouldMatchTenantBySlug()
    {
        // Act
        var result = await _service.ResolveHostAsync("apex.sgrowing.io");

        // Assert
        result.Should().NotBeNull();
        result.ResolvedTenantId.Should().Be(WhitelabelBrandingService.ApexTenantId);
        result.ResolutionType.Should().Be(HostResolutionType.Subdomain);
        result.BrandName.Should().Be("Apex Growth Partners");
    }

    [Fact]
    public async Task RegisterCustomDomain_ShouldGenerateCryptographicDnsChallengeToken()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var hostname = "clients.myagency.com";

        // Act
        var domain = await _service.RegisterCustomDomainAsync(tenantId, hostname, isPrimary: true);

        // Assert
        domain.Should().NotBeNull();
        domain.Hostname.Should().Be("clients.myagency.com");
        domain.TargetCname.Should().Be("cname.sgrowing.io");
        domain.DnsChallengeRecord.Should().Be("_sgrowing-challenge.clients.myagency.com");
        domain.DnsChallengeToken.Should().NotBeNullOrWhiteSpace();
        domain.DnsChallengeToken.Length.Should().Be(64); // SHA-256 hex string
        domain.SslStatus.Should().Be(WhitelabelSslStatus.PendingDns);
        domain.IsPrimary.Should().BeTrue();
    }

    [Fact]
    public async Task VerifyDomainDns_WhenSimulateValid_ShouldTransitionToIssuedWithTls13Metadata()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var domain = await _service.RegisterCustomDomainAsync(tenantId, "vip.growthpartners.com");

        // Act
        var verified = await _service.VerifyDomainDnsAsync(tenantId, "vip.growthpartners.com", simulateValidDns: true);

        // Assert
        verified.SslStatus.Should().Be(WhitelabelSslStatus.Issued);
        verified.FailureReason.Should().BeNull();
        verified.TlsVersion.Should().Be("TLSv1.3");
        verified.CipherSuite.Should().Be("TLS_AES_128_GCM_SHA256");
        verified.CertificateSerialNumber.Should().NotBeNullOrWhiteSpace();
        verified.IssuedAtUtc.Should().NotBeNull();
        verified.ExpiresAtUtc.Should().NotBeNull();
        verified.DaysUntilExpiry.Should().BeInRange(88, 90);
    }

    [Fact]
    public async Task VerifyDomainDns_WhenSimulateInvalid_ShouldTransitionToFailedWithReason()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        await _service.RegisterCustomDomainAsync(tenantId, "failing.domain.com");

        // Act
        var result = await _service.VerifyDomainDnsAsync(tenantId, "failing.domain.com", simulateValidDns: false);

        // Assert
        result.SslStatus.Should().Be(WhitelabelSslStatus.Failed);
        result.FailureReason.Should().Contain("DNS verification failed");
        result.IssuedAtUtc.Should().BeNull();
    }

    [Fact]
    public async Task RenewSslCertificate_WhenIssued_ShouldExtendExpirationBy90Days()
    {
        // Arrange
        var tenantId = WhitelabelBrandingService.ApexTenantId;
        var hostname = "portal.apex-marketing.com";

        // Act
        var renewed = await _service.RenewSslCertificateAsync(tenantId, hostname);

        // Assert
        renewed.SslStatus.Should().Be(WhitelabelSslStatus.Issued);
        renewed.TlsVersion.Should().Be("TLSv1.3");
        renewed.CipherSuite.Should().Be("TLS_AES_256_GCM_SHA384");
        renewed.ExpiresAtUtc.Should().BeAfter(DateTime.UtcNow.AddDays(85));
        renewed.DaysUntilExpiry.Should().BeInRange(88, 90);
    }

    [Fact]
    public async Task RevokeSslCertificate_ShouldTransitionToRevokedState()
    {
        // Arrange
        var tenantId = WhitelabelBrandingService.ApexTenantId;
        var hostname = "portal.apex-marketing.com";

        // Act
        var revoked = await _service.RevokeSslCertificateAsync(tenantId, hostname, "Client requested migration");

        // Assert
        revoked.SslStatus.Should().Be(WhitelabelSslStatus.Revoked);
        revoked.FailureReason.Should().Be("Client requested migration");
    }

    [Fact]
    public async Task GenerateDynamicCss_ShouldContainValidRootVariablesAndContrast()
    {
        // Arrange
        var tenantId = WhitelabelBrandingService.ApexTenantId;

        // Act
        var css = await _service.GenerateDynamicCssAsync(tenantId);

        // Assert
        css.Should().NotBeNullOrWhiteSpace();
        css.Should().Contain(":root {");
        css.Should().Contain("--sg-primary: #4338CA;");
        css.Should().Contain("--sg-primary-contrast: #FFFFFF;");
        css.Should().Contain("--sg-primary-rgb: 67, 56, 202;");
        css.Should().Contain("--sg-secondary: #06B6D4;");
        css.Should().Contain("--sg-border-radius: 12px;");
        css.Should().Contain("--sg-font: 'Inter, system-ui, sans-serif'");
        css.Should().Contain("--sg-heading-font: 'Plus Jakarta Sans, sans-serif'");
    }

    [Fact]
    public void CalculateLuminanceAndContrast_ShouldHandleLightAndDarkColorsCorrectly()
    {
        // Arrange & Act
        var whiteContrast = WhitelabelBrandingService.GetContrastColor("#FFFFFF");
        var lightYellowContrast = WhitelabelBrandingService.GetContrastColor("#FEF08A");
        var darkNavyContrast = WhitelabelBrandingService.GetContrastColor("#0B0F19");
        var indigoContrast = WhitelabelBrandingService.GetContrastColor("#4338CA");

        // Assert
        whiteContrast.Should().Be("#0F172A"); // Dark text on light background
        lightYellowContrast.Should().Be("#0F172A");
        darkNavyContrast.Should().Be("#FFFFFF"); // White text on dark background
        indigoContrast.Should().Be("#FFFFFF");
    }

    [Fact]
    public async Task UpdateBranding_ShouldPersistTokensAndReflectInDynamicCss()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var newTheme = new WhitelabelThemeTokens(
            PrimaryColor: "#10B981",
            SecondaryColor: "#F59E0B",
            AccentColor: "#EC4899",
            BackgroundColor: "#FFFFFF",
            SurfaceColor: "#F1F5F9",
            TextColor: "#0F172A",
            BorderRadius: "9999px",
            FontFamily: "Montserrat, sans-serif",
            HeadingFontFamily: "Cal Sans, sans-serif"
        );

        var request = new UpdateBrandingRequest(
            Profile: new WhitelabelAgencyProfile(BrandName: "Emerald Scale Partners"),
            Theme: newTheme,
            Assets: null,
            Email: null
        );

        // Act
        var updated = await _service.UpdateBrandingAsync(tenantId, request);
        var css = await _service.GenerateDynamicCssAsync(tenantId);

        // Assert
        updated.Profile.BrandName.Should().Be("Emerald Scale Partners");
        updated.Theme.PrimaryColor.Should().Be("#10B981");
        updated.Theme.BorderRadius.Should().Be("9999px");

        css.Should().Contain("--sg-primary: #10B981;");
        css.Should().Contain("--sg-border-radius: 9999px;");
        css.Should().Contain("--sg-font: 'Montserrat, sans-serif'");
    }

    [Fact]
    public async Task GenerateEmailWhitelabelDkim_ShouldCreateValidDkimKeyAndSpf()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var mailDomain = "mail.clientgrowth.co";

        // Act
        var emailConfig = await _service.GenerateEmailWhitelabelDkimAsync(tenantId, mailDomain);

        // Assert
        emailConfig.Should().NotBeNull();
        emailConfig.CustomMailDomain.Should().Be("mail.clientgrowth.co");
        emailConfig.DkimSelector.Should().Be("sg2026");
        emailConfig.DkimPublicKey.Should().StartWith("v=DKIM1; k=rsa; p=MIIBIjANBgkqhkiG9w0BAQEFAAOCAQ8AMIIBCgKCAQEA");
        emailConfig.SpfRecord.Should().Be("v=spf1 include:_spf.sgrowing.io ~all");
        emailConfig.DmarcRecord.Should().Contain("rua=mailto:dmarc@mail.clientgrowth.co");
        emailConfig.MxRecord.Should().Be("10 mail.sgrowing.io");
    }

    [Fact]
    public async Task ResetToDefaults_ShouldRestoreDefaultBrandValues()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        await _service.UpdateBrandingAsync(tenantId, new UpdateBrandingRequest(
            Profile: new WhitelabelAgencyProfile(BrandName: "Custom Brand Name"),
            Theme: null,
            Assets: null,
            Email: null
        ));

        // Act
        var reset = await _service.ResetToDefaultsAsync(tenantId);

        // Assert
        reset.Profile.BrandName.Should().Be("SGrowing Growth Agency");
        reset.Theme.PrimaryColor.Should().Be("#4F46E5");
        reset.CustomDomains.Should().BeEmpty();
    }
}
