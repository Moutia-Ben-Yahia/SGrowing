using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;

namespace SGrowing.Api.Modules.Tenancy;

public enum WhitelabelSslStatus
{
    PendingDns = 1,
    Validating = 2,
    Issued = 3,
    Renewing = 4,
    Failed = 5,
    Revoked = 6
}

public record WhitelabelThemeTokens(
    string PrimaryColor = "#4338CA",
    string SecondaryColor = "#06B6D4",
    string AccentColor = "#F59E0B",
    string BackgroundColor = "#0F172A",
    string SurfaceColor = "#1E293B",
    string TextColor = "#F8FAFC",
    string BorderRadius = "12px",
    string FontFamily = "Inter, system-ui, sans-serif",
    string HeadingFontFamily = "Plus Jakarta Sans, sans-serif"
);

public record WhitelabelAssets(
    string LogoUrl = "https://cdn.sgrowing.io/defaults/logo.svg",
    string DarkLogoUrl = "https://cdn.sgrowing.io/defaults/logo-dark.svg",
    string FaviconUrl = "https://cdn.sgrowing.io/defaults/favicon.ico"
);

public record WhitelabelAgencyProfile(
    string BrandName = "SGrowing Partner Agency",
    string LegalName = "SGrowing Partner Agency LLC",
    string SupportEmail = "support@agency.com",
    string SupportUrl = "https://agency.com/help",
    bool PoweredBySGrowingVisible = false
);

public record WhitelabelEmailConfig(
    string CustomMailDomain = "mail.agency.com",
    string DkimSelector = "sg2026",
    string DkimPublicKey = "",
    string SpfRecord = "v=spf1 include:_spf.sgrowing.io ~all",
    string DmarcRecord = "v=DMARC1; p=quarantine; pct=100",
    string MxRecord = "10 mail.sgrowing.io",
    bool IsVerified = true
);

public record CustomDomainMapping(
    Guid Id,
    Guid TenantId,
    string Hostname,
    string TargetCname,
    string DnsChallengeRecord,
    string DnsChallengeToken,
    WhitelabelSslStatus SslStatus,
    string? FailureReason,
    string TlsVersion,
    string CipherSuite,
    string CertificateIssuer,
    string CertificateSerialNumber,
    DateTime? IssuedAtUtc,
    DateTime? ExpiresAtUtc,
    DateTime CreatedAtUtc,
    DateTime? LastCheckedAtUtc,
    bool IsPrimary
)
{
    public int? DaysUntilExpiry => ExpiresAtUtc.HasValue
        ? Math.Max(0, (int)Math.Ceiling((ExpiresAtUtc.Value - DateTime.UtcNow).TotalDays))
        : null;
}

public record TenantWhitelabelConfig(
    Guid TenantId,
    string Slug,
    WhitelabelAgencyProfile Profile,
    WhitelabelThemeTokens Theme,
    WhitelabelAssets Assets,
    WhitelabelEmailConfig Email,
    List<CustomDomainMapping> CustomDomains,
    DateTime UpdatedAtUtc
);

public enum HostResolutionType
{
    CustomDomain = 1,
    Subdomain = 2,
    FallbackMaster = 3
}

public record HostResolutionResult(
    Guid ResolvedTenantId,
    string ResolvedHost,
    HostResolutionType ResolutionType,
    string BrandName,
    string? MatchedDomain,
    bool IsSslActive,
    TenantWhitelabelConfig Config
);

public record UpdateBrandingRequest(
    WhitelabelAgencyProfile? Profile,
    WhitelabelThemeTokens? Theme,
    WhitelabelAssets? Assets,
    WhitelabelEmailConfig? Email
);

public interface IWhitelabelBrandingService
{
    Task<TenantWhitelabelConfig> GetTenantConfigAsync(Guid tenantId);
    Task<TenantWhitelabelConfig> UpdateBrandingAsync(Guid tenantId, UpdateBrandingRequest request);
    Task<CustomDomainMapping> RegisterCustomDomainAsync(Guid tenantId, string hostname, bool isPrimary = false);
    Task<CustomDomainMapping> VerifyDomainDnsAsync(Guid tenantId, string hostname, bool simulateValidDns = true);
    Task<CustomDomainMapping?> GetDomainSslStatusAsync(Guid tenantId, string hostname);
    Task<CustomDomainMapping> RenewSslCertificateAsync(Guid tenantId, string hostname);
    Task<CustomDomainMapping> RevokeSslCertificateAsync(Guid tenantId, string hostname, string reason);
    Task<string> GenerateDynamicCssAsync(Guid tenantId);
    Task<string> GenerateDynamicCssForHostAsync(string host);
    Task<HostResolutionResult> ResolveHostAsync(string hostHeaderOrDomain);
    Task<IReadOnlyList<TenantWhitelabelConfig>> GetAllTenantsAsync();
    Task<WhitelabelEmailConfig> GenerateEmailWhitelabelDkimAsync(Guid tenantId, string customMailDomain);
    Task<TenantWhitelabelConfig> ResetToDefaultsAsync(Guid tenantId);
}

