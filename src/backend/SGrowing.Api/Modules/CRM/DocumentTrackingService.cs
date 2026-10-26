using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SGrowing.Api.Data;
using SGrowing.Core.Entities;
using SGrowing.Core.Outbox;

namespace SGrowing.Api.Modules.CRM;

public record DocumentDwellReportDto(
    Guid DocumentId,
    int TotalUniqueViewers,
    int TotalDwellSeconds,
    double AverageCompletionRatePercent,
    List<PageDwellMetricDto> PagesBreakdown
);

public record PageDwellMetricDto(
    int PageNumber,
    int TotalDwellSeconds,
    int ViewersCount,
    double AverageDwellSeconds
);

public record DocumentEnvelopeCertificateDto(
    Guid EnvelopeId,
    string DocumentTitle,
    string ChecksumSha256,
    string VerificationToken,
    DateTime CompletedAtUtc,
    List<SignerAuditRecordDto> Signers
);

public record SignerAuditRecordDto(
    string Name,
    string Email,
    string Role,
    string IpAddress,
    DateTime SignedAtUtc
);

public interface IDocumentTrackingService
{
    Task<DocumentEnvelope> CreateEnvelopeAsync(Guid tenantId, string title, string documentUrl, string rawContent, bool isSequential = true);
    Task<EnvelopeSigner> AddSignerAsync(Guid tenantId, Guid envelopeId, string name, string email, SignerRole role = SignerRole.Signer, int orderIndex = 1);
    Task<DocumentEnvelope> SubmitSignatureAsync(Guid tenantId, Guid envelopeId, string signerEmail, string signatureData, string ipAddress);
    Task<DocumentDwellTelemetry> RecordPageDwellAsync(Guid tenantId, Guid documentId, int pageNumber, string viewerEmail, string viewerIp, int dwellSeconds, int maxScrollPercent = 100);
    Task<DocumentDwellReportDto> GetDocumentDwellReportAsync(Guid tenantId, Guid documentId);
    Task<DocumentAccessGate> ConfigureAccessGateAsync(Guid tenantId, Guid documentId, bool requireEmail = true, string? passcode = null, bool enableWatermark = true, string watermarkPattern = "CONFIDENTIAL - {VIEWER_EMAIL} - {IP}");
    Task<bool> VerifyPasscodeAsync(Guid tenantId, Guid documentId, string passcode);
    Task<DocumentEnvelopeCertificateDto?> GetEnvelopeCertificateAsync(Guid tenantId, Guid envelopeId);
}

public class DocumentTrackingService : IDocumentTrackingService
{
    private readonly SGrowingDbContext _db;
    private readonly IOutbox _outbox;

    public DocumentTrackingService(SGrowingDbContext db, IOutbox outbox)
    {
        _db = db;
        _outbox = outbox;
    }

    public async Task<DocumentEnvelope> CreateEnvelopeAsync(Guid tenantId, string title, string documentUrl, string rawContent, bool isSequential = true)
    {
        var envelope = DocumentEnvelope.Create(tenantId, title, documentUrl, rawContent, isSequential);
        _db.DocumentEnvelopes.Add(envelope);
        await _db.SaveChangesAsync();
        return envelope;
    }

    public async Task<EnvelopeSigner> AddSignerAsync(Guid tenantId, Guid envelopeId, string name, string email, SignerRole role = SignerRole.Signer, int orderIndex = 1)
    {
        var envelope = await _db.DocumentEnvelopes.FirstOrDefaultAsync(e => e.TenantId == tenantId && e.Id == envelopeId);
        if (envelope == null)
            throw new KeyNotFoundException($"Document envelope {envelopeId} not found.");

        var signer = EnvelopeSigner.Create(tenantId, envelopeId, name, email, role, orderIndex);
        _db.EnvelopeSigners.Add(signer);

        if (envelope.Status == EnvelopeStatus.Draft)
        {
            envelope.Status = EnvelopeStatus.Sent;
        }

        await _db.SaveChangesAsync();
        return signer;
    }

    public async Task<DocumentEnvelope> SubmitSignatureAsync(Guid tenantId, Guid envelopeId, string signerEmail, string signatureData, string ipAddress)
    {
        var envelope = await _db.DocumentEnvelopes
            .FirstOrDefaultAsync(e => e.TenantId == tenantId && e.Id == envelopeId);

        if (envelope == null)
            throw new KeyNotFoundException($"Document envelope {envelopeId} not found.");

        var signers = await _db.EnvelopeSigners
            .Where(s => s.TenantId == tenantId && s.EnvelopeId == envelopeId)
            .OrderBy(s => s.OrderIndex)
            .ToListAsync();
        envelope.Signers = signers;

        var normalizedEmail = signerEmail.Trim().ToLowerInvariant();
        var signer = envelope.Signers.FirstOrDefault(s => s.Email == normalizedEmail);
        if (signer == null)
            throw new InvalidOperationException($"Signer with email '{signerEmail}' is not assigned to this envelope.");

        if (signer.Status == SignerStatus.Signed)
            throw new InvalidOperationException("Signer has already completed their signature.");

        // If sequential, verify all prior signers with lower order index have signed
        if (envelope.IsSequentialOrder)
        {
            var priorUnsigned = envelope.Signers
                .Where(s => s.OrderIndex < signer.OrderIndex && s.Role != SignerRole.Viewer && s.Status != SignerStatus.Signed)
                .ToList();

            if (priorUnsigned.Any())
            {
                throw new InvalidOperationException($"Sequential signing order violation. Prior signer '{priorUnsigned.First().Email}' must sign first.");
            }
        }

        signer.Status = SignerStatus.Signed;
        signer.SignatureData = signatureData;
        signer.IpAddress = ipAddress;
        signer.SignedAtUtc = DateTime.UtcNow;

        // Check if all signers/approvers are now completed
        var remainingPending = envelope.Signers
            .Where(s => s.Role != SignerRole.Viewer && s.Status != SignerStatus.Signed)
            .ToList();

        if (!remainingPending.Any())
        {
            envelope.Status = EnvelopeStatus.Completed;
            envelope.CompletedAtUtc = DateTime.UtcNow;

            var certSigners = envelope.Signers.Select(s => new SignerAuditRecordDto(
                Name: s.Name,
                Email: s.Email,
                Role: s.Role.ToString(),
                IpAddress: s.IpAddress,
                SignedAtUtc: s.SignedAtUtc ?? DateTime.UtcNow
            )).ToList();

            var cert = new DocumentEnvelopeCertificateDto(
                EnvelopeId: envelope.Id,
                DocumentTitle: envelope.Title,
                ChecksumSha256: envelope.ChecksumSha256,
                VerificationToken: Guid.NewGuid().ToString("N")[..12].ToUpperInvariant(),
                CompletedAtUtc: envelope.CompletedAtUtc.Value,
                Signers: certSigners
            );

            envelope.AuditCertificateJson = JsonSerializer.Serialize(cert);

            await _outbox.EnqueueAsync(tenantId, "Document.EnvelopeCompleted", cert);
        }

        await _db.SaveChangesAsync();
        return envelope;
    }

