using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using SGrowing.Api.Common;

namespace SGrowing.Api.Modules.Outbound.LinkedInCadence;

/// <summary>
/// Modular Minimal API endpoints for LinkedIn B2B Outreach & Social Selling Cadence Studio (Domain D4, D8 & F2).
/// Implements parameterless constructor for auto-discovery via IEndpointModule.
/// </summary>
public sealed class LinkedInCadenceEndpoints : IEndpointModule
{
    public LinkedInCadenceEndpoints() { }

    public void MapEndpoints(IEndpointRouteBuilder app) => app.MapLinkedInCadenceEndpoints();
}

public static class LinkedInCadenceEndpointExtensions
{
    public static readonly Guid DefaultTenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    public static IEndpointRouteBuilder MapLinkedInCadenceEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/outreach/linkedin")
            .WithTags("LinkedIn B2B Outreach & Social Selling Cadence Studio");

        // GET /api/v1/outreach/linkedin/cadences
        group.MapGet("/cadences", async (
            [FromQuery] Guid? tenantId,
            ILinkedInCadenceService service,
            CancellationToken ct) =>
        {
            var tid = tenantId.HasValue && tenantId.Value != Guid.Empty ? tenantId.Value : DefaultTenantId;
            var cadences = await service.GetCadencesAsync(tid, ct);
            return Results.Ok(cadences);
        })
        .WithName("GetLinkedInCadences")
        .WithSummary("Retrieves all LinkedIn outreach cadences for the authenticated tenant with active prospect metrics");

        // POST /api/v1/outreach/linkedin/cadences
        group.MapPost("/cadences", async (
            [FromBody] CreateCadenceRequest request,
            ILinkedInCadenceService service,
            CancellationToken ct) =>
        {
            if (request.TenantId == Guid.Empty)
            {
                request = request with { TenantId = DefaultTenantId };
            }

            try
            {
                var cadence = await service.CreateCadenceAsync(request, ct);
                return Results.Created($"/api/v1/outreach/linkedin/cadences/{cadence.CadenceId}", cadence);
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        })
        .WithName("CreateLinkedInCadence")
        .WithSummary("Creates a new multi-touch LinkedIn social selling cadence with anti-bot action quotas");

        // GET /api/v1/outreach/linkedin/cadences/{id}/steps
        group.MapGet("/cadences/{id:guid}/steps", async (
            Guid id,
            [FromQuery] Guid? tenantId,
            ILinkedInCadenceService service,
            CancellationToken ct) =>
        {
            var tid = tenantId.HasValue && tenantId.Value != Guid.Empty ? tenantId.Value : DefaultTenantId;
            var steps = await service.GetCadenceStepsAsync(tid, id, ct);
            return Results.Ok(steps);
        })
        .WithName("GetLinkedInCadenceSteps")
        .WithSummary("Retrieves multi-touch sequence steps and prospect engagements for a specific cadence");

        // POST /api/v1/outreach/linkedin/steps/{id}/execute
        group.MapPost("/steps/{id:guid}/execute", async (
            Guid id,
            [FromBody] ExecuteCadenceStepRequest request,
            ILinkedInCadenceService service,
            CancellationToken ct) =>
        {
            if (request.TenantId == Guid.Empty)
            {
                request = request with { TenantId = DefaultTenantId };
            }

            if (request.StepId == Guid.Empty)
            {
                request = request with { StepId = id };
            }

            try
            {
                var step = await service.ExecuteStepAsync(request, ct);
                return step is not null
                    ? Results.Ok(step)
                    : Results.NotFound(new { error = $"Cadence step '{id}' not found for cadence '{request.CadenceId}'." });
            }
            catch (InvalidOperationException ex)
            {
                return Results.Problem(detail: ex.Message, statusCode: StatusCodes.Status429TooManyRequests);
            }
        })
        .WithName("ExecuteLinkedInCadenceStep")
        .WithSummary("Executes or approves a LinkedIn touchpoint adhering to anti-bot pacing rules and daily quota limits");

        // POST /api/v1/outreach/linkedin/draft-ai
        group.MapPost("/draft-ai", async (
            [FromBody] DraftMessageAiRequest request,
            ILinkedInCadenceService service,
            CancellationToken ct) =>
        {
            if (request.TenantId == Guid.Empty)
            {
                request = request with { TenantId = DefaultTenantId };
            }

            var draft = await service.DraftAiMessageAsync(request, ct);
            return Results.Ok(draft);
        })
        .WithName("DraftLinkedInMessageAi")
        .WithSummary("Drafts highly personalized bilingual LinkedIn messages tailored for GCC enterprise decision-makers");

        // GET /api/v1/outreach/linkedin/overview
        group.MapGet("/overview", async (
            [FromQuery] Guid? tenantId,
            ILinkedInCadenceService service,
            CancellationToken ct) =>
        {
            var tid = tenantId.HasValue && tenantId.Value != Guid.Empty ? tenantId.Value : DefaultTenantId;
            var report = await service.GetOverviewReportAsync(tid, ct);
            return Results.Ok(report);
        })
        .WithName("GetLinkedInCadenceOverview")
        .WithSummary("Retrieves aggregate LinkedIn cadence KPIs, anti-bot pacing queue status, and recent touch activity");

        return app;
    }
}
