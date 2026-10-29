using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using SGrowing.Core.Entities;

namespace SGrowing.Api.Modules.CRM;

public static class Customer360Endpoints
{
    private static readonly Guid FallbackTenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    public static IEndpointRouteBuilder MapCustomer360Endpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/crm/customer-360")
            .WithTags("Customer 360 & Attribution Engine (Horizon 19)");

        // 1. GET /customers (search & list unified customers)
        group.MapGet("/customers", async (
            [FromQuery] Guid? tenantId,
            [FromQuery] string? q,
            [FromQuery] CustomerLifecycleStage? stage,
            [FromQuery] int? page,
            [FromQuery] int? pageSize,
            [FromHeader(Name = "X-Tenant-Id")] Guid? headerTenantId,
            ICustomer360AttributionService service,
            CancellationToken ct) =>
        {
            var effectiveTenantId = tenantId ?? headerTenantId ?? FallbackTenantId;
            var list = await service.SearchCustomersAsync(
                effectiveTenantId,
                q,
                stage,
                page ?? 1,
                pageSize ?? 20,
                ct);

            return Results.Ok(new
            {
                tenantId = effectiveTenantId,
                totalCount = list.Count,
                page = page ?? 1,
                pageSize = pageSize ?? 20,
                data = list
            });
        })
        .WithName("SearchUnifiedCustomers")
        .WithSummary("Search and list unified customer profiles across channels with churn risk scores");