public class WhitelabelBrandingService : IWhitelabelBrandingService
{
    public const string DefaultCnameTarget = "cname.sgrowing.io";
    public static readonly Guid MasterTenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");
    public static readonly Guid ApexTenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    public static readonly Guid NordicTenantId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    public static readonly Guid EcomTenantId = Guid.Parse("33333333-3333-3333-3333-333333333333");

    private readonly ConcurrentDictionary<Guid, TenantWhitelabelConfig> _tenants = new();

    public WhitelabelBrandingService()
    {
        SeedMockTenants();
    }

    private void SeedMockTenants()
    {
        // 1. Master Tenant
        var masterDomains = new List<CustomDomainMapping>
        {
            new(
                Id: Guid.NewGuid(),
                TenantId: MasterTenantId,
                Hostname: "app.sgrowing.io",
                TargetCname: DefaultCnameTarget,
                DnsChallengeRecord: "_sgrowing-challenge.app.sgrowing.io",
                DnsChallengeToken: ComputeChallengeToken("app.sgrowing.io", MasterTenantId),
                SslStatus: WhitelabelSslStatus.Issued,
                FailureReason: null,
                TlsVersion: "TLSv1.3",
                CipherSuite: "TLS_AES_128_GCM_SHA256",
                CertificateIssuer: "SGrowing Edge Zero-SSL Root CA",
                CertificateSerialNumber: "01:F8:8C:99:A2:4E:01:BC:90",
                IssuedAtUtc: DateTime.UtcNow.AddDays(-10),
                ExpiresAtUtc: DateTime.UtcNow.AddDays(80),
                CreatedAtUtc: DateTime.UtcNow.AddDays(-60),
                LastCheckedAtUtc: DateTime.UtcNow.AddMinutes(-15),
                IsPrimary: true
            )
        };

        _tenants[MasterTenantId] = new TenantWhitelabelConfig(
            TenantId: MasterTenantId,
            Slug: "master",
            Profile: new WhitelabelAgencyProfile(
                BrandName: "SGrowing Global Engine",
                LegalName: "SGrowing Omnichannel Cloud Corp.",
                SupportEmail: "support@sgrowing.io",
                SupportUrl: "https://sgrowing.io/help",
                PoweredBySGrowingVisible: true
            ),
            Theme: new WhitelabelThemeTokens(
                PrimaryColor: "#6366F1",
                SecondaryColor: "#14B8A6",
                AccentColor: "#EC4899",
                BackgroundColor: "#090D16",
                SurfaceColor: "#111827",
                TextColor: "#F3F4F6",
                BorderRadius: "10px",
                FontFamily: "Inter, system-ui, sans-serif",
                HeadingFontFamily: "Plus Jakarta Sans, sans-serif"
            ),
            Assets: new WhitelabelAssets(
                LogoUrl: "https://images.unsplash.com/photo-1618005182384-a83a8bd57fbe?w=160&h=48&fit=crop",
                DarkLogoUrl: "https://images.unsplash.com/photo-1618005182384-a83a8bd57fbe?w=160&h=48&fit=crop",
                FaviconUrl: "https://cdn.sgrowing.io/brand/favicon.ico"
            ),
            Email: GenerateEmailConfigInternal(MasterTenantId, "mail.sgrowing.io", isVerified: true),
            CustomDomains: masterDomains,
            UpdatedAtUtc: DateTime.UtcNow
        );

        // 2. Apex Growth Partners
        var apexDomains = new List<CustomDomainMapping>
        {
            new(
                Id: Guid.NewGuid(),
                TenantId: ApexTenantId,
                Hostname: "portal.apex-marketing.com",
                TargetCname: DefaultCnameTarget,
                DnsChallengeRecord: "_sgrowing-challenge.portal.apex-marketing.com",
                DnsChallengeToken: ComputeChallengeToken("portal.apex-marketing.com", ApexTenantId),
                SslStatus: WhitelabelSslStatus.Issued,
                FailureReason: null,
                TlsVersion: "TLSv1.3",
                CipherSuite: "TLS_AES_128_GCM_SHA256",
                CertificateIssuer: "Let's Encrypt Authority X3 / SGrowing Edge",
                CertificateSerialNumber: "04:BA:22:90:3A:7C:F1:82:11",
                IssuedAtUtc: DateTime.UtcNow.AddDays(-8),
                ExpiresAtUtc: DateTime.UtcNow.AddDays(82),
                CreatedAtUtc: DateTime.UtcNow.AddDays(-45),
                LastCheckedAtUtc: DateTime.UtcNow.AddMinutes(-30),
                IsPrimary: true
            )
        };

        _tenants[ApexTenantId] = new TenantWhitelabelConfig(
            TenantId: ApexTenantId,
            Slug: "apex",
            Profile: new WhitelabelAgencyProfile(
                BrandName: "Apex Growth Partners",
                LegalName: "Apex Growth Partners LLC",
                SupportEmail: "concierge@apex-marketing.com",
                SupportUrl: "https://apex-marketing.com/concierge",
                PoweredBySGrowingVisible: false
            ),
            Theme: new WhitelabelThemeTokens(
                PrimaryColor: "#4338CA",
                SecondaryColor: "#06B6D4",
                AccentColor: "#F59E0B",
                BackgroundColor: "#0B0F19",
                SurfaceColor: "#111827",
                TextColor: "#F9FAFB",
                BorderRadius: "12px",
                FontFamily: "Inter, system-ui, sans-serif",
                HeadingFontFamily: "Plus Jakarta Sans, sans-serif"
            ),
            Assets: new WhitelabelAssets(
                LogoUrl: "https://images.unsplash.com/photo-1618005182384-a83a8bd57fbe?w=160&h=48&fit=crop",
                DarkLogoUrl: "https://images.unsplash.com/photo-1618005182384-a83a8bd57fbe?w=160&h=48&fit=crop",
                FaviconUrl: "https://cdn.apex-marketing.com/favicon.ico"
            ),
            Email: GenerateEmailConfigInternal(ApexTenantId, "mail.apex-marketing.com", isVerified: true),
            CustomDomains: apexDomains,
            UpdatedAtUtc: DateTime.UtcNow
        );

        // 3. Nordic Scale Agency
        var nordicDomains = new List<CustomDomainMapping>
        {
            new(
                Id: Guid.NewGuid(),
                TenantId: NordicTenantId,
                Hostname: "app.nordicscale.se",
                TargetCname: DefaultCnameTarget,
                DnsChallengeRecord: "_sgrowing-challenge.app.nordicscale.se",
                DnsChallengeToken: ComputeChallengeToken("app.nordicscale.se", NordicTenantId),
                SslStatus: WhitelabelSslStatus.Issued,
                FailureReason: null,
                TlsVersion: "TLSv1.3",
                CipherSuite: "TLS_CHACHA20_POLY1305_SHA256",
                CertificateIssuer: "Let's Encrypt Authority X3 / SGrowing Edge",
                CertificateSerialNumber: "08:11:F2:70:E9:5A:00:CD:34",
                IssuedAtUtc: DateTime.UtcNow.AddDays(-26),
                ExpiresAtUtc: DateTime.UtcNow.AddDays(64),
                CreatedAtUtc: DateTime.UtcNow.AddDays(-90),
                LastCheckedAtUtc: DateTime.UtcNow.AddHours(-1),
                IsPrimary: true
            )
        };

        _tenants[NordicTenantId] = new TenantWhitelabelConfig(
            TenantId: NordicTenantId,
            Slug: "nordic",
            Profile: new WhitelabelAgencyProfile(
                BrandName: "Nordic Scale Agency",
                LegalName: "Nordic Scale Agency AB",
                SupportEmail: "hej@nordicscale.se",
                SupportUrl: "https://nordicscale.se/help",
                PoweredBySGrowingVisible: false
            ),
            Theme: new WhitelabelThemeTokens(
                PrimaryColor: "#0284C7",
                SecondaryColor: "#10B981",
                AccentColor: "#8B5CF6",
                BackgroundColor: "#F8FAFC",
                SurfaceColor: "#FFFFFF",
                TextColor: "#0F172A",
                BorderRadius: "8px",
                FontFamily: "system-ui, -apple-system, sans-serif",
                HeadingFontFamily: "Outfit, sans-serif"
            ),
            Assets: new WhitelabelAssets(
                LogoUrl: "https://images.unsplash.com/photo-1557683316-973673baf926?w=160&h=48&fit=crop",
                DarkLogoUrl: "https://images.unsplash.com/photo-1557683316-973673baf926?w=160&h=48&fit=crop",
                FaviconUrl: "https://nordicscale.se/favicon.ico"
            ),
            Email: GenerateEmailConfigInternal(NordicTenantId, "mail.nordicscale.se", isVerified: true),
            CustomDomains: nordicDomains,
            UpdatedAtUtc: DateTime.UtcNow
        );

        // 4. E-com Superchargers Ltd
        var ecomDomains = new List<CustomDomainMapping>
        {
            new(
                Id: Guid.NewGuid(),
                TenantId: EcomTenantId,
                Hostname: "growth.ecomsuperchargers.co.uk",
                TargetCname: DefaultCnameTarget,
                DnsChallengeRecord: "_sgrowing-challenge.growth.ecomsuperchargers.co.uk",
                DnsChallengeToken: ComputeChallengeToken("growth.ecomsuperchargers.co.uk", EcomTenantId),
                SslStatus: WhitelabelSslStatus.Validating,
                FailureReason: null,
                TlsVersion: "TLSv1.3",
                CipherSuite: "TLS_AES_256_GCM_SHA384",
                CertificateIssuer: "SGrowing Edge Automated ACME CA",
                CertificateSerialNumber: "Pending-Issuance",
                IssuedAtUtc: null,
                ExpiresAtUtc: null,
                CreatedAtUtc: DateTime.UtcNow.AddHours(-2),
                LastCheckedAtUtc: DateTime.UtcNow.AddMinutes(-5),
                IsPrimary: true
            )
        };

        _tenants[EcomTenantId] = new TenantWhitelabelConfig(
            TenantId: EcomTenantId,
            Slug: "ecom",
            Profile: new WhitelabelAgencyProfile(
                BrandName: "E-com Superchargers Ltd",
                LegalName: "E-com Superchargers International Ltd",
                SupportEmail: "ops@ecomsuperchargers.co.uk",
                SupportUrl: "https://ecomsuperchargers.co.uk/helpdesk",
                PoweredBySGrowingVisible: true
            ),
            Theme: new WhitelabelThemeTokens(
                PrimaryColor: "#059669",
                SecondaryColor: "#D97706",
                AccentColor: "#EF4444",
                BackgroundColor: "#18181B",
                SurfaceColor: "#27272A",
                TextColor: "#FAFAFA",
                BorderRadius: "16px",
                FontFamily: "DM Sans, sans-serif",
                HeadingFontFamily: "Cabinet Grotesk, sans-serif"
            ),
            Assets: new WhitelabelAssets(
                LogoUrl: "https://images.unsplash.com/photo-1579546929518-9e396f3cc809?w=160&h=48&fit=crop",
                DarkLogoUrl: "https://images.unsplash.com/photo-1579546929518-9e396f3cc809?w=160&h=48&fit=crop",
                FaviconUrl: "https://ecomsuperchargers.co.uk/favicon.ico"
            ),
            Email: GenerateEmailConfigInternal(EcomTenantId, "notify.ecomsuperchargers.co.uk", isVerified: false),
            CustomDomains: ecomDomains,
            UpdatedAtUtc: DateTime.UtcNow
        );
    }

