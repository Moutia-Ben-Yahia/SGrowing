namespace SGrowing.Api.Modules.Tenancy.CustomDomains;

public record CustomDomainRegistrationRequest(
    string TenantId,
    string DomainName,
    string? PrimaryColor = null,
    string? AccentColor = null,
    string? LogoUrl = null,
    string? FaviconUrl = null,
    string? CustomCss = null
);

public record TenantCustomDomain(
    string Id,
    string TenantId,
    string DomainName,
    string VerificationToken,
    string ExpectedCname,
    bool IsDnsVerified,
    string SslStatus,
    DateTime SslExpiresAtUtc,
    BrandThemingConfig Theming,
    DateTime CreatedAtUtc,
    DateTime? LastVerifiedAtUtc = null
);

public record DnsVerificationRecord(
    string RecordType,
    string Host,
    string ExpectedValue,
    bool IsConfigured
);

public record DnsVerificationResult(
    string DomainId,
    string DomainName,
    bool IsFullyVerified,
    IReadOnlyList<DnsVerificationRecord> Records,
    string StatusMessage
);

public record SslRenewalResult(
    string DomainId,
    string DomainName,
    string SslStatus,
    string CertificateFingerprint,
    DateTime IssuedAtUtc,
    DateTime ExpiresAtUtc,
    string Issuer
);

public record BrandThemingConfig(
    string PrimaryColor,
    string AccentColor,
    string? LogoUrl,
    string? FaviconUrl,
    string? CustomCss,
    string PageTitle
);

public record TenantBrandingPayload(
    string TenantId,
    string DomainName,
    BrandThemingConfig Theming,
    bool IsSecure,
    IReadOnlyDictionary<string, string> CssVariables
);