        // 2. GET /customers/{id} (customer 360 overview: LTV, churn risk, linked channels)
        group.MapGet("/customers/{id:guid}", async (
            Guid id,
            [FromQuery] Guid? tenantId,
            [FromHeader(Name = "X-Tenant-Id")] Guid? headerTenantId,
            ICustomer360AttributionService service,
            CancellationToken ct) =>
        {
            var effectiveTenantId = tenantId ?? headerTenantId ?? FallbackTenantId;
            try
            {
                var overview = await service.GetCustomer360OverviewAsync(effectiveTenantId, id, ct);
                return Results.Ok(overview);
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
        })
        .WithName("GetCustomer360Overview")
        .WithSummary("Retrieve Customer 360 overview with LTV, churn risk, and linked channel badges");

        // 3. GET /customers/{id}/timeline (chronological interaction timeline)
        group.MapGet("/customers/{id:guid}/timeline", async (
            Guid id,
            [FromQuery] Guid? tenantId,
            [FromQuery] string? channel,
            [FromQuery] string? eventType,
            [FromQuery] bool? chronological,
            [FromHeader(Name = "X-Tenant-Id")] Guid? headerTenantId,
            ICustomer360AttributionService service,
            CancellationToken ct) =>
        {
            var effectiveTenantId = tenantId ?? headerTenantId ?? FallbackTenantId;
            var timeline = await service.GetCustomerTimelineAsync(
                effectiveTenantId,
                id,
                channel,
                eventType,
                chronological ?? false,
                ct);

            return Results.Ok(new
            {
                customerId = id,
                count = timeline.Count,
                chronological = chronological ?? false,
                events = timeline
            });
        })
        .WithName("GetCustomerTimeline")
        .WithSummary("Retrieve master chronological interaction stream across orders, calls, WhatsApp, social, emails, and proposals");

        // 4. POST /identity/stitch (stitch anonymous cookie or channel ID to master profile)
        group.MapPost("/identity/stitch", async (
            [FromBody] StitchIdentityRequest req,
            [FromHeader(Name = "X-Tenant-Id")] Guid? headerTenantId,
            ICustomer360AttributionService service,
            CancellationToken ct) =>
        {
            var effectiveTenantId = req.TenantId != Guid.Empty ? req.TenantId : (headerTenantId ?? FallbackTenantId);
            var sanitizedReq = req with { TenantId = effectiveTenantId };

            var result = await service.StitchIdentityAsync(sanitizedReq, ct);
            return Results.Ok(result);
        })
        .WithName("StitchCustomerIdentity")
        .WithSummary("Stitch anonymous cookie, phone, email, WhatsApp, or social handle into unified master profile");

        // 5. POST /events (record touchpoint or conversion event)
        group.MapPost("/events", async (
            [FromBody] RecordCustomerEventRequest req,
            [FromHeader(Name = "X-Tenant-Id")] Guid? headerTenantId,
            ICustomer360AttributionService service,
            CancellationToken ct) =>
        {
            var effectiveTenantId = req.TenantId != Guid.Empty ? req.TenantId : (headerTenantId ?? FallbackTenantId);
            var sanitizedReq = req with { TenantId = effectiveTenantId };

            var recordedEvent = await service.RecordEventAsync(sanitizedReq, ct);
            return Results.Ok(recordedEvent);
        })
        .WithName("RecordCustomerInteractionEvent")
        .WithSummary("Record interaction event (order, call, WhatsApp, email, proposal, support) for customer stream");

        // 6. POST /touchpoints (record attribution touchpoint)
        group.MapPost("/touchpoints", async (
            [FromBody] RecordTouchpointRequest req,
            [FromHeader(Name = "X-Tenant-Id")] Guid? headerTenantId,
            ICustomer360AttributionService service,
            CancellationToken ct) =>
        {
            var effectiveTenantId = req.TenantId != Guid.Empty ? req.TenantId : (headerTenantId ?? FallbackTenantId);
            var sanitizedReq = req with { TenantId = effectiveTenantId };

            var touchpoint = await service.RecordTouchpointAsync(sanitizedReq, ct);
            return Results.Ok(touchpoint);
        })
        .WithName("RecordAttributionTouchpoint")
        .WithSummary("Record marketing campaign touchpoint with channel and cost for attribution");

        // 7. GET /attribution/compare (compare First-Touch, Last-Touch, Linear, U-Shaped, Time-Decay)
        group.MapGet("/attribution/compare", async (
            [FromQuery] Guid? tenantId,
            [FromQuery] Guid? customerId,
            [FromQuery] Guid? dealId,
            [FromHeader(Name = "X-Tenant-Id")] Guid? headerTenantId,
            ICustomer360AttributionService service,
            CancellationToken ct) =>
        {
            var effectiveTenantId = tenantId ?? headerTenantId ?? FallbackTenantId;
            var comparison = await service.CompareAttributionModelsAsync(effectiveTenantId, customerId, dealId, ct);
            return Results.Ok(comparison);
        })
        .WithName("CompareAttributionModels")
        .WithSummary("Compare 5 attribution models (First, Last, Linear, U-Shaped, Time-Decay) side-by-side");

        // 8. GET /attribution/campaign-roas (campaign attribution breakdown with true ROAS & CAC)
        group.MapGet("/attribution/campaign-roas", async (
            [FromQuery] Guid? tenantId,
            [FromQuery] AttributionModelType? model,
            [FromQuery] DateTime? fromUtc,
            [FromQuery] DateTime? toUtc,
            [FromHeader(Name = "X-Tenant-Id")] Guid? headerTenantId,
            ICustomer360AttributionService service,
            CancellationToken ct) =>
        {
            var effectiveTenantId = tenantId ?? headerTenantId ?? FallbackTenantId;
            var breakdown = await service.GetCampaignRoasBreakdownAsync(
                effectiveTenantId,
                model ?? AttributionModelType.TimeDecay,
                fromUtc,
                toUtc,
                ct);

            return Results.Ok(new
            {
                tenantId = effectiveTenantId,
                model = (model ?? AttributionModelType.TimeDecay).ToString(),
                campaigns = breakdown
            });
        })
        .WithName("GetCampaignAttributionRoas")
        .WithSummary("Get campaign-level revenue attribution, spend, true ROAS, and CAC");

        return app;
    }
}
