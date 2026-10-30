using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace SGrowing.Api.Modules.Documents;

public enum DocumentCategory
{
    SalesProposal,
    WholesaleContract,
    PricingDeck,
    PitchDeck
}

public enum DocumentPageType
{
    Cover,
    ExecutiveSummary,
    PricingPlan,
    LegalTerms,
    TechnicalSpecs
}

public enum DocumentDeviceType
{
    Desktop,
    Mobile,
    Tablet
}

public enum BuyingIntentTier
{
    Hot,
    Warm,
    Casual,
    Cold
}

public record TrackedDocumentPage(
    int PageNumber,
    string PageTitle,
    DocumentPageType PageType,
    string Summary
);

public record TrackedDocument(
    Guid Id,
    Guid TenantId,
    string Title,
    int TotalPages,
    string OwnerEmail,
    DateTimeOffset CreatedAt,
    long FileSizeKb,
    string Category,
    List<TrackedDocumentPage> Pages
);

public record DocumentShareLink(
    Guid Id,
    Guid TenantId,
    Guid DocumentId,
    string Token,
    string RecipientEmail,
    string RecipientCompany,
    bool AllowDownload,
    DateTimeOffset? ExpiresAt,
    string? PasscodeHash,
    bool IsActive,
    DateTimeOffset CreatedAt
);

public record PageViewTelemetry(
    Guid Id,
    Guid SessionId,
    Guid LinkId,
    int PageNumber,
    long DwellTimeMs,
    decimal ScrollDepthPct,
    bool CompletedView,
    DateTimeOffset Timestamp
);

public record DocumentSession(
    Guid SessionId,
    Guid LinkId,
    Guid DocumentId,
    string VisitorIp,
    string UserAgent,
    string DeviceType,
    string Country,
    DateTimeOffset StartedAt,
    DateTimeOffset LastHeartbeatAt,
    long TotalDwellTimeMs,
    int PagesViewedCount,
    decimal IntentScore,
    string IntentTier
);

public record PageHeatmapSummary(
    int PageNumber,
    string PageTitle,
    string PageType,
    long AverageDwellTimeMs,
    int TotalViews,
    decimal DropoffRatePct,
    decimal HighInterestHeatPct
);

public record HotLeadAlert(
    Guid Id,
    Guid TenantId,
    Guid DocumentId,
    string DocumentTitle,
    string RecipientEmail,
    string RecipientCompany,
    string TriggerReason,
    decimal IntentScore,
    DateTimeOffset TriggeredAt
);

public record CreateShareLinkRequest(
    Guid DocumentId,
    string RecipientEmail,
    string RecipientCompany,
    bool AllowDownload = true,
    string? Passcode = null,
    DateTimeOffset? ExpiresAt = null,
    Guid? TenantId = null
);

public record ShareLinkMetaDto(
    string Token,
    Guid DocumentId,
    string DocumentTitle,
    int TotalPages,
    string RecipientCompany,
    string RecipientEmail,
    bool RequiresPasscode,
    bool AllowDownload,
    bool IsActive,
    bool IsExpired,
    DateTimeOffset? ExpiresAt
);

public record HeartbeatPingRequest(
    string Token,
    Guid SessionId,
    int PageNumber,
    long DwellTimeIncrementMs,
    decimal ScrollDepthPct,
    string? VisitorIp = null,
    string? UserAgent = null,
    string? DeviceType = null,
    string? Country = null,
    string? Passcode = null
);

public record HeartbeatResultDto(
    Guid SessionId,
    int PageNumber,
    long PageDwellTimeMs,
    long TotalDwellTimeMs,
    decimal IntentScore,
    string IntentTier,
    bool IsHotLead,
    bool AlertTriggered,
    string? TriggerReason
);

public record DocumentHeatmapResponse(
    Guid DocumentId,
    string Title,
    int TotalPages,
    int TotalSessions,
    long AverageDwellTimeMs,
    IReadOnlyList<PageHeatmapSummary> Pages
);

public record TelemetrySummaryDto(
    int TotalTrackedLinks,
    int TotalViews,
    decimal AvgCompletionPct,
    int ActiveHotLeads,
    IReadOnlyList<HotLeadAlert> RecentAlerts
);

public interface IDocumentTelemetryHeatmapService
{
    Task<IReadOnlyList<TrackedDocument>> GetTrackedDocumentsAsync(Guid? tenantId = null);
    Task<TrackedDocument?> GetDocumentByIdAsync(Guid documentId);
    Task<DocumentShareLink> CreateShareLinkAsync(CreateShareLinkRequest request);
    Task<ShareLinkMetaDto> GetShareLinkMetaAsync(string token);
    Task<HeartbeatResultDto> RecordHeartbeatAsync(HeartbeatPingRequest request);
    Task<DocumentHeatmapResponse> GetDocumentHeatmapAsync(Guid documentId);
    Task<IReadOnlyList<DocumentSession>> GetDocumentSessionsAsync(Guid documentId);
    Task<IReadOnlyList<HotLeadAlert>> GetHotLeadAlertsAsync(Guid? tenantId = null);
    Task<TelemetrySummaryDto> GetTelemetrySummaryAsync(Guid? tenantId = null);
    Task<bool> RevokeShareLinkAsync(string token);
}

