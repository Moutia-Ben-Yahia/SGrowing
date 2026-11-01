using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SGrowing.Api.Data;
using SGrowing.Core.Entities;

namespace SGrowing.Api.Modules.Security.Vault;

public record GdprErasureCertificateRecord(
    Guid CertificateId,
    Guid TenantId,
    Guid ContactId,
    string DataSubjectEmailHash,
    string RequesterAgent,
    DateTime RequestedAtUtc,
    DateTime CompletedAtUtc,
    int ContactsPseudonymized,
    int ConversationsPseudonymized,
    int MessagesPseudonymized,
    int FormSubmissionsPseudonymized,
    int SurveyResponsesPseudonymized,
    int VoiceTranscriptsPseudonymized,
    int ElectronicSignaturesPseudonymized,
    int FinancialLedgersPreservedAndStripped,
    string ProofOfErasureSha256,
    string VerificationSignature,
    string Status
);

public record GdprCascadeResult(
    bool Success,
    GdprErasureCertificateRecord Certificate,
    string Message
);

public interface IGdprRightToBeForgottenCascadeService
{
    Task<GdprCascadeResult> ExecuteRightToBeForgottenCascadeAsync(
        Guid tenantId,
        Guid contactId,
        string dataSubjectEmail,
        string requesterAgent,
        CancellationToken cancellationToken = default);
}

