using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace SGrowing.Api.Modules.Tenancy.SnapshotsAndRebilling;

public record CreateSnapshotRequest(
    string SnapshotName,
    string Category,
    string? Description = ""
);

public record DeploySnapshotRequest(
    Guid TargetTenantId,
    string TargetTenantName
);

public static class TenantSnapshotEndpoints
{
    public static IEndpointRouteBuilder MapTenantSnapshotEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/tenants/snapshots")
            .WithTags("TenantSnapshots");

        group.MapPost("/", async (
            Guid tenantId,
            CreateSnapshotRequest req,
            ITenantSnapshotService service) =>
        {
            var snapshot = await service.CreateSnapshotAsync(
                tenantId,
                req.SnapshotName,
                req.Category,
                req.Description ?? "");

            return Results.Ok(snapshot);
        });

        group.MapPost("/{snapshotId:guid}/deploy", async (
            Guid snapshotId,
            DeploySnapshotRequest req,
            ITenantSnapshotService service) =>
        {
            var result = await service.DeploySnapshotAsync(
                req.TargetTenantId,
                snapshotId,
                req.TargetTenantName);

            return Results.Ok(result);
        });

        group.MapGet("/", async (
            Guid tenantId,
            ITenantSnapshotService service) =>
        {
            var list = await service.ListSnapshotsAsync(tenantId);
            return Results.Ok(list);
        });

        group.MapGet("/{snapshotId:guid}", async (
            Guid snapshotId,
            ITenantSnapshotService service) =>
        {
            var snapshot = await service.GetSnapshotAsync(snapshotId);
            return snapshot != null ? Results.Ok(snapshot) : Results.NotFound();
        });

        return app;
    }
}
