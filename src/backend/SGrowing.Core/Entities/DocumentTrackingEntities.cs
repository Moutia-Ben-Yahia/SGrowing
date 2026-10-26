using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace SGrowing.Core.Entities;

public enum EnvelopeStatus
{
    Draft = 1,
    Sent = 2,
    Completed = 3,
    Declined = 4,
    Voided = 5
}

public enum SignerStatus
{
    Pending = 1,
    Viewed = 2,
    Signed = 3,
    Declined = 4
}

public enum SignerRole
{
    Signer = 1,
    Approver = 2,
    Viewer = 3
}

public class DocumentEnvelope
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string DocumentUrl { get; set; } = string.Empty;
    public EnvelopeStatus Status { get; set; } = EnvelopeStatus.Draft;
    public string ChecksumSha256 { get; set; } = string.Empty;
    public bool IsSequentialOrder { get; set; } = true;
    public string AuditCertificateJson { get; set; } = "{}";
    public DateTime? CompletedAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public List<EnvelopeSigner> Signers { get; set; } = new();

    public static DocumentEnvelope Create(
        Guid tenantId,
        string title,
        string documentUrl,
        string rawContentForChecksum,
        bool isSequential = true)
    {
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawContentForChecksum)));
        return new DocumentEnvelope
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Title = title.Trim(),
            DocumentUrl = documentUrl.Trim(),
            Status = EnvelopeStatus.Draft,
            ChecksumSha256 = hash,
            IsSequentialOrder = isSequential,
            CreatedAtUtc = DateTime.UtcNow
        };
    }
}

public class EnvelopeSigner
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid EnvelopeId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public SignerRole Role { get; set; } = SignerRole.Signer;
    public int OrderIndex { get; set; } = 1;
    public SignerStatus Status { get; set; } = SignerStatus.Pending;
    public string SignatureData { get; set; } = string.Empty;
    public string IpAddress { get; set; } = string.Empty;
    public string VerificationToken { get; set; } = string.Empty;
    public DateTime? SignedAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public static EnvelopeSigner Create(
        Guid tenantId,
        Guid envelopeId,
        string name,
        string email,
        SignerRole role = SignerRole.Signer,
        int orderIndex = 1)
    {
        return new EnvelopeSigner
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            EnvelopeId = envelopeId,
            Name = name.Trim(),
            Email = email.Trim().ToLowerInvariant(),
            Role = role,
            OrderIndex = orderIndex,
            Status = SignerStatus.Pending,
            VerificationToken = Guid.NewGuid().ToString("N")[..16],
            CreatedAtUtc = DateTime.UtcNow
        };
    }
}

public class DocumentDwellTelemetry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid DocumentId { get; set; }
    public int PageNumber { get; set; } = 1;
    public string ViewerEmail { get; set; } = string.Empty;
    public string ViewerIp { get; set; } = "127.0.0.1";
    public int DwellTimeSeconds { get; set; } = 0;
    public int MaxScrollPercent { get; set; } = 100;
    public DateTime CapturedAtUtc { get; set; } = DateTime.UtcNow;

    public static DocumentDwellTelemetry Create(
        Guid tenantId,
        Guid documentId,
        int pageNumber,
        string viewerEmail,
        string viewerIp,
        int dwellSeconds,
        int maxScrollPercent = 100)
    {
        return new DocumentDwellTelemetry
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            DocumentId = documentId,
            PageNumber = pageNumber,
            ViewerEmail = viewerEmail.Trim().ToLowerInvariant(),
            ViewerIp = viewerIp.Trim(),
            DwellTimeSeconds = dwellSeconds,
            MaxScrollPercent = Math.Clamp(maxScrollPercent, 0, 100),
            CapturedAtUtc = DateTime.UtcNow
        };
    }
}

public class DocumentAccessGate
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid DocumentId { get; set; }
    public bool IsEmailRequired { get; set; } = true;
    public string? PasscodeHash { get; set; }
    public bool IsWatermarkEnabled { get; set; } = true;
    public string WatermarkPattern { get; set; } = "CONFIDENTIAL - {VIEWER_EMAIL} - {IP}";
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public static DocumentAccessGate Create(
        Guid tenantId,
        Guid documentId,
        bool requireEmail = true,
        string? passcode = null,
        bool enableWatermark = true,
        string watermarkPattern = "CONFIDENTIAL - {VIEWER_EMAIL} - {IP}")
    {
        string? passHash = null;
        if (!string.IsNullOrWhiteSpace(passcode))
        {
            passHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(passcode.Trim())));
        }

        return new DocumentAccessGate
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            DocumentId = documentId,
            IsEmailRequired = requireEmail,
            PasscodeHash = passHash,
            IsWatermarkEnabled = enableWatermark,
            WatermarkPattern = watermarkPattern,
            CreatedAtUtc = DateTime.UtcNow
        };
    }
}
