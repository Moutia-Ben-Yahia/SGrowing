using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using SGrowing.Api.Common;

namespace SGrowing.Api.Modules.Marketing.Journeys.Runtime;

/// <summary>
/// Modular Minimal API endpoints for Event-Driven Customer Journey Runtime Execution Engine.
/// Implements parameterless constructor for auto-discovery via IEndpointModule.
/// </summary>
public class CustomerJourneyRuntimeEndpoints : IEndpointModule
{
    public CustomerJourneyRuntimeEndpoints() { }

    public void MapEndpoints(IEndpointRouteBuilder app) => app.MapCustomerJourneyRuntimeEndpoints();
}

public static class CustomerJourneyRuntimeEndpointExtensions
{
    private static readonly Guid FallbackTenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    public static IEndpointRouteBuilder MapCustomerJourneyRuntimeEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/journeys/runtime")
            .WithTags("Customer Journey Runtime & Execution Engine");

        // POST /api/v1/journeys/runtime/trigger
        group.MapPost("/trigger", async (
            [FromBody] TriggerJourneyEventRequest req,
            ICustomerJourneyRuntimeService runtimeService,
            CancellationToken ct) =>
        {
            if (req.TenantId == Guid.Empty)
            {
                req = req with { TenantId = FallbackTenantId };
            }

            var progress = await runtimeService.TriggerJourney(req, ct);
            return Results.Ok(progress);
        });

        // POST /api/v1/journeys/runtime/advance
        group.MapPost("/advance", async (
            [FromBody] AdvanceJourneyRequest req,
            ICustomerJourneyRuntimeService runtimeService,
            CancellationToken ct) =>
        {
            if (req.TenantId == Guid.Empty)
            {
                req = req with { TenantId = FallbackTenantId };
            }

            var progress = await runtimeService.AdvanceProgress(req, ct);
            return progress is not null
                ? Results.Ok(progress)
                : Results.NotFound(new { error = $"Journey progress '{req.ProgressId}' was not found for tenant '{req.TenantId}'." });
        });

        // GET /api/v1/journeys/runtime/{journeyId}/contacts
        group.MapGet("/{journeyId}/contacts", async (
            string journeyId,
            [FromQuery] Guid? tenantId,
            ICustomerJourneyRuntimeService runtimeService,
            CancellationToken ct) =>
        {
            var tid = tenantId.HasValue && tenantId.Value != Guid.Empty ? tenantId.Value : FallbackTenantId;
            var contacts = await runtimeService.GetContactsInJourney(tid, journeyId, ct);
            return Results.Ok(contacts);
        });

        // GET /api/v1/journeys/runtime/progress/{id}
        group.MapGet("/progress/{id:guid}", async (
            Guid id,
            [FromQuery] Guid? tenantId,
            ICustomerJourneyRuntimeService runtimeService,
            CancellationToken ct) =>
        {
            var tid = tenantId.HasValue && tenantId.Value != Guid.Empty ? tenantId.Value : FallbackTenantId;
            var progress = await runtimeService.GetJourneyProgress(tid, id, ct);
            return progress is not null
                ? Results.Ok(progress)
                : Results.NotFound(new { error = $"Customer journey progress '{id}' not found for tenant '{tid}'." });
        });

        // GET /api/v1/journeys/runtime/active
        group.MapGet("/active", async (
            [FromQuery] Guid? tenantId,
            ICustomerJourneyRuntimeService runtimeService,
            CancellationToken ct) =>
        {
            var tid = tenantId.HasValue && tenantId.Value != Guid.Empty ? tenantId.Value : FallbackTenantId;
            var journeys = await runtimeService.GetActiveJourneys(tid, ct);
            return Results.Ok(journeys);
        });

        return app;
    }
}
