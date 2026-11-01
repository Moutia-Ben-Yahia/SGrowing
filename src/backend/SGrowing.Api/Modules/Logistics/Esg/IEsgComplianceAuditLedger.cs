namespace SGrowing.Api.Modules.Logistics.Esg;

/// <summary>
/// Interface for the immutable, Merkle-tree based corporate CSRD/GRI compliance ESG ledger.
/// </summary>
public interface IEsgComplianceAuditLedger
{
    /// <summary>
    /// Records an immutable emission or offset event into the tenant's cryptographic chain.
    /// </summary>
    EsgEventRecord RecordEsgEvent(string tenantId, string eventType, double co2eDeltaKg, string metadataHash);

    /// <summary>
    /// Generates an annual audited ESG balance sheet with Merkle cryptographic root.
    /// </summary>
    EsgBalanceSheet GenerateAuditBalanceSheet(string tenantId, int year, string authoritySecretKey);

    /// <summary>
    /// Cryptographically verifies a carbon offset certificate and checks ledger integrity.
    /// </summary>
    AuditCertificationStatus VerifyAuditCertificate(string certificateId, string secretKey);

    /// <summary>
    /// Registers a certified carbon offset certificate with the audit ledger.
    /// </summary>
    void RecordOffsetCertificate(CarbonOffsetCertificate certificate);

    /// <summary>
    /// Retrieves a registered certificate by ID.
    /// </summary>
    CarbonOffsetCertificate? GetCertificate(string certificateId);

    /// <summary>
    /// Retrieves the ordered audit trail of events for a tenant.
    /// </summary>
    IReadOnlyList<EsgEventRecord> GetAuditTrail(string tenantId);

    /// <summary>
    /// Verifies the cryptographic chaining integrity of the entire ledger for a tenant.
    /// </summary>
    bool VerifyLedgerIntegrity(string tenantId);

    /// <summary>
    /// Simulates data tampering in the ledger for verification and defensive testing.
    /// </summary>
    void SimulateTamper(string tenantId, int sequenceIndex, double tamperedDeltaKg);
}
