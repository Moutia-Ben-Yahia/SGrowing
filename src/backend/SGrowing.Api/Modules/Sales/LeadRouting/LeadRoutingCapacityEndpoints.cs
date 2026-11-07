using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using SGrowing.Api.Common;

namespace SGrowing.Api.Modules.Sales.LeadRouting;

public class LeadRoutingCapacityEndpoints : IEndpointModule
{
    public void MapEndpoints(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/sales/lead-routing")
            .WithTags("Sales Lead Routing & SDR Capacity Balancer");

        group.MapPost("/route", ([FromBody] RouteLeadRequest request, [FromServices] ILeadRoutingCapacityService service) =>
        {
            try
            {
                var result = service.RouteLead(request);
                return Results.Ok(result);
            }
            catch (InvalidOperationException ex)
            {
                return Results.Conflict(new { error = ex.Message });
            }
        })
        .WithName("RouteInboundLead")
        .WithSummary("Autonomously routes an inbound lead to the optimal online SDR by territory, skill, and weighted capacity.");

        group.MapGet("/overview", ([FromQuery] Guid? tenantId, [FromServices] ILeadRoutingCapacityService service) =>
        {
            var tid = tenantId ?? LeadRoutingCapacityService.DefaultTenantId;
            var overview = service.GetOverview(tid);
            return Results.Ok(overview);
        })
        .WithName("GetLeadRoutingOverview")
        .WithSummary("Retrieves the live SDR roster, capacity meters, and real-time SLA radar summary.");

        group.MapPut("/sdrs/{sdrId:guid}/status", (Guid sdrId, [FromBody] UpdateSdrStatusRequest request, [FromServices] ILeadRoutingCapacityService service) =>
        {
            var targetRequest = request.SdrId == Guid.Empty ? request with { SdrId = sdrId } : request;
            var updated = service.UpdateSdrStatus(targetRequest);
            return updated is not null ? Results.Ok(updated) : Results.NotFound(new { error = "SDR profile not found." });
        })
        .WithName("UpdateSdrAvailabilityStatus")
        .WithSummary("Updates an SDR rep availability status (Online, Busy, Away, Offline) and max capacity.");

        group.MapPost("/assignments/{assignmentId:guid}/contact", (Guid assignmentId, [FromBody] LogContactAttemptRequest request, [FromServices] ILeadRoutingCapacityService service) =>
        {
            var targetRequest = request.AssignmentId == Guid.Empty ? request with { AssignmentId = assignmentId } : request;
            var updated = service.LogFirstContact(targetRequest);
            return updated is not null ? Results.Ok(updated) : Results.NotFound(new { error = "Lead assignment record not found." });
        })
        .WithName("LogFirstLeadContact")
        .WithSummary("Logs SDR first contact attempt, calculating SLA compliance (WithinSla vs Breached) and updating rep active lead load.");

        group.MapPost("/sla/check-breaches", ([FromQuery] Guid? tenantId, [FromServices] ILeadRoutingCapacityService service) =>
        {
            var tid = tenantId ?? LeadRoutingCapacityService.DefaultTenantId;
            var breaches = service.CheckAndTriggerSlaBreaches(tid);
            return Results.Ok(breaches);
        })
        .WithName("CheckAndTriggerSlaBreaches")
        .WithSummary("Evaluates uncontacted leads past SLA deadline, flags breaches, and triggers autonomous reassignment to available SDRs.");
    }
}

public static class LeadRoutingCapacityEndpointExtensions
{
    public static IServiceCollection AddLeadRoutingCapacityServices(this IServiceCollection services)
    {
        services.AddSingleton<ILeadRoutingCapacityService, LeadRoutingCapacityService>();
        return services;
    }
}
