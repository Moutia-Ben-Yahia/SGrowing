using Microsoft.EntityFrameworkCore;
using SGrowing.Api.Data;
using SGrowing.Api.Modules.Telemetry;
using Xunit;

namespace SGrowing.Tests.Telemetry;

public class ProductTelemetryTests
{
    private readonly SGrowingDbContext _db;
    private readonly ProductTelemetryService _service;

    public ProductTelemetryTests()
    {
        var options = new DbContextOptionsBuilder<SGrowingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _db = new SGrowingDbContext(options);
        _service = new ProductTelemetryService(_db);
    }

    [Fact]
    public async Task IngestTelemetryEvent_ShouldRecordEventAndUpsertVisitorSession()
    {
        var tenantId = Guid.NewGuid();
        var sessionId = "sess_test_123";
        var visitorId = "vis_test_456";

        var ev = await _service.IngestTelemetryEventAsync(
            tenantId,
            sessionId,
            visitorId,
            "pageview",
            "/checkout/cod-step",
            "https://google.com",
            deviceType: "Mobile",
            browser: "Safari",
            countryCode: "TN",
            city: "Tunis"
        );

        Assert.NotNull(ev);
        Assert.Equal("pageview", ev.EventName);
        Assert.Equal("/checkout/cod-step", ev.Url);
        Assert.Equal("TN", ev.CountryCode);

        // Check session upserted
        var session = await _db.VisitorSessions.FirstOrDefaultAsync(s => s.TenantId == tenantId && s.SessionId == sessionId);
        Assert.NotNull(session);
        Assert.Equal(visitorId, session.VisitorId);
        Assert.Equal("Mobile", session.DeviceType);
        Assert.Equal(1, session.PagesViewedCount);

        // Second event in same session should increment pageviews
        await _service.IngestTelemetryEventAsync(
            tenantId,
            sessionId,
            visitorId,
            "button_click",
            "/checkout/confirm",
            deviceType: "Mobile",
            browser: "Safari",
            countryCode: "TN"
        );

        var updatedSession = await _db.VisitorSessions.FirstOrDefaultAsync(s => s.TenantId == tenantId && s.SessionId == sessionId);
        Assert.NotNull(updatedSession);
        Assert.Equal(2, updatedSession.PagesViewedCount);
    }

    [Fact]
    public async Task GetLiveSummary_AggregatesActiveVisitorsAndCountryBreakdown()
    {
        var tenantId = Guid.NewGuid();

        // Ingest events from 3 different visitors/sessions in Tunisia and France
        await _service.IngestTelemetryEventAsync(tenantId, "sess_1", "vis_1", "pageview", "/pricing", countryCode: "TN", deviceType: "Desktop");
        await _service.IngestTelemetryEventAsync(tenantId, "sess_2", "vis_2", "pageview", "/pricing", countryCode: "TN", deviceType: "Mobile");
        await _service.IngestTelemetryEventAsync(tenantId, "sess_3", "vis_3", "pageview", "/features", countryCode: "FR", deviceType: "Desktop");

        var summary = await _service.GetLiveSummaryAsync(tenantId, windowMinutes: 15);

        Assert.NotNull(summary);
        Assert.Equal(3, summary.ActiveVisitorsCount);
        Assert.Equal(3, summary.ActiveSessionsCount);

        // Top pages
        Assert.Contains(summary.TopActivePages, p => p.Url == "/pricing" && p.ActiveVisitors == 2);
        Assert.Contains(summary.TopActivePages, p => p.Url == "/features" && p.ActiveVisitors == 1);

        // Country breakdown
        Assert.Contains(summary.TopCountries, c => c.CountryCode == "TN" && c.VisitorCount == 2);
        Assert.Contains(summary.TopCountries, c => c.CountryCode == "FR" && c.VisitorCount == 1);

        // Device breakdown
        Assert.Contains(summary.DeviceBreakdown, d => d.DeviceType == "Desktop" && d.SessionCount == 2);
        Assert.Contains(summary.DeviceBreakdown, d => d.DeviceType == "Mobile" && d.SessionCount == 1);
    }

    [Fact]
    public async Task GetRecentEvents_ReturnsChronologicalStreamWithinLimit()
    {
        var tenantId = Guid.NewGuid();

        for (int i = 0; i < 15; i++)
        {
            await _service.IngestTelemetryEventAsync(tenantId, $"sess_{i}", $"vis_{i}", $"event_{i}", $"/path/{i}");
        }

        var recent = await _service.GetRecentEventsAsync(tenantId, limit: 10);

        Assert.Equal(10, recent.Count);
        Assert.All(recent, e => Assert.Equal(tenantId, e.TenantId));
    }
}
