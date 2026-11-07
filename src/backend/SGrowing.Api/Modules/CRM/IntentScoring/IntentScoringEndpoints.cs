using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using SGrowing.Api.Common;

namespace SGrowing.Api.Modules.CRM.IntentScoring;

/// <summary>
/// Minimal API endpoints for Predictive Buying Intent & Multi-Signal Lead Scoring Engine.
/// Implements <see cref="IEndpointModule"/> for automatic route discovery.
/// </summary>
public class IntentScoringEndpoints : IEndpointModule
{
    private static readonly IIntentScoringService FallbackService = new IntentScoringService();

    public IntentScoringEndpoints()
    {
    }

    public void MapEndpoints(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/crm/intent-scoring")
            .WithTags("CRM - Predictive Buying Intent & Multi-Signal Lead Scoring");

        // 1. GET / -> List all scored leads for tenant
        group.MapGet("/", (
            [FromQuery] Guid? tenantId,
            IServiceProvider sp,
            [FromServices] IIntentScoringService? service) =>
        {
            var s = ResolveService(sp, service);
            var tid = tenantId ?? IntentScoringService.DefaultTenantId;
            var leads = s.GetLeads(tid);
            return Results.Ok(leads);
        })
        .WithName("GetScoredLeads")
        .WithSummary("Lists all leads evaluated with multi-signal decay scoring, ICP fit grades, and buying stages.");

        // 2. GET /overview -> Executive radar and pipeline intent summary
        group.MapGet("/overview", (
            [FromQuery] Guid? tenantId,
            IServiceProvider sp,
            [FromServices] IIntentScoringService? service) =>
        {
            var s = ResolveService(sp, service);
            var tid = tenantId ?? IntentScoringService.DefaultTenantId;
            var overview = s.GetOverview(tid);
            return Results.Ok(overview);
        })
        .WithName("GetIntentScoringOverview")
        .WithSummary("Retrieves radar metrics, ICP grade breakdown, SQL/MQL counts, and top surging accounts.");

        // 3. GET /config -> Active scoring configuration
        group.MapGet("/config", (
            [FromQuery] Guid? tenantId,
            IServiceProvider sp,
            [FromServices] IIntentScoringService? service) =>
        {
            var s = ResolveService(sp, service);
            var tid = tenantId ?? IntentScoringService.DefaultTenantId;
            var config = s.GetConfig(tid);
            return Results.Ok(config);
        })
        .WithName("GetIntentScoringConfig")
        .WithSummary("Retrieves tenant-level signal weights, half-life decay days, and qualification thresholds.");

        // 4. PUT /config -> Update scoring configuration & trigger recalculation
        group.MapPut("/config", (
            [FromBody] UpdateIntentConfigDto dto,
            [FromQuery] Guid? tenantId,
            IServiceProvider sp,
            [FromServices] IIntentScoringService? service) =>
        {
            var s = ResolveService(sp, service);
            var tid = tenantId ?? IntentScoringService.DefaultTenantId;
            var updated = s.UpdateConfig(tid, dto);
            return Results.Ok(updated);
        })
        .WithName("UpdateIntentScoringConfig")
        .WithSummary("Updates signal weights and half-life parameters, automatically re-evaluating tenant leads.");

        // 5. GET /{leadId:guid} -> Single lead details
        group.MapGet("/{leadId:guid}", (
            Guid leadId,
            [FromQuery] Guid? tenantId,
            IServiceProvider sp,
            [FromServices] IIntentScoringService? service) =>
        {
            var s = ResolveService(sp, service);
            var tid = tenantId ?? IntentScoringService.DefaultTenantId;
            var lead = s.GetLeadById(leadId, tid);
            return lead is not null
                ? Results.Ok(lead)
                : Results.NotFound(new { error = $"Scored lead with ID '{leadId}' was not found." });
        })
        .WithName("GetScoredLeadById")
        .WithSummary("Retrieves detailed score breakdown, active signals, ICP fit, and next action recommendations for a lead.");

        // 6. POST /signals -> Ingest intent signal
        group.MapPost("/signals", (
            [FromBody] IngestSignalRequest request,
            IServiceProvider sp,
            [FromServices] IIntentScoringService? service) =>
        {
            var s = ResolveService(sp, service);
            try
            {
                var evaluated = s.IngestSignal(request);
                return Results.Ok(evaluated);
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
        })
        .WithName("IngestIntentSignal")
        .WithSummary("Ingests a first-party behavioral signal or third-party surge signal, instantly re-evaluating the lead.");

        // 7. POST /leads -> Upsert lead
        group.MapPost("/leads", (
            [FromBody] CreateOrUpdateLeadIntentRequest request,
            IServiceProvider sp,
            [FromServices] IIntentScoringService? service) =>
        {
            var s = ResolveService(sp, service);
            var evaluated = s.CreateOrUpdateLead(request);
            return Results.Ok(evaluated);
        })
        .WithName("UpsertLeadIntentProfile")
        .WithSummary("Creates or updates a prospect's firmographic and technographic profile and computes baseline score.");

        // 8. POST /recalculate -> Batch recalculation
        group.MapPost("/recalculate", (
            [FromBody] BatchRecalculateRequest? request,
            [FromQuery] Guid? tenantId,
            IServiceProvider sp,
            [FromServices] IIntentScoringService? service) =>
        {
            var s = ResolveService(sp, service);
            var tid = request?.TenantId ?? tenantId ?? IntentScoringService.DefaultTenantId;
            var response = s.BatchRecalculate(tid);
            return Results.Ok(response);
        })
        .WithName("BatchRecalculateIntentScores")
        .WithSummary("Recalculates scores, decays older signals, and executes automated stage transitions across all leads.");

        // 9. POST /reset -> Reset seeded state
        group.MapPost("/reset", (
            [FromQuery] Guid? tenantId,
            IServiceProvider sp,
            [FromServices] IIntentScoringService? service) =>
        {
            var s = ResolveService(sp, service);
            var tid = tenantId ?? IntentScoringService.DefaultTenantId;
            s.Reset(tid);
            return Results.Ok(new { message = $"Seeded intent scoring data reset successfully for tenant '{tid}'." });
        })
        .WithName("ResetIntentScoringData")
        .WithSummary("Resets tenant lead intent records and scoring configuration to baseline demonstration seeds.");
    }

    private static IIntentScoringService ResolveService(IServiceProvider sp, IIntentScoringService? injected)
    {
        return injected ?? sp.GetService<IIntentScoringService>() ?? FallbackService;
    }
}