    public Task<TenantWhitelabelConfig> GetTenantConfigAsync(Guid tenantId)
    {
        if (_tenants.TryGetValue(tenantId, out var config))
        {
            return Task.FromResult(config);
        }

        // Return a fresh tenant config initialized with defaults
        var newConfig = CreateDefaultTenantConfig(tenantId);
        _tenants[tenantId] = newConfig;
        return Task.FromResult(newConfig);
    }

    public Task<TenantWhitelabelConfig> UpdateBrandingAsync(Guid tenantId, UpdateBrandingRequest request)
    {
        var current = _tenants.GetOrAdd(tenantId, CreateDefaultTenantConfig);

        var updated = current with
        {
            Profile = request.Profile ?? current.Profile,
            Theme = request.Theme ?? current.Theme,
            Assets = request.Assets ?? current.Assets,
            Email = request.Email ?? current.Email,
            UpdatedAtUtc = DateTime.UtcNow
        };

        _tenants[tenantId] = updated;
        return Task.FromResult(updated);
    }

    public Task<CustomDomainMapping> RegisterCustomDomainAsync(Guid tenantId, string hostname, bool isPrimary = false)
    {
        var cleanHost = NormalizeHost(hostname);
        var current = _tenants.GetOrAdd(tenantId, CreateDefaultTenantConfig);

        var existing = current.CustomDomains.FirstOrDefault(d => d.Hostname.Equals(cleanHost, StringComparison.OrdinalIgnoreCase));
        if (existing != null)
        {
            return Task.FromResult(existing);
        }

        var challengeRecord = $"_sgrowing-challenge.{cleanHost}";
        var challengeToken = ComputeChallengeToken(cleanHost, tenantId);

        var newDomain = new CustomDomainMapping(
            Id: Guid.NewGuid(),
            TenantId: tenantId,
            Hostname: cleanHost,
            TargetCname: DefaultCnameTarget,
            DnsChallengeRecord: challengeRecord,
            DnsChallengeToken: challengeToken,
            SslStatus: WhitelabelSslStatus.PendingDns,
            FailureReason: null,
            TlsVersion: "TLSv1.3",
            CipherSuite: "TLS_AES_128_GCM_SHA256",
            CertificateIssuer: "SGrowing Edge Let's Encrypt / Zero-SSL CA",
            CertificateSerialNumber: "Pending-Issuance",
            IssuedAtUtc: null,
            ExpiresAtUtc: null,
            CreatedAtUtc: DateTime.UtcNow,
            LastCheckedAtUtc: DateTime.UtcNow,
            IsPrimary: isPrimary || current.CustomDomains.Count == 0
        );

        var updatedDomains = new List<CustomDomainMapping>(current.CustomDomains);
        if (newDomain.IsPrimary)
        {
            // Reset existing primary flags
            updatedDomains = updatedDomains.Select(d => d with { IsPrimary = false }).ToList();
        }
        updatedDomains.Add(newDomain);

        _tenants[tenantId] = current with
        {
            CustomDomains = updatedDomains,
            UpdatedAtUtc = DateTime.UtcNow
        };

        return Task.FromResult(newDomain);
    }

