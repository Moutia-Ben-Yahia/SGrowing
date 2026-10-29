using Microsoft.EntityFrameworkCore;
using SGrowing.Api.Data;
using SGrowing.Core.Entities;

namespace SGrowing.Api.Modules.Documents;

public record PageDwellMetricDto(
    int PageNumber,
    double TotalDwellSeconds,
    int ViewCount,
    double AverageDwellSeconds,
    double AttentionPercentage,
    bool IsPricingPage,
    bool HasHighInterestAlert
);

public record DropOffHeatmapPointDto(
    int PageNumber,
    int ViewersReached,
    double RetentionRate,
    double DropOffRate
);

public record HighInterestAlertDto(
    Guid DocumentId,
    int PageNumber,
    string AlertType,
    string Message,
    double DwellSeconds,
    DateTime DetectedAtUtc
);

public record DocumentTelemetrySummaryDto(
    Guid DocumentId,
    double TotalDocumentDwellSeconds,
    int TotalViews,
    int UniqueViewers,
    List<PageDwellMetricDto> PageDwellBreakdown,
    List<DropOffHeatmapPointDto> DropOffHeatmap,
    List<HighInterestAlertDto> HighInterestAlerts,
    bool HasPricingHighInterestAlert,
    DateTime? LastViewedAtUtc
);

public record RecordDwellTelemetryRequest(
    int PageNumber,
    double SecondsDwell,
    string? ViewerIp = null,
    string? ViewerEmail = null,
    int? MaxScrollPercent = null
);

public interface IDocumentTelemetryService
{
    Task<DocumentDwellTelemetry> RecordPageDwellTimeAsync(
        Guid tenantId, 
        Guid documentId, 
        int pageNumber, 
        double secondsDwell, 
        string viewerIp, 
        string? viewerEmail = null, 
        int maxScrollPercent = 100);

    Task<DocumentTelemetrySummaryDto> GetDocumentTelemetrySummaryAsync(Guid tenantId, Guid documentId);
}

public class DocumentTelemetryService : IDocumentTelemetryService
{
    private readonly SGrowingDbContext _db;
    public const double PricingPageHighInterestThresholdSeconds = 45.0;

    public DocumentTelemetryService(SGrowingDbContext db)
    {
        _db = db;
    }

    public async Task<DocumentDwellTelemetry> RecordPageDwellTimeAsync(
        Guid tenantId,
        Guid documentId,
        int pageNumber,
        double secondsDwell,
        string viewerIp,
        string? viewerEmail = null,
        int maxScrollPercent = 100)
    {
        var sanitizedIp = string.IsNullOrWhiteSpace(viewerIp) ? "127.0.0.1" : viewerIp.Trim();
        var email = !string.IsNullOrWhiteSpace(viewerEmail) 
            ? viewerEmail.Trim().ToLowerInvariant() 
            : $"viewer-{sanitizedIp.Replace(".", "-").Replace(":", "-")}@secure-view.sgrowing.io";

        var dwellSecondsInt = (int)Math.Max(1, Math.Round(secondsDwell));
        var record = DocumentDwellTelemetry.Create(
            tenantId,
            documentId,
            pageNumber,
            email,
            sanitizedIp,
            dwellSecondsInt,
            maxScrollPercent
        );

        _db.DocumentDwellTelemetries.Add(record);
        await _db.SaveChangesAsync();

        return record;
    }

    public async Task<DocumentTelemetrySummaryDto> GetDocumentTelemetrySummaryAsync(Guid tenantId, Guid documentId)
    {
        var telemetries = await _db.DocumentDwellTelemetries
            .Where(t => t.TenantId == tenantId && t.DocumentId == documentId)
            .OrderBy(t => t.CapturedAtUtc)
            .ToListAsync();

        if (telemetries.Count == 0)
        {
            return new DocumentTelemetrySummaryDto(
                documentId,
                0,
                0,
                0,
                new List<PageDwellMetricDto>(),
                new List<DropOffHeatmapPointDto>(),
                new List<HighInterestAlertDto>(),
                false,
                null
            );
        }

        var totalDwellSeconds = telemetries.Sum(t => t.DwellTimeSeconds);
        var totalViews = telemetries.Count;
        var uniqueViewers = telemetries.Select(t => t.ViewerEmail).Distinct().Count();
        var lastViewedAt = telemetries.Max(t => t.CapturedAtUtc);

        var pageGroups = telemetries
            .GroupBy(t => t.PageNumber)
            .OrderBy(g => g.Key)
            .ToList();

        var maxPage = pageGroups.Max(g => g.Key);
        var pageBreakdown = new List<PageDwellMetricDto>();
        var highInterestAlerts = new List<HighInterestAlertDto>();
        bool hasPricingHighInterestAlert = false;

        foreach (var group in pageGroups)
        {
            var pageNum = group.Key;
            var pageTotalDwell = group.Sum(t => (double)t.DwellTimeSeconds);
            var pageViews = group.Count();
            var avgDwell = pageViews > 0 ? pageTotalDwell / pageViews : 0.0;
            var attentionPct = totalDwellSeconds > 0 ? (pageTotalDwell / totalDwellSeconds) * 100.0 : 0.0;

            // Pricing page heuristic: Page 3 (standard Commercial/Pricing schedule), or Page 2 if a 2-page contract
            var isPricingPage = (pageNum == 3) || (maxPage <= 2 && pageNum == 2);
            var isHighInterest = isPricingPage && (pageTotalDwell > PricingPageHighInterestThresholdSeconds || group.Any(t => t.DwellTimeSeconds > PricingPageHighInterestThresholdSeconds));

            if (isHighInterest)
            {
                hasPricingHighInterestAlert = true;
                highInterestAlerts.Add(new HighInterestAlertDto(
                    documentId,
                    pageNum,
                    "PricingPageHighInterest",
                    $"High buying intent: Viewer spent {pageTotalDwell:F1}s reviewing the pricing schedule (exceeds {PricingPageHighInterestThresholdSeconds:F0}s threshold).",
                    pageTotalDwell,
                    group.Max(t => t.CapturedAtUtc)
                ));
            }

            pageBreakdown.Add(new PageDwellMetricDto(
                pageNum,
                pageTotalDwell,
                pageViews,
                Math.Round(avgDwell, 2),
                Math.Round(attentionPct, 2),
                isPricingPage,
                isHighInterest
            ));
        }

        // Drop-off Heatmap: Reader retention across sequential pages
        var dropOffHeatmap = new List<DropOffHeatmapPointDto>();
        var firstPageUniqueCount = Math.Max(1, pageGroups.FirstOrDefault(g => g.Key == 1)?.Select(t => t.ViewerEmail).Distinct().Count() ?? uniqueViewers);

        for (int p = 1; p <= maxPage; p++)
        {
            var viewersOnPage = telemetries.Where(t => t.PageNumber == p).Select(t => t.ViewerEmail).Distinct().Count();
            var retentionRate = Math.Round((double)viewersOnPage / firstPageUniqueCount * 100.0, 1);
            var dropOffRate = Math.Round(Math.Max(0.0, 100.0 - retentionRate), 1);

            dropOffHeatmap.Add(new DropOffHeatmapPointDto(
                p,
                viewersOnPage,
                retentionRate,
                dropOffRate
            ));
        }

        return new DocumentTelemetrySummaryDto(
            documentId,
            totalDwellSeconds,
            totalViews,
            uniqueViewers,
            pageBreakdown,
            dropOffHeatmap,
            highInterestAlerts,
            hasPricingHighInterestAlert,
            lastViewedAt
        );
    }
}
