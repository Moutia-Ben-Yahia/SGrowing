using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Security.ZkRollupBridge;

public static class ZkRollupEndpoints
{
    public static IServiceCollection AddZkRollupBridgeServices(this IServiceCollection services)
    {
        services.AddSingleton<ZkRollupBatchAggregatorEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapZkRollupBridgeEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/security/zk-rollup-bridge")
            .WithTags("Autonomous Zero-Knowledge Proof Rollup Aggregator & Merkle State Transition Bridge");

        group.MapPost("/process-batch", (
            ProcessBatchRequest request,
            ZkRollupBatchAggregatorEngine engine) =>
        {
            var response = engine.ProcessBatch(request);
            return Results.Ok(response);
        });

        group.MapPost("/verify-proof", (
            MerkleInclusionProof proof,
            ZkRollupBatchAggregatorEngine engine) =>
        {
            var isValid = engine.VerifyInclusionProof(proof);
            return Results.Ok(new { IsValid = isValid });
        });

        return app;
    }
}
