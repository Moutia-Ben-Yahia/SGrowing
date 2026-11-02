using Microsoft.AspNetCore.Mvc;

namespace SGrowing.Api.Modules.Finance.ZkL3WatchtowerFraudProof;

public static class ZkL3WatchtowerEndpoints
{
    public static IServiceCollection AddZkL3WatchtowerFraudProofServices(this IServiceCollection services)
    {
        services.AddScoped<IZkL3WatchtowerEngine, ZkL3WatchtowerEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapZkL3WatchtowerEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/finance/zk-l3-watchtower")
            .WithTags("Zero-Knowledge L3 Hyperchain Fraud-Proof Watchtower");

        group.MapPost("/evaluate", (
            [FromBody] L3WatchtowerEvaluationRequest request,
            [FromServices] IZkL3WatchtowerEngine engine) =>
        {
            var result = engine.VerifyProposal(request);
            return Results.Ok(result);
        })
        .WithName("VerifyL3HyperchainProposal")
        .WithSummary("Validates L3 hyperchain state transitions, KZG polynomial commitments, dispute challenge windows, and sequencer slashing.");

        group.MapGet("/status/{hyperchainId}", (string hyperchainId) =>
        {
            return Results.Ok(new
            {
                HyperchainId = hyperchainId,
                DisputeWindowBlocks = 450,
                SettlementLayer = "ARBITRUM_ONE_L2",
                WatchtowerStatus = "MONITORING_ACTIVE"
            });
        })
        .WithName("GetL3WatchtowerStatus")
        .WithSummary("Returns dispute window and monitoring status for a zero-knowledge L3 hyperchain.");

        return app;
    }
}
