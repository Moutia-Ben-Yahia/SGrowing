using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Aerospace.CryoTurbopumpCavitation;

public static class CryoTurbopumpEndpoints
{
    public static IServiceCollection AddCryoTurbopumpCavitationServices(this IServiceCollection services)
    {
        services.AddSingleton<ICryoTurbopumpCavitationEngine, CryoTurbopumpCavitationEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapCryoTurbopumpCavitationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/aerospace/turbopump")
            .WithTags("Aerospace - Cryogenic Turbopump Cavitation Arbiter");

        group.MapPost("/evaluate-cavitation", (
            CavitationEvaluationRequest request,
            ICryoTurbopumpCavitationEngine engine) =>
        {
            var directive = engine.EvaluateCavitation(request);
            return Results.Ok(directive);
        })
        .WithName("EvaluateTurbopumpCavitation")
        .WithSummary("Monitors cryogenic LOX/CH4 inducer Net Positive Suction Head (NPSH), detects cavitation, and commands autogenous pressurization")
        .Produces<TurbopumpThrottleDirective>(StatusCodes.Status200OK);

        return endpoints;
    }
}
