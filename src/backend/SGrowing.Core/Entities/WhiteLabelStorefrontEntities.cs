namespace SGrowing.Core.Entities;

public enum DomainValidationStatus
{
    PendingDns = 1,
    Active = 2,
    Failed = 3
}

public enum SslCertificateStatus
{
    Provisioning = 1,
    Issued = 2,
    Expired = 3
}

public class StorefrontDomain
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public string Hostname { get; set; } = string.Empty; // e.g. "shop.parfums-elissar.com"
    public bool IsPrimary { get; set; } = true;
    public string TargetCname { get; set; } = "cname.sgrowing.com";
    public DomainValidationStatus ValidationStatus { get; set; } = DomainValidationStatus.PendingDns;
    public SslCertificateStatus SslStatus { get; set; } = SslCertificateStatus.Provisioning;
    public DateTime? VerifiedAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public static StorefrontDomain Create(Guid tenantId, string hostname, bool isPrimary = true)
    {
        return new StorefrontDomain
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Hostname = hostname.Trim().ToLowerInvariant(),
            IsPrimary = isPrimary,
            TargetCname = "cname.sgrowing.com",
            ValidationStatus = DomainValidationStatus.PendingDns,
            SslStatus = SslCertificateStatus.Provisioning,
            CreatedAtUtc = DateTime.UtcNow
        };
    }
}

public class BrandThemeTokens
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public string StorefrontSlug { get; set; } = "default";
    public string BrandName { get; set; } = "My Store";
    public string PrimaryColorHex { get; set; } = "#4F46E5";
    public string SecondaryColorHex { get; set; } = "#06B6D4";
    public string FontFamily { get; set; } = "Inter, sans-serif";
    public string LogoUrl { get; set; } = string.Empty;
    public string FaviconUrl { get; set; } = string.Empty;
    public string SupportEmail { get; set; } = string.Empty;
    public string SupportPhone { get; set; } = string.Empty;
    public string SmsSenderId { get; set; } = "SGROWING";
    public string WhatsAppSenderPhone { get; set; } = string.Empty;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}

public record PublicStorefrontConfigDto(
    string StorefrontSlug,
    string BrandName,
    string PrimaryColorHex,
    string SecondaryColorHex,
    string FontFamily,
    string LogoUrl,
    string FaviconUrl,
    string SupportEmail,
    string SupportPhone,
    string Currency,
    decimal StandardShippingFee,
    bool CodEnabled,
    bool OnlinePaymentEnabled);
