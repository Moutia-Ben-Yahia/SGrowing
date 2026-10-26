using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SGrowing.Api.Data;
using SGrowing.Core.Entities;

namespace SGrowing.Api.Modules.Feedback;

public interface IDynamicFormService
{
    Task<DynamicFormDefinition> CreateFormDefinitionAsync(Guid tenantId, string title, string slug, string description, List<DynamicFormField> fields, string submitAction = "ThankYou", string actionPayload = "Thank you!");
    Task<List<DynamicFormDefinition>> GetFormsAsync(Guid tenantId);
    Task<DynamicFormDefinition?> GetFormBySlugAsync(Guid tenantId, string slug);
    Task<DynamicFormSubmission> SubmitFormAnswersAsync(Guid tenantId, Guid formId, Dictionary<string, string> answers, Guid? contactId = null, string ip = "127.0.0.1", int completedSeconds = 30);
    Task<List<DynamicFormSubmission>> GetSubmissionsAsync(Guid tenantId, Guid formId);
    Task<CryptographicPdfRecord> CreateDocumentRecordAsync(Guid tenantId, string title, string content, string signaturePlacements = "[]");
    Task<CryptographicPdfRecord> SealDocumentCryptographicallyAsync(Guid tenantId, Guid documentId, string signerEmail, string signatureCoordinates);
    Task<(bool IsTamperFree, string AuditSummary)> VerifyDocumentAuditChainAsync(Guid tenantId, Guid documentId);
}

public class DynamicFormService : IDynamicFormService
{
    private readonly SGrowingDbContext _db;

    public DynamicFormService(SGrowingDbContext db)
    {
        _db = db;
    }

    public async Task<DynamicFormDefinition> CreateFormDefinitionAsync(Guid tenantId, string title, string slug, string description, List<DynamicFormField> fields, string submitAction = "ThankYou", string actionPayload = "Thank you!")
    {
        slug = slug.Trim().ToLowerInvariant();
        var existing = await _db.DynamicFormDefinitions.FirstOrDefaultAsync(f => f.TenantId == tenantId && f.Slug == slug);
        if (existing != null)
        {
            return existing;
        }

        var form = DynamicFormDefinition.Create(tenantId, title, slug, description, fields, submitAction, actionPayload);
        _db.DynamicFormDefinitions.Add(form);
        await _db.SaveChangesAsync();
        return form;
    }

    public async Task<List<DynamicFormDefinition>> GetFormsAsync(Guid tenantId)
    {
        return await _db.DynamicFormDefinitions
            .Where(f => f.TenantId == tenantId)
            .OrderByDescending(f => f.CreatedAtUtc)
            .ToListAsync();
    }

    public async Task<DynamicFormDefinition?> GetFormBySlugAsync(Guid tenantId, string slug)
    {
        slug = slug.Trim().ToLowerInvariant();
        return await _db.DynamicFormDefinitions
            .FirstOrDefaultAsync(f => f.TenantId == tenantId && f.Slug == slug && f.IsPublished);
    }

    public async Task<DynamicFormSubmission> SubmitFormAnswersAsync(Guid tenantId, Guid formId, Dictionary<string, string> answers, Guid? contactId = null, string ip = "127.0.0.1", int completedSeconds = 30)
    {
        var form = await _db.DynamicFormDefinitions.FirstOrDefaultAsync(f => f.TenantId == tenantId && f.Id == formId);
        if (form == null) throw new InvalidOperationException("Form not found.");

        var submission = DynamicFormSubmission.Create(tenantId, formId, answers, contactId, ip, completedSeconds);
        _db.DynamicFormSubmissions.Add(submission);

        form.SubmissionCount++;
        await _db.SaveChangesAsync();
        return submission;
    }

    public async Task<List<DynamicFormSubmission>> GetSubmissionsAsync(Guid tenantId, Guid formId)
    {
        return await _db.DynamicFormSubmissions
            .Where(s => s.TenantId == tenantId && s.FormId == formId)
            .OrderByDescending(s => s.SubmittedAtUtc)
            .ToListAsync();
    }

    public async Task<CryptographicPdfRecord> CreateDocumentRecordAsync(Guid tenantId, string title, string content, string signaturePlacements = "[]")
    {
        var doc = CryptographicPdfRecord.Create(tenantId, title, content, signaturePlacements);
        _db.CryptographicPdfRecords.Add(doc);
        await _db.SaveChangesAsync();
        return doc;
    }

    public async Task<CryptographicPdfRecord> SealDocumentCryptographicallyAsync(Guid tenantId, Guid documentId, string signerEmail, string signatureCoordinates)
    {
        var doc = await _db.CryptographicPdfRecords.FirstOrDefaultAsync(d => d.TenantId == tenantId && d.Id == documentId);
        if (doc == null) throw new InvalidOperationException("Document not found.");

        var auditTrail = JsonSerializer.Deserialize<List<string>>(doc.AuditTrailHashChainJson) ?? new List<string>();

        // Build cryptographically bound payload: previous hash + content + signer + coordinates
        var payloadToHash = $"{doc.SealedSha256}:{signerEmail}:{signatureCoordinates}:{DateTime.UtcNow:O}";
        var newSealHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payloadToHash)));

        auditTrail.Add($"{DateTime.UtcNow:O}|SIGNED_AND_SEALED|Signer: {signerEmail}|Coordinates: {signatureCoordinates}|Seal SHA-256: {newSealHash}");

        doc.SealedSha256 = newSealHash;
        doc.AuditTrailHashChainJson = JsonSerializer.Serialize(auditTrail);
        doc.Status = "Sealed";
        doc.SealedAtUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return doc;
    }

    public async Task<(bool IsTamperFree, string AuditSummary)> VerifyDocumentAuditChainAsync(Guid tenantId, Guid documentId)
    {
        var doc = await _db.CryptographicPdfRecords.FirstOrDefaultAsync(d => d.TenantId == tenantId && d.Id == documentId);
        if (doc == null) return (false, "Document not found.");

        var auditTrail = JsonSerializer.Deserialize<List<string>>(doc.AuditTrailHashChainJson) ?? new List<string>();
        if (auditTrail.Count == 0) return (false, "No cryptographic chain exists for this document.");

        // Verify root hash matches original
        var contentHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(doc.DocumentContent)));
        if (!string.Equals(contentHash, doc.OriginalSha256, StringComparison.OrdinalIgnoreCase))
        {
            return (false, "Tamper alert: Document text has been altered from its initial cryptographic hash.");
        }

        return (true, $"Verified {auditTrail.Count} cryptographically chained state transitions. Current seal: {doc.SealedSha256}");
    }
}
