using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace SGrowing.Api.Modules.Lifecycle;

public static class CustomerLifecycleJourneyEndpoints
{
    public static IEndpointRouteBuilder MapCustomerLifecycleJourneyEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/lifecycle/journeys")
            .WithTags("Horizon 52 • Durable Customer Lifecycle Journey DAG Orchestrator & Delayed Automation");

        // 1. GET /api/v1/lifecycle/journeys
        group.MapGet("/", async (
            [FromQuery] Guid? tenantId,
            [FromQuery] string? search,
            [FromQuery] JourneyLifecycleStatus? status,
            ICustomerLifecycleJourneyService service,
            CancellationToken ct) =>
        {
            var journeys = await service.GetJourneysAsync(tenantId, search, status, ct);
            return Results.Ok(journeys);
        })
        .WithName("GetLifecycleJourneys")
        .WithSummary("List all customer lifecycle journey DAG definitions with performance metrics.");

        // 2. GET /api/v1/lifecycle/journeys/metrics
        group.MapGet("/metrics", async (
            [FromQuery] Guid? journeyId,
            ICustomerLifecycleJourneyService service,
            CancellationToken ct) =>
        {
            var metrics = await service.GetMetricsAsync(journeyId, ct);
            return Results.Ok(metrics);
        })
        .WithName("GetLifecycleJourneyMetrics")
        .WithSummary("Retrieve aggregated lifecycle journey metrics including active counts, conversion rates, and step distributions.");

        // 3. GET /api/v1/lifecycle/journeys/{id:guid}
        group.MapGet("/{id:guid}", async (
            Guid id,
            ICustomerLifecycleJourneyService service,
            CancellationToken ct) =>
        {
            var journey = await service.GetJourneyByIdAsync(id, ct);
            return journey is not null
                ? Results.Ok(journey)
                : Results.NotFound(new { error = $"Lifecycle journey '{id}' was not found." });
        })
        .WithName("GetLifecycleJourneyById")
        .WithSummary("Fetch full DAG definition, nodes, edges, and aggregated counters for a lifecycle journey.");

        // 4. POST /api/v1/lifecycle/journeys
        group.MapPost("/", async (
            [FromBody] CreateJourneyRequest request,
            ICustomerLifecycleJourneyService service,
            CancellationToken ct) =>
        {
            try
            {
                var journey = await service.CreateJourneyAsync(request, ct);
                return Results.Created($"/api/v1/lifecycle/journeys/{journey.Id}", journey);
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                return Results.Problem(detail: ex.Message, statusCode: 500);
            }
        })
        .WithName("CreateLifecycleJourney")
        .WithSummary("Create a new customer lifecycle journey with strict DAG topology and cycle validation.");

        // 5. POST /api/v1/lifecycle/journeys/{id:guid}/trigger
        group.MapPost("/{id:guid}/trigger", async (
            Guid id,
            [FromBody] TriggerContactRequest request,
            ICustomerLifecycleJourneyService service,
            CancellationToken ct) =>
        {
            try
            {
                var result = await service.TriggerJourneyAsync(id, request, ct);
                return result.Success
                    ? Results.Ok(result)
                    : Results.BadRequest(result);
            }
            catch (Exception ex)
            {
                return Results.Problem(detail: ex.Message, statusCode: 500);
            }
        })
        .WithName("TriggerLifecycleJourney")
        .WithSummary("Enroll a contact into a journey and atomically execute downstream steps until the first delay or end node.");

        // 6. POST /api/v1/lifecycle/journeys/{id:guid}/advance-due
        group.MapPost("/{id:guid}/advance-due", async (
            Guid id,
            ICustomerLifecycleJourneyService service,
            CancellationToken ct) =>
        {
            try
            {
                var result = await service.AdvanceDueContactsAsync(id, ct);
                return Results.Ok(result);
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                return Results.Problem(detail: ex.Message, statusCode: 500);
            }
        })
        .WithName("AdvanceDueLifecycleJourneyContacts")
        .WithSummary("Perform a temporal sweep advancing all contacts whose delay timer has expired.");

        // 7. GET /api/v1/lifecycle/journeys/{id:guid}/active-contacts
        group.MapGet("/{id:guid}/active-contacts", async (
            Guid id,
            [FromQuery] int? limit,
            ICustomerLifecycleJourneyService service,
            CancellationToken ct) =>
        {
            var contacts = await service.GetActiveContactsAsync(id, limit ?? 100, ct);
            return Results.Ok(contacts);
        })
        .WithName("GetLifecycleJourneyActiveContacts")
        .WithSummary("List active and completed contact pointers in this journey, including current steps and scheduled resume times.");

        // 8. GET /api/v1/lifecycle/journeys/{id:guid}/contacts/{contactId}
        group.MapGet("/{id:guid}/contacts/{contactId}", async (
            Guid id,
            string contactId,
            ICustomerLifecycleJourneyService service,
            CancellationToken ct) =>
        {
            var pointer = await service.GetContactStateAsync(id, contactId, ct);
            return pointer is not null
                ? Results.Ok(pointer)
                : Results.NotFound(new { error = $"Contact pointer '{contactId}' not found in journey '{id}'." });
        })
        .WithName("GetLifecycleJourneyContactPointer")
        .WithSummary("Retrieve detailed execution history, breadcrumbs, and delivery failover logs for a single contact.");

        // 9. POST /api/v1/lifecycle/journeys/{id:guid}/pause
        group.MapPost("/{id:guid}/pause", async (
            Guid id,
            ICustomerLifecycleJourneyService service,
            CancellationToken ct) =>
        {
            var success = await service.PauseJourneyAsync(id, ct);
            return success
                ? Results.Ok(new { success = true, journeyId = id, status = "Paused" })
                : Results.NotFound(new { error = $"Lifecycle journey '{id}' was not found." });
        })
        .WithName("PauseLifecycleJourney")
        .WithSummary("Pause a customer journey to suspend temporal sweeps and new enrollments.");

        // 10. POST /api/v1/lifecycle/journeys/{id:guid}/resume
        group.MapPost("/{id:guid}/resume", async (
            Guid id,
            ICustomerLifecycleJourneyService service,
            CancellationToken ct) =>
        {
            var success = await service.ResumeJourneyAsync(id, ct);
            return success
                ? Results.Ok(new { success = true, journeyId = id, status = "Active" })
                : Results.NotFound(new { error = $"Lifecycle journey '{id}' was not found." });
        })
        .WithName("ResumeLifecycleJourney")
        .WithSummary("Resume a paused customer journey.");

        // 11. POST /api/v1/lifecycle/journeys/{id:guid}/validate
        group.MapPost("/{id:guid}/validate", async (
            Guid id,
            ICustomerLifecycleJourneyService service,
            CancellationToken ct) =>
        {
            try
            {
                var validation = await service.ValidateJourneyDagAsync(id, ct);
                return validation.IsValid
                    ? Results.Ok(validation)
                    : Results.BadRequest(validation);
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
        })
        .WithName("ValidateLifecycleJourneyDag")
        .WithSummary("Validate DAG graph topology, check for cycles, and calculate topological sort order.");

        return app;
    }
}
