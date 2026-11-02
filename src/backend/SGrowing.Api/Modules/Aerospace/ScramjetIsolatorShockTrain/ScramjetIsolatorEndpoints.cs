namespace SGrowing.Api.Modules.Aerospace.ScramjetIsolatorShockTrain;

public static class ScramjetIsolatorEndpoints
{
    public static IServiceCollection AddScramjetIsolatorServices(this IServiceCollection services)
    {
        services.AddScoped<ScramjetIsolatorShockTrainEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapScramjetIsolatorEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/aerospace/scramjet-isolator")
            .WithTags("Scramjet Isolator Shock-Train & Unstart Arbiter");

        group.MapPost("/analyze", (ScramjetIsolatorTelemetry telemetry, ScramjetIsolatorShockTrainEngine engine) =>
        {
            var result = engine.AnalyzeShockTrain(telemetry);
            return Results.Ok(result);
        })
        .WithName("AnalyzeScramjetIsolatorShockTrain")
        .WithSummary("Analyzes hypersonic scramjet isolator shock-train length, Waltrup-Billig pressure ratios, and inlet unstart margins.");

        return endpoints;
    }
}