    public Task<CustomDomainMapping> VerifyDomainDnsAsync(Guid tenantId, string hostname, bool simulateValidDns = true)
    {
        var cleanHost = NormalizeHost(hostname);
        var current = _tenants.GetOrAdd(tenantId, CreateDefaultTenantConfig);

        var domain = current.CustomDomains.FirstOrDefault(d => d.Hostname.Equals(cleanHost, StringComparison.OrdinalIgnoreCase));
        if (domain == null)
        {
            throw new KeyNotFoundException($"Domain '{cleanHost}' is not registered for tenant '{tenantId}'.");
        }

        CustomDomainMapping updatedDomain;
        if (simulateValidDns)
        {
            updatedDomain = domain with
            {
                SslStatus = WhitelabelSslStatus.Issued,
                FailureReason = null,
                TlsVersion = "TLSv1.3",
                CipherSuite = "TLS_AES_128_GCM_SHA256",
                CertificateIssuer = "Let's Encrypt Authority X3 / SGrowing Zero-SSL Edge",
                CertificateSerialNumber = GenerateRandomSerial(),
                IssuedAtUtc = DateTime.UtcNow,
                ExpiresAtUtc = DateTime.UtcNow.AddDays(90),
                LastCheckedAtUtc = DateTime.UtcNow
            };
        }
        else
        {
            updatedDomain = domain with
            {
                SslStatus = WhitelabelSslStatus.Failed,
                FailureReason = $"DNS verification failed: TXT record '{domain.DnsChallengeRecord}' does not match cryptographic token '{domain.DnsChallengeToken}' or CNAME target '{DefaultCnameTarget}' was not found in authoritative DNS nameservers.",
                LastCheckedAtUtc = DateTime.UtcNow
            };
        }

        var list = current.CustomDomains.Select(d => d.Id == updatedDomain.Id ? updatedDomain : d).ToList();
        _tenants[tenantId] = current with
        {
            CustomDomains = list,
            UpdatedAtUtc = DateTime.UtcNow
        };

        return Task.FromResult(updatedDomain);
    }

