using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using SGrowing.Api.Data;
using SGrowing.Core.Entities;

namespace SGrowing.Api.Modules.Journeys;

public record CreateJourneyStepInput(
    int StepOrder,
    JourneyStepType StepType,
    string BodyTemplate,
    string? SubjectTemplate = null,
    int DelaySeconds = 0
);

public record CreateCustomerJourneyDto(
    Guid TenantId,
    string Name,
    string TriggerEvent,
    List<CreateJourneyStepInput> Steps
);

public record SimulateJourneyEventDto(
    Guid TenantId,
    Guid ContactId,
    string EventName,
    string PayloadJson = "{}"
);

public static class JourneyEndpoints
{
    public static IEndpointRouteBuilder MapJourneyEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/journeys").WithTags("Event-Driven Customer Journeys & Automation");

        group.MapGet("", async ([FromQuery] Guid tenantId, SGrowingDbContext db) =>
        {
            var journeys = await db.CustomerJourneys
                .Where(j => j.TenantId == tenantId)
                .OrderByDescending(j => j.CreatedAtUtc)
                .ToListAsync();

            var journeyIds = journeys.Select(j => j.Id).ToList();
            var steps = await db.JourneySteps
                .Where(s => s.TenantId == tenantId && journeyIds.Contains(s.JourneyId))
                .OrderBy(s => s.StepOrder)
                .ToListAsync();

            var result = journeys.Select(j => new
            {
                j.Id,
                j.TenantId,
                j.Name,
                j.TriggerEvent,
                j.Status,
                j.CreatedAtUtc,
                Steps = steps.Where(s => s.JourneyId == j.Id).ToList()
            });

            return Results.Ok(result);
        });

        group.MapPost("", async ([FromBody] CreateCustomerJourneyDto req, SGrowingDbContext db) =>
        {
            var journey = CustomerJourney.Create(req.TenantId, req.Name, req.TriggerEvent);
            db.CustomerJourneys.Add(journey);

            if (req.Steps != null && req.Steps.Count > 0)
            {
                foreach (var stepInput in req.Steps)
                {
                    var step = JourneyStep.Create(
                        req.TenantId,
                        journey.Id,
                        stepInput.StepOrder,
                        stepInput.StepType,
                        stepInput.BodyTemplate,
                        stepInput.SubjectTemplate,
                        stepInput.DelaySeconds
                    );
                    db.JourneySteps.Add(step);
                }
            }

            await db.SaveChangesAsync();
            return Results.Ok(journey);
        });

        group.MapGet("/{id:guid}/steps", async (Guid id, [FromQuery] Guid tenantId, SGrowingDbContext db) =>
        {
            var steps = await db.JourneySteps
                .Where(s => s.TenantId == tenantId && s.JourneyId == id)
                .OrderBy(s => s.StepOrder)
                .ToListAsync();

            return Results.Ok(steps);
        });

        group.MapPost("/{id:guid}/toggle", async (Guid id, [FromQuery] Guid tenantId, SGrowingDbContext db) =>
        {
            var journey = await db.CustomerJourneys
                .FirstOrDefaultAsync(j => j.TenantId == tenantId && j.Id == id);

            if (journey == null) return Results.NotFound(new { error = "Journey not found." });

            journey.Status = journey.Status == JourneyStatus.Active ? JourneyStatus.Paused : JourneyStatus.Active;
            await db.SaveChangesAsync();
            return Results.Ok(new { status = journey.Status.ToString(), journey });
        });

        group.MapPost("/simulate", async ([FromBody] SimulateJourneyEventDto req, JourneyExecutionService service) =>
        {
            var ingestResult = await service.IngestEventAsync(req.TenantId, req.ContactId, req.EventName, req.PayloadJson);
            if (!ingestResult.IsSuccess)
            {
                return Results.BadRequest(new { error = ingestResult.Error });
            }

            // Immediately process steps that have zero delay
            var processedCount = await service.ProcessDueStepsAsync(req.TenantId);

            return Results.Ok(new
            {
                eventIngested = ingestResult.Value,
                processedImmediateSteps = processedCount,
                message = "Event successfully ingested and matching journeys triggered."
            });
        });

        return endpoints;
    }
}