public class GdprRightToBeForgottenCascadeService(
    SGrowingDbContext db,
    IMerkleComplianceAuditLedgerService? auditLedger = null,
    ILogger<GdprRightToBeForgottenCascadeService>? logger = null,
    string hmacSigningKey = "SGrowing_Gdpr_Compliance_Vault_Hmac_Signing_Key_2026_Secure!")
    : IGdprRightToBeForgottenCascadeService
{
    private readonly SGrowingDbContext _db = db;
    private readonly IMerkleComplianceAuditLedgerService? _auditLedger = auditLedger;
    private readonly ILogger<GdprRightToBeForgottenCascadeService>? _logger = logger;
    private readonly string _hmacSigningKey = hmacSigningKey;

    public async Task<GdprCascadeResult> ExecuteRightToBeForgottenCascadeAsync(
        Guid tenantId,
        Guid contactId,
        string dataSubjectEmail,
        string requesterAgent,
        CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("TenantId must not be empty.", nameof(tenantId));
        if (contactId == Guid.Empty) throw new ArgumentException("ContactId must not be empty.", nameof(contactId));
        if (string.IsNullOrWhiteSpace(dataSubjectEmail)) throw new ArgumentException("DataSubjectEmail must not be empty.", nameof(dataSubjectEmail));

        var requestedAtUtc = DateTime.UtcNow;
        var emailLower = dataSubjectEmail.Trim().ToLowerInvariant();

        // Deterministic pseudonymization seed hash: SHA-256(contactId + email)
        var subjectHashBytes = SHA256.HashData(Encoding.UTF8.GetBytes($"{contactId:D}:{emailLower}"));
        var subjectHash = Convert.ToHexString(subjectHashBytes)[..12].ToLowerInvariant();
        var pseudonymizedName = $"GDPR_REDACTED_{subjectHash}";
        var pseudonymizedEmail = $"gdpr-redacted-{subjectHash}@pseudonymized.internal";
        var emailHashString = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(emailLower))).ToLowerInvariant();

        _logger?.LogInformation("Starting GDPR Right-to-be-Forgotten Cascade for Contact {ContactId} (Tenant {TenantId}) by {Agent}",
            contactId, tenantId, requesterAgent);

        // 1. Contacts Pseudonymization
        var contacts = await _db.Contacts
            .Where(c => c.TenantId == tenantId && (c.Id == contactId || (c.Email != null && c.Email.ToLower() == emailLower)))
            .ToListAsync(cancellationToken);

        int contactsCount = contacts.Count;
        var targetedContactIds = contacts.Select(c => c.Id).ToHashSet();
        targetedContactIds.Add(contactId);

        foreach (var c in contacts)
        {
            c.FirstName = "GDPR_REDACTED";
            c.LastName = pseudonymizedName;
            c.Email = pseudonymizedEmail;
            c.Phone = null;
            c.Country = "[REDACTED]";
            c.CustomFieldsJson = "{}";
            c.Tags = ["gdpr-erased"];
            c.UpdatedAtUtc = DateTime.UtcNow;
        }

        // 2. Conversations Resolution and Detachment
        var conversations = await _db.Conversations
            .Where(c => c.TenantId == tenantId && targetedContactIds.Contains(c.ContactId))
            .ToListAsync(cancellationToken);

        int conversationsCount = conversations.Count;
        var convIds = conversations.Select(c => c.Id).ToHashSet();

        foreach (var conv in conversations)
        {
            conv.Status = ConversationStatus.Resolved;
            conv.UpdatedAtUtc = DateTime.UtcNow;
        }

        // 3. Messages Redaction
        var messages = await _db.Messages
            .Where(m => m.TenantId == tenantId && convIds.Contains(m.ConversationId))
            .ToListAsync(cancellationToken);

        int messagesCount = messages.Count;
        foreach (var msg in messages)
        {
            msg.Content = $"[GDPR_REDACTED_{subjectHash} - Content expunged pursuant to GDPR Art. 17 Right to Erasure]";
            msg.MediaUrl = null;
            msg.ExternalMessageId = null;
        }

        // 4. Dynamic Forms Submissions Redaction
        var formSubmissions = await _db.DynamicFormSubmissions
            .Where(f => f.TenantId == tenantId &&
                        (targetedContactIds.Contains(f.RespondentContactId ?? Guid.Empty) ||
                         f.AnswersJson.Contains(emailLower)))
            .ToListAsync(cancellationToken);

        int formSubmissionsCount = formSubmissions.Count;
        foreach (var submission in formSubmissions)
        {
            submission.RespondentContactId = null;
            submission.RespondentIp = "0.0.0.0";
            submission.AnswersJson = JsonSerializer.Serialize(new
            {
                status = "PSEUDONYMIZED",
                redactedToken = pseudonymizedName,
                expungedAtUtc = DateTime.UtcNow
            });
        }

        // 5. Survey Responses Redaction (csat, feedback, micro-surveys)
        var surveyResponses = await _db.SurveyResponses
            .Where(s => s.TenantId == tenantId && targetedContactIds.Contains(s.ContactId))
            .ToListAsync(cancellationToken);

        int surveyResponsesCount = surveyResponses.Count;
        foreach (var sr in surveyResponses)
        {
            try
            {
                _db.Entry(sr).Property("FeedbackText").CurrentValue = $"[GDPR_REDACTED_{subjectHash}]";
            }
            catch
            {
                // In-memory or fallback reflection
                typeof(SurveyResponse).GetProperty(nameof(SurveyResponse.FeedbackText))?
                    .SetValue(sr, $"[GDPR_REDACTED_{subjectHash}]");
            }
        }

        var csatResponses = await _db.CsatSurveyResults
            .Where(cs => cs.TenantId == tenantId && targetedContactIds.Contains(cs.CustomerContactId))
            .ToListAsync(cancellationToken);

        foreach (var cs in csatResponses)
        {
            cs.CustomerName = pseudonymizedName;
            cs.FeedbackText = $"[GDPR_REDACTED_{subjectHash}]";
            surveyResponsesCount++;
        }

        // 6. Voice Call Sessions & Transcripts Expungement
        var voiceSessions = await _db.VoiceCallSessions
            .Where(v => v.TenantId == tenantId && targetedContactIds.Contains(v.ContactId))
            .ToListAsync(cancellationToken);

        int voiceTranscriptsCount = voiceSessions.Count;
        foreach (var vs in voiceSessions)
        {
            vs.Transcript = $"[GDPR_REDACTED_{subjectHash} - Voice recording and transcript expunged pursuant to GDPR Art. 17]";
            vs.RecordingUrl = null;
            vs.FromNumber = "[REDACTED]";
            vs.ToNumber = "[REDACTED]";
        }

        // 7. Electronic Signatures and Contracts Redaction
        int electronicSignaturesCount = 0;
        var digitalContracts = await _db.DigitalContracts
            .Where(dc => dc.TenantId == tenantId && (dc.SignerEmail.ToLower() == emailLower || dc.SignerName.Contains(pseudonymizedName)))
            .ToListAsync(cancellationToken);

        foreach (var dc in digitalContracts)
        {
            dc.SignerName = pseudonymizedName;
            dc.SignerEmail = pseudonymizedEmail;
            dc.SignedPdfUrl = null;
            electronicSignaturesCount++;
        }

        var envelopeSigners = await _db.EnvelopeSigners
            .Where(es => es.TenantId == tenantId && es.Email.ToLower() == emailLower)
            .ToListAsync(cancellationToken);

        foreach (var es in envelopeSigners)
        {
            es.Name = pseudonymizedName;
            es.Email = pseudonymizedEmail;
            es.SignatureData = "[GDPR_REDACTED]";
            es.IpAddress = "0.0.0.0";
            electronicSignaturesCount++;
        }

        // 8. Statutory Financial Accounting Ledgers Preservation
        // Under GDPR Art 17(3)(b), statutory financial accounting ledgers (Invoices, Tax ledgers, Courier Remittances)
        // MUST NOT be deleted to comply with statutory fiscal accounting requirements, but customer PII is expunged.
        var invoices = await _db.Invoices
            .Where(i => i.TenantId == tenantId && (targetedContactIds.Contains(i.ContactId ?? Guid.Empty) || (i.ClientEmail != null && i.ClientEmail.ToLower() == emailLower)))
            .ToListAsync(cancellationToken);

        int financialLedgersCount = invoices.Count;
        foreach (var inv in invoices)
        {
            inv.ClientName = pseudonymizedName;
            inv.ClientEmail = $"gdpr-redacted-{subjectHash}@statutory.internal";
            inv.Notes = "[Customer PII redacted pursuant to GDPR Art. 17; financial ledger retained under Art. 17(3)(b) statutory accounting requirements]";
            inv.ContactId = null; // Detach foreign key to erased contact
            inv.UpdatedAtUtc = DateTime.UtcNow;
        }

        // 9. DocumentChunks / Vector RAG clean-up
        var ragChunks = await _db.DocumentChunks
            .Where(ch => ch.TenantId == tenantId && ch.Content.Contains(emailLower))
            .ToListAsync(cancellationToken);

        if (ragChunks.Count > 0)
        {
            _db.DocumentChunks.RemoveRange(ragChunks);
        }

        // 10. Audit Record in PrivacyErasureRequests table
        var erasureAudit = PrivacyErasureRequest.Create(tenantId, pseudonymizedEmail);
        erasureAudit.Status = "Completed";
        erasureAudit.CompletedAtUtc = DateTime.UtcNow;
        erasureAudit.ErasedRecordsCount = contactsCount + conversationsCount + messagesCount +
                                          formSubmissionsCount + surveyResponsesCount +
                                          voiceTranscriptsCount + electronicSignaturesCount +
                                          financialLedgersCount;
        await _db.PrivacyErasureRequests.AddAsync(erasureAudit, cancellationToken);

        await _db.SaveChangesAsync(cancellationToken);

        var completedAtUtc = DateTime.UtcNow;
        var certificateId = Guid.NewGuid();

        // Compute Cryptographic Proof of Erasure (SHA-256)
        var proofRaw = $"{certificateId:D}|{tenantId:D}|{contactId:D}|{emailHashString}|{completedAtUtc:O}|" +
                       $"{contactsCount}|{conversationsCount}|{messagesCount}|{formSubmissionsCount}|" +
                       $"{surveyResponsesCount}|{voiceTranscriptsCount}|{electronicSignaturesCount}|{financialLedgersCount}";

        var proofSha256 = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(proofRaw))).ToLowerInvariant();

        // Compute HMAC-SHA256 non-repudiation signature
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_hmacSigningKey));
        var signatureBytes = hmac.ComputeHash(Encoding.UTF8.GetBytes($"{proofSha256}|{requesterAgent}|{certificateId:D}"));
        var verificationSignature = Convert.ToHexString(signatureBytes).ToLowerInvariant();

        var certificate = new GdprErasureCertificateRecord(
            CertificateId: certificateId,
            TenantId: tenantId,
            ContactId: contactId,
            DataSubjectEmailHash: emailHashString,
            RequesterAgent: requesterAgent,
            RequestedAtUtc: requestedAtUtc,
            CompletedAtUtc: completedAtUtc,
            ContactsPseudonymized: contactsCount,
            ConversationsPseudonymized: conversationsCount,
            MessagesPseudonymized: messagesCount,
            FormSubmissionsPseudonymized: formSubmissionsCount,
            SurveyResponsesPseudonymized: surveyResponsesCount,
            VoiceTranscriptsPseudonymized: voiceTranscriptsCount,
            ElectronicSignaturesPseudonymized: electronicSignaturesCount,
            FinancialLedgersPreservedAndStripped: financialLedgersCount,
            ProofOfErasureSha256: proofSha256,
            VerificationSignature: verificationSignature,
            Status: "CERTIFIED_AND_PSEUDONYMIZED"
        );

        // Record in Immutable Merkle Compliance Ledger
        if (_auditLedger != null)
        {
            await _auditLedger.AppendAuditBlockAsync(
                tenantId: tenantId,
                actor: requesterAgent,
                action: "GDPR_RIGHT_TO_BE_FORGOTTEN_CASCADE",
                resource: $"Contact/{contactId}",
                metadataJson: JsonSerializer.Serialize(new
                {
                    certificateId,
                    emailHash = emailHashString,
                    totalRecordsImpacted = erasureAudit.ErasedRecordsCount,
                    proofSha256
                }),
                timestampUtc: completedAtUtc);
        }

        _logger?.LogInformation("GDPR Erasure Cascade successfully certified. Certificate: {CertificateId}, Proof: {Proof}",
            certificateId, proofSha256);

        return new GdprCascadeResult(
            Success: true,
            Certificate: certificate,
            Message: $"GDPR Right-to-be-Forgotten cascade completed successfully. {erasureAudit.ErasedRecordsCount} entity instances pseudonymized/preserved."
        );
    }
}
