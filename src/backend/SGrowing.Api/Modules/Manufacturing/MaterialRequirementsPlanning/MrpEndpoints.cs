using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Manufacturing.MaterialRequirementsPlanning;

public static class MrpEndpoints
{
    public static IServiceCollection AddMrpServices(this IServiceCollection services)
    {
        services.AddSingleton<IMrpEngine, MrpEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapMrpEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/manufacturing/mrp").WithTags("Manufacturing MRP");

        group.MapPost("/explode", (MrpExplosionRequest request, IMrpEngine engine) =>
        {
            var result = engine.ExplodeRequirements(request);
            return Results.Ok(result);
        }).WithName("ExplodeMrpRequirements");

        return endpoints;
    }
}
