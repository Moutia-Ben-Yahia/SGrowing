using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Logistics.MaritimeEcoRouting;

public static class MaritimeEcoRoutingEndpoints
{
    public static IServiceCollection AddMaritimeEcoRoutingServices(this IServiceCollection services)
    {
        services.AddSingleton<MaritimeEcoRoutingEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapMaritimeEcoRoutingEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/logistics/maritime-eco-routing")
            .WithTags("Autonomous Maritime AIS Vessel Collision Avoidance & Bunker Fuel Eco-Routing");

        group.MapPost("/plan-voyage", (
            EcoRoutingRequest request,
            MaritimeEcoRoutingEngine engine) =>
        {
            var plan = engine.ComputeEcoRouteAndCollisionRisks(request);
            return Results.Ok(plan);
        });

        return app;
    }
}
