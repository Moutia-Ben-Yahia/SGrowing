using Microsoft.EntityFrameworkCore;
using SGrowing.Api.Data;
using SGrowing.Core.Entities;

namespace SGrowing.Api.Modules.Tenancy;

public interface IStorefrontDomainService
{
    Task<StorefrontDomain> RegisterCustomDomainAsync(Guid tenantId, string hostname, bool isPrimary = true);
    Task<StorefrontDomain> VerifyDomainDnsAsync(Guid tenantId, Guid domainId, bool simulateValidDns = true);
    Task<BrandThemeTokens> UpsertBrandThemeTokensAsync(Guid tenantId, BrandThemeTokens theme);
    Task<PublicStorefrontConfigDto?> GetPublicStorefrontConfigAsync(string storefrontSlugOrHostname);
}

public class StorefrontDomainService : IStorefrontDomainService
{
    private readonly SGrowingDbContext _db;

    public StorefrontDomainService(SGrowingDbContext db)
    {
        _db = db;
    }

    public async Task<StorefrontDomain> RegisterCustomDomainAsync(Guid tenantId, string hostname, bool isPrimary = true)
    {
        var cleanHostname = hostname.Trim().ToLowerInvariant();
        var existing = await _db.StorefrontDomains
            .FirstOrDefaultAsync(d => d.TenantId == tenantId && d.Hostname == cleanHostname);

        if (existing != null)
            return existing;

        var domain = StorefrontDomain.Create(tenantId, cleanHostname, isPrimary);
        _db.StorefrontDomains.Add(domain);
        await _db.SaveChangesAsync();

        return domain;
    }

    public async Task<StorefrontDomain> VerifyDomainDnsAsync(Guid tenantId, Guid domainId, bool simulateValidDns = true)
    {
        var domain = await _db.StorefrontDomains
            .FirstOrDefaultAsync(d => d.TenantId == tenantId && d.Id == domainId);

        if (domain == null)
            throw new InvalidOperationException($"Domain with ID {domainId} not found.");

        if (simulateValidDns)
        {
            domain.ValidationStatus = DomainValidationStatus.Active;
            domain.SslStatus = SslCertificateStatus.Issued;
            domain.VerifiedAtUtc = DateTime.UtcNow;
        }
        else
        {
            domain.ValidationStatus = DomainValidationStatus.Failed;
            domain.SslStatus = SslCertificateStatus.Provisioning;
        }

        await _db.SaveChangesAsync();
        return domain;
    }

    public async Task<BrandThemeTokens> UpsertBrandThemeTokensAsync(Guid tenantId, BrandThemeTokens theme)
    {
        var existing = await _db.BrandThemeTokens
            .FirstOrDefaultAsync(b => b.TenantId == tenantId && b.StorefrontSlug == theme.StorefrontSlug);

        if (existing == null)
        {
            theme.Id = Guid.NewGuid();
            theme.TenantId = tenantId;
            theme.UpdatedAtUtc = DateTime.UtcNow;
            _db.BrandThemeTokens.Add(theme);
            await _db.SaveChangesAsync();
            return theme;
        }

        existing.BrandName = theme.BrandName;
        existing.PrimaryColorHex = theme.PrimaryColorHex;
        existing.SecondaryColorHex = theme.SecondaryColorHex;
        existing.FontFamily = theme.FontFamily;
        existing.LogoUrl = theme.LogoUrl;
        existing.FaviconUrl = theme.FaviconUrl;
        existing.SupportEmail = theme.SupportEmail;
        existing.SupportPhone = theme.SupportPhone;
        existing.SmsSenderId = theme.SmsSenderId;
        existing.WhatsAppSenderPhone = theme.WhatsAppSenderPhone;
        existing.UpdatedAtUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return existing;
    }

    public async Task<PublicStorefrontConfigDto?> GetPublicStorefrontConfigAsync(string storefrontSlugOrHostname)
    {
        var query = storefrontSlugOrHostname.Trim().ToLowerInvariant();

        // 1. Check if it matches a custom domain
        var domain = await _db.StorefrontDomains
            .FirstOrDefaultAsync(d => d.Hostname == query && d.ValidationStatus == DomainValidationStatus.Active);

        Guid? tenantId = domain?.TenantId;

        // 2. If not found by domain, check BrandThemeTokens by slug
        BrandThemeTokens? theme = null;
        if (tenantId != null)
        {
            theme = await _db.BrandThemeTokens.FirstOrDefaultAsync(b => b.TenantId == tenantId.Value);
        }
        else
        {
            theme = await _db.BrandThemeTokens.FirstOrDefaultAsync(b => b.StorefrontSlug == query);
        }

        if (theme == null)
        {
            // Default fallback
            return new PublicStorefrontConfigDto(
                StorefrontSlug: "default",
                BrandName: "SGrowing Official Store",
                PrimaryColorHex: "#4F46E5",
                SecondaryColorHex: "#06B6D4",
                FontFamily: "Inter, sans-serif",
                LogoUrl: "https://cdn.sgrowing.com/logo.png",
                FaviconUrl: "https://cdn.sgrowing.com/favicon.ico",
                SupportEmail: "support@sgrowing.com",
                SupportPhone: "+21671000000",
                Currency: "TND",
                StandardShippingFee: 7.0m,
                CodEnabled: true,
                OnlinePaymentEnabled: true);
        }

        return new PublicStorefrontConfigDto(
            StorefrontSlug: theme.StorefrontSlug,
            BrandName: theme.BrandName,
            PrimaryColorHex: theme.PrimaryColorHex,
            SecondaryColorHex: theme.SecondaryColorHex,
            FontFamily: theme.FontFamily,
            LogoUrl: theme.LogoUrl,
            FaviconUrl: theme.FaviconUrl,
            SupportEmail: theme.SupportEmail,
            SupportPhone: theme.SupportPhone,
            Currency: "TND",
            StandardShippingFee: 7.0m,
            CodEnabled: true,
            OnlinePaymentEnabled: true);
    }
}
