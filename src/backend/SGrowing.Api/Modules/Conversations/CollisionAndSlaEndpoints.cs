using Microsoft.AspNetCore.Mvc;

namespace SGrowing.Api.Modules.Conversations;

public static class CollisionAndSlaEndpoints
{
    public static IEndpointRouteBuilder MapCollisionAndSlaEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/conversations")
            .WithTags("Collision Detection, SLAs & CSAT");

        group.MapPost("/{id:guid}/presence", async (
            [FromHeader(Name = "X-Tenant-Id")] Guid tenantId,
            Guid id,
            [FromBody] UpdatePresenceRequest request,
            ICollisionDetectionAndSlaService service) =>
        {
            var result = await service.UpdatePresenceAsync(
                tenantId,
                id,
                request.OperatorId,
                request.OperatorName,
                request.IsTyping
            );
            return Results.Ok(result);
        });

        group.MapGet("/{id:guid}/presence", async (
            [FromHeader(Name = "X-Tenant-Id")] Guid tenantId,
            Guid id,
            ICollisionDetectionAndSlaService service) =>
        {
            var result = await service.GetActiveViewersAsync(tenantId, id);
            return Results.Ok(result);
        });

        group.MapGet("/sla/breaches", async (
            [FromHeader(Name = "X-Tenant-Id")] Guid tenantId,
            ICollisionDetectionAndSlaService service) =>
        {
            var result = await service.EvaluateSlaBreachesAsync(tenantId);
            return Results.Ok(result);
        });

        group.MapPost("/{id:guid}/csat", async (
            [FromHeader(Name = "X-Tenant-Id")] Guid tenantId,
            Guid id,
            [FromBody] SubmitCsatRequest request,
            ICollisionDetectionAndSlaService service) =>
        {
            var result = await service.RecordCsatFeedbackAsync(
                tenantId,
                id,
                request.RatingScore,
                request.Comment
            );
            return Results.Ok(result);
        });

        return app;
    }
}

public record UpdatePresenceRequest(
    string OperatorId,
    string OperatorName,
    bool IsTyping
);

public record SubmitCsatRequest(
    int RatingScore,
    string? Comment
);
