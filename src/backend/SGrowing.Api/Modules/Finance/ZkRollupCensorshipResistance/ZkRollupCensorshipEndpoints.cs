using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Finance.ZkRollupCensorshipResistance;

public static class ZkRollupCensorshipEndpoints
{
    public static IServiceCollection AddZkRollupCensorshipServices(this IServiceCollection services)
    {
        services.AddScoped<IZkRollupCensorshipResistanceEngine, ZkRollupCensorshipResistanceEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapZkRollupCensorshipEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/finance/zk-rollup-censorship")
            .WithTags("Finance - zk-Rollup Sequencer Censorship Resistance");

        group.MapPost("/analyze-sequencer", (
            ZkRollupCensorshipTelemetry telemetry,
            IZkRollupCensorshipResistanceEngine engine) =>
        {
            var result = engine.AnalyzeRollupSequencerState(telemetry);
            return Results.Ok(result);
        })
        .WithName("AnalyzeRollupSequencer")
        .WithSummary("Analyzes multi-prover zk-rollup sequencer censorship, forced L1 inclusion queue, and emergency escape hatch slashing.");

        return app;
    }
}
