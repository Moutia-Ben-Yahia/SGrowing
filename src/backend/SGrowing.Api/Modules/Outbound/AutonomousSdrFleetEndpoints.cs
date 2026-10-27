using Microsoft.AspNetCore.Mvc;

namespace SGrowing.Api.Modules.Outbound;

public static class AutonomousSdrFleetEndpoints
{
    public static IEndpointRouteBuilder MapAutonomousSdrFleetEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/outbound/sdr")
            .WithTags("Autonomous SDR Fleet & Calling");

        group.MapPost("/inbox-rotation/allocate", async (
            [FromHeader(Name = "X-Tenant-Id")] Guid tenantId,
            [FromQuery] int volume,
            IAutonomousSdrFleetService service) =>
        {
            var result = await service.AllocateInboxSlotsAsync(tenantId, volume > 0 ? volume : 100);
            return Results.Ok(result);
        });

        group.MapPost("/calls/session", async (
            [FromHeader(Name = "X-Tenant-Id")] Guid tenantId,
            [FromBody] InitiateCallRequest request,
            IAutonomousSdrFleetService service) =>
        {
            var result = await service.StartWebRtcCallSessionAsync(tenantId, request.ProspectPhone, request.AgentPhone);
            return Results.Ok(result);
        });

        group.MapPost("/calls/disposition", async (
            [FromHeader(Name = "X-Tenant-Id")] Guid tenantId,
            [FromBody] CallDispositionDto request,
            IAutonomousSdrFleetService service) =>
        {
            var result = await service.SubmitCallDispositionAsync(tenantId, request);
            return Results.Ok(new { Success = result });
        });

        group.MapPost("/replies/classify", async (
            [FromHeader(Name = "X-Tenant-Id")] Guid tenantId,
            [FromBody] ClassifyReplyRequest request,
            IAutonomousSdrFleetService service) =>
        {
            var result = await service.ClassifyInboundReplyAsync(tenantId, request.Subject, request.Body);
            return Results.Ok(result);
        });

        return app;
    }
}

public record InitiateCallRequest(
    string ProspectPhone,
    string AgentPhone
);

public record ClassifyReplyRequest(
    string Subject,
    string Body
);
