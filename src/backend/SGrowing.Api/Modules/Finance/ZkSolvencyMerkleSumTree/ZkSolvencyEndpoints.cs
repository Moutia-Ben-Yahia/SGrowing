namespace SGrowing.Api.Modules.Finance.ZkSolvencyMerkleSumTree;

public static class ZkSolvencyEndpoints
{
    public static IServiceCollection AddZkSolvencyServices(this IServiceCollection services)
    {
        services.AddScoped<ZkSolvencyMerkleSumTreeEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapZkSolvencyEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/finance/zk-solvency")
            .WithTags("Zero-Knowledge Proof-of-Solvency Merkle-Sum-Tree Arbiter");

        group.MapPost("/audit", (SolvencyAuditRequest request, ZkSolvencyMerkleSumTreeEngine engine) =>
        {
            var result = engine.AuditExchangeSolvency(request);
            return Results.Ok(result);
        })
        .WithName("AuditExchangeSolvency")
        .WithSummary("Audits digital asset exchange full-reserve solvency using cryptographic Merkle-Sum-Trees and Zero-Knowledge proofs without disclosing user balances.");

        return endpoints;
    }
}
