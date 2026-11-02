namespace SGrowing.Api.Modules.Finance.PqcCrossChainHtlc;

public static class PqcHtlcEndpoints
{
    public static IServiceCollection AddPqcCrossChainHtlcServices(this IServiceCollection services)
    {
        services.AddScoped<PqcCrossChainHtlcEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapPqcHtlcEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/finance/pqc-htlc")
            .WithTags("Post-Quantum Cross-Chain Atomic ZK-HTLC Arbiter");

        group.MapPost("/evaluate", (PqcCrossChainHtlcTelemetry telemetry, PqcCrossChainHtlcEngine engine) =>
        {
            var result = engine.EvaluateSwap(telemetry);
            return Results.Ok(result);
        })
        .WithName("EvaluatePqcHtlcSwap")
        .WithSummary("Evaluates post-quantum cross-chain atomic hash-time-locked contracts, SHA-3 preimage validity, and timelock safety bounds.");

        return endpoints;
    }
}
