namespace SGrowing.Api.Modules.Manufacturing.HighNaEuvStochasticDefects;

public static class HighNaEuvEndpoints
{
    public static IServiceCollection AddHighNaEuvServices(this IServiceCollection services)
    {
        services.AddScoped<HighNaEuvStochasticDefectsEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapHighNaEuvEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/manufacturing/high-na-euv")
            .WithTags("High-NA EUV Stochastic Defects & Flare Arbiter");

        group.MapPost("/analyze", (HighNaEuvTelemetry telemetry, HighNaEuvStochasticDefectsEngine engine) =>
        {
            var result = engine.AnalyzeWaferField(telemetry);
            return Results.Ok(result);
        })
        .WithName("AnalyzeHighNaEuvField")
        .WithSummary("Analyzes 0.55 High-NA EUV sub-2nm lithography, Rayleigh resolution, stochastic micro-bridges, and optical flare.");

        return endpoints;
    }
}
