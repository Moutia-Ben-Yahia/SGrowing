using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Finance.ZkSolvencyMerkleSumAudit;

public static class ZkSolvencyAuditEndpoints
{
    public static IServiceCollection AddZkSolvencyAuditServices(this IServiceCollection services)
    {
        services.AddScoped<IZkSolvencyMerkleSumAuditEngine, ZkSolvencyMerkleSumAuditEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapZkSolvencyAuditEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/finance/zk-solvency-audit")
            .WithTags("Finance - Zero-Knowledge Proof-of-Solvency Merkle-Sum Tree");

        group.MapPost("/verify-solvency", (
            ZkSolvencyTelemetry telemetry,
            IZkSolvencyMerkleSumAuditEngine engine) =>
        {
            var result = engine.AnalyzeProofOfSolvency(telemetry);
            return Results.Ok(result);
        })
        .WithName("VerifySolvencyProof")
        .WithSummary("Verifies privacy-preserving zero-knowledge proof of solvency (PoS), Merkle-sum tree liabilities, and on-chain reserve surplus.");

        return app;
    }
}
