using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using SGrowing.Core.Entities;

namespace SGrowing.Api.Modules.Workforce.Timesheets;

public static class TimesheetEndpoints
{
    private static readonly Guid DefaultTenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    private static Guid ResolveTenantId(Guid? tenantId, Guid? headerTenantId)
    {
        if (tenantId.HasValue && tenantId.Value != Guid.Empty) return tenantId.Value;
        if (headerTenantId.HasValue && headerTenantId.Value != Guid.Empty) return headerTenantId.Value;
        return DefaultTenantId;
    }

    public static IEndpointRouteBuilder MapTimesheetEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/workforce/timesheets")
            .WithTags("Workforce Timesheets & Margin Labor Costing");

        group.MapPost("/", async (
            [FromBody] LogTimecardRequest req,
            [FromHeader(Name = "X-Tenant-Id")] Guid? headerTenantId,
            [FromQuery] Guid? tenantId,
            ITimesheetService service,
            CancellationToken ct) =>
        {
            var effectiveTenantId = ResolveTenantId(tenantId, headerTenantId);
            var entry = await service.LogTimecardAsync(effectiveTenantId, req, ct);
            return Results.Ok(entry);
        })
        .WithName("LogTimecard")
        .WithSummary("Log employee work hours against tasks and CRM deals");

        group.MapGet("/", async (
            [FromQuery] Guid? dealId,
            [FromQuery] Guid? teamMemberId,
            [FromQuery] string? status,
            [FromHeader(Name = "X-Tenant-Id")] Guid? headerTenantId,
            [FromQuery] Guid? tenantId,
            ITimesheetService service,
            CancellationToken ct) =>
        {
            var effectiveTenantId = ResolveTenantId(tenantId, headerTenantId);
            var entries = await service.GetTimesheetsAsync(effectiveTenantId, dealId, teamMemberId, status, ct);
            return Results.Ok(entries);
        })
        .WithName("GetTimesheets")
        .WithSummary("List timesheet records filtered by deal or team member");

        group.MapPost("/{id:guid}/approve", async (
            Guid id,
            [FromBody] ApproveTimecardRequest req,
            [FromHeader(Name = "X-Tenant-Id")] Guid? headerTenantId,
            [FromQuery] Guid? tenantId,
            ITimesheetService service,
            CancellationToken ct) =>
        {
            var effectiveTenantId = ResolveTenantId(tenantId, headerTenantId);
            var entry = await service.ApproveTimecardAsync(effectiveTenantId, id, req, ct);
            if (entry == null)
            {
                return Results.NotFound(new { error = "Timesheet entry not found" });
            }
            return Results.Ok(entry);
        })
        .WithName("ApproveTimecard")
        .WithSummary("Approve timesheet and roll labor cost into True Gross Margin Ledger");

        group.MapGet("/summary", async (
            [FromQuery] Guid? dealId,
            [FromHeader(Name = "X-Tenant-Id")] Guid? headerTenantId,
            [FromQuery] Guid? tenantId,
            ITimesheetService service,
            CancellationToken ct) =>
        {
            var effectiveTenantId = ResolveTenantId(tenantId, headerTenantId);
            var summary = await service.GetTimesheetSummaryAsync(effectiveTenantId, dealId, ct);
            return Results.Ok(summary);
        })
        .WithName("GetTimesheetSummary")
        .WithSummary("Get aggregated labor hours and costs summary");

        return app;
    }
}
