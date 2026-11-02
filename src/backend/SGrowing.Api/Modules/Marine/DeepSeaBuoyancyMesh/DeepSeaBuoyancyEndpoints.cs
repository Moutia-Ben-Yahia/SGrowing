using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Marine.DeepSeaBuoyancyMesh;

public static class DeepSeaBuoyancyEndpoints
{
    public static IServiceCollection AddDeepSeaBuoyancyMeshServices(this IServiceCollection services)
    {
        services.AddSingleton<IDeepSeaBuoyancyMeshEngine, DeepSeaBuoyancyMeshEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapDeepSeaBuoyancyMeshEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/marine/submersible")
            .WithTags("Marine - Deep-Sea Submersible Buoyancy Mesh");

        group.MapPost("/evaluate-buoyancy", (
            DeepSeaBuoyancyRequest request,
            IDeepSeaBuoyancyMeshEngine engine) =>
        {
            var plan = engine.EvaluateBuoyancy(request);
            return Results.Ok(plan);
        })
        .WithName("EvaluateSubmersibleBuoyancy")
        .WithSummary("Models full-ocean-depth Hadal hydrostatic pressures, syntactic foam compressibility, and commands Variable Buoyancy Engine oil displacements")
        .Produces<BuoyancyControlPlan>(StatusCodes.Status200OK);

        return endpoints;
    }
}
