using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace SGrowing.Api.Modules.Outbound;

public record LogCallOutcomeDto(
    Guid TenantId,
    Guid ContactId,
    string Outcome,
    int DurationSeconds,
    string Notes = "",
    DateTime? CallbackScheduledAtUtc = null
);

public static class PowerDialerEndpoints
{
    public static IEndpointRouteBuilder MapPowerDialerEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/sdr/dialer").WithTags("Autonomous SDR Power Dialer");

        group.MapGet("/queue", async ([FromQuery] Guid tenantId, IPowerDialerService dialerService) =>
        {
            var queue = await dialerService.GetDialerQueueAsync(tenantId);
            return Results.Ok(queue);
        });

        group.MapPost("/call-outcome", async ([FromBody] LogCallOutcomeDto req, IPowerDialerService dialerService) =>
        {
            try
            {
                var result = await dialerService.LogCallOutcomeAsync(
                    req.TenantId,
                    req.ContactId,
                    req.Outcome,
                    req.DurationSeconds,
                    req.Notes,
                    req.CallbackScheduledAtUtc
                );
                return Results.Ok(result);
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        return endpoints;
    }
}
