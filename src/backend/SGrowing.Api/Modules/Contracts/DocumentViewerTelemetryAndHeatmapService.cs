using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR;
using SGrowing.Api.Hubs;

namespace SGrowing.Api.Modules.Contracts;

#region Records & DTOs

public record RecordPageViewRequest(
    Guid TenantId,
    Guid ContractId,
    int PageNumber,
    double DwellTimeSeconds,
    double ScrollDepthPercentage = 100.0,
    double ZoomLevel = 1.0,
    string? ViewerIp = null,
    string? ViewerCountry = null,
    string? ViewerEmail = null,
    Guid? SessionId = null,
    string? PageTitle = null,
    bool? IsPricingOrTerms = null
);

public record PageViewTelemetryRecord(
    Guid EventId,
    Guid TenantId,
    Guid ContractId,
    Guid SessionId,
    int PageNumber,
    string PageTitle,
    double DwellTimeSeconds,
    double ScrollDepthPercentage,
    double ZoomLevel,
    string ViewerIp,
    string ViewerCountry,
    string? ViewerEmail,
    bool IsPricingOrTermsPage,
    DateTimeOffset TimestampUtc
);

public record PageHeatmapEntry(
    int PageNumber,
    string PageTitle,
    double TotalDwellTimeSeconds,
    double AverageDwellTimeSeconds,
    int TotalViews,
    int UniqueViewersCount,
    double AverageScrollDepthPercentage,
    double AverageZoomLevel,
    bool IsPricingOrTermsPage,
    double AttentionSharePercentage
);

public record ContractHeatmapSummary(
    Guid ContractId,
    Guid TenantId,
    string ContractTitle,
    int TotalPages,
    double TotalDwellTimeSeconds,
    int TotalViews,
    int UniqueViewers,
    List<PageHeatmapEntry> PageHeatmaps,
    int HighIntentAlertsCount,
    DateTimeOffset? LastViewedAtUtc
);

public record HighIntentContractViewAlert(
    Guid AlertId,
    Guid TenantId,
    Guid ContractId,
    string ContractTitle,
    string ViewerEmail,
    string ViewerIp,
    string ViewerCountry,
    int PageNumber,
    double DwellTimeSeconds,
    string TriggerReason,
    DateTimeOffset TimestampUtc
);

#endregion

#region Service Interface

public interface IDocumentViewerTelemetryAndHeatmapService
{
    Task<PageViewTelemetryRecord> RecordPageViewAsync(RecordPageViewRequest request);
    Task<ContractHeatmapSummary> GetContractHeatmapAsync(Guid tenantId, Guid contractId);
    Task<List<HighIntentContractViewAlert>> GetHighIntentAlertsAsync(Guid tenantId, Guid? contractId = null);
    bool IsPricingOrTermsPage(int pageNumber, string? pageTitle);
}

#endregion

#region Service Implementation

public class DocumentViewerTelemetryAndHeatmapService : IDocumentViewerTelemetryAndHeatmapService
{
    private static readonly ConcurrentBag<PageViewTelemetryRecord> _telemetryEvents = [];
    private static readonly ConcurrentBag<HighIntentContractViewAlert> _highIntentAlerts = [];

    private readonly IHubContext<OmniHub, IOmniClient>? _hubContext;
    private readonly IMultiPartySigningWorkflowService? _workflowService;
    private readonly ILogger<DocumentViewerTelemetryAndHeatmapService>? _logger;

    public DocumentViewerTelemetryAndHeatmapService(
        IHubContext<OmniHub, IOmniClient>? hubContext = null,
        IMultiPartySigningWorkflowService? workflowService = null,
        ILogger<DocumentViewerTelemetryAndHeatmapService>? logger = null)
    {
        _hubContext = hubContext;
        _workflowService = workflowService;
        _logger = logger;
    }

    public async Task<PageViewTelemetryRecord> RecordPageViewAsync(RecordPageViewRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.PageNumber < 1)
        {
            throw new ArgumentException("Page number must be at least 1.", nameof(request));
        }

        var eventId = Guid.NewGuid();
        var sessionId = request.SessionId ?? Guid.NewGuid();
        var viewerIp = !string.IsNullOrWhiteSpace(request.ViewerIp) ? request.ViewerIp : "127.0.0.1";
        var viewerCountry = !string.IsNullOrWhiteSpace(request.ViewerCountry) ? request.ViewerCountry : "US";
        var dwellSeconds = Math.Max(0.0, request.DwellTimeSeconds);
        var scrollDepth = Math.Clamp(request.ScrollDepthPercentage, 0.0, 100.0);
        var zoomLevel = Math.Clamp(request.ZoomLevel, 0.25, 5.0);

        // Resolve page title & determine if pricing / terms page
        var pageTitle = request.PageTitle ?? $"Page {request.PageNumber}";
        var isPricingOrTerms = request.IsPricingOrTerms ?? IsPricingOrTermsPage(request.PageNumber, pageTitle);

        var record = new PageViewTelemetryRecord(
            eventId,
            request.TenantId,
            request.ContractId,
            sessionId,
            request.PageNumber,
            pageTitle,
            dwellSeconds,
            scrollDepth,
            zoomLevel,
            viewerIp,
            viewerCountry,
            request.ViewerEmail,
            isPricingOrTerms,
            DateTimeOffset.UtcNow
        );

        _telemetryEvents.Add(record);

        // Evaluate High-Intent Alert: prospect spends > 60 seconds on pricing or terms page
        if (isPricingOrTerms && dwellSeconds >= 60.0)
        {
            await TriggerHighIntentAlertAsync(record);
        }