    public Task<CustomDomainMapping?> GetDomainSslStatusAsync(Guid tenantId, string hostname)
    {
        var cleanHost = NormalizeHost(hostname);
        if (_tenants.TryGetValue(tenantId, out var tenant))
        {
            var match = tenant.CustomDomains.FirstOrDefault(d => d.Hostname.Equals(cleanHost, StringComparison.OrdinalIgnoreCase));
            return Task.FromResult(match);
        }

        return Task.FromResult<CustomDomainMapping?>(null);
    }

    public Task<CustomDomainMapping> RenewSslCertificateAsync(Guid tenantId, string hostname)
    {
        var cleanHost = NormalizeHost(hostname);
        var current = _tenants.GetOrAdd(tenantId, CreateDefaultTenantConfig);

        var domain = current.CustomDomains.FirstOrDefault(d => d.Hostname.Equals(cleanHost, StringComparison.OrdinalIgnoreCase));
        if (domain == null)
        {
            throw new KeyNotFoundException($"Domain '{cleanHost}' not found.");
        }

        // Renew certificate: transitions Renewing -> Issued with refreshed 90-day expiry
        var renewedDomain = domain with
        {
            SslStatus = WhitelabelSslStatus.Issued,
            FailureReason = null,
            TlsVersion = "TLSv1.3",
            CipherSuite = "TLS_AES_256_GCM_SHA384",
            CertificateSerialNumber = GenerateRandomSerial(),
            IssuedAtUtc = DateTime.UtcNow,
            ExpiresAtUtc = DateTime.UtcNow.AddDays(90),
            LastCheckedAtUtc = DateTime.UtcNow
        };

        var list = current.CustomDomains.Select(d => d.Id == renewedDomain.Id ? renewedDomain : d).ToList();
        _tenants[tenantId] = current with { CustomDomains = list, UpdatedAtUtc = DateTime.UtcNow };

        return Task.FromResult(renewedDomain);
    }