public class DocumentTelemetryHeatmapService : IDocumentTelemetryHeatmapService
{
    public const long BenchmarkTimeMs = 30000;
    public const long PricingAlertDwellMs = 45000;
    public const decimal HotLeadScoreThreshold = 75.0m;

    private static readonly ConcurrentDictionary<Guid, TrackedDocument> _documents = new();
    private static readonly ConcurrentDictionary<string, DocumentShareLink> _linksByToken = new();
    private static readonly ConcurrentDictionary<Guid, DocumentSession> _sessions = new();
    private static readonly ConcurrentDictionary<(Guid SessionId, int PageNumber), PageViewTelemetry> _pageTelemetry = new();
    private static readonly ConcurrentDictionary<Guid, HotLeadAlert> _alerts = new();

    private static readonly object _initLock = new();
    private static bool _initialized = false;

    public DocumentTelemetryHeatmapService()
    {
        EnsurePreSeededData();
    }

    public static string GenerateSecureToken()
    {
        // 18 bytes * 8 = 144 bits -> 24 Base64 characters without padding
        var bytes = RandomNumberGenerator.GetBytes(18);
        var base64 = Convert.ToBase64String(bytes)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
        return "doc_view_" + base64;
    }

    public static bool ValidateTokenFormat(string token)
    {
        if (string.IsNullOrWhiteSpace(token)) return false;
        return Regex.IsMatch(token, @"^doc_view_[A-Za-z0-9_-]{24}$");
    }

