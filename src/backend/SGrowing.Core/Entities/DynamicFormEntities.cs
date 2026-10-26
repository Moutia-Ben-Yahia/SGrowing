using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace SGrowing.Core.Entities;

public class DynamicFormField
{
    public string FieldKey { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string FieldType { get; set; } = "text"; // text, email, phone, select, rating, textarea, file
    public bool IsRequired { get; set; } = false;
    public List<string> Options { get; set; } = new();
    public string? ConditionalParentKey { get; set; }
    public string? ConditionalExpectedValue { get; set; }
}

public class DynamicFormDefinition
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string FieldsSchemaJson { get; set; } = "[]";
    public string SubmitAction { get; set; } = "ThankYou"; // ThankYou, Redirect, CreateLead, TriggerWebhook
    public string ActionPayload { get; set; } = "Thank you for your response!";
    public int SubmissionCount { get; set; } = 0;
    public bool IsPublished { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public static DynamicFormDefinition Create(
        Guid tenantId,
        string title,
        string slug,
        string description,
        List<DynamicFormField> fields,
        string submitAction = "ThankYou",
        string actionPayload = "Thank you for your submission!")
    {
        return new DynamicFormDefinition
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Title = title.Trim(),
            Slug = slug.Trim().ToLowerInvariant(),
            Description = description.Trim(),
            FieldsSchemaJson = JsonSerializer.Serialize(fields),
            SubmitAction = submitAction,
            ActionPayload = actionPayload,
            SubmissionCount = 0,
            IsPublished = true,
            CreatedAtUtc = DateTime.UtcNow
        };
    }
}

public class DynamicFormSubmission
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid FormId { get; set; }
    public Guid? RespondentContactId { get; set; }
    public string RespondentIp { get; set; } = "127.0.0.1";
    public string AnswersJson { get; set; } = "{}";
    public int CompletedInSeconds { get; set; } = 30;
    public DateTime SubmittedAtUtc { get; set; } = DateTime.UtcNow;

    public static DynamicFormSubmission Create(
        Guid tenantId,
        Guid formId,
        Dictionary<string, string> answers,
        Guid? respondentContactId = null,
        string respondentIp = "127.0.0.1",
        int completedInSeconds = 30)
    {
        return new DynamicFormSubmission
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            FormId = formId,
            RespondentContactId = respondentContactId,
            RespondentIp = respondentIp,
            AnswersJson = JsonSerializer.Serialize(answers),
            CompletedInSeconds = completedInSeconds,
            SubmittedAtUtc = DateTime.UtcNow
        };
    }
}

public class CryptographicPdfRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public string DocumentTitle { get; set; } = string.Empty;
    public string DocumentContent { get; set; } = string.Empty;
    public string OriginalSha256 { get; set; } = string.Empty;
    public string SealedSha256 { get; set; } = string.Empty;
    public string SignaturePlacementJson { get; set; } = "[]"; // List of {signerEmail, pageNumber, x, y}
    public string AuditTrailHashChainJson { get; set; } = "[]";
    public string Status { get; set; } = "Draft"; // Draft, PendingSignatures, Sealed
    public DateTime? SealedAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public static CryptographicPdfRecord Create(
        Guid tenantId,
        string title,
        string content,
        string signaturePlacements = "[]")
    {
        var contentBytes = Encoding.UTF8.GetBytes(content);
        var initialHash = Convert.ToHexString(SHA256.HashData(contentBytes));

        var initialAuditTrail = new List<string>
        {
            $"{DateTime.UtcNow:O}|CREATED|Original SHA-256: {initialHash}"
        };

        return new CryptographicPdfRecord
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            DocumentTitle = title.Trim(),
            DocumentContent = content,
            OriginalSha256 = initialHash,
            SealedSha256 = initialHash,
            SignaturePlacementJson = signaturePlacements,
            AuditTrailHashChainJson = JsonSerializer.Serialize(initialAuditTrail),
            Status = "Draft",
            CreatedAtUtc = DateTime.UtcNow
        };
    }
}