    public Task<CustomDomainMapping> RevokeSslCertificateAsync(Guid tenantId, string hostname, string reason)
    {
        var cleanHost = NormalizeHost(hostname);
        var current = _tenants.GetOrAdd(tenantId, CreateDefaultTenantConfig);

        var domain = current.CustomDomains.FirstOrDefault(d => d.Hostname.Equals(cleanHost, StringComparison.OrdinalIgnoreCase));
        if (domain == null)
        {
            throw new KeyNotFoundException($"Domain '{cleanHost}' not found.");
        }

        var revokedDomain = domain with
        {
            SslStatus = WhitelabelSslStatus.Revoked,
            FailureReason = reason ?? "Certificate revoked by administrator.",
            LastCheckedAtUtc = DateTime.UtcNow
        };

        var list = current.CustomDomains.Select(d => d.Id == revokedDomain.Id ? revokedDomain : d).ToList();
        _tenants[tenantId] = current with { CustomDomains = list, UpdatedAtUtc = DateTime.UtcNow };

        return Task.FromResult(revokedDomain);
    }

    public async Task<string> GenerateDynamicCssAsync(Guid tenantId)
    {
        var config = await GetTenantConfigAsync(tenantId);
        return CompileCss(config);
    }

    public async Task<string> GenerateDynamicCssForHostAsync(string host)
    {
        var resolution = await ResolveHostAsync(host);
        return CompileCss(resolution.Config);
    }

    public Task<HostResolutionResult> ResolveHostAsync(string hostHeaderOrDomain)
    {
        var clean = NormalizeHost(hostHeaderOrDomain);

        // 1. Try exact match on custom domains across all tenants
        foreach (var (_, tenant) in _tenants)
        {
            var matchedDomain = tenant.CustomDomains.FirstOrDefault(d =>
                d.Hostname.Equals(clean, StringComparison.OrdinalIgnoreCase));

            if (matchedDomain != null)
            {
                return Task.FromResult(new HostResolutionResult(
                    ResolvedTenantId: tenant.TenantId,
                    ResolvedHost: clean,
                    ResolutionType: HostResolutionType.CustomDomain,
                    BrandName: tenant.Profile.BrandName,
                    MatchedDomain: matchedDomain.Hostname,
                    IsSslActive: matchedDomain.SslStatus == WhitelabelSslStatus.Issued,
                    Config: tenant
                ));
            }
        }

        // 2. Try subdomain resolution (e.g. apex.sgrowing.io or apex.localhost)
        var parts = clean.Split('.');
        if (parts.Length >= 2)
        {
            var potentialSubdomain = parts[0].ToLowerInvariant();
            var matchedBySlug = _tenants.Values.FirstOrDefault(t =>
                t.Slug.Equals(potentialSubdomain, StringComparison.OrdinalIgnoreCase));

            if (matchedBySlug != null)
            {
                return Task.FromResult(new HostResolutionResult(
                    ResolvedTenantId: matchedBySlug.TenantId,
                    ResolvedHost: clean,
                    ResolutionType: HostResolutionType.Subdomain,
                    BrandName: matchedBySlug.Profile.BrandName,
                    MatchedDomain: null,
                    IsSslActive: true,
                    Config: matchedBySlug
                ));
            }
        }

        // 3. Fallback to Master Tenant
        var master = _tenants.TryGetValue(MasterTenantId, out var m) ? m : CreateDefaultTenantConfig(MasterTenantId);
        return Task.FromResult(new HostResolutionResult(
            ResolvedTenantId: MasterTenantId,
            ResolvedHost: clean,
            ResolutionType: HostResolutionType.FallbackMaster,
            BrandName: master.Profile.BrandName,
            MatchedDomain: null,
            IsSslActive: true,
            Config: master
        ));
    }

    public Task<IReadOnlyList<TenantWhitelabelConfig>> GetAllTenantsAsync()
    {
        IReadOnlyList<TenantWhitelabelConfig> list = _tenants.Values.ToList();
        return Task.FromResult(list);
    }

