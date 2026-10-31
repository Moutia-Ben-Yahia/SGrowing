using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace SGrowing.Api.Modules.Documents.Security;

#region Enums

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PiiCategory
{
    TaxId,
    NationalId,
    CreditCard,
    BankAccountRib,
    PhoneNumber,
    EmailAddress
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum SecurityClassification
{
    Public,
    Internal,
    Confidential,
    RestrictedSecret
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum AccessPermission
{
    ViewOnly,
    DownloadAllowed,
    PrintAllowed,
    Revoked
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum RedactionMaskStyle
{
    Asterisks,      // ************1234
    BlackoutBox,    // [REDACTED:NATIONAL_ID]
    HashMask,       // ############
    CategoryLabel   // [CONFIDENTIAL PII]
}

#endregion

#region Models & DTOs

public record RedactedSpan(
    PiiCategory Category,
    string MaskedValue,
    int StartIndex,
    int Length
);

public record DocumentRedactionResult(
    Guid DocumentId,
    Guid TenantId,
    string RedactedText,
    int DetectedSpansCount,
    List<RedactedSpan> Spans,
    DateTime RedactedAtUtc
);

public record ForensicWatermarkConfig(
    string ViewerIdentity,
    string ViewerIp,
    SecurityClassification Classification = SecurityClassification.Confidential,
    string? CustomWatermarkText = null,
    double Opacity = 0.25,
    int DiagonalAngleDegrees = -45
);

public record WatermarkedPayloadResult(
    Guid DocumentId,
    string WatermarkOverlaySvg,
    string FormattedFooter,
    string ForensicFingerprintSha256,
    DateTime GeneratedAtUtc
);

public record EphemeralShareLink
{
    public Guid LinkId { get; init; } = Guid.NewGuid();
    public Guid TenantId { get; init; }
    public Guid DocumentId { get; init; }
    public string Token { get; init; } = string.Empty;
    public string RecipientEmail { get; init; } = string.Empty;
    public AccessPermission Permission { get; set; } = AccessPermission.ViewOnly;
    public int MaxViews { get; init; } = 1;
    public int ViewCount { get; set; }
    public DateTime ExpiresAtUtc { get; init; }
    public bool IsBurned { get; set; }
    public DateTime CreatedAtUtc { get; init; } = DateTime.UtcNow;
}

public record DocumentAccessAuditEntry(
    Guid AuditId,
    Guid TenantId,
    Guid DocumentId,
    Guid? LinkId,
    string ViewerIdentity,
    string ViewerIp,
    string UserAgent,
    string Action,
    int DwellSeconds,
    DateTime AccessedAtUtc
);

public record RedactPiiRequest(
    Guid TenantId,
    Guid DocumentId,
    string ContentText,
    RedactionMaskStyle MaskStyle = RedactionMaskStyle.BlackoutBox,
    List<PiiCategory>? TargetCategories = null
);

public record CreateShareLinkRequest(
    Guid TenantId,
    Guid DocumentId,
    string RecipientEmail,
    AccessPermission Permission = AccessPermission.ViewOnly,
    int MaxViews = 1,
    int ExpirationMinutes = 60
);

public record AccessDocumentRequest(
    Guid TenantId,
    string Token,
    string ViewerIp,
    string UserAgent,
    int DwellSeconds = 0
);

public record DocumentSecurityMetrics(
    Guid TenantId,
    int TotalRedactionsPerformed,
    int TotalActiveShareLinks,
    int TotalBurnedLinks,
    int TotalAccessAuditEvents
);

#endregion

public interface IDocumentSecurityService
{
    Task<DocumentRedactionResult> RedactPiiAsync(RedactPiiRequest request, CancellationToken ct = default);
    Task<WatermarkedPayloadResult> GenerateWatermarkedPayloadAsync(Guid tenantId, Guid documentId, ForensicWatermarkConfig config, CancellationToken ct = default);

    Task<EphemeralShareLink> CreateEphemeralShareLinkAsync(CreateShareLinkRequest request, CancellationToken ct = default);
    Task<EphemeralShareLink> AccessDocumentViaLinkAsync(AccessDocumentRequest request, CancellationToken ct = default);
    Task<bool> RevokeShareLinkAsync(Guid tenantId, Guid linkId, CancellationToken ct = default);
    Task<List<EphemeralShareLink>> GetActiveShareLinksAsync(Guid tenantId, Guid documentId, CancellationToken ct = default);

    Task<List<DocumentAccessAuditEntry>> GetDocumentAuditLedgerAsync(Guid tenantId, Guid documentId, CancellationToken ct = default);
    Task<DocumentSecurityMetrics> GetMetricsAsync(Guid tenantId, CancellationToken ct = default);
}

public class DocumentSecurityService : IDocumentSecurityService
{
    private readonly ConcurrentDictionary<Guid, EphemeralShareLink> _shareLinks = new();
    private readonly ConcurrentDictionary<string, Guid> _tokenIndex = new();
    private readonly ConcurrentDictionary<Guid, List<DocumentAccessAuditEntry>> _auditLedger = new();
    private int _redactionCount = 0;

    // Compiled Regex Patterns for Common PII in Enterprise Documents
    private static readonly Regex CreditCardRegex = new(@"\b(?:\d{4}[ -]?){3}\d{4}\b", RegexOptions.Compiled);
    private static readonly Regex NationalIdRegex = new(@"\b(?:CIN|ID)?[: ]?([0-9]{8})\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex BankRibRegex = new(@"\b(?:RIB|IBAN)?[: ]?([0-9]{20,24})\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex TaxIdRegex = new(@"\b(?:MF|TVA|TAX)?[: ]?([0-9]{7}[A-Z]/\w/\w/\w{3})\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex PhoneRegex = new(@"\b(?:\+?[0-9]{1,3}[- ]?)?\(?[0-9]{2,4}\)?[- ]?[0-9]{3,4}[- ]?[0-9]{3,4}\b", RegexOptions.Compiled);
    private static readonly Regex EmailRegex = new(@"\b[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Z|a-z]{2,7}\b", RegexOptions.Compiled);

    public Task<DocumentRedactionResult> RedactPiiAsync(RedactPiiRequest request, CancellationToken ct = default)
    {
        if (request.TenantId == Guid.Empty) throw new ArgumentException("TenantId is required.", nameof(request));
        if (string.IsNullOrEmpty(request.ContentText))
        {
            return Task.FromResult(new DocumentRedactionResult(request.DocumentId, request.TenantId, string.Empty, 0, new List<RedactedSpan>(), DateTime.UtcNow));
        }

        var text = request.ContentText;
        var categories = request.TargetCategories ?? Enum.GetValues<PiiCategory>().ToList();
        var spans = new List<RedactedSpan>();

        // 1. Credit Cards
        if (categories.Contains(PiiCategory.CreditCard))
        {
            text = RedactMatches(text, CreditCardRegex, PiiCategory.CreditCard, request.MaskStyle, spans);
        }

        // 2. Bank Accounts
        if (categories.Contains(PiiCategory.BankAccountRib))
        {
            text = RedactMatches(text, BankRibRegex, PiiCategory.BankAccountRib, request.MaskStyle, spans);
        }

        // 3. Tax IDs
        if (categories.Contains(PiiCategory.TaxId))
        {
            text = RedactMatches(text, TaxIdRegex, PiiCategory.TaxId, request.MaskStyle, spans);
        }

        // 4. National IDs
        if (categories.Contains(PiiCategory.NationalId))
        {
            text = RedactMatches(text, NationalIdRegex, PiiCategory.NationalId, request.MaskStyle, spans);
        }

        // 5. Emails
        if (categories.Contains(PiiCategory.EmailAddress))
        {
            text = RedactMatches(text, EmailRegex, PiiCategory.EmailAddress, request.MaskStyle, spans);
        }

        // 6. Phones
        if (categories.Contains(PiiCategory.PhoneNumber))
        {
            text = RedactMatches(text, PhoneRegex, PiiCategory.PhoneNumber, request.MaskStyle, spans);
        }

        Interlocked.Increment(ref _redactionCount);

        return Task.FromResult(new DocumentRedactionResult(
            request.DocumentId,
            request.TenantId,
            text,
            spans.Count,
            spans,
            DateTime.UtcNow
        ));
    }

    private string RedactMatches(string source, Regex regex, PiiCategory category, RedactionMaskStyle style, List<RedactedSpan> spans)
    {
        return regex.Replace(source, match =>
        {
            string masked = style switch
            {
                RedactionMaskStyle.Asterisks => match.Value.Length > 4
                    ? new string('*', match.Value.Length - 4) + match.Value[^4..]
                    : new string('*', match.Value.Length),
                RedactionMaskStyle.BlackoutBox => $"[REDACTED:{category.ToString().ToUpperInvariant()}]",
                RedactionMaskStyle.HashMask => new string('#', match.Value.Length),
                RedactionMaskStyle.CategoryLabel => "[RESTRICTED PII]",
                _ => "[REDACTED]"
            };

            spans.Add(new RedactedSpan(category, masked, match.Index, match.Length));
            return masked;
        });
    }

    public Task<WatermarkedPayloadResult> GenerateWatermarkedPayloadAsync(Guid tenantId, Guid documentId, ForensicWatermarkConfig config, CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("TenantId is required.", nameof(tenantId));

        var now = DateTime.UtcNow;
        var watermarkText = config.CustomWatermarkText ?? $"{config.Classification.ToString().ToUpperInvariant()} • {config.ViewerIdentity} • {config.ViewerIp} • {now:yyyy-MM-dd HH:mm:ss} UTC";

        // Generate forensic fingerprint SHA-256
        var rawData = $"{tenantId}:{documentId}:{config.ViewerIdentity}:{config.ViewerIp}:{now:O}";
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawData))).ToLowerInvariant();

        var svg = $@"<svg xmlns='http://www.w3.org/2000/svg' width='800' height='600' viewBox='0 0 800 600'>
  <style>
    .watermark-text {{ font-family: sans-serif; font-size: 20px; font-weight: bold; fill: rgba(239, 68, 68, {config.Opacity:F2}); text-anchor: middle; }}
  </style>
  <g transform='rotate({config.DiagonalAngleDegrees} 400 300)'>
    <text x='400' y='200' class='watermark-text'>{watermarkText}</text>
    <text x='400' y='300' class='watermark-text'>{watermarkText}</text>
    <text x='400' y='400' class='watermark-text'>{watermarkText}</text>
  </g>
</svg>";

        var footer = $"Classification: {config.Classification} | Accessed By: {config.ViewerIdentity} ({config.ViewerIp}) | Forensic Hash: {hash[..16]}";

        return Task.FromResult(new WatermarkedPayloadResult(
            documentId,
            svg,
            footer,
            hash,
            now
        ));
    }

    public Task<EphemeralShareLink> CreateEphemeralShareLinkAsync(CreateShareLinkRequest request, CancellationToken ct = default)
    {
        if (request.TenantId == Guid.Empty) throw new ArgumentException("TenantId is required.", nameof(request));
        if (request.DocumentId == Guid.Empty) throw new ArgumentException("DocumentId is required.", nameof(request));

        var tokenBytes = RandomNumberGenerator.GetBytes(24);
        var token = Convert.ToHexString(tokenBytes).ToLowerInvariant();

        var link = new EphemeralShareLink
        {
            TenantId = request.TenantId,
            DocumentId = request.DocumentId,
            Token = token,
            RecipientEmail = request.RecipientEmail?.Trim() ?? string.Empty,
            Permission = request.Permission,
            MaxViews = request.MaxViews > 0 ? request.MaxViews : 1,
            ViewCount = 0,
            ExpiresAtUtc = DateTime.UtcNow.AddMinutes(request.ExpirationMinutes > 0 ? request.ExpirationMinutes : 60),
            IsBurned = false
        };

        _shareLinks[link.LinkId] = link;
        _tokenIndex[token] = link.LinkId;

        return Task.FromResult(link);
    }

    public Task<EphemeralShareLink> AccessDocumentViaLinkAsync(AccessDocumentRequest request, CancellationToken ct = default)
    {
        if (!_tokenIndex.TryGetValue(request.Token, out var linkId) ||
            !_shareLinks.TryGetValue(linkId, out var link) ||
            link.TenantId != request.TenantId)
        {
            throw new KeyNotFoundException("Invalid or non-existent document access token.");
        }

        var now = DateTime.UtcNow;

        if (link.Permission == AccessPermission.Revoked)
        {
            throw new InvalidOperationException("Access to this document has been revoked by the administrator.");
        }

        if (link.IsBurned)
        {
            throw new InvalidOperationException("This secure document link has already been burned after its maximum views.");
        }

        if (now > link.ExpiresAtUtc)
        {
            link.IsBurned = true;
            throw new InvalidOperationException("This secure document link has expired.");
        }

        link.ViewCount++;
        if (link.ViewCount >= link.MaxViews)
        {
            link.IsBurned = true;
        }

        // Record audit
        var audit = new DocumentAccessAuditEntry(
            Guid.NewGuid(),
            request.TenantId,
            link.DocumentId,
            link.LinkId,
            link.RecipientEmail,
            request.ViewerIp,
            request.UserAgent,
            link.IsBurned ? "ViewedAndBurned" : "Viewed",
            request.DwellSeconds,
            now
        );

        _auditLedger.AddOrUpdate(
            link.DocumentId,
            _ => new List<DocumentAccessAuditEntry> { audit },
            (_, list) => { lock (list) { list.Add(audit); } return list; }
        );

        return Task.FromResult(link);
    }

    public Task<bool> RevokeShareLinkAsync(Guid tenantId, Guid linkId, CancellationToken ct = default)
    {
        if (_shareLinks.TryGetValue(linkId, out var link) && link.TenantId == tenantId)
        {
            link.Permission = AccessPermission.Revoked;
            link.IsBurned = true;
            return Task.FromResult(true);
        }
        return Task.FromResult(false);
    }

    public Task<List<EphemeralShareLink>> GetActiveShareLinksAsync(Guid tenantId, Guid documentId, CancellationToken ct = default)
    {
        var list = _shareLinks.Values
            .Where(l => l.TenantId == tenantId && l.DocumentId == documentId && !l.IsBurned && l.ExpiresAtUtc > DateTime.UtcNow)
            .OrderByDescending(l => l.CreatedAtUtc)
            .ToList();

        return Task.FromResult(list);
    }

    public Task<List<DocumentAccessAuditEntry>> GetDocumentAuditLedgerAsync(Guid tenantId, Guid documentId, CancellationToken ct = default)
    {
        if (_auditLedger.TryGetValue(documentId, out var list))
        {
            lock (list)
            {
                return Task.FromResult(list.Where(a => a.TenantId == tenantId).OrderByDescending(a => a.AccessedAtUtc).ToList());
            }
        }
        return Task.FromResult(new List<DocumentAccessAuditEntry>());
    }

    public Task<DocumentSecurityMetrics> GetMetricsAsync(Guid tenantId, CancellationToken ct = default)
    {
        var links = _shareLinks.Values.Where(l => l.TenantId == tenantId).ToList();
        var active = links.Count(l => !l.IsBurned && l.ExpiresAtUtc > DateTime.UtcNow);
        var burned = links.Count(l => l.IsBurned || l.ExpiresAtUtc <= DateTime.UtcNow);

        int auditCount = _auditLedger.Values.SelectMany(l => l).Count(a => a.TenantId == tenantId);

        return Task.FromResult(new DocumentSecurityMetrics(
            tenantId,
            _redactionCount,
            active,
            burned,
            auditCount
        ));
    }
}
