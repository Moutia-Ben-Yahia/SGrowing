using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Sales.IntentRollup;

public static class IntentRollupEndpoints
{
    public static IServiceCollection AddIntentRollupServices(this IServiceCollection services)
    {
        services.AddSingleton<IIntentRollupEngine, IntentRollupEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapIntentRollupEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/sales/intent-rollup")
            .WithTags("Sales - Intent De-Duplication & Account Rollup");

        group.MapGet("/overview", (IIntentRollupEngine engine) =>
        {
            var overview = engine.GetOverview();
            return Results.Ok(overview);
        })
        .WithName("GetIntentRollupOverview")
        .WithSummary("Consolidated account hierarchies, aggregate intent velocity, and committee signal clusters");

        group.MapPost("/ingest-signal", (IngestIntentSignalCommand command, IIntentRollupEngine engine) =>
        {
            var result = engine.IngestIntentSignal(command);
            return Results.Ok(result);
        })
        .WithName("IngestIntentSignal")
        .WithSummary("Ingests multi-touch intent signals, executes fuzzy parent rollup, and dedupes repetitive pings");

        group.MapPost("/lock-outreach", (LockAccountOutreachCommand command, IIntentRollupEngine engine) =>
        {
            var result = engine.LockAccountOutreach(command);
            return Results.Ok(result);
        })
        .WithName("LockAccountOutreach")
        .WithSummary("Establishes rep exclusivity lock across corporate parent hierarchy to prevent buyer collision");

        return app;
    }
}
