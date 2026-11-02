using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Energy.UhvCoronaIonization;

public static class UhvCoronaEndpoints
{
    public static IServiceCollection AddUhvCoronaIonizationServices(this IServiceCollection services)
    {
        services.AddSingleton<IUhvCoronaIonizationEngine, UhvCoronaIonizationEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapUhvCoronaIonizationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/energy/uhv-corona")
            .WithTags("Energy - UHV AC Corona Discharge & Ionization Arbiter");

        group.MapPost("/evaluate-loss", (
            CoronaEvaluationRequest request,
            IUhvCoronaIonizationEngine engine) =>
        {
            var profile = engine.EvaluateCoronaDischarge(request);
            return Results.Ok(profile);
        })
        .WithName("EvaluateUhvCoronaDischarge")
        .WithSummary("Models 1100kV AC Peek dielectric breakdown, atmospheric density, and predicts megawatt rain/fog corona losses")
        .Produces<GridCoronaLossProfile>(StatusCodes.Status200OK);

        return endpoints;
    }
}
