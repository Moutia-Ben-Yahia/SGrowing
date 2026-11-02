using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;

namespace SGrowing.Api.Modules.Tenancy.CustomDomains;

public class CustomDomainEngine
{
    private readonly ConcurrentDictionary<string, TenantCustomDomain> _domains = new(StringComparer.OrdinalIgnoreCase);
    private readonly string _platformCnameTarget;

    public CustomDomainEngine(string platformCnameTarget = "cname.growth-os.cloud")
    {
        _platformCnameTarget = platformCnameTarget;
        SeedDemoDomains();
    }

    private void SeedDemoDomains()
    {
        var domain = new TenantCustomDomain(
            Id: "dom_acme_enterprise",
            TenantId: "tenant_acme_corp",
            DomainName: "portal.acmecorp.com",
            VerificationToken: "growth-os-verify=acme_7f9c2d1e8",
            ExpectedCname: _platformCnameTarget,
            IsDnsVerified: true,
            SslStatus: "Active",
            SslExpiresAtUtc: DateTime.UtcNow.AddDays(75),
            Theming: new BrandThemingConfig(
                PrimaryColor: "#1e40af",
                AccentColor: "#f59e0b",
                LogoUrl: "https://assets.acmecorp.com/brand/logo.svg",
                FaviconUrl: "https://assets.acmecorp.com/brand/favicon.ico",
                CustomCss: ":root { --brand-border-radius: 8px; }",
                PageTitle: "Acme Corp Growth Portal"
            ),
            CreatedAtUtc: DateTime.UtcNow.AddDays(-15),
            LastVerifiedAtUtc: DateTime.UtcNow.AddHours(-2)
        );
        _domains[domain.Id] = domain;
        _domains[domain.DomainName] = domain;
    }

    public Task<TenantCustomDomain> RegisterCustomDomainAsync(CustomDomainRegistrationRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.DomainName))
        {
            throw new ArgumentException("Domain name is required", nameof(request));
        }

        var normalizedDomain = request.DomainName.Trim().ToLowerInvariant();
        var domainId = $"dom_{Guid.NewGuid():N}";
        var token = $"growth-os-verify={Guid.NewGuid():N}";

        var theming = new BrandThemingConfig(
            PrimaryColor: request.PrimaryColor ?? "#3b82f6",
            AccentColor: request.AccentColor ?? "#10b981",
            LogoUrl: request.LogoUrl,
            FaviconUrl: request.FaviconUrl,
            CustomCss: request.CustomCss,
            PageTitle: $"{normalizedDomain} Portal"
        );

        var domain = new TenantCustomDomain(
            Id: domainId,
            TenantId: request.TenantId,
            DomainName: normalizedDomain,
            VerificationToken: token,
            ExpectedCname: _platformCnameTarget,
            IsDnsVerified: false,
            SslStatus: "PendingVerification",
            SslExpiresAtUtc: DateTime.UtcNow.AddDays(90),
            Theming: theming,
            CreatedAtUtc: DateTime.UtcNow
        );

        _domains[domain.Id] = domain;
        _domains[domain.DomainName] = domain;

        return Task.FromResult(domain);
    }

    public Task<DnsVerificationResult> VerifyDnsRecordsAsync(string domainId, CancellationToken ct = default)
    {
        if (!_domains.TryGetValue(domainId, out var domain))
        {
            throw new KeyNotFoundException($"Custom domain '{domainId}' not found.");
        }

        // Simulate DNS check
        var records = new List<DnsVerificationRecord>
        {
            new("CNAME", domain.DomainName, domain.ExpectedCname, true),
            new("TXT", $"_acme-challenge.{domain.DomainName}", domain.VerificationToken, true)
        };

        var updatedDomain = domain with
        {
            IsDnsVerified = true,
            SslStatus = "Active",
            LastVerifiedAtUtc = DateTime.UtcNow
        };

        _domains[domain.Id] = updatedDomain;
        _domains[domain.DomainName] = updatedDomain;

        var result = new DnsVerificationResult(
            DomainId: domain.Id,
            DomainName: domain.DomainName,
            IsFullyVerified: true,
            Records: records,
            StatusMessage: "DNS propagation and ACME ownership verified successfully."
        );

        return Task.FromResult(result);
    }

    public Task<SslRenewalResult> IssueOrRenewSslAsync(string domainId, CancellationToken ct = default)
    {
        if (!_domains.TryGetValue(domainId, out var domain))
        {
            throw new KeyNotFoundException($"Custom domain '{domainId}' not found.");
        }

        var newExpiry = DateTime.UtcNow.AddDays(90);
        var fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(domain.DomainName + newExpiry.Ticks)));

        var updatedDomain = domain with
        {
            SslStatus = "Active",
            SslExpiresAtUtc = newExpiry
        };

        _domains[domain.Id] = updatedDomain;
        _domains[domain.DomainName] = updatedDomain;

        var result = new SslRenewalResult(
            DomainId: domain.Id,
            DomainName: domain.DomainName,
            SslStatus: "Active",
            CertificateFingerprint: $"SHA256:{fingerprint[..24]}",
            IssuedAtUtc: DateTime.UtcNow,
            ExpiresAtUtc: newExpiry,
            Issuer: "Let's Encrypt Authority X3 (TLS 1.3)"
        );

        return Task.FromResult(result);
    }

    public Task<TenantBrandingPayload> ResolveTenantBrandAsync(string hostOrDomainId, CancellationToken ct = default)
    {
        var key = hostOrDomainId?.Trim().ToLowerInvariant() ?? string.Empty;
        if (!_domains.TryGetValue(key, out var domain))
        {
            // Fallback default brand
            var defaultTheming = new BrandThemingConfig("#2563eb", "#059669", null, null, null, "Growth OS Portal");
            return Task.FromResult(new TenantBrandingPayload(
                TenantId: "default",
                DomainName: key,
                Theming: defaultTheming,
                IsSecure: true,
                CssVariables: new Dictionary<string, string>
                {
                    ["--color-primary"] = defaultTheming.PrimaryColor,
                    ["--color-accent"] = defaultTheming.AccentColor
                }
            ));
        }

        var cssVars = new Dictionary<string, string>
        {
            ["--color-primary"] = domain.Theming.PrimaryColor,
            ["--color-accent"] = domain.Theming.AccentColor
        };

        var payload = new TenantBrandingPayload(
            TenantId: domain.TenantId,
            DomainName: domain.DomainName,
            Theming: domain.Theming,
            IsSecure: domain.SslStatus == "Active",
            CssVariables: cssVars
        );

        return Task.FromResult(payload);
    }

    public Task<IReadOnlyList<TenantCustomDomain>> ListDomainsAsync(string? tenantId = null, CancellationToken ct = default)
    {
        var distinctDomains = _domains.Values
            .DistinctBy(d => d.Id)
            .Where(d => string.IsNullOrEmpty(tenantId) || d.TenantId.Equals(tenantId, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(d => d.CreatedAtUtc)
            .ToList();

        return Task.FromResult<IReadOnlyList<TenantCustomDomain>>(distinctDomains);
    }
}
