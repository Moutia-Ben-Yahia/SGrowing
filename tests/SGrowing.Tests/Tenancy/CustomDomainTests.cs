using SGrowing.Api.Modules.Tenancy.CustomDomains;
using Xunit;

namespace SGrowing.Tests.Tenancy;

public sealed class CustomDomainTests
{
    private readonly CustomDomainEngine _engine = new("cname.growth-os.cloud");

    [Fact]
    public async Task RegisterCustomDomain_ReturnsPendingVerificationWithTokens()
    {
        var request = new CustomDomainRegistrationRequest(
            TenantId: "tenant_stellar_ai",
            DomainName: "app.stellarai.io",
            PrimaryColor: "#6366f1",
            AccentColor: "#ec4899",
            CustomCss: ":root { --stellar-radius: 12px; }"
        );

        var domain = await _engine.RegisterCustomDomainAsync(request);

        Assert.NotNull(domain);
        Assert.Equal("app.stellarai.io", domain.DomainName);
        Assert.Equal("tenant_stellar_ai", domain.TenantId);
        Assert.False(domain.IsDnsVerified);
        Assert.Equal("PendingVerification", domain.SslStatus);
        Assert.StartsWith("growth-os-verify=", domain.VerificationToken);
        Assert.Equal("cname.growth-os.cloud", domain.ExpectedCname);
        Assert.Equal("#6366f1", domain.Theming.PrimaryColor);
    }

    [Fact]
    public async Task VerifyDnsRecords_UpdatesSslStatusToActive()
    {
        var request = new CustomDomainRegistrationRequest(
            TenantId: "tenant_beta_corp",
            DomainName: "shop.betacorp.com"
        );
        var domain = await _engine.RegisterCustomDomainAsync(request);

        var result = await _engine.VerifyDnsRecordsAsync(domain.Id);

        Assert.NotNull(result);
        Assert.True(result.IsFullyVerified);
        Assert.Equal(2, result.Records.Count);
        Assert.Contains(result.Records, r => r.RecordType == "CNAME" && r.IsConfigured);
        Assert.Contains(result.Records, r => r.RecordType == "TXT" && r.IsConfigured);

        var resolved = await _engine.ResolveTenantBrandAsync(domain.DomainName);
        Assert.True(resolved.IsSecure);
    }

    [Fact]
    public async Task IssueOrRenewSsl_GeneratesValidSha256Fingerprint()
    {
        var request = new CustomDomainRegistrationRequest(
            TenantId: "tenant_omega",
            DomainName: "hub.omegasecurity.net"
        );
        var domain = await _engine.RegisterCustomDomainAsync(request);

        var renewal = await _engine.IssueOrRenewSslAsync(domain.Id);

        Assert.NotNull(renewal);
        Assert.Equal("Active", renewal.SslStatus);
        Assert.StartsWith("SHA256:", renewal.CertificateFingerprint);
        Assert.True(renewal.ExpiresAtUtc > DateTime.UtcNow.AddDays(80));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task RegisterCustomDomain_EmptyDomain_ThrowsArgumentException(string? domain)
    {
        var request = new CustomDomainRegistrationRequest(
            TenantId: "tenant_err",
            DomainName: domain!
        );

        await Assert.ThrowsAsync<ArgumentException>(() =>
            _engine.RegisterCustomDomainAsync(request));
    }

    [Fact]
    public async Task VerifyDnsRecords_NonExistentDomain_ThrowsKeyNotFoundException()
    {
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _engine.VerifyDnsRecordsAsync("dom_nonexistent_xyz"));
    }

    [Fact]
    public async Task IssueOrRenewSsl_NonExistentDomain_ThrowsKeyNotFoundException()
    {
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _engine.IssueOrRenewSslAsync("dom_nonexistent_xyz"));
    }

    [Fact]
    public async Task ResolveTenantBrand_UnknownDomain_ReturnsDefaultFallbackBrand()
    {
        var brand = await _engine.ResolveTenantBrandAsync("unknown.domain.org");

        Assert.NotNull(brand);
        Assert.Equal("default", brand.TenantId);
        Assert.Equal("unknown.domain.org", brand.DomainName);
        Assert.Equal("Growth OS Portal", brand.Theming.PageTitle);
        Assert.True(brand.IsSecure);
    }

    [Fact]
    public async Task ListDomainsAsync_FiltersByTenantIdAndReturnsConfiguredDomains()
    {
        var tenantId = "tenant_list_test";
        await _engine.RegisterCustomDomainAsync(new CustomDomainRegistrationRequest(tenantId, "domain1.test.com"));
        await _engine.RegisterCustomDomainAsync(new CustomDomainRegistrationRequest(tenantId, "domain2.test.com"));

        var domains = await _engine.ListDomainsAsync(tenantId);

        Assert.Equal(2, domains.Count);
        Assert.All(domains, d => Assert.Equal(tenantId, d.TenantId));
    }
}
