namespace SGrowing.Api.Modules.BioTech.MetabolicFluxOptimizer;

public static class MetabolicFluxEndpoints
{
    public static IServiceCollection AddMetabolicFluxServices(this IServiceCollection services)
    {
        services.AddSingleton<MetabolicFluxOptimizerEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapMetabolicFluxEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/biotech/metabolic-flux-optimizer")
            .WithTags("Metabolic Flux Optimizer");

        group.MapPost("/optimize-fed-batch", (FedBatchOptimizationRequest request, MetabolicFluxOptimizerEngine engine) =>
        {
            var result = engine.OptimizeFedBatch(request);
            return Results.Ok(result);
        })
        .WithName("OptimizeFedBatchCulture")
        .WithSummary("Optimizes fed-batch substrate feeding rate, oxygen enrichment, and Monod growth kinetics for industrial synthetic biology bioreactors");

        return app;
    }
}
