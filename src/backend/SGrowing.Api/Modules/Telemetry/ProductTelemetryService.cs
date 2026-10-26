using Microsoft.EntityFrameworkCore;
using SGrowing.Api.Data;
using SGrowing.Core.Entities;

namespace SGrowing.Api.Modules.Telemetry;

public record LiveTelemetrySummaryDto(
    int ActiveVisitorsCount,
    int ActiveSessionsCount,
    List<ActivePageMetricDto> TopActivePages,
    List<CountryMetricDto> TopCountries,
    List<DeviceMetricDto> DeviceBreakdown
);

public record ActivePageMetricDto(string Url, int ActiveVisitors);
public record CountryMetricDto(string CountryCode, int VisitorCount, double Percentage);
public record DeviceMetricDto(string DeviceType, int SessionCount, double Percentage);

public record FunnelDropoffAnalysisDto(
    List<string> FunnelSteps,
    int TotalInitialVisitors,
    int TotalCompletedVisitors,
    double OverallConversionRatePercent,
    List<StepDropoffMetricDto> StepMetrics
);

public record StepDropoffMetricDto(
    string StepName,
    int StepIndex,
    int ReachedCount,
    int DroppedCount,
    double ConversionRateFromPreviousPercent,
    double OverallStepConversionPercent
);

public interface IProductTelemetryService
{
    Task<TelemetryEvent> IngestTelemetryEventAsync(Guid tenantId, string sessionId, string visitorId, string eventName, string url, string referrer = "", string deviceType = "Desktop", string browser = "Chrome", string countryCode = "TN", string city = "Tunis", string propertiesJson = "{}");
    Task<LiveTelemetrySummaryDto> GetLiveSummaryAsync(Guid tenantId, int windowMinutes = 15);
    Task<List<TelemetryEvent>> GetRecentEventsAsync(Guid tenantId, int limit = 50);
    Task<FunnelDropoffAnalysisDto> ComputeFunnelDropoffAsync(Guid tenantId, List<string> orderedStepEvents, DateTime? fromUtc = null, DateTime? toUtc = null);
}

public class ProductTelemetryService : IProductTelemetryService
{
    private readonly SGrowingDbContext _db;

    public ProductTelemetryService(SGrowingDbContext db)
    {
        _db = db;
    }

    public async Task<TelemetryEvent> IngestTelemetryEventAsync(Guid tenantId, string sessionId, string visitorId, string eventName, string url, string referrer = "", string deviceType = "Desktop", string browser = "Chrome", string countryCode = "TN", string city = "Tunis", string propertiesJson = "{}")
    {
        var ev = TelemetryEvent.Create(tenantId, sessionId, visitorId, eventName, url, referrer, deviceType, browser, countryCode, city, propertiesJson);
        _db.TelemetryEvents.Add(ev);

        // Update or create session
        var session = await _db.VisitorSessions.FirstOrDefaultAsync(s => s.TenantId == tenantId && s.SessionId == sessionId);
        if (session != null)
        {
            session.PagesViewedCount++;
            session.LastActiveAtUtc = DateTime.UtcNow;
            session.SessionDurationSeconds = (int)(session.LastActiveAtUtc - session.StartedAtUtc).TotalSeconds;
            session.IsActive = true;
        }
        else
        {
            session = VisitorSession.Create(tenantId, sessionId, visitorId, countryCode, city, deviceType, browser, referrer);
            _db.VisitorSessions.Add(session);
        }

        await _db.SaveChangesAsync();
        return ev;
    }