    public async Task<DocumentDwellTelemetry> RecordPageDwellAsync(Guid tenantId, Guid documentId, int pageNumber, string viewerEmail, string viewerIp, int dwellSeconds, int maxScrollPercent = 100)
    {
        var telemetry = DocumentDwellTelemetry.Create(tenantId, documentId, pageNumber, viewerEmail, viewerIp, dwellSeconds, maxScrollPercent);
        _db.DocumentDwellTelemetries.Add(telemetry);
        await _db.SaveChangesAsync();
        return telemetry;
    }

    public async Task<DocumentDwellReportDto> GetDocumentDwellReportAsync(Guid tenantId, Guid documentId)
    {
        var records = await _db.DocumentDwellTelemetries
            .Where(d => d.TenantId == tenantId && d.DocumentId == documentId)
            .ToListAsync();

        var uniqueViewers = records.Select(r => r.ViewerEmail).Distinct().Count();
        var totalDwellSeconds = records.Sum(r => r.DwellTimeSeconds);

        var pages = records.GroupBy(r => r.PageNumber)
            .OrderBy(g => g.Key)
            .Select(g => new PageDwellMetricDto(
                PageNumber: g.Key,
                TotalDwellSeconds: g.Sum(r => r.DwellTimeSeconds),
                ViewersCount: g.Select(r => r.ViewerEmail).Distinct().Count(),
                AverageDwellSeconds: Math.Round(g.Average(r => r.DwellTimeSeconds), 1)
            )).ToList();

        double avgCompletion = records.Count > 0 ? records.Average(r => r.MaxScrollPercent) : 0.0;

        return new DocumentDwellReportDto(
            DocumentId: documentId,
            TotalUniqueViewers: uniqueViewers,
            TotalDwellSeconds: totalDwellSeconds,
            AverageCompletionRatePercent: Math.Round(avgCompletion, 1),
            PagesBreakdown: pages
        );
    }

    public async Task<DocumentAccessGate> ConfigureAccessGateAsync(Guid tenantId, Guid documentId, bool requireEmail = true, string? passcode = null, bool enableWatermark = true, string watermarkPattern = "CONFIDENTIAL - {VIEWER_EMAIL} - {IP}")
    {
        var existing = await _db.DocumentAccessGates.FirstOrDefaultAsync(g => g.TenantId == tenantId && g.DocumentId == documentId);
        if (existing != null)
        {
            existing.IsEmailRequired = requireEmail;
            existing.IsWatermarkEnabled = enableWatermark;
            existing.WatermarkPattern = watermarkPattern;
            if (!string.IsNullOrWhiteSpace(passcode))
            {
                existing.PasscodeHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(passcode.Trim())));
            }
            await _db.SaveChangesAsync();
            return existing;
        }

        var gate = DocumentAccessGate.Create(tenantId, documentId, requireEmail, passcode, enableWatermark, watermarkPattern);
        _db.DocumentAccessGates.Add(gate);
        await _db.SaveChangesAsync();
        return gate;
    }

    public async Task<bool> VerifyPasscodeAsync(Guid tenantId, Guid documentId, string passcode)
    {
        var gate = await _db.DocumentAccessGates.FirstOrDefaultAsync(g => g.TenantId == tenantId && g.DocumentId == documentId);
        if (gate == null || string.IsNullOrWhiteSpace(gate.PasscodeHash)) return true;

        var inputHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(passcode.Trim())));
        return string.Equals(gate.PasscodeHash, inputHash, StringComparison.OrdinalIgnoreCase);
    }

    public async Task<DocumentEnvelopeCertificateDto?> GetEnvelopeCertificateAsync(Guid tenantId, Guid envelopeId)
    {
        var envelope = await _db.DocumentEnvelopes.FirstOrDefaultAsync(e => e.TenantId == tenantId && e.Id == envelopeId);
        if (envelope == null || envelope.Status != EnvelopeStatus.Completed || string.IsNullOrWhiteSpace(envelope.AuditCertificateJson))
            return null;

        return JsonSerializer.Deserialize<DocumentEnvelopeCertificateDto>(envelope.AuditCertificateJson);
    }
}