    public Task<WhitelabelEmailConfig> GenerateEmailWhitelabelDkimAsync(Guid tenantId, string customMailDomain)
    {
        var cleanMailDomain = NormalizeHost(customMailDomain);
        var emailConfig = GenerateEmailConfigInternal(tenantId, cleanMailDomain, isVerified: false);

        var current = _tenants.GetOrAdd(tenantId, CreateDefaultTenantConfig);
        _tenants[tenantId] = current with
        {
            Email = emailConfig,
            UpdatedAtUtc = DateTime.UtcNow
        };

        return Task.FromResult(emailConfig);
    }

    public Task<TenantWhitelabelConfig> ResetToDefaultsAsync(Guid tenantId)
    {
        var defaultConfig = CreateDefaultTenantConfig(tenantId);
        _tenants[tenantId] = defaultConfig;
        return Task.FromResult(defaultConfig);
    }

    // --- Helper Methods ---

    public static string ComputeChallengeToken(string hostname, Guid tenantId)
    {
        var raw = $"{hostname.ToLowerInvariant()}:{tenantId}:sgrowing-cryptographic-dns-salt-2026";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    public static string NormalizeHost(string? host)
    {
        if (string.IsNullOrWhiteSpace(host)) return "localhost";
        var cleaned = host.Trim();
        if (cleaned.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            cleaned = cleaned[8..];
        else if (cleaned.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
            cleaned = cleaned[7..];

        // Strip path if present
        var slashIdx = cleaned.IndexOf('/');
        if (slashIdx >= 0) cleaned = cleaned[..slashIdx];

        // Strip port if present
        var colonIdx = cleaned.IndexOf(':');
        if (colonIdx >= 0) cleaned = cleaned[..colonIdx];

        return cleaned.ToLowerInvariant();
    }

    public static string CompileCss(TenantWhitelabelConfig config)
    {
        var theme = config.Theme;
        var primaryContrast = GetContrastColor(theme.PrimaryColor);
        var secondaryContrast = GetContrastColor(theme.SecondaryColor);
        var accentContrast = GetContrastColor(theme.AccentColor);
        var surfaceContrast = GetContrastColor(theme.SurfaceColor);
        var primaryRgb = HexToRgbString(theme.PrimaryColor);
        var secondaryRgb = HexToRgbString(theme.SecondaryColor);
        var accentRgb = HexToRgbString(theme.AccentColor);

        var sb = new StringBuilder();
        sb.AppendLine("/**");
        sb.AppendLine($" * SGrowing Multi-Tenant Whitelabel Stylesheet");
        sb.AppendLine($" * Tenant: {config.Profile.BrandName} ({config.TenantId})");
        sb.AppendLine($" * Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
        sb.AppendLine(" */");
        sb.AppendLine(":root {");
        sb.AppendLine($"  --sg-primary: {theme.PrimaryColor};");
        sb.AppendLine($"  --sg-primary-contrast: {primaryContrast};");
        sb.AppendLine($"  --sg-primary-rgb: {primaryRgb};");
        sb.AppendLine($"  --sg-secondary: {theme.SecondaryColor};");
        sb.AppendLine($"  --sg-secondary-contrast: {secondaryContrast};");
        sb.AppendLine($"  --sg-secondary-rgb: {secondaryRgb};");
        sb.AppendLine($"  --sg-accent: {theme.AccentColor};");
        sb.AppendLine($"  --sg-accent-contrast: {accentContrast};");
        sb.AppendLine($"  --sg-accent-rgb: {accentRgb};");
        sb.AppendLine($"  --sg-background: {theme.BackgroundColor};");
        sb.AppendLine($"  --sg-surface: {theme.SurfaceColor};");
        sb.AppendLine($"  --sg-surface-contrast: {surfaceContrast};");
        sb.AppendLine($"  --sg-surface-border: rgba(255, 255, 255, 0.08);");
        sb.AppendLine($"  --sg-text: {theme.TextColor};");
        sb.AppendLine($"  --sg-border-radius: {theme.BorderRadius};");
        sb.AppendLine($"  --sg-font: '{theme.FontFamily}', system-ui, -apple-system, sans-serif;");
        sb.AppendLine($"  --sg-heading-font: '{theme.HeadingFontFamily}', system-ui, -apple-system, sans-serif;");
        sb.AppendLine("}");

        return sb.ToString();
    }

    public static double CalculateLuminance(string hexColor)
    {
        try
        {
            var hex = hexColor.TrimStart('#');
            if (hex.Length == 3)
            {
                hex = $"{hex[0]}{hex[0]}{hex[1]}{hex[1]}{hex[2]}{hex[2]}";
            }
            if (hex.Length != 6) return 0.5;

            var r = Convert.ToInt32(hex[0..2], 16) / 255.0;
            var g = Convert.ToInt32(hex[2..4], 16) / 255.0;
            var b = Convert.ToInt32(hex[4..6], 16) / 255.0;

            var rLin = r <= 0.03928 ? r / 12.92 : Math.Pow((r + 0.055) / 1.055, 2.4);
            var gLin = g <= 0.03928 ? g / 12.92 : Math.Pow((g + 0.055) / 1.055, 2.4);
            var bLin = b <= 0.03928 ? b / 12.92 : Math.Pow((b + 0.055) / 1.055, 2.4);

            return 0.2126 * rLin + 0.7152 * gLin + 0.0722 * bLin;
        }
        catch
        {
            return 0.5;
        }
    }

    public static string GetContrastColor(string hexColor)
    {
        var lum = CalculateLuminance(hexColor);
        return lum > 0.42 ? "#0F172A" : "#FFFFFF";
    }

    public static string HexToRgbString(string hexColor)
    {
        try
        {
            var hex = hexColor.TrimStart('#');
            if (hex.Length == 3)
            {
                hex = $"{hex[0]}{hex[0]}{hex[1]}{hex[1]}{hex[2]}{hex[2]}";
            }
            if (hex.Length != 6) return "99, 102, 241";

            var r = Convert.ToInt32(hex[0..2], 16);
            var g = Convert.ToInt32(hex[2..4], 16);
            var b = Convert.ToInt32(hex[4..6], 16);
            return $"{r}, {g}, {b}";
        }
        catch
        {
            return "99, 102, 241";
        }
    }

    private static string GenerateRandomSerial()
    {
        var bytes = new byte[8];
        RandomNumberGenerator.Fill(bytes);
        return string.Join(":", bytes.Select(b => b.ToString("X2")));
    }

    private static WhitelabelEmailConfig GenerateEmailConfigInternal(Guid tenantId, string customMailDomain, bool isVerified)
    {
        var rawKey = $"{tenantId}:{customMailDomain}:sgrowing-dkim-rsa2048-key";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(rawKey));
        var base64Sample = Convert.ToBase64String(hash);
        var dkimPublicKey = $"v=DKIM1; k=rsa; p=MIIBIjANBgkqhkiG9w0BAQEFAAOCAQ8AMIIBCgKCAQEA{base64Sample}IDAQAB";

        return new WhitelabelEmailConfig(
            CustomMailDomain: customMailDomain,
            DkimSelector: "sg2026",
            DkimPublicKey: dkimPublicKey,
            SpfRecord: "v=spf1 include:_spf.sgrowing.io ~all",
            DmarcRecord: $"v=DMARC1; p=quarantine; rua=mailto:dmarc@{customMailDomain}; pct=100",
            MxRecord: "10 mail.sgrowing.io",
            IsVerified: isVerified
        );
    }

    private static TenantWhitelabelConfig CreateDefaultTenantConfig(Guid tenantId)
    {
        var slug = $"tenant-{tenantId.ToString()[..8]}";
        return new TenantWhitelabelConfig(
            TenantId: tenantId,
            Slug: slug,
            Profile: new WhitelabelAgencyProfile(
                BrandName: "SGrowing Growth Agency",
                LegalName: "SGrowing Partner Agency Ltd",
                SupportEmail: $"support@{slug}.sgrowing.io",
                SupportUrl: $"https://{slug}.sgrowing.io/help",
                PoweredBySGrowingVisible: true
            ),
            Theme: new WhitelabelThemeTokens(
                PrimaryColor: "#4F46E5",
                SecondaryColor: "#06B6D4",
                AccentColor: "#F59E0B",
                BackgroundColor: "#0F172A",
                SurfaceColor: "#1E293B",
                TextColor: "#F8FAFC",
                BorderRadius: "12px",
                FontFamily: "Inter, system-ui, sans-serif",
                HeadingFontFamily: "Plus Jakarta Sans, sans-serif"
            ),
            Assets: new WhitelabelAssets(
                LogoUrl: "https://cdn.sgrowing.io/defaults/logo.svg",
                DarkLogoUrl: "https://cdn.sgrowing.io/defaults/logo-dark.svg",
                FaviconUrl: "https://cdn.sgrowing.io/defaults/favicon.ico"
            ),
            Email: GenerateEmailConfigInternal(tenantId, $"mail.{slug}.sgrowing.io", isVerified: false),
            CustomDomains: new List<CustomDomainMapping>(),
            UpdatedAtUtc: DateTime.UtcNow
        );
    }
}
