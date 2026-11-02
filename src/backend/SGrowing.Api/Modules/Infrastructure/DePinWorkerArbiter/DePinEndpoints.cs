namespace SGrowing.Api.Modules.Infrastructure.DePinWorkerArbiter;

public static class DePinEndpoints
{
    public static IServiceCollection AddDePinServices(this IServiceCollection services)
    {
        services.AddSingleton<DePinWorkerArbiterEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapDePinEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/infrastructure/depin-worker-arbiter")
            .WithTags("DePIN Worker Arbiter");

        group.MapPost("/arbitrate", (DePinArbitrationRequest request, DePinWorkerArbiterEngine engine) =>
        {
            var result = engine.ArbitrateBatch(request);
            return Results.Ok(result);
        })
        .WithName("ArbitrateDePinWorkers")
        .WithSummary("Arbitrates proof-of-useful-work computations, slashes byzantine nodes, and disburses token rewards across decentralized edge workers");

        return app;
    }
}
