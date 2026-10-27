using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace SGrowing.Api.Modules.Deliverability;

public record GenerateWarmupThreadRequest(
    string SenderEmail,
    string PeerEmail,
    WarmupDialogueTopic Topic,
    int TurnsCount = 4,
    int? Seed = null
);

public record SeedPlacementTestRequest(
    Guid TenantId,
    string SenderEmail,
    string Subject,
    string BodyHtml
);

public static class DeliverabilityPillarEndpoints
{
    public static IEndpointRouteBuilder MapDeliverabilityPillarEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/deliverability")
            .WithTags("Deliverability & Warmup Swarm");

        group.MapPost("/warmup/threads/generate", (GenerateWarmupThreadRequest req, IWarmupThreadGeneratorService generator) =>
        {
            var thread = generator.GenerateThread(req.SenderEmail, req.PeerEmail, req.Topic, req.TurnsCount, req.Seed);
            return Results.Ok(thread);
        })
        .WithName("GenerateWarmupDialogueThread")
        .WithSummary("Generate multi-turn realistic contextual email warmup threads with threading headers and behavioral signals");

        group.MapPost("/placement-test", async (SeedPlacementTestRequest req, IPlacementTestService placementService, CancellationToken ct) =>
        {
            var report = await placementService.RunSeedPlacementTestAsync(req.TenantId, req.SenderEmail, req.Subject, req.BodyHtml, ct);
            return Results.Ok(report);
        })
        .WithName("RunEspSeedPlacementTest")
        .WithSummary("Execute inbox vs spam placement seed test across Google, Microsoft, Yahoo, Zoho, and Proton");

        return app;
    }
}
