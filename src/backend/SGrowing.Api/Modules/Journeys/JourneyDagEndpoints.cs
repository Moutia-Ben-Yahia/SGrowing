using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using SGrowing.Core.Entities;

namespace SGrowing.Api.Modules.Journeys;

public static class JourneyDagEndpoints
{
    private static readonly Guid DefaultTenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    private static Guid ResolveTenantId(Guid? tenantId, Guid? headerTenantId)
    {
        if (tenantId.HasValue && tenantId.Value != Guid.Empty) return tenantId.Value;
        if (headerTenantId.HasValue && headerTenantId.Value != Guid.Empty) return headerTenantId.Value;
        return DefaultTenantId;
    }

    public static IEndpointRouteBuilder MapJourneyDagEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/journeys/dag")
            .WithTags("Horizon 15: Event-Driven Customer Journey DAG & Stateful Wait-Until Engine");

        // 1. Validate Journey DAG
        group.MapPost("/validate-dag", (
            [FromBody] List<JourneyNode> nodes,
            IStatefulJourneyEngineService service) =>
        {
            var result = service.ValidateDag(nodes);
            return result.IsValid ? Results.Ok(result) : Results.BadRequest(result);
        })
        .WithName("ValidateJourneyDag")
        .WithSummary("Validate a Visual Journey DAG for cycles, disconnected targets, and valid entrypoints");

        // 2. Create or Update Journey DAG
        group.MapPost("", async (
            [FromBody] SaveJourneyDefinitionRequest req,
            [FromHeader(Name = "X-Tenant-Id")] Guid? headerTenantId,
            [FromQuery] Guid? tenantId,
            IStatefulJourneyEngineService service,
            CancellationToken ct) =>
        {
            var effectiveTenantId = ResolveTenantId(tenantId, headerTenantId);
            try
            {
                var definition = await service.SaveJourneyDefinitionAsync(effectiveTenantId, req, ct);
                return Results.Ok(definition);
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        })
        .WithName("SaveJourneyDagDefinition")
        .WithSummary("Create or update a visual customer journey DAG definition");

        // 3. List Journeys
        group.MapGet("", async (
            [FromHeader(Name = "X-Tenant-Id")] Guid? headerTenantId,
            [FromQuery] Guid? tenantId,
            IStatefulJourneyEngineService service,
            CancellationToken ct) =>
        {
            var effectiveTenantId = ResolveTenantId(tenantId, headerTenantId);
            var journeys = await service.GetJourneyDefinitionsAsync(effectiveTenantId, ct);
            return Results.Ok(journeys);
        })
        .WithName("ListJourneyDagDefinitions")
        .WithSummary("List all saved visual Journey DAGs for the tenant");

        // 4. Get Journey Definition by Id
        group.MapGet("/{id:guid}", async (
            Guid id,
            [FromHeader(Name = "X-Tenant-Id")] Guid? headerTenantId,
            [FromQuery] Guid? tenantId,
            IStatefulJourneyEngineService service,
            CancellationToken ct) =>
        {
            var effectiveTenantId = ResolveTenantId(tenantId, headerTenantId);
            var journey = await service.GetJourneyDefinitionByIdAsync(effectiveTenantId, id, ct);
            if (journey == null)
            {
                return Results.NotFound(new { error = $"Journey definition '{id}' not found." });
            }
            return Results.Ok(journey);
        })
        .WithName("GetJourneyDagDefinitionById")
        .WithSummary("Retrieve a specific Journey DAG definition with all nodes and edge configurations");

        // 5. Start Journey: Enroll Contact
        group.MapPost("/{id:guid}/start", async (
            Guid id,
            [FromBody] StartJourneyRequest req,
            [FromHeader(Name = "X-Tenant-Id")] Guid? headerTenantId,
            [FromQuery] Guid? tenantId,
            IStatefulJourneyEngineService service,
            CancellationToken ct) =>
        {
            var effectiveTenantId = ResolveTenantId(tenantId, headerTenantId);
            try
            {
                var instance = await service.StartJourneyAsync(effectiveTenantId, id, req.ContactId, req.Attributes, ct);
                return Results.Ok(instance);
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        })
        .WithName("StartJourneyDagEnrollment")
        .WithSummary("Enroll a contact into a journey DAG and execute initial steps");

        // 6. Ingest Customer Behavioral Event
        group.MapPost("/events/ingest", async (
            [FromBody] IngestJourneyEventRequest req,
            [FromHeader(Name = "X-Tenant-Id")] Guid? headerTenantId,
            [FromQuery] Guid? tenantId,
            IStatefulJourneyEngineService service,
            CancellationToken ct) =>
        {
            var effectiveTenantId = ResolveTenantId(tenantId, headerTenantId);
            var result = await service.IngestEventAsync(effectiveTenantId, req.ContactId, req.EventName, req.Payload, ct);
            return Results.Ok(result);
        })
        .WithName("IngestJourneyEvent")
        .WithSummary("Ingest customer behavioral event: advances wait-until instances or triggers dynamic unenrollment");

        // 7. Sweep Wait-Until Timeouts
        group.MapPost("/sweep-timeouts", async (
            [FromHeader(Name = "X-Tenant-Id")] Guid? headerTenantId,
            [FromQuery] Guid? tenantId,
            IStatefulJourneyEngineService service,
            CancellationToken ct) =>
        {
            var effectiveTenantId = ResolveTenantId(tenantId, headerTenantId);
            var sweptCount = await service.SweepExpiredTimeoutsAsync(effectiveTenantId, ct);
            return Results.Ok(new
            {
                sweptCount,
                message = $"Successfully swept and advanced {sweptCount} expired wait-until instance(s)."
            });
        })
        .WithName("SweepJourneyTimeouts")
        .WithSummary("Evaluate and advance all active instances waiting on expired TTLs down their timeout branch");

        // 8. Get Node Metrics & Drop-Off Funnel
        group.MapGet("/{id:guid}/metrics", async (
            Guid id,
            [FromHeader(Name = "X-Tenant-Id")] Guid? headerTenantId,
            [FromQuery] Guid? tenantId,
            IStatefulJourneyEngineService service,
            CancellationToken ct) =>
        {
            var effectiveTenantId = ResolveTenantId(tenantId, headerTenantId);
            try
            {
                var metrics = await service.GetJourneyMetricsAsync(effectiveTenantId, id, ct);
                return Results.Ok(metrics);
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
        })
        .WithName("GetJourneyDagMetrics")
        .WithSummary("Retrieve per-node step execution counts, completion rate, and drop-off rate for a Journey DAG");

        // 9. Get Instances for Journey
        group.MapGet("/{id:guid}/instances", async (
            Guid id,
            [FromHeader(Name = "X-Tenant-Id")] Guid? headerTenantId,
            [FromQuery] Guid? tenantId,
            IStatefulJourneyEngineService service,
            CancellationToken ct) =>
        {
            var effectiveTenantId = ResolveTenantId(tenantId, headerTenantId);
            var instances = await service.GetJourneyInstancesAsync(effectiveTenantId, id, ct);
            return Results.Ok(instances);
        })
        .WithName("GetJourneyDagInstances")
        .WithSummary("Retrieve all enrolled instances and execution histories for a Journey DAG");

        return app;
    }
}
