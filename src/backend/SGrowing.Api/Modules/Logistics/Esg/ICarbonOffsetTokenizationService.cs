namespace SGrowing.Api.Modules.Logistics.Esg;

/// <summary>
/// Service responsible for converting carbon emissions liabilities into certified retirement tokens
/// and issuing cryptographically verified CarbonOffsetCertificates.
/// </summary>
public interface ICarbonOffsetTokenizationService
{
    /// <summary>
    /// Permanently retires carbon offset credits for a tenant and issues a signed certificate.
    /// Pricing standard: 1,000 kg CO2e (1 MT) = 3,500.00 DZD.
    /// </summary>
    CarbonOffsetCertificate RetireOffsetCredits(string tenantId, double kgToOffset, OffsetProjectType projectType, string secretKey);

    /// <summary>
    /// Retrieves a non-fungible carbon offset certificate by its unique identifier.
    /// </summary>
    CarbonOffsetCertificate? GetCertificate(string certificateId);

    /// <summary>
    /// Retrieves all retired certificates associated with a specific tenant.
    /// </summary>
    IReadOnlyList<CarbonOffsetCertificate> GetCertificatesForTenant(string tenantId);

    /// <summary>
    /// Gets total kilograms of CO2e retired for a tenant across all projects.
    /// </summary>
    double GetTotalOffsetKg(string tenantId);
}
