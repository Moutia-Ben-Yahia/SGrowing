using Microsoft.AspNetCore.Mvc;

namespace SGrowing.Api.Modules.Finance.ZkStateRelayAggregator;

public static class ZkStateRelayEndpoints
{
    public static IServiceCollection AddZkStateRelayAggregatorServices(this IServiceCollection services)
    {
        services.AddScoped<IZkStateRelayAggregatorEngine, ZkStateRelayAggregatorEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapZkStateRelayEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/finance/zk-state-relay")
            .WithTags("Zero-Knowledge State Relay & Rollup Aggregation");

        group.MapPost("/aggregate", (
            [FromBody] ZkStateRelayAggregationRequest request,
            [FromServices] IZkStateRelayAggregatorEngine engine) =>
        {
            var result = engine.AggregateProofs(request);
            return Results.Ok(result);
        })
        .WithName("AggregateZkRollupProofs")
        .WithSummary("Aggregates multiple zk-Rollup sub-proofs into a single recursive outer proof with calldata compression.");

        group.MapGet("/status/{relayId}", (string relayId) =>
        {
            return Results.Ok(new
            {
                RelaySessionId = relayId,
                Status = "RELAY_BRIDGE_STANDBY",
                SupportedProofSystems = new[] { "Halo2_KZG", "Groth16_BN254", "Plonky2" },
                TargetChain = "ETHEREUM_MAINNET"
            });
        })
        .WithName("GetZkRelayStatus")
        .WithSummary("Returns supported proof systems and operational status for the ZK cross-chain state relay bridge.");

        return app;
    }
}
