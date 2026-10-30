using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace SGrowing.Api.Modules.CRM;

public static class LeadEnrichmentEndpoints
{
    public static IEndpointRouteBuilder MapLeadEnrichmentEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/crm/leads/enrichment")
            .WithTags("Lead Enrichment & Intent Radar (Horizon 35)");

        // 1. GET / -> List all enriched leads
        group.MapGet("/", async (
            [FromServices] ILeadEnrichmentRadarService service,
            CancellationToken ct) =>
        {
            var leads = await service.GetAllLeadsAsync(ct);
            return Results.Ok(leads);
        })
        .WithName("GetAllEnrichedLeads")
        .WithSummary("List all enriched leads with firmographic, technographic, and deliverability radar data");

        // 2. GET /{id:guid} -> Get lead details
        group.MapGet("/{id:guid}", async (
            Guid id,
            [FromServices] ILeadEnrichmentRadarService service,
            CancellationToken ct) =>
        {
            var lead = await service.GetLeadByIdAsync(id, ct);
            return lead is not null
                ? Results.Ok(lead)
                : Results.NotFound(new { error = $"Enriched lead with ID '{id}' was not found." });
        })
        .WithName("GetEnrichedLeadById")
        .WithSummary("Retrieve details, score breakdown, and DNS verification for a specific lead");

        // 3. POST /enrich -> Enrich a new lead email and company name
        group.MapPost("/enrich", async (
            [FromBody] EnrichLeadRequest request,
            [FromServices] ILeadEnrichmentRadarService service,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(request.Email))
            {
                return Results.BadRequest(new { error = "Email address is required for lead enrichment." });
            }

            var enriched = await service.EnrichLeadAsync(request, ct);
            return Results.Created($"/api/v1/crm/leads/enrichment/{enriched.Id}", enriched);
        })
        .WithName("EnrichLead")
        .WithSummary("Enrich a lead with DNS/MX verification, firmographics, technographics, composite scoring, and auto-routing");

        // 4. POST /verify-email -> Run standalone DNS MX & deliverability verification
        group.MapPost("/verify-email", async (
            [FromBody] VerifyEmailDto request,
            [FromServices] ILeadEnrichmentRadarService service,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(request.Email))
            {
                return Results.BadRequest(new { error = "Email address is required for verification." });
            }

            var verification = await service.VerifyEmailAsync(request.Email, ct);
            return Results.Ok(verification);
        })
        .WithName("VerifyEmailDeliverability")
        .WithSummary("Run standalone DNS MX, SPF/DKIM/DMARC simulation and disposable blacklist verification");

        // 5. POST /recalculate -> Batch recalculate composite scores
        group.MapPost("/recalculate", async (
            [FromServices] ILeadEnrichmentRadarService service,
            CancellationToken ct) =>
        {
            var result = await service.RecalculateAllScoresAsync(ct);
            return Results.Ok(result);
        })
        .WithName("BatchRecalculateLeadScores")
        .WithSummary("Batch re-evaluate composite lead scores, qualification thresholds, and sales rep assignments");

        // 6. GET /metrics -> Get KPI metrics summary
        group.MapGet("/metrics", async (
            [FromServices] ILeadEnrichmentRadarService service,
            CancellationToken ct) =>
        {
            var metrics = await service.GetKpiMetricsAsync(ct);
            return Results.Ok(metrics);
        })
        .WithName("GetLeadEnrichmentMetrics")
        .WithSummary("Get aggregate KPI metrics for lead enrichment, SQL rate, and deliverability pass rate");

        // 7. DELETE /{id:guid} -> Delete a lead
        group.MapDelete("/{id:guid}", async (
            Guid id,
            [FromServices] ILeadEnrichmentRadarService service,
            CancellationToken ct) =>
        {
            bool removed = await service.DeleteLeadAsync(id, ct);
            return removed
                ? Results.Ok(new { success = true, message = $"Lead {id} deleted." })
                : Results.NotFound(new { error = $"Lead {id} not found." });
        })
        .WithName("DeleteEnrichedLead")
        .WithSummary("Remove an enriched lead from the radar registry");

        return app;
    }
}

public record VerifyEmailDto(string Email);
