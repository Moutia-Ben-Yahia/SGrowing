using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Physics.ColdAtomGravimeterInterferometry;

public static class ColdAtomGravimeterEndpoints
{
    public static IServiceCollection AddColdAtomGravimeterServices(this IServiceCollection services)
    {
        services.AddScoped<IColdAtomGravimeterInterferometryEngine, ColdAtomGravimeterInterferometryEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapColdAtomGravimeterEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/physics/cold-atom-gravimeter")
            .WithTags("Physics - Cold Atom BEC Gravimeter Atom Interferometry");

        group.MapPost("/analyze-gravimeter", (
            ColdAtomGravimeterTelemetry telemetry,
            IColdAtomGravimeterInterferometryEngine engine) =>
        {
            var result = engine.AnalyzeGravimeter(telemetry);
            return Results.Ok(result);
        })
        .WithName("AnalyzeColdAtomGravimeter")
        .WithSummary("Analyzes Bose-Einstein condensate cold atom gravimetry, Raman interferometry phase shift, Doppler chirp matching, and microGal precision.");

        return app;
    }
}
