using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
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

        ev.Should().NotBeNull();
        ev.EventName.Should().Be("pageview");
        ev.Url.Should().Be("/checkout/cod-step");
        ev.CountryCode.Should().Be("TN");

        // Check session upserted
        var session = await _db.VisitorSessions.FirstOrDefaultAsync(s => s.TenantId == tenantId && s.SessionId == sessionId);
        session.Should().NotBeNull();
        session!.VisitorId.Should().Be(visitorId);
        session.DeviceType.Should().Be("Mobile");
        session.PagesViewedCount.Should().Be(1);

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
        updatedSession.Should().NotBeNull();
        updatedSession!.PagesViewedCount.Should().Be(2);
    }

    [Fact]
    public async Task IngestTelemetryEvent_WithCustomEventDataJson_PreservesPayload()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var eventData = "{\"cartTotal\":250.0,\"items\":3}";

        // Act
        var ev = await _service.IngestTelemetryEventAsync(
            tenantId,
            "sess_custom_data",
            "vis_custom",
            "add_to_cart",
            "/product/smart-watch",
            propertiesJson: eventData
        );

        // Assert
        ev.PropertiesJson.Should().Be(eventData);
        var stored = await _db.TelemetryEvents.FindAsync(ev.Id);
        stored!.PropertiesJson.Should().Be(eventData);
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

        summary.Should().NotBeNull();
        summary.ActiveVisitorsCount.Should().Be(3);
        summary.ActiveSessionsCount.Should().Be(3);

        // Top pages
        summary.TopActivePages.Should().Contain(p => p.Url == "/pricing" && p.ActiveVisitors == 2);
        summary.TopActivePages.Should().Contain(p => p.Url == "/features" && p.ActiveVisitors == 1);

        // Country breakdown
        summary.TopCountries.Should().Contain(c => c.CountryCode == "TN" && c.VisitorCount == 2);
        summary.TopCountries.Should().Contain(c => c.CountryCode == "FR" && c.VisitorCount == 1);

        // Device breakdown
        summary.DeviceBreakdown.Should().Contain(d => d.DeviceType == "Desktop" && d.SessionCount == 2);
        summary.DeviceBreakdown.Should().Contain(d => d.DeviceType == "Mobile" && d.SessionCount == 1);
    }

    [Fact]
    public async Task GetLiveSummary_RespectsTenantIsolation()
    {
        // Arrange
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();

        await _service.IngestTelemetryEventAsync(tenantA, "sess_a", "vis_a", "pageview", "/app");
        await _service.IngestTelemetryEventAsync(tenantB, "sess_b", "vis_b", "pageview", "/app");

        // Act
        var summaryA = await _service.GetLiveSummaryAsync(tenantA, windowMinutes: 15);

        // Assert
        summaryA.ActiveVisitorsCount.Should().Be(1);
        summaryA.ActiveSessionsCount.Should().Be(1);
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

        recent.Count.Should().Be(10);
        recent.Should().AllSatisfy(e => e.TenantId.Should().Be(tenantId));
    }

    [Fact]
    public async Task ComputeFunnelDropoff_CalculatesStepConversionAndDropoffs()
    {
        var tenantId = Guid.NewGuid();

        // 100 visitors do pageview
        for (int i = 0; i < 100; i++)
        {
            var vis = $"visitor_{i}";
            await _service.IngestTelemetryEventAsync(tenantId, $"sess_{i}", vis, "pageview", "/store");

            if (i < 50)
            {
                // 50 visitors add to cart
                await _service.IngestTelemetryEventAsync(tenantId, $"sess_{i}", vis, "add_to_cart", "/cart");
            }
            if (i < 25)
            {
                // 25 visitors checkout
                await _service.IngestTelemetryEventAsync(tenantId, $"sess_{i}", vis, "begin_checkout", "/checkout");
            }
            if (i < 10)
            {
                // 10 visitors purchase
                await _service.IngestTelemetryEventAsync(tenantId, $"sess_{i}", vis, "purchase", "/thank-you");
            }
        }

        var funnelSteps = new List<string> { "pageview", "add_to_cart", "begin_checkout", "purchase" };
        var dropoff = await _service.ComputeFunnelDropoffAsync(tenantId, funnelSteps);

        dropoff.Should().NotBeNull();
        dropoff.TotalInitialVisitors.Should().Be(100);
        dropoff.TotalCompletedVisitors.Should().Be(10);
        dropoff.OverallConversionRatePercent.Should().Be(10.0);
        dropoff.StepMetrics.Should().HaveCount(4);

        // Step 1: pageview
        dropoff.StepMetrics[0].ReachedCount.Should().Be(100);
        dropoff.StepMetrics[0].DroppedCount.Should().Be(0);
        dropoff.StepMetrics[0].ConversionRateFromPreviousPercent.Should().Be(100.0);

        // Step 2: add_to_cart
        dropoff.StepMetrics[1].ReachedCount.Should().Be(50);
        dropoff.StepMetrics[1].DroppedCount.Should().Be(50);
        dropoff.StepMetrics[1].ConversionRateFromPreviousPercent.Should().Be(50.0);

        // Step 3: begin_checkout
        dropoff.StepMetrics[2].ReachedCount.Should().Be(25);
        dropoff.StepMetrics[2].DroppedCount.Should().Be(25);
        dropoff.StepMetrics[2].ConversionRateFromPreviousPercent.Should().Be(50.0);

        // Step 4: purchase
        dropoff.StepMetrics[3].ReachedCount.Should().Be(10);
        dropoff.StepMetrics[3].DroppedCount.Should().Be(15);
        dropoff.StepMetrics[3].ConversionRateFromPreviousPercent.Should().Be(40.0);
    }

    [Fact]
    public async Task ComputeFunnelDropoff_EmptyStepsList_DefaultsToStandardEcommerceSteps()
    {
        // Arrange
        var tenantId = Guid.NewGuid();

        // Act
        var result = await _service.ComputeFunnelDropoffAsync(tenantId, new List<string>());

        // Assert
        result.TotalInitialVisitors.Should().Be(0);
        result.TotalCompletedVisitors.Should().Be(0);
        result.OverallConversionRatePercent.Should().Be(0.0);
        result.StepMetrics.Should().HaveCount(4);
        result.StepMetrics.Select(s => s.StepName).Should().ContainInOrder("pageview", "add_to_cart", "begin_checkout", "purchase");
    }
}
