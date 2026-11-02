using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Aerospace.ScramjetThermalProtection;

public static class ScramjetThermalEndpoints
{
    public static IServiceCollection AddScramjetThermalProtectionServices(this IServiceCollection services)
    {
        services.AddSingleton<IScramjetThermalProtectionEngine, ScramjetThermalProtectionEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapScramjetThermalProtectionEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/aerospace/hypersonic")
            .WithTags("Aerospace - Scramjet Thermal Protection Mesh");

        group.MapPost("/evaluate-thermal-protection", (
            ScramjetThermalEvaluationRequest request,
            IScramjetThermalProtectionEngine engine) =>
        {
            var plan = engine.EvaluateThermalProtection(request);
            return Results.Ok(plan);
        })
        .WithName("EvaluateHypersonicThermalProtection")
        .WithSummary("Evaluates boundary layer stagnation aerothermal heating and adjusts regenerative fuel-film cooling channels for hypersonic vehicles")
        .Produces<ScramjetThermalProtectionPlan>(StatusCodes.Status200OK);

        return endpoints;
    }
}