        return record;
    }

    public async Task<ContractHeatmapSummary> GetContractHeatmapAsync(Guid tenantId, Guid contractId)
    {
        var contractTitle = "Enterprise Digital Agreement";
        int detectedTotalPages = 1;

        if (_workflowService != null)
        {
            var workflow = await _workflowService.GetWorkflowAsync(tenantId, contractId);
            if (workflow != null)
            {
                contractTitle = workflow.Title;
                detectedTotalPages = Math.Max(1, workflow.Pages.Count);
            }
        }

        var matchingEvents = _telemetryEvents
            .Where(e => e.TenantId == tenantId && e.ContractId == contractId)
            .ToList();

        if (matchingEvents.Count == 0)
        {
            return new ContractHeatmapSummary(
                contractId,
                tenantId,
                contractTitle,
                detectedTotalPages,
                0.0,
                0,
                0,
                [],
                0,
                null
            );
        }

        var totalDwellSeconds = matchingEvents.Sum(e => e.DwellTimeSeconds);
        var totalViews = matchingEvents.Count;
        var uniqueViewers = matchingEvents
            .Select(e => e.ViewerEmail ?? e.ViewerIp)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();

        var alerts = _highIntentAlerts
            .Where(a => a.TenantId == tenantId && a.ContractId == contractId)
            .ToList();

        var pageGroups = matchingEvents.GroupBy(e => e.PageNumber).OrderBy(g => g.Key);
        var pageHeatmaps = new List<PageHeatmapEntry>();

        foreach (var group in pageGroups)
        {
            var pageNumber = group.Key;
            var groupList = group.ToList();
            var pageTotalDwell = groupList.Sum(e => e.DwellTimeSeconds);
            var pageAvgDwell = groupList.Average(e => e.DwellTimeSeconds);
            var pageAvgScroll = groupList.Average(e => e.ScrollDepthPercentage);
            var pageAvgZoom = groupList.Average(e => e.ZoomLevel);
            var pageUnique = groupList.Select(e => e.ViewerEmail ?? e.ViewerIp).Distinct(StringComparer.OrdinalIgnoreCase).Count();
            var title = groupList.First().PageTitle;
            var isPricing = groupList.First().IsPricingOrTermsPage;

            var attentionShare = totalDwellSeconds > 0
                ? Math.Round((pageTotalDwell / totalDwellSeconds) * 100.0, 1)
                : 0.0;

            pageHeatmaps.Add(new PageHeatmapEntry(
                pageNumber,
                title,
                Math.Round(pageTotalDwell, 1),
                Math.Round(pageAvgDwell, 1),
                groupList.Count,
                pageUnique,
                Math.Round(pageAvgScroll, 1),
                Math.Round(pageAvgZoom, 2),
                isPricing,
                attentionShare
            ));
        }

        var lastViewed = matchingEvents.Max(e => e.TimestampUtc);

        return new ContractHeatmapSummary(
            contractId,
            tenantId,
            contractTitle,
            Math.Max(detectedTotalPages, pageHeatmaps.Count),
            Math.Round(totalDwellSeconds, 1),
            totalViews,
            uniqueViewers,
            pageHeatmaps,
            alerts.Count,
            lastViewed
        );
    }

    public Task<List<HighIntentContractViewAlert>> GetHighIntentAlertsAsync(Guid tenantId, Guid? contractId = null)
    {
        var query = _highIntentAlerts.Where(a => a.TenantId == tenantId);
        if (contractId.HasValue)
        {
            query = query.Where(a => a.ContractId == contractId.Value);
        }

        return Task.FromResult(query.OrderByDescending(a => a.TimestampUtc).ToList());
    }

    public bool IsPricingOrTermsPage(int pageNumber, string? pageTitle)
    {
        if (string.IsNullOrWhiteSpace(pageTitle))
        {
            // By default in many B2B proposal templates, Page 2 or Page 3 is pricing
            return pageNumber == 2;
        }

        var normalized = pageTitle.ToLowerInvariant();
        return normalized.Contains("pricing")
            || normalized.Contains("price")
            || normalized.Contains("terms")
            || normalized.Contains("commercial")
            || normalized.Contains("investment")
            || normalized.Contains("cost")
            || normalized.Contains("fee")
            || normalized.Contains("quote");
    }

    private async Task TriggerHighIntentAlertAsync(PageViewTelemetryRecord record)
    {
        var alert = new HighIntentContractViewAlert(
            Guid.NewGuid(),
            record.TenantId,
            record.ContractId,
            "Enterprise Contract Proposal",
            record.ViewerEmail ?? "anonymous.prospect@client.com",
            record.ViewerIp,
            record.ViewerCountry,
            record.PageNumber,
            record.DwellTimeSeconds,
            $"High Purchase Intent: Prospect spent {record.DwellTimeSeconds:F1}s on Pricing/Terms page (Page #{record.PageNumber} - \"{record.PageTitle}\").",
            DateTimeOffset.UtcNow
        );

        _highIntentAlerts.Add(alert);

        if (_hubContext != null)
        {
            try
            {
                var groupName = OmniHub.GetTenantGroup(record.TenantId);
                var alertDto = new HighIntentProposalViewedEventDto(
                    record.TenantId,
                    record.ContractId,
                    alert.ContractTitle,
                    alert.ViewerEmail,
                    alert.ViewerIp,
                    record.PageNumber,
                    record.DwellTimeSeconds,
                    1,
                    alert.TriggerReason,
                    alert.TimestampUtc.UtcDateTime
                );

                await _hubContext.Clients.Group(groupName).OnHighIntentProposalViewed(alertDto);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Failed to broadcast HighIntentProposalViewed event to SignalR OmniHub.");
            }
        }
    }
}

#endregion
