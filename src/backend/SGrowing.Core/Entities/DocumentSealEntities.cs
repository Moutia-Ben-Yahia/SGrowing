using System;

namespace SGrowing.Core.Entities;

/// <summary>
/// Cryptographic document seal record representing a legally binding, tamper-evident PAdES/PKCS#7 signature.
/// Schema: compliance
/// </summary>
public class DocumentSealRecordEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid DocumentId { get; set; }
    public string Sha256Hash { get; set; } = string.Empty;
    public string SignatureFormat { get; set; } = "ISO 32000-1 PKCS#7 / PAdES-B-LTV";
    public string CertificateIssuer { get; set; } = "CN=SGrowing Enterprise Root CA, O=SGrowing Platform, C=DZ";
    public string CertificateThumbprintSha256 { get; set; } = string.Empty;
    public string VerificationToken { get; set; } = string.Empty;
    public string SignerEmail { get; set; } = string.Empty;
    public string SignerIpAddress { get; set; } = "127.0.0.1";
    public string SealedPdfUrl { get; set; } = string.Empty;
    public bool HasRfc3161Timestamp { get; set; } = true;
    public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
    public bool IsTampered { get; set; } = false;
    public string AuditChecksum { get; set; } = string.Empty;
}
