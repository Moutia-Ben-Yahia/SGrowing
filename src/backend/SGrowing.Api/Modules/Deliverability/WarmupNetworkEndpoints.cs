using Microsoft.AspNetCore.Mvc;

namespace SGrowing.Api.Modules.Deliverability;

public static class WarmupNetworkEndpoints
{
    public static IEndpointRouteBuilder MapWarmupNetworkEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/deliverability/warmup")
            .WithTags("Mailbox Warmup & Spam Rescue");

        group.MapPost("/peers/pair", async (
            [FromHeader(Name = "X-Tenant-Id")] Guid tenantId,
            IWarmupPeerNetworkService service) =>
        {
            var result = await service.PairMailboxesForWarmupAsync(tenantId);
            return Results.Ok(result);
        });

        group.MapPost("/spam-rescue/run", async (
            [FromHeader(Name = "X-Tenant-Id")] Guid tenantId,
            IWarmupPeerNetworkService service) =>
        {
            var result = await service.ExecuteSpamRescueSweepAsync(tenantId);
            return Results.Ok(result);
        });

        group.MapGet("/dns-diagnostics/{domain}", async (
            string domain,
            IWarmupPeerNetworkService service) =>
        {
            var result = await service.RunDnsDiagnosticsAsync(domain);
            return Results.Ok(result);
        });

        return app;
    }
}
