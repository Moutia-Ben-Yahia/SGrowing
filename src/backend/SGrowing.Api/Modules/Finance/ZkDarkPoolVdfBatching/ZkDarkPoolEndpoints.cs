using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Finance.ZkDarkPoolVdfBatching;

public static class ZkDarkPoolEndpoints
{
    public static IServiceCollection AddZkDarkPoolServices(this IServiceCollection services)
    {
        services.AddScoped<IZkDarkPoolVdfBatchingEngine, ZkDarkPoolVdfBatchingEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapZkDarkPoolEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/finance/zk-darkpool")
            .WithTags("Finance - ZK Dark Pool VDF Batching");

        group.MapPost("/analyze-batch", (
            DarkPoolBatchTelemetry telemetry,
            IZkDarkPoolVdfBatchingEngine engine) =>
        {
            var result = engine.AnalyzeBatchSettlement(telemetry);
            return Results.Ok(result);
        })
        .WithName("AnalyzeDarkPoolBatch")
        .WithSummary("Clears privacy-preserving dark pool batch orders with ZK proofs and Verifiable Delay Functions.");

        return app;
    }
}
