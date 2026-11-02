namespace SGrowing.Api.Modules.BioTech.AntibodyAffinityMaturation;

public static class AntibodyAffinityEndpoints
{
    public static IServiceCollection AddAntibodyAffinityServices(this IServiceCollection services)
    {
        services.AddScoped<AntibodyAffinityMaturationEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapAntibodyAffinityEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/biotech/antibody-affinity-maturation")
            .WithTags("In-Silico Antibody Affinity Maturation Deep Mutation Scanning Arbiter");

        group.MapPost("/evaluate", (AntibodyMaturationRequest request, AntibodyAffinityMaturationEngine engine) =>
        {
            var result = engine.EvaluateCandidates(request);
            return Results.Ok(result);
        })
        .WithName("EvaluateAntibodyCandidates")
        .WithSummary("Evaluates deep mutational scanning candidates for binding affinity enhancement, thermostability, and aggregation developability liabilities.");

        return endpoints;
    }
}