    public async Task<LiveTelemetrySummaryDto> GetLiveSummaryAsync(Guid tenantId, int windowMinutes = 15)
    {
        var cutoff = DateTime.UtcNow.AddMinutes(-windowMinutes);

        var activeSessions = await _db.VisitorSessions
            .Where(s => s.TenantId == tenantId && s.LastActiveAtUtc >= cutoff)
            .ToListAsync();

        var recentEvents = await _db.TelemetryEvents
            .Where(e => e.TenantId == tenantId && e.TimestampUtc >= cutoff)
            .ToListAsync();

        int uniqueVisitors = activeSessions.Select(s => s.VisitorId).Distinct().Count();
        if (uniqueVisitors == 0 && activeSessions.Count > 0) uniqueVisitors = activeSessions.Count;

        var topPages = recentEvents
            .GroupBy(e => e.Url)
            .OrderByDescending(g => g.Count())
            .Take(5)
            .Select(g => new ActivePageMetricDto(g.Key, g.Select(x => x.VisitorId).Distinct().Count()))
            .ToList();

        int totalSessions = activeSessions.Count;

        var topCountries = activeSessions
            .GroupBy(s => s.CountryCode)
            .OrderByDescending(g => g.Count())
            .Take(5)
            .Select(g => new CountryMetricDto(
                g.Key,
                g.Count(),
                totalSessions > 0 ? Math.Round((double)g.Count() / totalSessions * 100, 1) : 0
            ))
            .ToList();

        var deviceBreakdown = activeSessions
            .GroupBy(s => s.DeviceType)
            .Select(g => new DeviceMetricDto(
                g.Key,
                g.Count(),
                totalSessions > 0 ? Math.Round((double)g.Count() / totalSessions * 100, 1) : 0
            ))
            .ToList();

        return new LiveTelemetrySummaryDto(
            uniqueVisitors,
            activeSessions.Count,
            topPages,
            topCountries,
            deviceBreakdown
        );
    }

    public async Task<List<TelemetryEvent>> GetRecentEventsAsync(Guid tenantId, int limit = 50)
    {
        return await _db.TelemetryEvents
            .Where(e => e.TenantId == tenantId)
            .OrderByDescending(e => e.TimestampUtc)
            .Take(limit)
            .ToListAsync();
    }

    public async Task<FunnelDropoffAnalysisDto> ComputeFunnelDropoffAsync(
        Guid tenantId,
        List<string> orderedStepEvents,
        DateTime? fromUtc = null,
        DateTime? toUtc = null)
    {
        var from = fromUtc ?? DateTime.UtcNow.AddDays(-30);
        var to = toUtc ?? DateTime.UtcNow.AddDays(1);

        if (orderedStepEvents == null || orderedStepEvents.Count == 0)
        {
            orderedStepEvents = new List<string> { "pageview", "add_to_cart", "begin_checkout", "purchase" };
        }

        var events = await _db.TelemetryEvents
            .Where(e => e.TenantId == tenantId && e.TimestampUtc >= from && e.TimestampUtc <= to)
            .ToListAsync();

        // Group by VisitorId
        var visitorEvents = events
            .GroupBy(e => e.VisitorId)
            .ToDictionary(g => g.Key, g => g.Select(x => x.EventName.ToLowerInvariant()).ToHashSet());

        var stepMetrics = new List<StepDropoffMetricDto>();
        int previousStepCount = 0;
        int initialCount = 0;

        for (int i = 0; i < orderedStepEvents.Count; i++)
        {
            var stepName = orderedStepEvents[i].ToLowerInvariant();
            int reached = visitorEvents.Count(v => v.Value.Contains(stepName));

            if (i == 0)
            {
                initialCount = reached;
                previousStepCount = reached;
            }

            int dropped = Math.Max(0, previousStepCount - reached);
            double convFromPrev = previousStepCount > 0 ? Math.Round(((double)reached / previousStepCount) * 100.0, 1) : 0.0;
            double overallConv = initialCount > 0 ? Math.Round(((double)reached / initialCount) * 100.0, 1) : 0.0;

            stepMetrics.Add(new StepDropoffMetricDto(
                StepName: orderedStepEvents[i],
                StepIndex: i + 1,
                ReachedCount: reached,
                DroppedCount: dropped,
                ConversionRateFromPreviousPercent: convFromPrev,
                OverallStepConversionPercent: overallConv
            ));

            previousStepCount = reached;
        }

        int completed = stepMetrics.Count > 0 ? stepMetrics.Last().ReachedCount : 0;
        double overallRate = initialCount > 0 ? Math.Round(((double)completed / initialCount) * 100.0, 1) : 0.0;

        return new FunnelDropoffAnalysisDto(
            FunnelSteps: orderedStepEvents,
            TotalInitialVisitors: initialCount,
            TotalCompletedVisitors: completed,
            OverallConversionRatePercent: overallRate,
            StepMetrics: stepMetrics
        );
    }
}