    public static string HashPasscode(string passcode)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(passcode.Trim()));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    public static bool VerifyPasscode(string? inputPasscode, string? storedHash)
    {
        if (string.IsNullOrEmpty(storedHash)) return true;
        if (string.IsNullOrWhiteSpace(inputPasscode)) return false;
        var computed = HashPasscode(inputPasscode);
        return string.Equals(computed, storedHash, StringComparison.OrdinalIgnoreCase);
    }

    public static decimal CalculateIntentScore(
        IEnumerable<PageViewTelemetry> pageViews, 
        IReadOnlyDictionary<int, DocumentPageType> pageTypes)
    {
        decimal totalScore = 0.0m;

        var grouped = pageViews
            .GroupBy(p => p.PageNumber)
            .ToList();

        foreach (var pageGroup in grouped)
        {
            var pageNumber = pageGroup.Key;
            var cumulativeDwellMs = pageGroup.Sum(p => p.DwellTimeMs);
            var maxScrollDepth = pageGroup.Max(p => p.ScrollDepthPct);

            var pageType = pageTypes.TryGetValue(pageNumber, out var type) ? type : DocumentPageType.TechnicalSpecs;
            decimal weight = pageType switch
            {
                DocumentPageType.PricingPlan => 3.5m,
                DocumentPageType.LegalTerms => 2.0m,
                DocumentPageType.ExecutiveSummary => 1.5m,
                _ => 1.0m
            };

            decimal dwellRatio = Math.Min(1.0m, (decimal)cumulativeDwellMs / BenchmarkTimeMs);
            decimal scrollRatio = Math.Min(1.0m, Math.Max(0.0m, maxScrollDepth / 100.0m));

            decimal contribution = weight * dwellRatio * scrollRatio * 15.0m;
            totalScore += contribution;
        }

        return Math.Min(100.0m, Math.Round(totalScore, 1));
    }

    public static BuyingIntentTier GetIntentTier(decimal intentScore) => intentScore switch
    {
        >= 75.0m => BuyingIntentTier.Hot,
        >= 50.0m => BuyingIntentTier.Warm,
        >= 25.0m => BuyingIntentTier.Casual,
        _ => BuyingIntentTier.Cold
    };

    public Task<IReadOnlyList<TrackedDocument>> GetTrackedDocumentsAsync(Guid? tenantId = null)
    {
        var docs = tenantId.HasValue
            ? _documents.Values.Where(d => d.TenantId == tenantId.Value).ToList()
            : _documents.Values.ToList();
        return Task.FromResult<IReadOnlyList<TrackedDocument>>(docs);
    }

    public Task<TrackedDocument?> GetDocumentByIdAsync(Guid documentId)
    {
        _documents.TryGetValue(documentId, out var doc);
        return Task.FromResult(doc);
    }

    public Task<DocumentShareLink> CreateShareLinkAsync(CreateShareLinkRequest request)
    {
        if (!_documents.TryGetValue(request.DocumentId, out var doc))
        {
            throw new KeyNotFoundException($"Tracked document with Id {request.DocumentId} was not found.");
        }

        var tenantId = request.TenantId ?? doc.TenantId;
        var token = GenerateSecureToken();
        var passcodeHash = !string.IsNullOrWhiteSpace(request.Passcode) ? HashPasscode(request.Passcode) : null;

        var link = new DocumentShareLink(
            Id: Guid.NewGuid(),
            TenantId: tenantId,
            DocumentId: doc.Id,
            Token: token,
            RecipientEmail: request.RecipientEmail.Trim().ToLowerInvariant(),
            RecipientCompany: request.RecipientCompany.Trim(),
            AllowDownload: request.AllowDownload,
            ExpiresAt: request.ExpiresAt,
            PasscodeHash: passcodeHash,
            IsActive: true,
            CreatedAt: DateTimeOffset.UtcNow
        );

        _linksByToken[token] = link;
        return Task.FromResult(link);
    }

    public Task<ShareLinkMetaDto> GetShareLinkMetaAsync(string token)
    {
        if (!_linksByToken.TryGetValue(token, out var link))
        {
            throw new KeyNotFoundException($"Share link token '{token}' was not found.");
        }

        if (!_documents.TryGetValue(link.DocumentId, out var doc))
        {
            throw new KeyNotFoundException($"Document referenced by share link was not found.");
        }

        var isExpired = link.ExpiresAt.HasValue && link.ExpiresAt.Value < DateTimeOffset.UtcNow;
        if (!link.IsActive)
        {
            throw new InvalidOperationException("The document share link has been revoked or deactivated.");
        }
        if (isExpired)
        {
            throw new InvalidOperationException("The document share link has expired.");
        }

        var meta = new ShareLinkMetaDto(
            Token: link.Token,
            DocumentId: doc.Id,
            DocumentTitle: doc.Title,
            TotalPages: doc.TotalPages,
            RecipientCompany: link.RecipientCompany,
            RecipientEmail: link.RecipientEmail,
            RequiresPasscode: !string.IsNullOrEmpty(link.PasscodeHash),
            AllowDownload: link.AllowDownload,
            IsActive: link.IsActive,
            IsExpired: isExpired,
            ExpiresAt: link.ExpiresAt
        );

        return Task.FromResult(meta);
    }

    public Task<HeartbeatResultDto> RecordHeartbeatAsync(HeartbeatPingRequest request)
    {
        if (!_linksByToken.TryGetValue(request.Token, out var link))
        {
            throw new KeyNotFoundException($"Share link token '{request.Token}' was not found.");
        }

        if (!link.IsActive)
        {
            throw new InvalidOperationException("The document share link has been deactivated.");
        }

        if (link.ExpiresAt.HasValue && link.ExpiresAt.Value < DateTimeOffset.UtcNow)
        {
            throw new InvalidOperationException("The document share link has expired.");
        }

        if (!string.IsNullOrEmpty(link.PasscodeHash) && !VerifyPasscode(request.Passcode, link.PasscodeHash))
        {
            throw new UnauthorizedAccessException("Invalid or missing passcode for protected document share link.");
        }

        if (!_documents.TryGetValue(link.DocumentId, out var doc))
        {
            throw new KeyNotFoundException("Document for this share link was not found.");
        }

        if (request.PageNumber < 1 || request.PageNumber > doc.TotalPages)
        {
            throw new ArgumentOutOfRangeException(nameof(request.PageNumber), $"Page number must be between 1 and {doc.TotalPages}.");
        }

        var now = DateTimeOffset.UtcNow;
        var sanitizedScroll = Math.Clamp(request.ScrollDepthPct, 0.0m, 100.0m);
        var dwellIncrement = Math.Max(0, request.DwellTimeIncrementMs);

        // Update or create page view telemetry
        var pageKey = (request.SessionId, request.PageNumber);
        var pageTelemetry = _pageTelemetry.AddOrUpdate(
            pageKey,
            key => new PageViewTelemetry(
                Id: Guid.NewGuid(),
                SessionId: request.SessionId,
                LinkId: link.Id,
                PageNumber: request.PageNumber,
                DwellTimeMs: dwellIncrement,
                ScrollDepthPct: sanitizedScroll,
                CompletedView: sanitizedScroll >= 80.0m && dwellIncrement >= 5000,
                Timestamp: now
            ),
            (key, existing) =>
            {
                var newDwell = existing.DwellTimeMs + dwellIncrement;
                var maxScroll = Math.Max(existing.ScrollDepthPct, sanitizedScroll);
                var completed = existing.CompletedView || (maxScroll >= 80.0m && newDwell >= 5000);
                return existing with
                {
                    DwellTimeMs = newDwell,
                    ScrollDepthPct = maxScroll,
                    CompletedView = completed,
                    Timestamp = now
                };
            }
        );

        // Map page types for document
        var pageTypes = doc.Pages.ToDictionary(p => p.PageNumber, p => p.PageType);

        // Get all pages viewed in this session
        var sessionPageViews = _pageTelemetry.Values
            .Where(p => p.SessionId == request.SessionId)
            .ToList();

        var intentScore = CalculateIntentScore(sessionPageViews, pageTypes);
        var intentTier = GetIntentTier(intentScore).ToString();

        // Update or create DocumentSession
        var session = _sessions.AddOrUpdate(
            request.SessionId,
            key => new DocumentSession(
                SessionId: request.SessionId,
                LinkId: link.Id,
                DocumentId: doc.Id,
                VisitorIp: request.VisitorIp ?? "127.0.0.1",
                UserAgent: request.UserAgent ?? "Unknown Browser",
                DeviceType: string.IsNullOrWhiteSpace(request.DeviceType) ? DocumentDeviceType.Desktop.ToString() : request.DeviceType,
                Country: request.Country ?? "US",
                StartedAt: now,
                LastHeartbeatAt: now,
                TotalDwellTimeMs: dwellIncrement,
                PagesViewedCount: sessionPageViews.Select(p => p.PageNumber).Distinct().Count(),
                IntentScore: intentScore,
                IntentTier: intentTier
            ),
            (key, existing) =>
            {
                return existing with
                {
                    LastHeartbeatAt = now,
                    TotalDwellTimeMs = existing.TotalDwellTimeMs + dwellIncrement,
                    PagesViewedCount = sessionPageViews.Select(p => p.PageNumber).Distinct().Count(),
                    IntentScore = intentScore,
                    IntentTier = intentTier,
                    DeviceType = !string.IsNullOrWhiteSpace(request.DeviceType) ? request.DeviceType : existing.DeviceType
                };
            }
        );

        // Hot Lead Trigger Machine
        // Condition 1: IntentScore >= 75
        // Condition 2: DwellTime on a PricingPlan page >= 45,000 ms
        var currentPageType = pageTypes.TryGetValue(request.PageNumber, out var pt) ? pt : DocumentPageType.TechnicalSpecs;
        bool isPricingThresholdHit = currentPageType == DocumentPageType.PricingPlan && pageTelemetry.DwellTimeMs >= PricingAlertDwellMs;
        bool isIntentThresholdHit = intentScore >= HotLeadScoreThreshold;

        bool alertTriggered = false;
        string? triggerReason = null;

        if (isPricingThresholdHit || isIntentThresholdHit)
        {
            if (isPricingThresholdHit && isIntentThresholdHit)
            {
                triggerReason = $"High Intent Score ({intentScore}/100) and Deep Pricing Engagement ({pageTelemetry.DwellTimeMs / 1000}s on Page {request.PageNumber})";
            }
            else if (isPricingThresholdHit)
            {
                triggerReason = $"Deep Pricing Engagement: Prospect spent {pageTelemetry.DwellTimeMs / 1000}s on Pricing Plan (Page {request.PageNumber})";
            }
            else
            {
                triggerReason = $"High Predictive Buying Intent ({intentScore}/100) exceeded Hot Tier threshold";
            }

            // Record alert if not already logged for this session and reason
            var alertId = Guid.NewGuid();
            var alert = new HotLeadAlert(
                Id: alertId,
                TenantId: link.TenantId,
                DocumentId: doc.Id,
                DocumentTitle: doc.Title,
                RecipientEmail: link.RecipientEmail,
                RecipientCompany: link.RecipientCompany,
                TriggerReason: triggerReason,
                IntentScore: intentScore,
                TriggeredAt: now
            );

            _alerts[alertId] = alert;
            alertTriggered = true;
        }

        var isHotLead = intentScore >= HotLeadScoreThreshold || isPricingThresholdHit;

        var result = new HeartbeatResultDto(
            SessionId: request.SessionId,
            PageNumber: request.PageNumber,
            PageDwellTimeMs: pageTelemetry.DwellTimeMs,
            TotalDwellTimeMs: session.TotalDwellTimeMs,
            IntentScore: intentScore,
            IntentTier: intentTier,
            IsHotLead: isHotLead,
            AlertTriggered: alertTriggered,
            TriggerReason: triggerReason
        );

        return Task.FromResult(result);
    }

    public Task<DocumentHeatmapResponse> GetDocumentHeatmapAsync(Guid documentId)
    {
        if (!_documents.TryGetValue(documentId, out var doc))
        {
            throw new KeyNotFoundException($"Tracked document {documentId} not found.");
        }

        var sessionsForDoc = _sessions.Values.Where(s => s.DocumentId == documentId).ToList();
        var sessionIds = sessionsForDoc.Select(s => s.SessionId).ToHashSet();
        var allPageViews = _pageTelemetry.Values.Where(p => sessionIds.Contains(p.SessionId)).ToList();

        var pageSummaries = new List<PageHeatmapSummary>();
        int previousPageViews = sessionsForDoc.Count;

        for (int pNum = 1; pNum <= doc.TotalPages; pNum++)
        {
            var pageInfo = doc.Pages.FirstOrDefault(p => p.PageNumber == pNum);
            var title = pageInfo?.PageTitle ?? $"Page {pNum}";
            var type = pageInfo?.PageType.ToString() ?? DocumentPageType.TechnicalSpecs.ToString();

            var viewsForPage = allPageViews.Where(p => p.PageNumber == pNum && p.DwellTimeMs > 0).ToList();
            int currentViews = viewsForPage.Select(p => p.SessionId).Distinct().Count();

            long avgDwell = currentViews > 0
                ? (long)viewsForPage.Average(p => p.DwellTimeMs)
                : 0;

            decimal dropoffPct = 0.0m;
            if (pNum == 1)
            {
                dropoffPct = 0.0m;
            }
            else
            {
                if (previousPageViews > 0)
                {
                    dropoffPct = Math.Max(0.0m, Math.Round((decimal)(previousPageViews - currentViews) / previousPageViews * 100.0m, 1));
                }
            }

            previousPageViews = Math.Max(1, currentViews);

            decimal heatPct = Math.Min(100.0m, Math.Round((decimal)avgDwell / BenchmarkTimeMs * 100.0m, 1));

            pageSummaries.Add(new PageHeatmapSummary(
                PageNumber: pNum,
                PageTitle: title,
                PageType: type,
                AverageDwellTimeMs: avgDwell,
                TotalViews: currentViews,
                DropoffRatePct: dropoffPct,
                HighInterestHeatPct: heatPct
            ));
        }

        long totalAvgDwell = sessionsForDoc.Count > 0
            ? (long)sessionsForDoc.Average(s => s.TotalDwellTimeMs)
            : 0;

        var response = new DocumentHeatmapResponse(
            DocumentId: doc.Id,
            Title: doc.Title,
            TotalPages: doc.TotalPages,
            TotalSessions: sessionsForDoc.Count,
            AverageDwellTimeMs: totalAvgDwell,
            Pages: pageSummaries
        );

        return Task.FromResult(response);
    }

    public Task<IReadOnlyList<DocumentSession>> GetDocumentSessionsAsync(Guid documentId)
    {
        var list = _sessions.Values
            .Where(s => s.DocumentId == documentId)
            .OrderByDescending(s => s.StartedAt)
            .ToList();
        return Task.FromResult<IReadOnlyList<DocumentSession>>(list);
    }

    public Task<IReadOnlyList<HotLeadAlert>> GetHotLeadAlertsAsync(Guid? tenantId = null)
    {
        var list = tenantId.HasValue
            ? _alerts.Values.Where(a => a.TenantId == tenantId.Value).OrderByDescending(a => a.TriggeredAt).ToList()
            : _alerts.Values.OrderByDescending(a => a.TriggeredAt).ToList();
        return Task.FromResult<IReadOnlyList<HotLeadAlert>>(list);
    }

    public Task<TelemetrySummaryDto> GetTelemetrySummaryAsync(Guid? tenantId = null)
    {
        var links = tenantId.HasValue
            ? _linksByToken.Values.Where(l => l.TenantId == tenantId.Value).ToList()
            : _linksByToken.Values.ToList();

        var sessions = tenantId.HasValue
            ? _sessions.Values.Where(s => links.Any(l => l.Id == s.LinkId)).ToList()
            : _sessions.Values.ToList();

        var alerts = tenantId.HasValue
            ? _alerts.Values.Where(a => a.TenantId == tenantId.Value).OrderByDescending(a => a.TriggeredAt).ToList()
            : _alerts.Values.OrderByDescending(a => a.TriggeredAt).ToList();

        int totalTrackedLinks = links.Count;
        int totalViews = sessions.Count;

        decimal avgCompletion = 0.0m;
        if (sessions.Count > 0)
        {
            var completionPcts = new List<decimal>();
            foreach (var s in sessions)
            {
                if (_documents.TryGetValue(s.DocumentId, out var doc) && doc.TotalPages > 0)
                {
                    completionPcts.Add(Math.Min(100.0m, Math.Round((decimal)s.PagesViewedCount / doc.TotalPages * 100.0m, 1)));
                }
            }
            if (completionPcts.Count > 0)
            {
                avgCompletion = Math.Round(completionPcts.Average(), 1);
            }
        }

        var hotSessionsCount = sessions.Count(s => s.IntentTier == BuyingIntentTier.Hot.ToString() || s.IntentScore >= HotLeadScoreThreshold);
        var activeHotLeads = Math.Max(alerts.Count, hotSessionsCount);

        var summary = new TelemetrySummaryDto(
            TotalTrackedLinks: totalTrackedLinks,
            TotalViews: totalViews,
            AvgCompletionPct: avgCompletion,
            ActiveHotLeads: activeHotLeads,
            RecentAlerts: alerts.Take(10).ToList()
        );

        return Task.FromResult(summary);
    }

    public Task<bool> RevokeShareLinkAsync(string token)
    {
        if (_linksByToken.TryGetValue(token, out var existing))
        {
            _linksByToken[token] = existing with { IsActive = false };
            return Task.FromResult(true);
        }
        return Task.FromResult(false);
    }

    private void EnsurePreSeededData()
    {
        if (_initialized) return;

        lock (_initLock)
        {
            if (_initialized) return;

            var defaultTenantId = Guid.Parse("11111111-2222-3333-4444-555555555555");

            // Document 1: SGrowing Enterprise SaaS Agreement - 10 Pages
            var doc1Id = Guid.Parse("a1111111-1111-1111-1111-111111111111");
            var doc1Pages = new List<TrackedDocumentPage>
            {
                new(1, "Enterprise Agreement Cover & Parties", DocumentPageType.Cover, "Overview of contracting entities and effective dates"),
                new(2, "Executive Summary & Platform Scope", DocumentPageType.ExecutiveSummary, "Unified Omnichannel CRM & Voice Agent subscription tiers"),
                new(3, "Platform Architecture & High-Availability SLA", DocumentPageType.TechnicalSpecs, "99.99% uptime guarantee, multi-region failover, latency metrics"),
                new(4, "Data Security, Encryption & ISO 27001", DocumentPageType.TechnicalSpecs, "End-to-end encryption, SOC2 Type II controls, zero-trust RBAC"),
                new(5, "Tiered Enterprise Licensing Matrix", DocumentPageType.PricingPlan, "Seat tiers, consumption credits, add-ons, annual commitment discount"),
                new(6, "Implementation, Migration & Dedicated CS Scope", DocumentPageType.PricingPlan, "Turnkey onboarding package, custom CRM sync, White-glove SLA"),
                new(7, "Master Services Agreement (MSA) Terms", DocumentPageType.LegalTerms, "Licensing rights, IP ownership, confidentiality covenants"),
                new(8, "Data Processing Addendum (DPA) & GDPR", DocumentPageType.LegalTerms, "Standard Contractual Clauses (SCCs), sub-processor disclosures"),
                new(9, "Limitation of Liability & Indemnification", DocumentPageType.LegalTerms, "Mutual indemnity clauses, insurance thresholds"),
                new(10, "Authorized Execution & Electronic Signatures", DocumentPageType.LegalTerms, "ESIGN & eIDAS digital signature execution blocks")
            };

            var doc1 = new TrackedDocument(
                Id: doc1Id,
                TenantId: defaultTenantId,
                Title: "SGrowing Enterprise SaaS Agreement - 10 Pages",
                TotalPages: 10,
                OwnerEmail: "sales-director@sgrowing.io",
                CreatedAt: DateTimeOffset.UtcNow.AddDays(-14),
                FileSizeKb: 2450,
                Category: DocumentCategory.SalesProposal.ToString(),
                Pages: doc1Pages
            );
            _documents[doc1Id] = doc1;

            // Document 2: Wholesale Fragrance Master Supply Proposal - 8 Pages
            var doc2Id = Guid.Parse("b2222222-2222-2222-2222-222222222222");
            var doc2Pages = new List<TrackedDocumentPage>
            {
                new(1, "Wholesale Master Supply Agreement Cover", DocumentPageType.Cover, "Primary supplier identification & distribution schedule"),
                new(2, "Volume Commitments & Territory Allocation", DocumentPageType.ExecutiveSummary, "Annual minimum order volume & exclusive geographical rights"),
                new(3, "Formula Specifications & Quality Controls", DocumentPageType.TechnicalSpecs, "IFRA compliance, raw batch testing, certificate of analysis"),
                new(4, "Tiered Wholesale Unit Pricing & Discounts", DocumentPageType.PricingPlan, "Per-unit cost schedule across order volume tiers"),
                new(5, "Payment Schedule & Letter of Credit Terms", DocumentPageType.PricingPlan, "Net 30/60 terms, revolving standby letter of credit"),
                new(6, "Incoterms 2020, Shipping & Risk Transfer", DocumentPageType.LegalTerms, "DAP/FOB freight terms, customs clearance, loss provisions"),
                new(7, "Inspection, Product Warranties & Defect Claims", DocumentPageType.LegalTerms, "Acceptance window, replacement protocols, batch rejection"),
                new(8, "Execution & Counterparts Execution", DocumentPageType.LegalTerms, "Authorized signatory authorization blocks")
            };

            var doc2 = new TrackedDocument(
                Id: doc2Id,
                TenantId: defaultTenantId,
                Title: "Wholesale Fragrance Master Supply Proposal - 8 Pages",
                TotalPages: 8,
                OwnerEmail: "contracts@sgrowing.io",
                CreatedAt: DateTimeOffset.UtcNow.AddDays(-7),
                FileSizeKb: 1820,
                Category: DocumentCategory.WholesaleContract.ToString(),
                Pages: doc2Pages
            );
            _documents[doc2Id] = doc2;

            // Pre-seed Links
            // Ensure token adheres to 24-char format if checked: "doc_view_" + 24 characters
            var link1 = new DocumentShareLink(
                Id: Guid.Parse("c1111111-1111-1111-1111-111111111111"),
                TenantId: defaultTenantId,
                DocumentId: doc1Id,
                Token: "doc_view_acme_corp_ent_deal_001a", // exactly 24 chars
                RecipientEmail: "cfo@acmecorp.com",
                RecipientCompany: "Acme Corporation",
                AllowDownload: true,
                ExpiresAt: DateTimeOffset.UtcNow.AddDays(14),
                PasscodeHash: null,
                IsActive: true,
                CreatedAt: DateTimeOffset.UtcNow.AddDays(-5)
            );
            _linksByToken[link1.Token] = link1;

            var link2 = new DocumentShareLink(
                Id: Guid.Parse("c2222222-2222-2222-2222-222222222222"),
                TenantId: defaultTenantId,
                DocumentId: doc1Id,
                Token: "doc_view_globex_saas_link_002b",
                RecipientEmail: "procurement@globex.com",
                RecipientCompany: "Globex Retail",
                AllowDownload: false,
                ExpiresAt: DateTimeOffset.UtcNow.AddDays(7),
                PasscodeHash: HashPasscode("Globex2026!"),
                IsActive: true,
                CreatedAt: DateTimeOffset.UtcNow.AddDays(-3)
            );
            _linksByToken[link2.Token] = link2;

            var link3 = new DocumentShareLink(
                Id: Guid.Parse("c3333333-3333-3333-3333-333333333333"),
                TenantId: defaultTenantId,
                DocumentId: doc2Id,
                Token: "doc_view_initech_wholesal_003c",
                RecipientEmail: "buyer@initech.com",
                RecipientCompany: "Initech Holdings",
                AllowDownload: true,
                ExpiresAt: DateTimeOffset.UtcNow.AddDays(30),
                PasscodeHash: null,
                IsActive: true,
                CreatedAt: DateTimeOffset.UtcNow.AddDays(-2)
            );
            _linksByToken[link3.Token] = link3;

            // Pre-seed Session 1 (Acme Corp - HOT Lead)
            var session1Id = Guid.Parse("d1111111-1111-1111-1111-111111111111");
            var acmePageDwells = new Dictionary<int, (long Dwell, decimal Scroll)>
            {
                { 1, (12000, 100.0m) },
                { 2, (34000, 100.0m) },
                { 3, (28000, 90.0m) },
                { 4, (22000, 85.0m) },
                { 5, (52000, 100.0m) }, // Pricing plan: 52s (> 45s trigger!)
                { 6, (38000, 100.0m) }, // Pricing plan: 38s
                { 7, (35000, 95.0m) },  // Legal terms: 35s
                { 8, (25000, 80.0m) },
                { 9, (20000, 75.0m) },
                { 10, (18000, 100.0m) }
            };

            foreach (var kvp in acmePageDwells)
            {
                var pt = new PageViewTelemetry(
                    Id: Guid.NewGuid(),
                    SessionId: session1Id,
                    LinkId: link1.Id,
                    PageNumber: kvp.Key,
                    DwellTimeMs: kvp.Value.Dwell,
                    ScrollDepthPct: kvp.Value.Scroll,
                    CompletedView: true,
                    Timestamp: DateTimeOffset.UtcNow.AddHours(-3).AddMinutes(kvp.Key * 2)
                );
                _pageTelemetry[(session1Id, kvp.Key)] = pt;
            }

            var doc1PageTypes = doc1Pages.ToDictionary(p => p.PageNumber, p => p.PageType);
            var acmeViews = _pageTelemetry.Values.Where(p => p.SessionId == session1Id).ToList();
            var acmeScore = CalculateIntentScore(acmeViews, doc1PageTypes); // ~85.0m+ Hot!

            var session1 = new DocumentSession(
                SessionId: session1Id,
                LinkId: link1.Id,
                DocumentId: doc1Id,
                VisitorIp: "198.51.100.42",
                UserAgent: "Mozilla/5.0 (Windows NT 10.0; Win64; x64) Chrome/130.0.0.0",
                DeviceType: DocumentDeviceType.Desktop.ToString(),
                Country: "US",
                StartedAt: DateTimeOffset.UtcNow.AddHours(-3),
                LastHeartbeatAt: DateTimeOffset.UtcNow.AddHours(-2).AddMinutes(-40),
                TotalDwellTimeMs: acmePageDwells.Values.Sum(v => v.Dwell),
                PagesViewedCount: 10,
                IntentScore: acmeScore,
                IntentTier: BuyingIntentTier.Hot.ToString()
            );
            _sessions[session1Id] = session1;

            // Pre-seed Alert for Acme Corp
            var alert1 = new HotLeadAlert(
                Id: Guid.Parse("e1111111-1111-1111-1111-111111111111"),
                TenantId: defaultTenantId,
                DocumentId: doc1Id,
                DocumentTitle: doc1.Title,
                RecipientEmail: link1.RecipientEmail,
                RecipientCompany: link1.RecipientCompany,
                TriggerReason: "Deep Pricing Engagement: Prospect spent 52s on Pricing Plan (Page 5)",
                IntentScore: acmeScore,
                TriggeredAt: DateTimeOffset.UtcNow.AddHours(-2).AddMinutes(-45)
            );
            _alerts[alert1.Id] = alert1;

            // Pre-seed Session 2 (Globex Retail - WARM Lead)
            var session2Id = Guid.Parse("d2222222-2222-2222-2222-222222222222");
            var globexPageDwells = new Dictionary<int, (long Dwell, decimal Scroll)>
            {
                { 1, (10000, 100.0m) },
                { 2, (25000, 80.0m) },
                { 3, (15000, 70.0m) },
                { 4, (12000, 60.0m) },
                { 5, (28000, 85.0m) } // Pricing: 28s
            };

            foreach (var kvp in globexPageDwells)
            {
                var pt = new PageViewTelemetry(
                    Id: Guid.NewGuid(),
                    SessionId: session2Id,
                    LinkId: link2.Id,
                    PageNumber: kvp.Key,
                    DwellTimeMs: kvp.Value.Dwell,
                    ScrollDepthPct: kvp.Value.Scroll,
                    CompletedView: kvp.Key <= 3,
                    Timestamp: DateTimeOffset.UtcNow.AddHours(-5).AddMinutes(kvp.Key * 2)
                );
                _pageTelemetry[(session2Id, kvp.Key)] = pt;
            }

            var globexViews = _pageTelemetry.Values.Where(p => p.SessionId == session2Id).ToList();
            var globexScore = CalculateIntentScore(globexViews, doc1PageTypes); // ~55-60m Warm

            var session2 = new DocumentSession(
                SessionId: session2Id,
                LinkId: link2.Id,
                DocumentId: doc1Id,
                VisitorIp: "203.0.113.18",
                UserAgent: "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) Safari/605.1.15",
                DeviceType: DocumentDeviceType.Desktop.ToString(),
                Country: "GB",
                StartedAt: DateTimeOffset.UtcNow.AddHours(-5),
                LastHeartbeatAt: DateTimeOffset.UtcNow.AddHours(-4).AddMinutes(-48),
                TotalDwellTimeMs: globexPageDwells.Values.Sum(v => v.Dwell),
                PagesViewedCount: 5,
                IntentScore: globexScore,
                IntentTier: BuyingIntentTier.Warm.ToString()
            );
            _sessions[session2Id] = session2;

            // Pre-seed Session 3 (Initech - CASUAL Lead)
            var session3Id = Guid.Parse("d3333333-3333-3333-3333-333333333333");
            var initechPageDwells = new Dictionary<int, (long Dwell, decimal Scroll)>
            {
                { 1, (8000, 100.0m) },
                { 2, (12000, 50.0m) }
            };

            foreach (var kvp in initechPageDwells)
            {
                var pt = new PageViewTelemetry(
                    Id: Guid.NewGuid(),
                    SessionId: session3Id,
                    LinkId: link3.Id,
                    PageNumber: kvp.Key,
                    DwellTimeMs: kvp.Value.Dwell,
                    ScrollDepthPct: kvp.Value.Scroll,
                    CompletedView: false,
                    Timestamp: DateTimeOffset.UtcNow.AddDays(-1)
                );
                _pageTelemetry[(session3Id, kvp.Key)] = pt;
            }

            var doc2PageTypes = doc2Pages.ToDictionary(p => p.PageNumber, p => p.PageType);
            var initechViews = _pageTelemetry.Values.Where(p => p.SessionId == session3Id).ToList();
            var initechScore = CalculateIntentScore(initechViews, doc2PageTypes); // Casual (<50)

            var session3 = new DocumentSession(
                SessionId: session3Id,
                LinkId: link3.Id,
                DocumentId: doc2Id,
                VisitorIp: "198.51.100.99",
                UserAgent: "Mozilla/5.0 (iPhone; CPU iPhone OS 17_4 like Mac OS X)",
                DeviceType: DocumentDeviceType.Mobile.ToString(),
                Country: "DE",
                StartedAt: DateTimeOffset.UtcNow.AddDays(-1),
                LastHeartbeatAt: DateTimeOffset.UtcNow.AddDays(-1).AddMinutes(3),
                TotalDwellTimeMs: initechPageDwells.Values.Sum(v => v.Dwell),
                PagesViewedCount: 2,
                IntentScore: initechScore,
                IntentTier: BuyingIntentTier.Casual.ToString()
            );
            _sessions[session3Id] = session3;

            _initialized = true;
        }
    }
}
