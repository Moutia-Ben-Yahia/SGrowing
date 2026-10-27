using Microsoft.AspNetCore.Mvc;

namespace SGrowing.Api.Modules.Conversations;

public static class HelpdeskCommercialContextEndpoints
{
    public static IEndpointRouteBuilder MapHelpdeskCommercialContextEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/helpdesk")
            .WithTags("Helpdesk ERP Context & Pause-on-Hold SLAs");

        group.MapGet("/tickets/{id:guid}/erp-context", async (
            [FromHeader(Name = "X-Tenant-Id")] Guid tenantId,
            Guid id,
            IHelpdeskCommercialContextService service) =>
        {
            var result = await service.GetTicketCommercialContextAsync(tenantId, id);
            return Results.Ok(result);
        });

        group.MapPost("/tickets/{id:guid}/pause-sla", async (
            [FromHeader(Name = "X-Tenant-Id")] Guid tenantId,
            Guid id,
            [FromBody] PauseSlaRequest request,
            IHelpdeskCommercialContextService service) =>
        {
            var result = await service.SetTicketSlaPauseStateAsync(tenantId, id, request.Pause, request.Reason);
            return Results.Ok(result);
        });

        return app;
    }
}

public record PauseSlaRequest(
    bool Pause,
    string Reason
);
